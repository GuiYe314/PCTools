namespace JuDianWorkbench.Models;

public sealed class AppData
{
    public int SchemaVersion { get; set; } = 7;
    public List<FolderRecord> Folders { get; set; } = [];
    public List<EventRecord> Events { get; set; } = [];
    public List<GitHubTrendSnapshot> GitHubTrendSnapshots { get; set; } = [];
    public List<CommandRecord> Commands { get; set; } = [];
    public List<QuickFilterRecord> QuickFilters { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}
