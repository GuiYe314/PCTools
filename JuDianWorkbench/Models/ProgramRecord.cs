using System.Text.Json.Serialization;

namespace JuDianWorkbench.Models;

public sealed class ProgramRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "常用";
    public string TargetType { get; set; } = "程序";
    public string Target { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunAsAdministrator { get; set; }
    public bool SkipIfRunning { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLaunchedAt { get; set; }
    public int LaunchCount { get; set; }

    [JsonIgnore] public string TargetSummary => Target;
    [JsonIgnore] public string LastLaunchText => LastLaunchedAt is null ? "尚未启动" : $"{LastLaunchedAt:yyyy-MM-dd HH:mm} · 共 {LaunchCount} 次";
    [JsonIgnore] public string DetailToolTipText => $"{TargetType}：{Target}";
}
