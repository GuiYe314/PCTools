using System.Security.Cryptography;
using System.Text;
using JuDianFileShare.Server.Models;
using JuDianFileShare.Server.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ");
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5080");
var configuredOptions = builder.Configuration.GetSection(FileShareOptions.SectionName).Get<FileShareOptions>() ?? new FileShareOptions();
var requestLimit = checked(configuredOptions.MaxFileSizeBytes + 1_048_576);
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = requestLimit);
builder.Services.Configure<FileShareOptions>(builder.Configuration.GetSection(FileShareOptions.SectionName));
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = requestLimit);
builder.Services.AddSingleton(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var options = serviceProvider.GetRequiredService<IOptions<FileShareOptions>>().Value;
    return new FileStore(environment.ContentRootPath, options);
});

var app = builder.Build();
var options = app.Services.GetRequiredService<IOptions<FileShareOptions>>().Value;
if (string.IsNullOrWhiteSpace(options.AccessPassword))
    throw new InvalidOperationException("FileShare:AccessPassword 不能为空。");
if (options.AccessPassword == "change-me-now")
    app.Logger.LogWarning("当前仍在使用默认访问密码，请在 appsettings.json 中立即修改。 ");

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api") ||
        context.Request.Path.StartsWithSegments("/api/login") ||
        context.Request.Path.StartsWithSegments("/api/status"))
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

    if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
    {
        if (!string.Equals(context.Request.Headers["X-FileShare-Request"], "1", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "请求来源无效。" });
            return;
        }
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
    var password = form["password"].ToString();
    if (!FixedTimeEquals(password, options.AccessPassword))
    {
        await Task.Delay(350, context.RequestAborted);
        return Results.Json(new { error = "访问密码错误。" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    context.Response.Cookies.Append("jdfs-auth", CreateAuthValue(options.AccessPassword), new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        MaxAge = TimeSpan.FromHours(12),
        IsEssential = true
    });
    return Results.Ok(new { success = true });
});

app.MapPost("/api/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("jdfs-auth");
    return Results.Ok(new { success = true });
});

app.MapGet("/api/files", async (string? q, FileStore store, CancellationToken cancellationToken) =>
{
    var files = await store.ListAsync(q, cancellationToken);
    return Results.Ok(files.Select(x => new
    {
        x.Id,
        x.OriginalName,
        x.Size,
        sizeText = FileStore.FormatSize(x.Size),
        x.UploadedAt,
        x.Sha256
    }));
});

app.MapPost("/api/files", async (HttpRequest request, FileStore store, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "请使用 multipart/form-data 上传文件。" });
    if (request.ContentLength is > 0 && request.ContentLength > store.MaxFileSizeBytes + 1_048_576)
        return Results.Json(new { error = $"文件不能超过 {FileStore.FormatSize(store.MaxFileSizeBytes)}。" }, statusCode: StatusCodes.Status413PayloadTooLarge);

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0) return Results.BadRequest(new { error = "请选择非空文件。" });
        await using var stream = file.OpenReadStream();
        var saved = await store.SaveAsync(stream, file.FileName, file.Length, cancellationToken);
        app.Logger.LogInformation("文件上传完成：{FileName} ({Size} bytes, SHA-256 {Hash})", saved.OriginalName, saved.Size, saved.Sha256);
        return Results.Created($"/api/files/{saved.Id}/download", new { saved.Id, saved.OriginalName, saved.Size, saved.UploadedAt, saved.Sha256 });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status413PayloadTooLarge);
    }
});

app.MapGet("/api/files/{id:guid}/download", async (Guid id, FileStore store, CancellationToken cancellationToken) =>
{
    var found = await store.FindAsync(id, cancellationToken);
    return found is null
        ? Results.NotFound(new { error = "文件不存在。" })
        : Results.File(found.Value.Path, "application/octet-stream", found.Value.Entry.OriginalName, enableRangeProcessing: true);
});

app.MapDelete("/api/files/{id:guid}", async (Guid id, FileStore store, CancellationToken cancellationToken) =>
{
    var found = await store.FindAsync(id, cancellationToken);
    if (found is null) return Results.NotFound(new { error = "文件不存在。" });
    if (!await store.DeleteAsync(id, cancellationToken)) return Results.NotFound(new { error = "文件不存在。" });
    app.Logger.LogInformation("文件已删除：{FileName}", found.Value.Entry.OriginalName);
    return Results.NoContent();
});

app.Run();

static bool IsAuthenticated(HttpContext context, string password) =>
    context.Request.Cookies.TryGetValue("jdfs-auth", out var value) &&
    FixedTimeEquals(value, CreateAuthValue(password));

static string CreateAuthValue(string password) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("JuDianFileShare|" + password))).ToLowerInvariant();

static bool FixedTimeEquals(string left, string right)
{
    var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
    var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
    return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
}

public partial class Program;
