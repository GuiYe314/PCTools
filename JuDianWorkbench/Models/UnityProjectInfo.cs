namespace JuDianWorkbench.Models;

public sealed class UnityProjectInfo
{
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectPath { get; init; } = string.Empty;
    public string EditorVersion { get; init; } = "未知版本";
    public string EditorPath { get; init; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore] public bool HasMatchingEditor => File.Exists(EditorPath);
    [System.Text.Json.Serialization.JsonIgnore] public string EditorStatusText => HasMatchingEditor ? $"已找到 Editor：{EditorVersion}" : $"未安装匹配的 Editor：{EditorVersion}";
    [System.Text.Json.Serialization.JsonIgnore] public string DetailToolTipText => $"项目：{ProjectPath}\nEditor：{EditorVersion}";
}
