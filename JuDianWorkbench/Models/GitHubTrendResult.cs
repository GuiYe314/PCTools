namespace JuDianWorkbench.Models;

public sealed class GitHubTrendResult
{
    public required IReadOnlyList<GitHubProjectRecord> Projects { get; init; }
    public required DateTime FetchedAt { get; init; }
    public int? RateLimitRemaining { get; init; }
    public DateTime? RateLimitResetAt { get; init; }
    public bool IncompleteResults { get; init; }
}
