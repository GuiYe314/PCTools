using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.ViewModels;

namespace JuDianWorkbench;

public partial class TaskArchiveWindow : Window
{
    private readonly MainViewModel _viewModel;

    public TaskArchiveWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EventRecord task }) _viewModel.RestoreTask(task);
    }
}
