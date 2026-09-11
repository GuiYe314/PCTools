namespace JuDianFileShare.Server.Models;

public sealed class FileShareOptions
{
    public const string SectionName = "FileShare";
    public string StoragePath { get; set; } = "Data/Files";
    public string AccessPassword { get; set; } = "change-me-now";
    public long MaxFileSizeBytes { get; set; } = 536_870_912;
}
