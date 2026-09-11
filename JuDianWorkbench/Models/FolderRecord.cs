using System.Text.Json.Serialization;

namespace JuDianWorkbench.Models;

public sealed class FolderRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Category { get; set; } = "其他";
    public string Tags { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? LastOpenedAt { get; set; }
    public DateTime? LastReadAt { get; set; }
    public string ReadStatus { get; set; } = "尚未读取";
    public List<UnityProjectInfo> DetectedUnityProjects { get; set; } = [];
    [JsonIgnore] public bool Exists => Directory.Exists(Path);
    [JsonIgnore] public string StatusText => Exists ? "正常" : "路径失效";
    [JsonIgnore] public bool HasUnityProjects => DetectedUnityProjects.Count > 0;
    [JsonIgnore] public string ReadSummaryText => LastReadAt is null
        ? "尚未读取文件夹信息"
        : $"{ReadStatus} · {LastReadAt:yyyy-MM-dd HH:mm}";
    [JsonIgnore] public string UnityProjectSummaryText => HasUnityProjects
        ? $"检测到 {DetectedUnityProjects.Count} 个 Unity 项目"
        : "未检测到 Unity 项目";
    [JsonIgnore] public string DetailToolTipText => string.IsNullOrWhiteSpace(Notes)
        ? "详细备注：暂无备注"
        : $"详细备注：{Notes}";
}
