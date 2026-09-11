using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace JuDianWorkbench.Models;

public sealed class EventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Type { get; set; } = "普通任务";
    public string Priority { get; set; } = "普通";
    public string Status { get; set; } = "未完成";
    public string Tags { get; set; } = string.Empty;
    public string CompletionNote { get; set; } = string.Empty;
    public string PlannedTime { get; set; } = "09:00";
    public Guid? FolderId { get; set; }
    public bool IsSystemLog { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderMinutesBefore { get; set; } = 10;
    public DateTime? ReminderSentAt { get; set; }
    public ObservableCollection<SubTaskRecord> SubTasks { get; set; } = [];
    public string RepeatType { get; set; } = "不重复";
    public int RepeatInterval { get; set; } = 1;
    public DateTime? RepeatEndDate { get; set; }
    public Guid? RecurrenceSeriesId { get; set; }
    public Guid? NextOccurrenceId { get; set; }
    public bool RepeatAdvanceProcessed { get; set; }
    [JsonIgnore]
    public bool IsCompleted
    {
        get => Status == "已完成";
        set => Status = value ? "已完成" : "未完成";
    }
    [JsonIgnore] public string PlannedDateTimeText => $"{OccurredAt:yyyy-MM-dd} {PlannedTime}";
    [JsonIgnore] public DateTime DueAt => DateTime.TryParse($"{OccurredAt:yyyy-MM-dd} {PlannedTime}", out var value) ? value : OccurredAt;
    [JsonIgnore] public bool IsOverdue => Status == "未完成" && DueAt < DateTime.Now;
    [JsonIgnore] public string TimeStateText => IsOverdue ? "已逾期" : $"截止 {PlannedDateTimeText}";
    [JsonIgnore] public int CompletedSubTaskCount => SubTasks.Count(x => x.IsCompleted);
    [JsonIgnore] public string SubTaskSummaryText => SubTasks.Count == 0 ? "无子任务" : $"子任务 {CompletedSubTaskCount}/{SubTasks.Count}";
    [JsonIgnore] public bool IsRepeating => RepeatType != "不重复";
    [JsonIgnore] public string RepeatText => RepeatType switch
    {
        "每天" => RepeatInterval == 1 ? "每天" : $"每 {RepeatInterval} 天",
        "每周" => RepeatInterval == 1 ? "每周" : $"每 {RepeatInterval} 周",
        "每月" => RepeatInterval == 1 ? "每月" : $"每 {RepeatInterval} 个月",
        "每年" => RepeatInterval == 1 ? "每年" : $"每 {RepeatInterval} 年",
        _ => "不重复"
    };
    [JsonIgnore] public DateTime ReminderAt => DueAt.AddMinutes(-ReminderMinutesBefore);
    [JsonIgnore] public string ReminderStateText => Status == "已完成" ? "已完成" : IsOverdue ? "已逾期" : ReminderSentAt is not null ? "已提醒" : ReminderAt <= DateTime.Now.AddDays(1) ? "即将提醒" : "等待提醒";
    [JsonIgnore] public string DetailToolTipText
    {
        get
        {
            var content = string.IsNullOrWhiteSpace(Content) ? "暂无任务说明" : Content;
            return string.IsNullOrWhiteSpace(CompletionNote) ? content : $"{content}\n\n处理注释：{CompletionNote}";
        }
    }
}
