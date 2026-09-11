namespace JuDianWorkbench.Models;

public sealed record WorkModeStepResult(string Name, string Status, string? Error = null);

public sealed class WorkModeRunSummary
{
    public List<WorkModeStepResult> Results { get; } = [];
    public int StartedCount => Results.Count(x => x.Status == "已启动");
    public int SkippedCount => Results.Count(x => x.Status == "已跳过");
    public int FailedCount => Results.Count(x => x.Status == "失败");
    public string StatusText => FailedCount == 0
        ? $"已启动 {StartedCount} 项" + (SkippedCount > 0 ? $"，跳过 {SkippedCount} 项" : string.Empty)
        : $"已启动 {StartedCount} 项，失败 {FailedCount} 项";
}
