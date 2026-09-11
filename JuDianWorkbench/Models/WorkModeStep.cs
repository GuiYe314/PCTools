namespace JuDianWorkbench.Models;

public sealed class WorkModeStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int DelayAfterSeconds { get; set; }

    public string DisplayText => DelayAfterSeconds <= 0 ? ProgramName : $"{ProgramName} · 启动后等待 {DelayAfterSeconds} 秒";
}
