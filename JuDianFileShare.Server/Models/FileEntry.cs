namespace JuDianFileShare.Server.Models;

public sealed class FileEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string Sha256 { get; set; } = string.Empty;
}
