using System.Windows;
using JuDianWorkbench.Models;

namespace JuDianWorkbench;

public partial class FeatureManagementWindow : Window
{
    public bool ShowDashboard { get; private set; }
    public bool ShowFolders { get; private set; }
    public bool ShowTasks { get; private set; }
    public bool ShowCommands { get; private set; }
    public bool ShowGitHub { get; private set; }
    public bool ShowSystemNetwork { get; private set; }

    public FeatureManagementWindow(AppSettings settings)
    {
        InitializeComponent();
        DashboardCheckBox.IsChecked = settings.ShowDashboard;
        FoldersCheckBox.IsChecked = settings.ShowFolderManagement;
        TasksCheckBox.IsChecked = settings.ShowTaskManagement;
        CommandsCheckBox.IsChecked = settings.ShowCommandCenter;
        GitHubCheckBox.IsChecked = settings.ShowGitHubTrending;
        SystemNetworkCheckBox.IsChecked = settings.ShowSystemNetwork;
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        DashboardCheckBox.IsChecked = FoldersCheckBox.IsChecked = TasksCheckBox.IsChecked =
            CommandsCheckBox.IsChecked = GitHubCheckBox.IsChecked = SystemNetworkCheckBox.IsChecked = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboard = DashboardCheckBox.IsChecked == true;
        ShowFolders = FoldersCheckBox.IsChecked == true;
        ShowTasks = TasksCheckBox.IsChecked == true;
        ShowCommands = CommandsCheckBox.IsChecked == true;
        ShowGitHub = GitHubCheckBox.IsChecked == true;
        ShowSystemNetwork = SystemNetworkCheckBox.IsChecked == true;
        DialogResult = true;
    }
}
