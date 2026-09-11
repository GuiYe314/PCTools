namespace JuDianWorkbench.Models;

public sealed class AppSettings
{
    public string ApplicationName { get; set; } = "聚点工作台";
    public bool StartWithWindows { get; set; }
    public bool AutoBackupEnabled { get; set; } = true;
    public int BackupRetentionCount { get; set; } = 14;
    public string SelectedNetworkAdapterName { get; set; } = string.Empty;
    public int GitHubTrendPeriodDays { get; set; } = 7;
    public string GitHubTrendLanguage { get; set; } = "全部语言";
    public bool ShowDashboard { get; set; } = true;
    public bool ShowFolderManagement { get; set; } = true;
    public bool ShowTaskManagement { get; set; } = true;
    public bool ShowCommandCenter { get; set; } = true;
    public bool ShowGitHubTrending { get; set; } = true;
    public bool ShowSystemNetwork { get; set; } = true;
    public bool ShowFileShare { get; set; } = true;
    public int FileSharePort { get; set; } = 5080;
    public string FileSharePassword { get; set; } = "change-me-now";
    public string FileShareStoragePath { get; set; } = string.Empty;
}
