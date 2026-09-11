using System.Windows;
using JuDianWorkbench.Models;
using JuDianWorkbench.ViewModels;

namespace JuDianWorkbench;

public partial class QuickFilterEditWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly string _module;
    public bool DeleteRequested { get; private set; }
    public IReadOnlyList<QuickFilterColorOption> ColorOptions { get; } =
    [
        new("蓝色", "#566FEB"), new("青色", "#168AAD"), new("绿色", "#2F855A"),
        new("橙色", "#C77700"), new("红色", "#C94B50"), new("紫色", "#805AD5")
    ];
    public string[] TagMatchModes { get; } = ["任意标签", "全部标签"];
    public string[] FavoriteModes { get; } = ["不限", "只看收藏", "不看收藏"];
    public string[] UnityModes { get; } = ["不限", "只看 Unity 项目", "不看 Unity 项目"];
    public string[] AdministratorModes { get; } = ["不限", "仅管理员命令", "仅普通命令"];
    public string[] ReminderModes { get; } = ["不限", "仅启用提醒", "仅未启用提醒", "仅已逾期"];
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<string> StatusOptions { get; }
    public IReadOnlyList<string> TypeOptions { get; }
    public IReadOnlyList<string> PriorityOptions { get; }

    public QuickFilterEditWindow(QuickFilterRecord filter, MainWindow owner, bool isNew)
    {
        InitializeComponent();
        _mainWindow = owner;
        _module = filter.Module;
        Owner = owner;
        var viewModel = (MainViewModel)owner.DataContext;
        Categories = filter.Module switch
        {
            MainViewModel.FolderFilterModule => ["不限", .. viewModel.FolderCategories],
            MainViewModel.CommandFilterModule => ["不限", .. viewModel.CommandCategoryFilters.Where(x => x != "全部分类")],
            _ => ["不限"]
        };
        StatusOptions = filter.Module switch
        {
            MainViewModel.FolderFilterModule => ["不限", "正常", "路径失效"],
            MainViewModel.TaskFilterModule => ["不限", .. viewModel.EventStatuses],
            _ => ["不限"]
        };
        TypeOptions = filter.Module switch
        {
            MainViewModel.TaskFilterModule => ["不限", .. viewModel.EventTypes],
            MainViewModel.CommandFilterModule => ["不限", .. viewModel.CommandTypes],
            _ => ["不限"]
        };
        PriorityOptions = filter.Module == MainViewModel.TaskFilterModule ? ["不限", .. viewModel.EventPriorities] : ["不限"];

        CategoryPanel.Visibility = filter.Module == MainViewModel.TaskFilterModule ? Visibility.Collapsed : Visibility.Visible;
        StatusPanel.Visibility = filter.Module == MainViewModel.CommandFilterModule ? Visibility.Collapsed : Visibility.Visible;
        TypePanel.Visibility = filter.Module == MainViewModel.FolderFilterModule ? Visibility.Collapsed : Visibility.Visible;
        PriorityPanel.Visibility = filter.Module == MainViewModel.TaskFilterModule ? Visibility.Visible : Visibility.Collapsed;
        ReminderPanel.Visibility = filter.Module == MainViewModel.TaskFilterModule ? Visibility.Visible : Visibility.Collapsed;
        FolderModesPanel.Visibility = filter.Module == MainViewModel.FolderFilterModule ? Visibility.Visible : Visibility.Collapsed;
        AdministratorPanel.Visibility = filter.Module == MainViewModel.CommandFilterModule ? Visibility.Visible : Visibility.Collapsed;
        AvailableTagsText.Text = viewModel.GetAvailableTags(filter.Module) is { Count: > 0 } tags
            ? $"已有标签：{string.Join("、", tags)}"
            : "当前还没有数据标签，可直接输入新标签。";
        DataContext = filter;
        DialogTitle.Text = isNew ? "新建快捷筛选标签" : "编辑快捷筛选标签";
        Title = DialogTitle.Text;
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QuickFilterRecord filter || !string.IsNullOrWhiteSpace(filter.Name)) DialogResult = true;
        else MessageBox.Show(this, "请输入标签名称。", "快捷筛选标签", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QuickFilterRecord filter) return;
        if (MessageBox.Show(this, $"删除快捷筛选标签“{filter.Name}”？\n不会删除任何实际条目。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DeleteRequested = true;
        DialogResult = true;
    }

    private void ManageLayout_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainViewModel)_mainWindow.DataContext;
        _ = new QuickFilterManagementWindow(viewModel, _module, this).ShowDialog();
    }
}
