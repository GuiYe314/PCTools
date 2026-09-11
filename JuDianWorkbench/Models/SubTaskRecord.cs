using System.Text.Json.Serialization;

namespace JuDianWorkbench.Models;

public sealed class SubTaskRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    [JsonIgnore] public string StatusIcon => IsCompleted ? "✓" : "○";
}
