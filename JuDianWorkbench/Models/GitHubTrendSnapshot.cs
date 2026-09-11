namespace JuDianWorkbench.Models;

public sealed class GitHubTrendSnapshot
{
    public string Key { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
    public List<GitHubProjectRecord> Projects { get; set; } = [];
}
