using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using JuDianFileShare.Server.Models;
using JuDianFileShare.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JuDianWorkbench.Services;

public sealed class LocalFileShareService : IAsyncDisposable
{
    private WebApplication? _application;

    public bool IsRunning => _application is not null;

    public async Task<IReadOnlyList<string>> StartAsync(int port, string password, string storagePath, CancellationToken cancellationToken = default)
    {
        if (IsRunning) return GetAccessUrls(port);
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "端口必须在 1024 到 65535 之间。");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4) throw new ArgumentException("访问密码至少需要 4 个字符。", nameof(password));
        if (string.IsNullOrWhiteSpace(storagePath)) throw new ArgumentException("请选择共享文件保存目录。", nameof(storagePath));

        var options = new FileShareOptions
        {
            StoragePath = Path.GetFullPath(storagePath),
            AccessPassword = password,
            MaxFileSizeBytes = 536_870_912
        };
        var requestLimit = checked(options.MaxFileSizeBytes + 1_048_576);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(LocalFileShareService).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new FileShareLoggerProvider());
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = requestLimit);
        builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = requestLimit);
        builder.Services.AddSingleton(new FileStore(AppContext.BaseDirectory, options));

        var app = builder.Build();
        ConfigurePipeline(app, options);
        try
        {
            await app.StartAsync(cancellationToken);
            _application = app;
            return GetAccessUrls(port);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null) return;
        var app = _application;
        _application = null;
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    public static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JuDianWorkbench", "SharedFiles");

    public static IReadOnlyList<string> GetAccessUrls(int port)
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address))
            .Select(x => $"http://{x.Address}:{port}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        if (addresses.Count == 0) addresses.Add($"http://localhost:{port}");
        return addresses;
    }

    private static void ConfigurePipeline(WebApplication app, FileShareOptions options)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/api/login") || context.Request.Path.StartsWithSegments("/api/status"))
            {
                await next();
                return;
            }
            if (!IsAuthenticated(context, options.AccessPassword))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "请先输入访问密码。" });
                return;
            }
            if ((HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)) &&
                !string.Equals(context.Request.Headers["X-FileShare-Request"], "1", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "请求来源无效。" });
                return;
            }
            await next();
        });

        app.MapGet("/api/status", (HttpContext context) => Results.Ok(new
        {
            authenticated = IsAuthenticated(context, options.AccessPassword),
            maxFileSizeBytes = options.MaxFileSizeBytes
        }));
        app.MapPost("/api/login", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!FixedTimeEquals(form["password"].ToString(), options.AccessPassword))
            {
                await Task.Delay(350, context.RequestAborted);
                return Results.Json(new { error = "访问密码错误。" }, statusCode: StatusCodes.Status401Unauthorized);
            }
            context.Response.Cookies.Append("jdfs-auth", CreateAuthValue(options.AccessPassword), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromHours(12), IsEssential = true
            });
            return Results.Ok(new { success = true });
        });
        app.MapPost("/api/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete("jdfs-auth");
            return Results.Ok(new { success = true });
        });
        app.MapGet("/api/files", async (string? q, FileStore store, CancellationToken token) =>
        {
            var files = await store.ListAsync(q, token);
            return Results.Ok(files.Select(x => new { x.Id, x.OriginalName, x.Size, sizeText = FileStore.FormatSize(x.Size), x.UploadedAt, x.Sha256 }));
        });
        app.MapPost("/api/files", async (HttpRequest request, FileStore store, CancellationToken token) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "请使用 multipart/form-data 上传文件。" });
            if (request.ContentLength is > 0 && request.ContentLength > store.MaxFileSizeBytes + 1_048_576)
                return Results.Json(new { error = $"文件不能超过 {FileStore.FormatSize(store.MaxFileSizeBytes)}。" }, statusCode: StatusCodes.Status413PayloadTooLarge);
            try
            {
                var form = await request.ReadFormAsync(token);
                var file = form.Files.GetFile("file");
                if (file is null || file.Length == 0) return Results.BadRequest(new { error = "请选择非空文件。" });
                await using var stream = file.OpenReadStream();
                var saved = await store.SaveAsync(stream, file.FileName, file.Length, token);
                AppLogger.Info($"局域网文件上传：{saved.OriginalName}，大小={saved.Size}，SHA-256={saved.Sha256}");
                return Results.Created($"/api/files/{saved.Id}/download", new { saved.Id, saved.OriginalName, saved.Size, saved.UploadedAt, saved.Sha256 });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status413PayloadTooLarge);
            }
        });
        app.MapGet("/api/files/{id:guid}/download", async (Guid id, FileStore store, CancellationToken token) =>
        {
            var found = await store.FindAsync(id, token);
            return found is null ? Results.NotFound(new { error = "文件不存在。" }) : Results.File(found.Value.Path, "application/octet-stream", found.Value.Entry.OriginalName, enableRangeProcessing: true);
        });
        app.MapDelete("/api/files/{id:guid}", async (Guid id, FileStore store, CancellationToken token) =>
        {
            var found = await store.FindAsync(id, token);
            if (found is null || !await store.DeleteAsync(id, token)) return Results.NotFound(new { error = "文件不存在。" });
            AppLogger.Info($"局域网共享文件删除：{found.Value.Entry.OriginalName}");
            return Results.NoContent();
        });
    }

    private static bool IsAuthenticated(HttpContext context, string password) =>
        context.Request.Cookies.TryGetValue("jdfs-auth", out var value) && FixedTimeEquals(value, CreateAuthValue(password));

    private static string CreateAuthValue(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("JuDianFileShare|" + password))).ToLowerInvariant();

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed class FileShareLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new FileShareLogger(categoryName);
        public void Dispose() { }
    }

    private sealed class FileShareLogger(string categoryName) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            AppLogger.Info($"文件共享[{categoryName}/{logLevel}] {formatter(state, exception)}");
    }
}
