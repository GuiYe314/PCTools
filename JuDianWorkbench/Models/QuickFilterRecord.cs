namespace JuDianWorkbench.Models;

public sealed class QuickFilterRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Module { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#566FEB";
    public int SortOrder { get; set; }
    public bool IsPinned { get; set; } = true;
    public string Category { get; set; } = "不限";
    public string Tags { get; set; } = string.Empty;
    public string TagMatchMode { get; set; } = "任意标签";
    public string Status { get; set; } = "不限";
    public string Type { get; set; } = "不限";
    public string Priority { get; set; } = "不限";
    public string FavoriteMode { get; set; } = "不限";
    public string UnityMode { get; set; } = "不限";
    public string AdministratorMode { get; set; } = "不限";
    public string ReminderMode { get; set; } = "不限";
    public bool IsActive { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string DetailToolTipText => $"{Name}\n分类：{Category}\n数据标签：{(string.IsNullOrWhiteSpace(Tags) ? "不限" : Tags)}";
}

public sealed record QuickFilterColorOption(string Name, string Value);
