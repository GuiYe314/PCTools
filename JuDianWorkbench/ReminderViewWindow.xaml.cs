using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.ViewModels;

namespace JuDianWorkbench;

public partial class ReminderViewWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IReadOnlyList<EventRecord> _allTasks;
    private readonly ObservableCollection<EventRecord> _visibleTasks = [];

    public ReminderViewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _allTasks = viewModel.GetReminderTasks();
        ReminderList.ItemsSource = _visibleTasks;
        UpdateCounts();
        ApplyFilter();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    private void ApplyFilter()
    {
        var now = DateTime.Now;
        var filter = (FilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var query = filter switch
        {
            "Overdue" => _allTasks.Where(x => x.IsOverdue),
            "Next7Days" => _allTasks.Where(x => x.Status == "未完成" && x.DueAt >= now && x.DueAt <= now.AddDays(7)),
            "Sent" => _allTasks.Where(x => x.ReminderSentAt is not null),
            _ => _allTasks.AsEnumerable()
        };
        _visibleTasks.Clear();
        foreach (var task in query.OrderBy(x => x.DueAt)) _visibleTasks.Add(task);
    }

    private void UpdateCounts()
    {
        var now = DateTime.Now;
        AllCountText.Text = _allTasks.Count.ToString();
        OverdueCountText.Text = _allTasks.Count(x => x.IsOverdue).ToString();
        NextWeekCountText.Text = _allTasks.Count(x => x.Status == "未完成" && x.DueAt >= now && x.DueAt <= now.AddDays(7)).ToString();
        SentCountText.Text = _allTasks.Count(x => x.ReminderSentAt is not null).ToString();
    }

    private void OpenTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EventRecord task }) return;
        _viewModel.SelectedEvent = task;
        Close();
    }
}
