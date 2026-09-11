using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class GitHubTrendingService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<GitHubTrendResult> FetchAsync(int periodDays, string language, CancellationToken cancellationToken = default)
    {
        periodDays = periodDays is 7 or 30 or 90 ? periodDays : 7;
        var since = DateTime.UtcNow.Date.AddDays(-periodDays).ToString("yyyy-MM-dd");
        var qualifiers = $"created:>={since} fork:false archived:false";
        if (!string.IsNullOrWhiteSpace(language) && language != "全部语言") qualifiers += $" language:\"{language}\"";
        var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(qualifiers)}&sort=stars&order=desc&per_page=30";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        var remaining = ReadIntHeader(response, "X-RateLimit-Remaining");
        var resetAt = ReadResetHeader(response);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiMessage = TryReadApiMessage(body);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                var resetText = resetAt is null ? "稍后重试" : $"{resetAt:HH:mm} 后重试";
                throw new InvalidOperationException($"GitHub 查询频率已受限，请在 {resetText}。{apiMessage}");
            }
            throw new HttpRequestException($"GitHub 返回 {(int)response.StatusCode}：{apiMessage}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<SearchResponse>(stream, cancellationToken: cancellationToken)
                      ?? throw new JsonException("GitHub 返回了空数据。");
        var projects = payload.Items.Select((item, index) => new GitHubProjectRecord
        {
            RepositoryId = item.Id,
            FullName = item.FullName,
            Description = string.IsNullOrWhiteSpace(item.Description) ? "暂无项目说明" : item.Description,
            Url = item.HtmlUrl,
            CloneUrl = item.CloneUrl,
            Owner = item.Owner?.Login ?? item.FullName.Split('/')[0],
            Language = string.IsNullOrWhiteSpace(item.Language) ? "未标注" : item.Language,
            License = item.License?.SpdxId is null or "NOASSERTION" ? "未标注" : item.License.SpdxId,
            Topics = item.Topics.Count == 0 ? "暂无主题" : string.Join(" · ", item.Topics),
            Stars = item.StargazersCount,
            Forks = item.ForksCount,
            OpenIssues = item.OpenIssuesCount,
            CreatedAt = item.CreatedAt.LocalDateTime,
            UpdatedAt = item.UpdatedAt.LocalDateTime,
            CurrentRank = index + 1
        }).ToList();

        return new GitHubTrendResult
        {
            Projects = projects,
            FetchedAt = DateTime.Now,
            RateLimitRemaining = remaining,
            RateLimitResetAt = resetAt,
            IncompleteResults = payload.IncompleteResults
        };
    }

    public static void CompareWithPrevious(IList<GitHubProjectRecord> current, GitHubTrendSnapshot? previous, DateTime now)
    {
        var previousById = previous?.Projects.ToDictionary(x => x.RepositoryId) ?? [];
        foreach (var project in current)
        {
            if (!previousById.TryGetValue(project.RepositoryId, out var old))
            {
                project.FirstSeenAt = now;
                project.PreviousStars = 0;
                project.PreviousRank = 0;
                project.TrendStatus = previous is null ? "首次发现" : "新上榜";
                continue;
            }
            project.FirstSeenAt = old.FirstSeenAt == default ? now : old.FirstSeenAt;
            project.TranslatedDescription = old.TranslatedDescription;
            project.PreviousStars = old.Stars;
            project.PreviousRank = old.CurrentRank;
            project.TrendStatus = old.CurrentRank > project.CurrentRank
                ? $"排名上升 {old.CurrentRank - project.CurrentRank} 位"
                : project.Stars > old.Stars ? "热度上升" : old.CurrentRank < project.CurrentRank ? "排名下降" : "持续热门";
        }
    }

    public static string BuildSnapshotKey(int days, string language) => $"{days}|{language.Trim().ToLowerInvariant()}";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JuDianWorkbench/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return client;
    }

    private static int? ReadIntHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out var value) ? value : null;

    private static DateTime? ReadResetHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Reset", out var values) ||
            !long.TryParse(values.FirstOrDefault(), out var unix)) return null;
        return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
    }

    private static string TryReadApiMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty;
        }
        catch { return string.Empty; }
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("incomplete_results")] public bool IncompleteResults { get; set; }
        [JsonPropertyName("items")] public List<RepositoryDto> Items { get; set; } = [];
    }

    private sealed class RepositoryDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("clone_url")] public string CloneUrl { get; set; } = string.Empty;
        [JsonPropertyName("language")] public string? Language { get; set; }
        [JsonPropertyName("stargazers_count")] public int StargazersCount { get; set; }
        [JsonPropertyName("forks_count")] public int ForksCount { get; set; }
        [JsonPropertyName("open_issues_count")] public int OpenIssuesCount { get; set; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
        [JsonPropertyName("topics")] public List<string> Topics { get; set; } = [];
        [JsonPropertyName("owner")] public OwnerDto? Owner { get; set; }
        [JsonPropertyName("license")] public LicenseDto? License { get; set; }
    }

    private sealed class OwnerDto { [JsonPropertyName("login")] public string Login { get; set; } = string.Empty; }
    private sealed class LicenseDto { [JsonPropertyName("spdx_id")] public string? SpdxId { get; set; } }
}
