namespace JuDianWorkbench.Models;

public sealed class BackupInfo
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTime CreatedAt { get; init; }
    public long Size { get; init; }
    public string SizeText => Size < 1024 ? $"{Size} B" : Size < 1024 * 1024 ? $"{Size / 1024d:F1} KB" : $"{Size / 1024d / 1024d:F1} MB";
    public string DetailToolTipText => $"备份文件：{FilePath}\n创建时间：{CreatedAt:yyyy-MM-dd HH:mm:ss}\n大小：{SizeText}";
}
