using System.Windows;
using JuDianWorkbench.Models;

namespace JuDianWorkbench;

public partial class TaskEditWindow : Window
{
    public string[] Types { get; }
    public string[] Priorities { get; }
    public string[] Statuses { get; }
    public string[] RepeatTypes { get; }
    public int[] RepeatIntervals { get; }
    public IEnumerable<FolderRecord> Folders { get; }
    public IReadOnlyList<ReminderOption> ReminderOptions { get; } =
    [
        new("到期时", 0),
        new("提前 10 分钟", 10),
        new("提前 1 小时", 60),
        new("提前 1 天", 1440)
    ];

    public TaskEditWindow(EventRecord task, MainWindow owner, bool isNew)
    {
        InitializeComponent();
        Owner = owner;
        var viewModel = (ViewModels.MainViewModel)owner.DataContext;
        Types = viewModel.EventTypes;
        Priorities = viewModel.EventPriorities;
        Statuses = viewModel.EventStatuses;
        RepeatTypes = viewModel.RepeatTypes;
        RepeatIntervals = viewModel.RepeatIntervals;
        Folders = viewModel.Folders;
        DataContext = task;
        DialogTitle.Text = isNew ? "添加任务" : "编辑任务";
        Title = DialogTitle.Text;
        UpdateRepeatFields();
    }

    private void AddSubTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EventRecord task) task.SubTasks.Add(new SubTaskRecord());
    }

    private void DeleteSubTask_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is EventRecord task && sender is FrameworkElement { Tag: SubTaskRecord subTask })
            task.SubTasks.Remove(subTask);
    }

    private void RepeatType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateRepeatFields();

    private void UpdateRepeatFields()
    {
        if (RepeatFieldsPanel is null || RepeatEndPanel is null) return;
        var enabled = RepeatTypeComboBox.SelectedItem is string repeatType && repeatType != "不重复";
        RepeatFieldsPanel.IsEnabled = enabled;
        RepeatEndPanel.IsEnabled = enabled;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EventRecord task) return;
        if (task.SubTasks.Any(x => string.IsNullOrWhiteSpace(x.Title)))
        {
            MessageBox.Show(this, "请填写子任务标题，或删除空白子任务。", "聚点工作台", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
