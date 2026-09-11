using System.Text.Json.Serialization;

namespace JuDianWorkbench.Models;

public sealed class CommandRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "常用";
    public string Tags { get; set; } = string.Empty;
    public string ExecutionType { get; set; } = "CMD 命令";
    public string CommandText { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;
    public string BatchPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunAsAdministrator { get; set; }
    public bool ConfirmBeforeRun { get; set; } = true;
    public bool KeepWindowOpen { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastRunAt { get; set; }
    public int RunCount { get; set; }
    public string LastRunStatus { get; set; } = "尚未执行";

    [JsonIgnore] public string TargetSummary => ExecutionType switch
    {
        "启动软件" => ProgramPath,
        "BAT 脚本" => BatchPath,
        _ => CommandText
    };
    [JsonIgnore] public string RunModeText => RunAsAdministrator ? "管理员权限" : "普通权限";
    [JsonIgnore] public string LastRunText => LastRunAt is null ? "尚未执行" : $"{LastRunAt:yyyy-MM-dd HH:mm} · {LastRunStatus}";
    [JsonIgnore] public string DetailToolTipText => string.IsNullOrWhiteSpace(Description)
        ? $"执行内容：{TargetSummary}"
        : $"{Description}\n\n执行内容：{TargetSummary}";
}
