using System.Security.Cryptography;
using System.Text.Json;
using JuDianFileShare.Server.Models;

namespace JuDianFileShare.Server.Services;

public sealed class FileStore
{
    private readonly string _storageRoot;
    private readonly string _indexFile;
    private readonly long _maxFileSizeBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public FileStore(string contentRoot, FileShareOptions options)
    {
        if (options.MaxFileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(options.MaxFileSizeBytes));
        _maxFileSizeBytes = options.MaxFileSizeBytes;
        _storageRoot = Path.GetFullPath(Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(contentRoot, options.StoragePath));
        Directory.CreateDirectory(_storageRoot);
        _indexFile = Path.Combine(_storageRoot, "index.json");
    }

    public long MaxFileSizeBytes => _maxFileSizeBytes;

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string? search, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadUnsafeAsync(cancellationToken);
            var query = items.Where(x => File.Exists(GetStoredPath(x.StoredName)));
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.OriginalName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            return query.OrderByDescending(x => x.UploadedAt).ToList();
        }
        finally { _gate.Release(); }
    }

    public async Task<FileEntry> SaveAsync(Stream source, string originalName, long? declaredLength = null, CancellationToken cancellationToken = default)
    {
        originalName = Path.GetFileName(originalName?.Trim()) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(originalName)) throw new InvalidOperationException("文件名无效。");
        if (declaredLength is > 0 && declaredLength > _maxFileSizeBytes)
            throw new InvalidOperationException($"文件不能超过 {FormatSize(_maxFileSizeBytes)}。");

        var extension = Path.GetExtension(originalName);
        if (extension.Length > 20) extension = string.Empty;
        var entry = new FileEntry { OriginalName = originalName, StoredName = $"{Guid.NewGuid():N}{extension}" };
        var destination = GetStoredPath(entry.StoredName);
        var temporary = destination + ".upload";

        await _gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long total = 0;
                {
                    await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                    var buffer = new byte[81920];
                    while (true)
                    {
                        var read = await source.ReadAsync(buffer, cancellationToken);
                        if (read == 0) break;
                        total += read;
                        if (total > _maxFileSizeBytes) throw new InvalidOperationException($"文件不能超过 {FormatSize(_maxFileSizeBytes)}。");
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        hash.AppendData(buffer, 0, read);
                    }
                    await output.FlushAsync(cancellationToken);
                }
                entry.Size = total;
                entry.Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                File.Move(temporary, destination);

                var items = await LoadUnsafeAsync(cancellationToken);
                items.Add(entry);
                await SaveUnsafeAsync(items, cancellationToken);
                return entry;
            }
            catch
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<(FileEntry Entry, string Path)?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entry = (await LoadUnsafeAsync(cancellationToken)).FirstOrDefault(x => x.Id == id);
            if (entry is null) return null;
            var path = GetStoredPath(entry.StoredName);
            return File.Exists(path) ? (entry, path) : null;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadUnsafeAsync(cancellationToken);
            var entry = items.FirstOrDefault(x => x.Id == id);
            if (entry is null) return false;
            var path = GetStoredPath(entry.StoredName);
            if (File.Exists(path)) File.Delete(path);
            items.Remove(entry);
            await SaveUnsafeAsync(items, cancellationToken);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task<List<FileEntry>> LoadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexFile)) return [];
        await using var stream = File.OpenRead(_indexFile);
        return await JsonSerializer.DeserializeAsync<List<FileEntry>>(stream, _jsonOptions, cancellationToken) ?? [];
    }

    private async Task SaveUnsafeAsync(List<FileEntry> entries, CancellationToken cancellationToken)
    {
        var temporary = _indexFile + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, entries, _jsonOptions, cancellationToken);
        File.Move(temporary, _indexFile, true);
    }

    private string GetStoredPath(string storedName)
    {
        var fileName = Path.GetFileName(storedName);
        if (!string.Equals(fileName, storedName, StringComparison.Ordinal)) throw new InvalidDataException("存储文件名无效。");
        var path = Path.GetFullPath(Path.Combine(_storageRoot, fileName));
        if (!path.StartsWith(_storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("文件路径超出共享目录。");
        return path;
    }

    public static string FormatSize(long bytes) => bytes < 1024
        ? $"{bytes} B"
        : bytes < 1024 * 1024
            ? $"{bytes / 1024d:F1} KB"
            : bytes < 1024L * 1024 * 1024
                ? $"{bytes / 1024d / 1024:F1} MB"
                : $"{bytes / 1024d / 1024 / 1024:F1} GB";
}
