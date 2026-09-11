using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JuDianWorkbench.Models;

public sealed class GitHubProjectRecord : INotifyPropertyChanged
{
    private string _translatedDescription = string.Empty;
    private bool _isTranslationVisible = true;

    public long RepositoryId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Language { get; set; } = "未标注";
    public string License { get; set; } = "未标注";
    public string Topics { get; set; } = string.Empty;
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int OpenIssues { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.Now;
    public int CurrentRank { get; set; }
    public int PreviousRank { get; set; }
    public int PreviousStars { get; set; }
    public string TrendStatus { get; set; } = "首次发现";
    public string TranslatedDescription
    {
        get => _translatedDescription;
        set
        {
            if (_translatedDescription == value) return;
            _translatedDescription = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(DisplayDescription));
            RaisePropertyChanged(nameof(TranslationActionText));
        }
    }

    [JsonIgnore]
    public bool IsTranslationVisible
    {
        get => _isTranslationVisible;
        set
        {
            if (_isTranslationVisible == value) return;
            _isTranslationVisible = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(DisplayDescription));
            RaisePropertyChanged(nameof(TranslationActionText));
        }
    }

    [JsonIgnore] public int StarGain => Math.Max(0, Stars - PreviousStars);
    [JsonIgnore] public string StarChangeText => PreviousStars <= 0 ? "首次记录" : StarGain > 0 ? $"+{StarGain:N0} Star" : "暂无新增";
    [JsonIgnore] public string RankText => $"#{CurrentRank}";
    [JsonIgnore] public string DisplayDescription => IsTranslationVisible && !string.IsNullOrWhiteSpace(TranslatedDescription) ? TranslatedDescription : Description;
    [JsonIgnore] public string TranslationActionText => string.IsNullOrWhiteSpace(TranslatedDescription) ? "翻译为中文" : IsTranslationVisible ? "显示原文" : "显示中文";
    [JsonIgnore] public string DetailToolTipText => DisplayDescription;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
