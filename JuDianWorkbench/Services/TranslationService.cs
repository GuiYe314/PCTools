using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JuDianWorkbench.Services;

public sealed class TranslationService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> TranslateToChineseAsync(string source, CancellationToken cancellationToken = default)
    {
        source = source.Trim();
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        if (ContainsMostlyChinese(source)) return source;

        var segments = SplitUtf8(source, 450);
        var translated = new List<string>();
        foreach (var segment in segments)
        {
            var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(segment)}&langpair=en%7Czh-CN&mt=1";
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<TranslationResponse>(stream, cancellationToken: cancellationToken)
                          ?? throw new JsonException("翻译服务返回了空数据。");
            if (payload.ResponseStatus is < 200 or >= 300 || string.IsNullOrWhiteSpace(payload.ResponseData?.TranslatedText))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload.ResponseDetails) ? "翻译服务没有返回有效结果。" : payload.ResponseDetails);
            translated.Add(WebUtility.HtmlDecode(payload.ResponseData.TranslatedText).Trim());
        }
        return string.Join(" ", translated);
    }

    public static IReadOnlyList<string> SplitUtf8(string source, int maxBytes)
    {
        var result = new List<string>();
        var remaining = source.Trim();
        while (Encoding.UTF8.GetByteCount(remaining) > maxBytes)
        {
            var length = Math.Min(remaining.Length, maxBytes);
            while (length > 1 && Encoding.UTF8.GetByteCount(remaining[..length]) > maxBytes) length--;
            var split = remaining.LastIndexOfAny([' ', '.', ',', ';', '!', '?'], length - 1, length);
            if (split < length / 2) split = length;
            result.Add(remaining[..split].Trim());
            remaining = remaining[split..].TrimStart();
        }
        if (remaining.Length > 0) result.Add(remaining);
        return result;
    }

    private static bool ContainsMostlyChinese(string source)
    {
        var letters = source.Count(char.IsLetter);
        if (letters == 0) return false;
        var chinese = source.Count(c => c is >= '\u3400' and <= '\u9FFF');
        return chinese * 2 >= letters;
    }

    private sealed class TranslationResponse
    {
        [JsonPropertyName("responseData")] public TranslationData? ResponseData { get; set; }
        [JsonPropertyName("responseStatus")] public int ResponseStatus { get; set; }
        [JsonPropertyName("responseDetails")] public string? ResponseDetails { get; set; }
    }

    private sealed class TranslationData
    {
        [JsonPropertyName("translatedText")] public string TranslatedText { get; set; } = string.Empty;
    }
}
