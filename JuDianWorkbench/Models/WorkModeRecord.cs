using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace JuDianWorkbench.Models;

public sealed class WorkModeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ConfirmBeforeRun { get; set; } = true;
    public bool StopOnFailure { get; set; }
    public bool IsPinned { get; set; } = true;
    public ObservableCollection<WorkModeStep> Steps { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastRunAt { get; set; }
    public string LastRunStatus { get; set; } = "尚未启动";
    public int RunCount { get; set; }

    [JsonIgnore] public int StepCount => Steps.Count;
    [JsonIgnore] public string StepSummaryText => Steps.Count == 0 ? "尚未添加启动项" : string.Join("  →  ", Steps.Select(x => x.ProgramName));
    [JsonIgnore] public string LastRunText => LastRunAt is null ? "尚未启动" : $"{LastRunAt:yyyy-MM-dd HH:mm} · {LastRunStatus}";
    [JsonIgnore] public string DetailToolTipText => string.IsNullOrWhiteSpace(Description) ? StepSummaryText : $"{Description}\n\n{StepSummaryText}";
}
