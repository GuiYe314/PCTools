using System.Windows;
using System.Windows.Input;
using JuDianWorkbench.Models;

namespace JuDianWorkbench;

public partial class UnityProjectSelectionWindow : Window
{
    public UnityProjectInfo? SelectedProject => ProjectList.SelectedItem as UnityProjectInfo;

    public UnityProjectSelectionWindow(IReadOnlyList<UnityProjectInfo> projects, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = projects;
        ProjectList.SelectedIndex = projects.Count > 0 ? 0 : -1;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProject is null) return;
        DialogResult = true;
    }

    private void ProjectList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedProject is null) return;
        DialogResult = true;
    }
}
