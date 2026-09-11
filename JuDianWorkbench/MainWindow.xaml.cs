using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Globalization;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;
using JuDianWorkbench.ViewModels;
using Microsoft.Win32;

namespace JuDianWorkbench;

public partial class MainWindow : Window
{
    private FolderRecord? _folderBeforeEdit;
    private EventRecord? _eventBeforeEdit;
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly SystemNetworkService _systemNetworkService = new();
    private readonly GitHubTrendingService _gitHubTrendingService = new();
    private readonly TranslationService _translationService = new();
    private readonly CommandExecutionService _commandExecutionService = new();
    private readonly UnityProjectService _unityProjectService = new();
    private readonly LocalFileShareService _fileShareService = new();
    private readonly WorkModeService _workModeService = new();
    private bool _isWorkModeRunning;
    private IReadOnlyList<string> _fileShareUrls = [];
    private bool _systemNetworkLoaded;
    private bool _gitHubInitialCheckAttempted;
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        if (string.IsNullOrWhiteSpace(ViewModel.Settings.FileShareStoragePath))
            ViewModel.Settings.FileShareStoragePath = LocalFileShareService.DefaultStoragePath;
        FileSharePortTextBox.Text = ViewModel.Settings.FileSharePort.ToString(CultureInfo.InvariantCulture);
        FileSharePasswordBox.Password = ViewModel.Settings.FileSharePassword;
        FileShareStoragePathTextBox.Text = ViewModel.Settings.FileShareStoragePath;
        ApplyApplicationIdentity();
        ViewModel.Settings.StartWithWindows = AutoStartService.IsEnabled();
        TryCreateDailyBackup();
        _reminderTimer.Tick += ReminderTimer_Tick;
        _reminderTimer.Start();
        GlobalSearchBox.TextChanged += (_, _) =>
        {
            if (FoldersPage.Visibility == Visibility.Visible) ViewModel.FolderSearch = GlobalSearchBox.Text;
            if (EventsPage.Visibility == Visibility.Visible) ViewModel.EventSearch = GlobalSearchBox.Text;
            if (CommandsPage.Visibility == Visibility.Visible) ViewModel.CommandSearch = GlobalSearchBox.Text;
            if (GitHubPage.Visibility == Visibility.Visible) ViewModel.GitHubProjectSearch = GlobalSearchBox.Text;
        };
        Loaded += (_, _) => ApplyFeatureVisibility();
        Closed += async (_, _) => await _fileShareService.StopAsync();
        AppLogger.Info("应用启动");
    }

    private async void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton button) return;
        var target = button.Tag?.ToString() ?? "Dashboard";
        ShowPage(target);
        UpdatePageChrome(target);
        GlobalSearchBox.Text = target switch
        {
            "Folders" => ViewModel.FolderSearch,
            "Events" => ViewModel.EventSearch,
            "Commands" => ViewModel.CommandSearch,
            "GitHub" => ViewModel.GitHubProjectSearch,
            _ => string.Empty
        };
        if (SystemNetworkPage.Visibility == Visibility.Visible && !_systemNetworkLoaded)
            await RefreshSystemNetworkAsync();
        if (GitHubPage.Visibility == Visibility.Visible && ViewModel.GitHubTrendCount == 0 && !_gitHubInitialCheckAttempted)
        {
            _gitHubInitialCheckAttempted = true;
            await RefreshGitHubTrendingAsync(showErrorDialog: false);
        }
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var record = new FolderRecord();
        var dialog = new FolderEditWindow(record, ViewModel.FolderCategories, true) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveFolder(record);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = "文件夹资料已保存";
    }

    private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e) => SetFolderEditMode(false);

    private void EditFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is null) return;
        var record = ViewModel.SelectedFolder;
        var snapshot = CopyFolder(record);
        var dialog = new FolderEditWindow(record, ViewModel.FolderCategories, false) { Owner = this };
        if (dialog.ShowDialog() != true) { RestoreFolder(record, snapshot); RefreshBindings(); return; }
        if (!string.Equals(record.Path, snapshot.Path, StringComparison.OrdinalIgnoreCase)) ViewModel.ClearFolderReadResult(record);
        var error = ViewModel.SaveFolder(record);
        if (error is not null) { RestoreFolder(record, snapshot); RefreshBindings(); ShowInfo(error); return; }
        StatusText.Text = "文件夹资料已保存";
    }

    private void CancelFolderEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_folderBeforeEdit is null)
        {
            ViewModel.SelectedFolder = null;
        }
        else if (ViewModel.SelectedFolder is not null)
        {
            RestoreFolder(ViewModel.SelectedFolder, _folderBeforeEdit);
            RefreshBindings();
        }
        SetFolderEditMode(false);
        StatusText.Text = "已退出编辑模式";
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择要管理的文件夹", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        var viewModel = ViewModel;
        viewModel.SelectedFolder ??= new FolderRecord();
        viewModel.SelectedFolder.Path = dialog.FolderName;
        if (string.IsNullOrWhiteSpace(viewModel.SelectedFolder.Name))
            viewModel.SelectedFolder.Name = System.IO.Path.GetFileName(dialog.FolderName.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        DataContext = null;
        DataContext = viewModel;
    }

    private void SaveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is null) { ShowInfo("请先添加或选择一个文件夹。"); return; }
        var error = ViewModel.SaveFolder(ViewModel.SelectedFolder);
        if (error is not null) { ShowInfo(error); return; }
        _folderBeforeEdit = null;
        SetFolderEditMode(false);
        StatusText.Text = "文件夹资料已保存";
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is null) return;
        if (MessageBox.Show($"仅从管理列表移除“{ViewModel.SelectedFolder.Name}”？\n不会删除实际文件夹。", "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.DeleteFolder(ViewModel.SelectedFolder);
        StatusText.Text = "管理记录已移除，实际文件夹未删除";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is null) return;
        try { ViewModel.OpenFolder(ViewModel.SelectedFolder); }
        catch (Exception ex) { AppLogger.Error("打开文件夹失败", ex); ShowInfo("文件夹不存在或无法访问。"); }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.SelectedFolder?.Path)) return;
        Clipboard.SetText(ViewModel.SelectedFolder.Path);
        StatusText.Text = "路径已复制";
    }

    private async void ReadFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is not { } folder) return;
        if (!Directory.Exists(folder.Path)) { ShowInfo("文件夹不存在或当前无法访问，请先修改路径。"); return; }
        ReadFolderButton.IsEnabled = false;
        ReadFolderButton.Content = "正在读取…";
        StatusText.Text = "正在读取文件夹信息并识别项目";
        try
        {
            var projects = await Task.Run(() => _unityProjectService.FindProjects(folder.Path));
            ViewModel.SaveFolderReadResult(folder, projects);
            StatusText.Text = projects.Count == 0 ? "读取完成，未检测到 Unity 项目" : $"读取完成，检测到 {projects.Count} 个 Unity 项目";
            AppLogger.Info($"读取文件夹：{folder.Path}，Unity 项目={projects.Count}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"读取文件夹失败：{folder.Path}", ex);
            StatusText.Text = "文件夹读取失败";
            ShowInfo($"读取文件夹失败：{ex.Message}\n详细信息已写入日志。");
        }
        finally
        {
            ReadFolderButton.IsEnabled = true;
            ReadFolderButton.Content = "读取文件夹";
        }
    }

    private void OpenUnityProject_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var folder = (sender as FrameworkElement)?.Tag as FolderRecord ?? ViewModel.SelectedFolder;
        if (folder is null || folder.DetectedUnityProjects.Count == 0) return;
        var triggerButton = sender as Button;
        if (triggerButton is not null) triggerButton.IsEnabled = false;
        try
        {
            var projects = folder.DetectedUnityProjects;

            UnityProjectInfo? project;
            if (projects.Count == 1) project = projects[0];
            else
            {
                var dialog = new UnityProjectSelectionWindow(projects, this);
                if (dialog.ShowDialog() != true) { StatusText.Text = "已取消打开 Unity 项目"; return; }
                project = dialog.SelectedProject;
            }

            if (project is null) return;
            var processId = _unityProjectService.LaunchProject(project);
            StatusText.Text = $"正在打开 Unity 项目：{project.ProjectName}";
            AppLogger.Info($"打开 Unity 项目：{project.ProjectPath}，Editor={project.EditorVersion}，PID={processId}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"打开 Unity 项目失败：{folder.Path}", ex);
            StatusText.Text = "Unity 项目启动失败";
            ShowInfo($"无法打开 Unity 项目：{ex.Message}\n详细信息已写入日志。");
        }
        finally
        {
            if (triggerButton is not null) triggerButton.IsEnabled = true;
        }
    }

    private void FolderTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderTreeNode node }) node.LoadChildren();
    }

    private void OpenTreeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: FolderTreeNode node }) OpenTreeNode(node);
    }

    private void FolderTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindTreeViewItem(source) is not { } item) return;
        item.IsSelected = true;
        item.Focus();
        if (item.DataContext is not FolderTreeNode node || node.IsPlaceholder)
        {
            item.ContextMenu = null;
            return;
        }

        var menu = new ContextMenu { Tag = node };
        if (node.IsDirectory)
        {
            var openFolder = new MenuItem { Header = "打开文件夹", Tag = node };
            openFolder.Click += OpenTreeFolder_Click;
            menu.Items.Add(openFolder);
        }
        else
        {
            var openFile = new MenuItem { Header = "打开文件", Tag = node };
            openFile.Click += OpenTreeFolder_Click;
            menu.Items.Add(openFile);
            var openLocation = new MenuItem { Header = "打开所在文件夹", Tag = node };
            openLocation.Click += OpenContainingFolder_Click;
            menu.Items.Add(openLocation);
        }
        item.ContextMenu = menu;
    }

    private void FolderTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var item = FindTreeViewItem(source);
        if (item?.DataContext is not FolderTreeNode { IsDirectory: true, IsPlaceholder: false }) return;
        item.IsExpanded = !item.IsExpanded;
        e.Handled = true;
    }

    private void OpenContainingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: FolderTreeNode node } || node.IsDirectory || !File.Exists(node.FullPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{node.FullPath}\"") { UseShellExecute = true });
            AppLogger.Info($"打开文件所在文件夹：{node.FullPath}");
            StatusText.Text = $"已定位：{node.Name}";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"打开文件所在文件夹失败：{node.FullPath}", ex);
            ShowInfo("无法打开文件所在文件夹，详细信息已写入日志。");
        }
    }

    private static TreeViewItem? FindTreeViewItem(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TreeViewItem item) return item;
            current = current is FrameworkContentElement content
                ? content.Parent
                : VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void OpenTreeNode(FolderTreeNode node)
    {
        if (node.IsPlaceholder || string.IsNullOrWhiteSpace(node.FullPath)) return;
        if (!Directory.Exists(node.FullPath) && !File.Exists(node.FullPath))
        {
            ShowInfo("该文件夹或文件不存在，或者当前无法访问。");
            return;
        }

        try
        {
            var startInfo = node.IsDirectory
                ? new ProcessStartInfo("explorer.exe", node.FullPath) { UseShellExecute = true }
                : new ProcessStartInfo(node.FullPath) { UseShellExecute = true };
            Process.Start(startInfo);
            AppLogger.Info($"从目录结构打开{(node.IsDirectory ? "文件夹" : "文件")}：{node.FullPath}");
            StatusText.Text = $"已打开：{node.Name}";
        }
        catch (Exception ex)
        {
            AppLogger.Error($"从目录结构打开失败：{node.FullPath}", ex);
            ShowInfo("无法打开，详细信息已写入日志。");
        }
    }

    private void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        OpenNewTaskDialog();
    }

    private void EventList_SelectionChanged(object sender, SelectionChangedEventArgs e) => SetEventEditMode(false);

    private void EditEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEvent is null) return;
        var task = ViewModel.SelectedEvent;
        var snapshot = CopyEvent(task);
        var dialog = new TaskEditWindow(task, this, false);
        if (dialog.ShowDialog() != true) { RestoreEvent(task, snapshot); RefreshBindings(); return; }
        if (task.OccurredAt.Date != snapshot.OccurredAt.Date || task.PlannedTime != snapshot.PlannedTime ||
            task.ReminderEnabled != snapshot.ReminderEnabled || task.ReminderMinutesBefore != snapshot.ReminderMinutesBefore)
            task.ReminderSentAt = null;
        var error = ViewModel.SaveEvent(task);
        if (error is not null) { RestoreEvent(task, snapshot); RefreshBindings(); ShowInfo(error); return; }
        StatusText.Text = "任务已保存";
    }

    private void CancelEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_eventBeforeEdit is null)
        {
            ViewModel.SelectedEvent = null;
        }
        else if (ViewModel.SelectedEvent is not null)
        {
            RestoreEvent(ViewModel.SelectedEvent, _eventBeforeEdit);
            RefreshBindings();
        }
        SetEventEditMode(false);
        StatusText.Text = "已退出编辑模式";
    }

    private void QuickEvent_Click(object sender, RoutedEventArgs e)
    {
        OpenNewTaskDialog();
    }

    private void NavigateToFeature_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string target }) return;
        NavigateToFeature(target);
    }

    private void NavigateToFeature(string target)
    {
        var button = new[] { DashboardNavButton, FoldersNavButton, TasksNavButton, WorkModesNavButton, CommandNavButton, GitHubNavButton, FileShareNavButton, SystemNetworkNavButton, SettingsNavButton }
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), target, StringComparison.Ordinal));
        if (button is null) return;
        if (button.Visibility != Visibility.Visible)
        {
            ShowInfo("该功能已在设置的“功能管理”中隐藏。");
            return;
        }
        button.IsChecked = true;
    }

    private void ShowPage(string target)
    {
        DashboardPage.Visibility = target == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        FoldersPage.Visibility = target == "Folders" ? Visibility.Visible : Visibility.Collapsed;
        EventsPage.Visibility = target == "Events" ? Visibility.Visible : Visibility.Collapsed;
        CommandsPage.Visibility = target == "Commands" ? Visibility.Visible : Visibility.Collapsed;
        WorkModesPage.Visibility = target == "WorkModes" ? Visibility.Visible : Visibility.Collapsed;
        GitHubPage.Visibility = target == "GitHub" ? Visibility.Visible : Visibility.Collapsed;
        FileSharePage.Visibility = target == "FileShare" ? Visibility.Visible : Visibility.Collapsed;
        SystemNetworkPage.Visibility = target == "SystemNetwork" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = target == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePageChrome(string target)
    {
        var pageName = target switch
        {
            "Folders" => "文件夹管理",
            "Events" => "任务管理",
            "Commands" => "命令中心",
            "WorkModes" => "工作模式",
            "GitHub" => "GitHub 热门",
            "FileShare" => "文件共享",
            "SystemNetwork" => "系统与网络",
            "Settings" => "设置",
            _ => "工作台"
        };
        CurrentPageTitleText.Text = $"当前位置 · {pageName}";
        var placeholder = target switch
        {
            "Folders" => "搜索文件夹名称、路径、用途或标签…",
            "Events" => "搜索任务标题、说明、状态或标签…",
            "Commands" => "搜索命令名称、分类或执行内容…",
            "GitHub" => "搜索仓库名称、说明或开发语言…",
            _ => string.Empty
        };
        GlobalSearchContainer.Visibility = string.IsNullOrEmpty(placeholder) ? Visibility.Collapsed : Visibility.Visible;
        SearchPlaceholderText.Text = placeholder;
        GlobalSearchBox.ToolTip = string.IsNullOrEmpty(placeholder) ? null : $"{placeholder.TrimEnd('…')}（Ctrl+K）";
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control && GlobalSearchContainer.Visibility == Visibility.Visible)
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && GlobalSearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(GlobalSearchBox.Text))
        {
            GlobalSearchBox.Clear();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.N || Keyboard.Modifiers != ModifierKeys.Control) return;
        if (FoldersPage.Visibility == Visibility.Visible) NewFolder_Click(this, e);
        else if (WorkModesPage.Visibility == Visibility.Visible) NewWorkMode_Click(this, e);
        else if (CommandsPage.Visibility == Visibility.Visible) NewCommand_Click(this, e);
        else OpenNewTaskDialog();
        e.Handled = true;
    }

    private void SaveEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEvent is null) { ShowInfo("请先新建或选择一个事件。"); return; }
        var error = ViewModel.SaveEvent(ViewModel.SelectedEvent);
        if (error is not null) { ShowInfo(error); return; }
        _eventBeforeEdit = null;
        SetEventEditMode(false);
        StatusText.Text = "事件已保存";
    }

    private void OpenFolderFromList_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: FolderRecord folder }) return;
        try { ViewModel.OpenFolder(folder); StatusText.Text = $"已打开：{folder.Name}"; }
        catch (Exception ex) { AppLogger.Error("打开文件夹失败", ex); ShowInfo("文件夹不存在或无法访问。"); }
    }

    private void ToggleFolderFavorite_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: FolderRecord folder }) return;
        ViewModel.SetFolderFavorite(folder, !folder.IsFavorite);
        StatusText.Text = folder.IsFavorite ? "文件夹已收藏" : "已取消文件夹收藏";
    }

    private void TaskStatus_DropDownClosed(object sender, EventArgs e)
    {
        if (!IsLoaded || sender is not ComboBox { Tag: EventRecord task }) return;
        ViewModel.SaveEvent(task);
        ViewModel.SelectedEvent = task;
        StatusText.Text = $"任务状态已更新为：{task.Status}";
    }

    private void ArchiveTask_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: EventRecord task }) return;
        if (MessageBox.Show($"把任务“{task.Title}”移入归档？", "归档任务", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.ArchiveTask(task);
        StatusText.Text = "任务已归档";
    }

    private void ShowArchive_Click(object sender, RoutedEventArgs e) =>
        new TaskArchiveWindow(ViewModel) { Owner = this }.ShowDialog();

    private void ShowReminders_Click(object sender, RoutedEventArgs e) =>
        new ReminderViewWindow(ViewModel) { Owner = this }.ShowDialog();

    private void OpenNewTaskDialog()
    {
        var task = new EventRecord();
        var dialog = new TaskEditWindow(task, this, true);
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveEvent(task);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = "任务已添加";
    }

    private void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEvent is null) return;
        if (MessageBox.Show($"确定删除事件“{ViewModel.SelectedEvent.Title}”？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.DeleteEvent(ViewModel.SelectedEvent);
        StatusText.Text = "事件已删除";
    }

    private void NewProgram_Click(object sender, RoutedEventArgs e)
    {
        var program = new ProgramRecord();
        var dialog = new ProgramEditWindow(program, this, true);
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveProgram(program);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = $"启动项“{program.Name}”已加入程序库";
    }

    private void EditProgram_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProgram is not { } program) return;
        var snapshot = CopyProgram(program);
        var dialog = new ProgramEditWindow(program, this, false);
        if (dialog.ShowDialog() != true) { RestoreProgram(program, snapshot); RefreshBindings(); return; }
        var error = ViewModel.SaveProgram(program);
        if (error is not null) { RestoreProgram(program, snapshot); RefreshBindings(); ShowInfo(error); return; }
        StatusText.Text = $"启动项“{program.Name}”已保存";
    }

    private void DeleteProgram_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProgram is not { } program) return;
        if (MessageBox.Show(this, $"从程序库移除“{program.Name}”？\n不会卸载程序或删除任何文件。", "确认移除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var error = ViewModel.DeleteProgram(program);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = "启动项已从程序库移除";
    }

    private async void RunProgram_Click(object sender, RoutedEventArgs e)
    {
        var program = (sender as FrameworkElement)?.Tag as ProgramRecord ?? ViewModel.SelectedProgram;
        if (program is null) return;
        var result = await _workModeService.LaunchProgramAsync(program);
        if (result.Status == "已启动") ViewModel.RecordProgramLaunch(program);
        StatusText.Text = $"{program.Name}：{result.Status}";
        if (result.Status == "失败") { AppLogger.Error($"启动程序库项目失败：{program.Name}，{result.Error}"); ShowInfo($"启动失败：{result.Error}"); }
        else AppLogger.Info($"启动程序库项目：{program.Name}，{result.Status}");
    }

    private void NewWorkMode_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Programs.Count == 0) { ShowInfo("请先在“程序库”中添加至少一个程序、网页或文件夹。"); WorkModeTabControl.SelectedIndex = 1; return; }
        var mode = new WorkModeRecord();
        var dialog = new WorkModeEditWindow(mode, ViewModel.Programs, this, true);
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveWorkMode(mode);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = $"工作模式“{mode.Name}”已创建";
    }

    private void EditWorkMode_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWorkMode is not { } mode) return;
        var snapshot = CopyWorkMode(mode);
        var dialog = new WorkModeEditWindow(mode, ViewModel.Programs, this, false);
        if (dialog.ShowDialog() != true) { RestoreWorkMode(mode, snapshot); RefreshBindings(); return; }
        var error = ViewModel.SaveWorkMode(mode);
        if (error is not null) { RestoreWorkMode(mode, snapshot); RefreshBindings(); ShowInfo(error); return; }
        StatusText.Text = $"工作模式“{mode.Name}”已保存";
    }

    private void DeleteWorkMode_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedWorkMode is not { } mode) return;
        if (MessageBox.Show(this, $"删除工作模式“{mode.Name}”？\n程序库中的启动项不会被删除。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.DeleteWorkMode(mode);
        StatusText.Text = "工作模式已删除";
    }

    private async void RunWorkMode_Click(object sender, RoutedEventArgs e)
    {
        if (_isWorkModeRunning) { StatusText.Text = "已有工作模式正在启动，请稍候"; return; }
        var mode = (sender as FrameworkElement)?.Tag as WorkModeRecord ?? ViewModel.SelectedWorkMode;
        if (mode is null) return;
        var validationError = WorkModeService.ValidateMode(mode, ViewModel.Programs);
        if (validationError is not null) { ShowInfo(validationError); return; }
        if (mode.ConfirmBeforeRun)
        {
            var list = string.Join(Environment.NewLine, mode.Steps.Select((x, index) => $"{index + 1}. {x.DisplayText}"));
            if (MessageBox.Show(this, $"准备启动工作模式“{mode.Name}”：\n\n{list}\n\n是否继续？", "启动工作模式", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }

        _isWorkModeRunning = true;
        RunWorkModeButton.IsEnabled = false;
        StatusText.Text = $"正在启动工作模式：{mode.Name}";
        try
        {
            var summary = await _workModeService.LaunchModeAsync(mode, ViewModel.Programs);
            ViewModel.RecordWorkModeRun(mode, summary);
            StatusText.Text = $"{mode.Name}：{summary.StatusText}";
            AppLogger.Info($"启动工作模式：{mode.Name}，{summary.StatusText}");
            if (summary.FailedCount > 0)
            {
                var failures = string.Join(Environment.NewLine, summary.Results.Where(x => x.Status == "失败").Select(x => $"• {x.Name}：{x.Error}"));
                AppLogger.Error($"工作模式部分项目启动失败：{mode.Name}{Environment.NewLine}{failures}");
                ShowInfo($"工作模式已执行，但有项目失败：\n{failures}");
            }
        }
        catch (OperationCanceledException) { StatusText.Text = "工作模式启动已取消"; }
        finally { _isWorkModeRunning = false; RunWorkModeButton.IsEnabled = true; }
    }

    private void NewCommand_Click(object sender, RoutedEventArgs e)
    {
        var record = new CommandRecord();
        var dialog = new CommandEditWindow(record, this, true);
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveCommand(record);
        if (error is not null) { ShowInfo(error); return; }
        StatusText.Text = $"快捷命令“{record.Name}”已添加";
    }

    private void EditCommand_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCommand is not { } record) return;
        var snapshot = CopyCommand(record);
        var dialog = new CommandEditWindow(record, this, false);
        if (dialog.ShowDialog() != true)
        {
            RestoreCommand(record, snapshot);
            RefreshBindings();
            return;
        }

        var error = ViewModel.SaveCommand(record);
        if (error is not null)
        {
            RestoreCommand(record, snapshot);
            RefreshBindings();
            ShowInfo(error);
            return;
        }
        StatusText.Text = $"快捷命令“{record.Name}”已保存";
    }

    private void DeleteCommand_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCommand is not { } record) return;
        if (MessageBox.Show(this, $"确定删除快捷命令“{record.Name}”？\n只删除按钮记录，不会删除软件或脚本文件。", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ViewModel.DeleteCommand(record);
        StatusText.Text = "快捷命令已删除";
    }

    private void RunCommand_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: CommandRecord record }) ExecuteCommand(record);
    }

    private void RunSelectedCommand_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedCommand is { } record) ExecuteCommand(record);
    }

    private void NewQuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string module }) return;
        var filter = new QuickFilterRecord { Module = module };
        var dialog = new QuickFilterEditWindow(filter, this, true);
        if (dialog.ShowDialog() != true) return;
        var error = ViewModel.SaveQuickFilter(filter);
        if (error is not null) { ShowInfo(error); return; }
        ViewModel.ApplyQuickFilter(filter);
        StatusText.Text = $"快捷筛选标签“{filter.Name}”已固定并应用";
    }

    private void ApplyQuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: QuickFilterRecord filter }) return;
        ViewModel.ApplyQuickFilter(filter);
        StatusText.Text = $"已应用快捷筛选：{filter.Name}";
    }

    private void ClearQuickFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string module }) return;
        ViewModel.ClearQuickFilter(module);
        StatusText.Text = "已清除快捷筛选";
    }

    private void EditQuickFilter_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: QuickFilterRecord filter }) return;
        var snapshot = CopyQuickFilter(filter);
        var dialog = new QuickFilterEditWindow(filter, this, false);
        if (dialog.ShowDialog() != true)
        {
            RestoreQuickFilter(filter, snapshot);
            RefreshBindings();
            return;
        }
        if (dialog.DeleteRequested)
        {
            ViewModel.DeleteQuickFilter(filter);
            StatusText.Text = "快捷筛选标签已删除";
            return;
        }
        var error = ViewModel.SaveQuickFilter(filter);
        if (error is not null)
        {
            RestoreQuickFilter(filter, snapshot);
            RefreshBindings();
            ShowInfo(error);
            return;
        }
        StatusText.Text = $"快捷筛选标签“{filter.Name}”已更新";
    }

    private void ExecuteCommand(CommandRecord record)
    {
        var validationError = CommandExecutionService.Validate(record);
        if (validationError is not null) { ShowInfo(validationError); return; }

        if (record.ConfirmBeforeRun)
        {
            var arguments = string.IsNullOrWhiteSpace(record.Arguments) ? "无" : record.Arguments;
            var workingDirectory = string.IsNullOrWhiteSpace(record.WorkingDirectory) ? "自动选择" : record.WorkingDirectory;
            var detail = $"名称：{record.Name}\n类型：{record.ExecutionType}\n内容：{Shorten(record.TargetSummary, 500)}\n参数：{Shorten(arguments, 300)}\n工作目录：{workingDirectory}\n权限：{record.RunModeText}\n\n确定执行吗？";
            if (MessageBox.Show(this, detail, "确认执行命令", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                StatusText.Text = "已取消执行";
                return;
            }
        }

        try
        {
            var result = _commandExecutionService.Launch(record);
            var status = $"已启动（PID {result.ProcessId}）";
            ViewModel.RecordCommandLaunch(record, status);
            StatusText.Text = $"{record.Name}：{status}";
            AppLogger.Info($"执行快捷命令：{record.Name}，类型={record.ExecutionType}，PID={result.ProcessId}");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            StatusText.Text = "已取消管理员授权，命令未执行";
            AppLogger.Info($"取消快捷命令管理员授权：{record.Name}");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{record.Name}：执行失败";
            AppLogger.Error($"执行快捷命令失败：{record.Name}，类型={record.ExecutionType}", ex);
            ShowInfo($"命令没有成功启动：{ex.Message}\n详细信息已写入日志。");
        }
    }

    private static string Shorten(string text, int maxLength) =>
        text.Length <= maxLength ? text : $"{text[..maxLength]}…";

    private void ShowInfo(string message) => MessageBox.Show(this, message, ViewModel.Settings.ApplicationName, MessageBoxButton.OK, MessageBoxImage.Information);

    private void EditApplicationName_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ApplicationNameWindow(ViewModel.Settings.ApplicationName) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var oldName = ViewModel.Settings.ApplicationName;
        ViewModel.Settings.ApplicationName = dialog.ApplicationName;
        ViewModel.SaveSettings();
        ApplyApplicationIdentity();
        AppLogger.Info($"修改软件名称：{oldName} -> {dialog.ApplicationName}");
        StatusText.Text = "软件名称已更新";
    }

    private void ManageFeatures_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FeatureManagementWindow(ViewModel.Settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var settings = ViewModel.Settings;
        settings.ShowDashboard = dialog.ShowDashboard;
        settings.ShowFolderManagement = dialog.ShowFolders;
        settings.ShowTaskManagement = dialog.ShowTasks;
        settings.ShowCommandCenter = dialog.ShowCommands;
        settings.ShowWorkModes = dialog.ShowWorkModes;
        settings.ShowGitHubTrending = dialog.ShowGitHub;
        settings.ShowFileShare = dialog.ShowFileShare;
        settings.ShowSystemNetwork = dialog.ShowSystemNetwork;
        ViewModel.SaveSettings();
        ApplyFeatureVisibility();
        AppLogger.Info($"更新功能显示：工作台={settings.ShowDashboard}，文件夹={settings.ShowFolderManagement}，任务={settings.ShowTaskManagement}，工作模式={settings.ShowWorkModes}，命令中心={settings.ShowCommandCenter}，GitHub={settings.ShowGitHubTrending}，文件共享={settings.ShowFileShare}，系统网络={settings.ShowSystemNetwork}");
        StatusText.Text = "左侧功能面板已更新";
    }

    private void ApplyApplicationIdentity()
    {
        var name = string.IsNullOrWhiteSpace(ViewModel.Settings.ApplicationName) ? "聚点工作台" : ViewModel.Settings.ApplicationName.Trim();
        ViewModel.Settings.ApplicationName = name;
        Title = name;
        ApplicationNameText.Text = name;
        CurrentApplicationNameText.Text = name;
        ApplicationInitialText.Text = StringInfo.GetNextTextElement(name);
        StartupDescriptionText.Text = $"登录当前 Windows 用户后自动运行{name}。此设置仅影响当前用户，不需要管理员权限。";
    }

    private void ApplyFeatureVisibility()
    {
        var settings = ViewModel.Settings;
        DashboardNavButton.Visibility = settings.ShowDashboard ? Visibility.Visible : Visibility.Collapsed;
        FoldersNavButton.Visibility = settings.ShowFolderManagement ? Visibility.Visible : Visibility.Collapsed;
        TasksNavButton.Visibility = settings.ShowTaskManagement ? Visibility.Visible : Visibility.Collapsed;
        CommandNavButton.Visibility = settings.ShowCommandCenter ? Visibility.Visible : Visibility.Collapsed;
        WorkModesNavButton.Visibility = settings.ShowWorkModes ? Visibility.Visible : Visibility.Collapsed;
        GitHubNavButton.Visibility = settings.ShowGitHubTrending ? Visibility.Visible : Visibility.Collapsed;
        FileShareNavButton.Visibility = settings.ShowFileShare ? Visibility.Visible : Visibility.Collapsed;
        SystemNetworkNavButton.Visibility = settings.ShowSystemNetwork ? Visibility.Visible : Visibility.Collapsed;
        SettingsNavButton.Visibility = Visibility.Visible;

        var featureButtons = new[] { DashboardNavButton, FoldersNavButton, TasksNavButton, WorkModesNavButton, CommandNavButton, GitHubNavButton, FileShareNavButton, SystemNetworkNavButton };
        var checkedButton = featureButtons.Append(SettingsNavButton).FirstOrDefault(x => x.IsChecked == true);
        if (checkedButton?.Visibility != Visibility.Visible)
            (featureButtons.FirstOrDefault(x => x.Visibility == Visibility.Visible) ?? SettingsNavButton).IsChecked = true;

        var enabledCount = featureButtons.Count(x => x.Visibility == Visibility.Visible);
        EnabledFeatureCountText.Text = $"已显示 {enabledCount} / {featureButtons.Length} 个功能";
    }

    private async void RefreshGitHubTrending_Click(object sender, RoutedEventArgs e) =>
        await RefreshGitHubTrendingAsync(showErrorDialog: true);

    private void GitHubFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        Dispatcher.BeginInvoke(() =>
        {
            GitHubStatusText.Text = ViewModel.GitHubTrendCount > 0
                ? "已显示本地缓存，点击“检测热门项目”获取最新结果"
                : "当前筛选没有缓存，点击“检测热门项目”开始查询";
        });
    }

    private async Task RefreshGitHubTrendingAsync(bool showErrorDialog)
    {
        GitHubRefreshButton.IsEnabled = false;
        GitHubRefreshButton.Content = "正在检测…";
        GitHubStatusText.Text = "正在连接 GitHub…";
        StatusText.Text = "正在检测 GitHub 热门项目";
        try
        {
            var result = await _gitHubTrendingService.FetchAsync(ViewModel.SelectedGitHubPeriodDays, ViewModel.SelectedGitHubLanguage);
            ViewModel.ApplyGitHubTrendResult(result);
            var rising = ViewModel.GitHubProjects.Count(x => x.TrendStatus.StartsWith("排名上升", StringComparison.Ordinal) || x.TrendStatus == "热度上升");
            var rateText = result.RateLimitRemaining is null ? string.Empty : $"，搜索额度剩余 {result.RateLimitRemaining}";
            GitHubStatusText.Text = $"检测完成：新上榜 {ViewModel.GitHubNewCount}，热度上升 {rising}{rateText}" + (result.IncompleteResults ? "（结果可能不完整）" : string.Empty);
            StatusText.Text = $"GitHub 热门项目检测完成，共 {ViewModel.GitHubTrendCount} 项";
            AppLogger.Info($"检测 GitHub 热门项目：近 {ViewModel.SelectedGitHubPeriodDays} 天，{ViewModel.SelectedGitHubLanguage}，{ViewModel.GitHubTrendCount} 项");
        }
        catch (Exception ex)
        {
            AppLogger.Error("检测 GitHub 热门项目失败", ex);
            GitHubStatusText.Text = ex.Message;
            StatusText.Text = "GitHub 热门项目检测失败";
            if (showErrorDialog) ShowInfo($"检测失败：{ex.Message}");
        }
        finally
        {
            GitHubRefreshButton.IsEnabled = true;
            GitHubRefreshButton.Content = "检测热门项目";
        }
    }

    private void OpenGitHubProject_Click(object sender, RoutedEventArgs e)
    {
        var project = (sender as FrameworkElement)?.Tag as GitHubProjectRecord ?? ViewModel.SelectedGitHubProject;
        if (project is null || !Uri.TryCreate(project.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            AppLogger.Info($"打开 GitHub 项目：{project.FullName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"打开 GitHub 项目失败：{project.FullName}", ex);
            ShowInfo("无法打开项目页面，详细信息已写入日志。");
        }
    }

    private void CopyGitHubCloneUrl_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGitHubProject is not { CloneUrl.Length: > 0 } project) return;
        Clipboard.SetText(project.CloneUrl);
        StatusText.Text = $"已复制 {project.FullName} 的克隆地址";
        AppLogger.Info($"复制 GitHub 克隆地址：{project.FullName}");
    }

    private async void TranslateGitHubDescription_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGitHubProject is not { } project) return;
        if (!string.IsNullOrWhiteSpace(project.TranslatedDescription))
        {
            project.IsTranslationVisible = !project.IsTranslationVisible;
            return;
        }

        GitHubTranslateButton.IsEnabled = false;
        GitHubTranslateButton.Content = "正在翻译…";
        StatusText.Text = $"正在翻译：{project.FullName}";
        try
        {
            var translation = await _translationService.TranslateToChineseAsync(project.Description);
            ViewModel.SaveGitHubTranslation(project, translation);
            StatusText.Text = "项目说明已翻译并缓存";
            AppLogger.Info($"翻译 GitHub 项目说明：{project.FullName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"翻译 GitHub 项目说明失败：{project.FullName}", ex);
            StatusText.Text = "项目说明翻译失败";
            ShowInfo($"翻译失败：{ex.Message}");
        }
        finally
        {
            GitHubTranslateButton.IsEnabled = true;
            GitHubTranslateButton.SetBinding(ContentControl.ContentProperty, new System.Windows.Data.Binding(nameof(GitHubProjectRecord.TranslationActionText)));
        }
    }

    private async void RefreshSystemNetwork_Click(object sender, RoutedEventArgs e) => await RefreshSystemNetworkAsync();

    private void BrowseFileShareStorage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择共享文件保存目录", Multiselect = false };
        if (dialog.ShowDialog(this) == true) FileShareStoragePathTextBox.Text = dialog.FolderName;
    }

    private async void StartFileShare_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FileSharePortTextBox.Text.Trim(), out var port) || port is < 1024 or > 65535)
        {
            ShowInfo("端口必须是 1024 到 65535 之间的数字。");
            return;
        }
        var password = FileSharePasswordBox.Password;
        if (password.Length < 4)
        {
            ShowInfo("访问密码至少需要 4 个字符。");
            return;
        }
        var storagePath = FileShareStoragePathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            ShowInfo("请选择共享文件保存目录。");
            return;
        }

        StartFileShareButton.IsEnabled = false;
        FileShareStatusText.Text = "正在启动…";
        try
        {
            Directory.CreateDirectory(storagePath);
            _fileShareUrls = await _fileShareService.StartAsync(port, password, storagePath);
            ViewModel.Settings.FileSharePort = port;
            ViewModel.Settings.FileSharePassword = password;
            ViewModel.Settings.FileShareStoragePath = Path.GetFullPath(storagePath);
            ViewModel.SaveSettings();
            FileShareStoragePathTextBox.Text = ViewModel.Settings.FileShareStoragePath;
            FileShareStatusText.Text = "正在运行";
            FileShareAddressesText.Text = string.Join(Environment.NewLine, _fileShareUrls);
            StopFileShareButton.IsEnabled = true;
            OpenFileShareButton.IsEnabled = true;
            CopyFileShareAddressButton.IsEnabled = true;
            FileSharePortTextBox.IsEnabled = FileSharePasswordBox.IsEnabled = FileShareStoragePathTextBox.IsEnabled = false;
            StatusText.Text = $"局域网文件共享已启动，端口 {port}";
            AppLogger.Info($"启动局域网文件共享：端口={port}，目录={ViewModel.Settings.FileShareStoragePath}");
        }
        catch (Exception ex)
        {
            StartFileShareButton.IsEnabled = true;
            FileShareStatusText.Text = "启动失败";
            FileShareAddressesText.Text = ex.Message;
            AppLogger.Error("启动局域网文件共享失败", ex);
            ShowInfo($"文件共享启动失败：{ex.Message}");
        }
    }

    private async void StopFileShare_Click(object sender, RoutedEventArgs e)
    {
        StopFileShareButton.IsEnabled = false;
        FileShareStatusText.Text = "正在停止…";
        try
        {
            await _fileShareService.StopAsync();
            _fileShareUrls = [];
            FileShareStatusText.Text = "尚未启动";
            FileShareAddressesText.Text = "启动服务后显示";
            StartFileShareButton.IsEnabled = true;
            OpenFileShareButton.IsEnabled = false;
            CopyFileShareAddressButton.IsEnabled = false;
            FileSharePortTextBox.IsEnabled = FileSharePasswordBox.IsEnabled = FileShareStoragePathTextBox.IsEnabled = true;
            StatusText.Text = "局域网文件共享已停止";
            AppLogger.Info("停止局域网文件共享");
        }
        catch (Exception ex)
        {
            FileShareStatusText.Text = "停止失败";
            StopFileShareButton.IsEnabled = true;
            AppLogger.Error("停止局域网文件共享失败", ex);
            ShowInfo($"文件共享停止失败：{ex.Message}");
        }
    }

    private void OpenFileShare_Click(object sender, RoutedEventArgs e)
    {
        if (!_fileShareService.IsRunning || !int.TryParse(FileSharePortTextBox.Text, out var port)) return;
        try { Process.Start(new ProcessStartInfo($"http://localhost:{port}") { UseShellExecute = true }); }
        catch (Exception ex) { AppLogger.Error("打开文件共享页面失败", ex); ShowInfo("无法打开浏览器。"); }
    }

    private void CopyFileShareAddress_Click(object sender, RoutedEventArgs e)
    {
        if (_fileShareUrls.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, _fileShareUrls));
        StatusText.Text = _fileShareUrls.Count == 1 ? "访问地址已复制" : $"已复制 {_fileShareUrls.Count} 个访问地址";
    }

    private async Task RefreshSystemNetworkAsync()
    {
        RefreshSystemButton.IsEnabled = false;
        RefreshSystemButton.Content = "正在检测…";
        StatusText.Text = "正在检测电脑和网络信息";
        var selectedName = (NetworkAdapterComboBox.SelectedItem as NetworkAdapterInfo)?.Name;
        if (string.IsNullOrWhiteSpace(selectedName)) selectedName = ViewModel.Settings.SelectedNetworkAdapterName;
        try
        {
            var snapshot = await _systemNetworkService.GetSnapshotAsync(includePublicInfo: true);
            _systemNetworkLoaded = true;
            LocalNetworkStatusText.Text = snapshot.HasLocalNetwork ? "已连接" : "未连接";
            InternetStatusText.Text = snapshot.HasInternet ? "可访问" : "不可访问";
            InternetStatusText.ToolTip = snapshot.InternetDetail;
            PublicIpText.Text = snapshot.PublicIp;
            LocationText.Text = snapshot.Location;
            IspText.Text = $"运营商：{snapshot.Isp}";
            ComputerNameText.Text = snapshot.ComputerName;
            OperatingSystemText.Text = snapshot.OperatingSystem;
            CpuText.Text = snapshot.Cpu;
            MemoryText.Text = snapshot.Memory;
            ArchitectureText.Text = snapshot.Architecture;
            WindowsVersionText.Text = snapshot.WindowsVersion;
            CpuDetailsText.Text = snapshot.CpuDetails;
            MemoryDetailsText.Text = snapshot.MemoryDetails;
            GraphicsText.Text = snapshot.Graphics;
            GraphicsDriverText.Text = snapshot.GraphicsDriver;
            DisplayText.Text = snapshot.Display;
            MotherboardText.Text = snapshot.Motherboard;
            BiosText.Text = snapshot.Bios;
            DrivesText.Text = snapshot.Drives;
            UptimeText.Text = snapshot.Uptime;
            NetworkAdapterComboBox.ItemsSource = snapshot.Adapters;
            NetworkAdapterComboBox.SelectedItem = snapshot.Adapters.FirstOrDefault(x => x.Name == selectedName)
                                                        ?? snapshot.Adapters.FirstOrDefault(x => x.Status == "已连接")
                                                        ?? snapshot.Adapters.FirstOrDefault();
            StatusText.Text = $"系统与网络信息已刷新（{DateTime.Now:HH:mm:ss}）";
            AppLogger.Info("刷新系统与网络信息");
        }
        catch (Exception ex)
        {
            AppLogger.Error("刷新系统与网络信息失败", ex);
            StatusText.Text = "系统与网络信息刷新失败";
            ShowInfo("读取系统或网络信息失败，详细信息已写入日志。");
        }
        finally
        {
            RefreshSystemButton.IsEnabled = true;
            RefreshSystemButton.Content = "刷新全部信息";
        }
    }

    private void NetworkAdapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NetworkAdapterComboBox.SelectedItem is not NetworkAdapterInfo adapter) return;
        if (!string.Equals(ViewModel.Settings.SelectedNetworkAdapterName, adapter.Name, StringComparison.Ordinal))
        {
            ViewModel.Settings.SelectedNetworkAdapterName = adapter.Name;
            ViewModel.SaveSettings();
            AppLogger.Info($"设置默认网卡：{adapter.Name}");
        }
        AdapterDescriptionText.Text = adapter.Description;
        LocalIpText.Text = adapter.LocalIpv4;
        GatewayText.Text = adapter.Gateway;
        DnsText.Text = adapter.DnsServers;
        CurrentIpModeText.Text = adapter.IpModeText;
    }

    private async void OpenIpSettings_Click(object sender, RoutedEventArgs e)
    {
        var adapters = NetworkAdapterComboBox.ItemsSource?.Cast<NetworkAdapterInfo>().ToList() ?? [];
        if (adapters.Count == 0)
        {
            ShowInfo("没有可配置的网卡，请先刷新系统与网络信息。");
            return;
        }
        var selected = NetworkAdapterComboBox.SelectedItem as NetworkAdapterInfo;
        var dialog = new IpSettingsWindow(adapters, selected) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Request is not { } request) return;

        var targetDescription = request.UseDhcp
            ? "自动获取 IP 和 DNS（DHCP）"
            : $"静态 IP {request.IpAddress}，网关 {request.Gateway}，DNS {request.PrimaryDns}";
        var confirmation = $"将网卡“{request.AdapterName}”改为：\n{targetDescription}\n\n应用时网络会短暂断开，并弹出 Windows 管理员确认。是否继续？";
        if (MessageBox.Show(this, confirmation, "确认修改 IP 设置", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        StatusText.Text = "正在应用 IP 设置";
        try
        {
            await _systemNetworkService.ApplyIpConfigurationAsync(request);
            AppLogger.Info($"修改网卡 IP 设置：{request.AdapterName}，{targetDescription}");
            StatusText.Text = "IP 设置已应用，正在刷新";
            await Task.Delay(1200);
            await RefreshSystemNetworkAsync();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            StatusText.Text = "已取消 IP 设置";
            AppLogger.Info($"用户取消管理员授权，未修改网卡：{request.AdapterName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"修改网卡 IP 设置失败：{request.AdapterName}", ex);
            StatusText.Text = "IP 设置失败";
            ShowInfo("IP 设置没有成功。请确认管理员授权及参数是否正确，详细信息已写入日志。");
        }
    }

    private void SaveStartupSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = StartWithWindowsCheckBox.IsChecked == true;
            AutoStartService.SetEnabled(enabled);
            ViewModel.Settings.StartWithWindows = enabled;
            ViewModel.SaveSettings();
            StartupStatusText.Text = enabled ? "已开启，下次登录 Windows 时自动启动。" : "已关闭开机启动。";
            StatusText.Text = "启动设置已保存";
        }
        catch (Exception ex)
        {
            AppLogger.Error("保存开机启动设置失败", ex);
            StartupStatusText.Text = "保存失败，请查看运行日志。";
            ShowInfo("无法修改开机启动设置，详细信息已写入日志。");
        }
    }

    private void SaveBackupSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Settings.BackupRetentionCount = Math.Clamp(ViewModel.Settings.BackupRetentionCount, 1, 100);
        ViewModel.SaveSettings();
        BackupStatusText.Text = ViewModel.Settings.AutoBackupEnabled
            ? $"已开启每日备份，最多保留 {ViewModel.Settings.BackupRetentionCount} 份。"
            : "已关闭每日自动备份，仍可手动创建备份。";
        StatusText.Text = "备份设置已保存";
    }

    private void ShowBackups_Click(object sender, RoutedEventArgs e) =>
        new DataBackupWindow(ViewModel) { Owner = this }.ShowDialog();

    private void CheckAllFolders_Click(object sender, RoutedEventArgs e)
    {
        var result = ViewModel.CheckAllFolderPaths();
        StatusText.Text = $"路径检查完成：{result.Normal} 个正常，{result.Invalid} 个失效";
        if (result.Invalid > 0)
            MessageBox.Show(this, $"检查完成：{result.Normal} 个路径正常，{result.Invalid} 个路径失效。\n可使用“路径失效”筛选查看并编辑路径。", "路径检查", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void TryCreateDailyBackup()
    {
        if (!ViewModel.Settings.AutoBackupEnabled) return;
        try
        {
            var backup = ViewModel.CreateBackup(dailyOnly: true);
            if (backup is not null) BackupStatusText.Text = $"今日自动备份已完成：{backup.CreatedAt:HH:mm}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("每日自动备份失败", ex);
        }
    }

    private void ReminderTimer_Tick(object? sender, EventArgs e)
    {
        var dueTasks = ViewModel.GetDueReminders(DateTime.Now);
        if (dueTasks.Count == 0) return;
        var lines = string.Join(Environment.NewLine, dueTasks.Take(5).Select(x => $"• {x.Title}（{x.PlannedDateTimeText}）"));
        if (dueTasks.Count > 5) lines += $"{Environment.NewLine}另有 {dueTasks.Count - 5} 项任务";
        foreach (var task in dueTasks) ViewModel.MarkReminderShown(task);
        MessageBox.Show(this, lines, "任务提醒", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetFolderEditMode(bool editing)
    {
        if (!IsLoaded) return;
        FolderList.IsEnabled = !editing;
        FolderDisplayPanel.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        FolderDisplayActions.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        FolderEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        FolderEditActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetEventEditMode(bool editing)
    {
        if (!IsLoaded) return;
        EventList.IsEnabled = !editing;
        EventDisplayPanel.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        EventDisplayActions.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        EventEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        EventEditActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshBindings()
    {
        var viewModel = ViewModel;
        DataContext = null;
        DataContext = viewModel;
    }

    private static FolderRecord CopyFolder(FolderRecord source) => new()
    {
        Id = source.Id, Name = source.Name, Path = source.Path, Purpose = source.Purpose,
        Notes = source.Notes, Category = source.Category, Tags = source.Tags,
        IsFavorite = source.IsFavorite, CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt, LastOpenedAt = source.LastOpenedAt,
        LastReadAt = source.LastReadAt, ReadStatus = source.ReadStatus,
        DetectedUnityProjects = source.DetectedUnityProjects.Select(x => new UnityProjectInfo
        {
            ProjectName = x.ProjectName, ProjectPath = x.ProjectPath, EditorVersion = x.EditorVersion, EditorPath = x.EditorPath
        }).ToList()
    };

    private static void RestoreFolder(FolderRecord target, FolderRecord source)
    {
        target.Name = source.Name; target.Path = source.Path; target.Purpose = source.Purpose;
        target.Notes = source.Notes; target.Category = source.Category; target.Tags = source.Tags;
        target.IsFavorite = source.IsFavorite; target.UpdatedAt = source.UpdatedAt;
        target.LastOpenedAt = source.LastOpenedAt; target.LastReadAt = source.LastReadAt;
        target.ReadStatus = source.ReadStatus; target.DetectedUnityProjects = source.DetectedUnityProjects.ToList();
    }

    private static EventRecord CopyEvent(EventRecord source) => new()
    {
        Id = source.Id, Title = source.Title, Content = source.Content,
        OccurredAt = source.OccurredAt, CreatedAt = source.CreatedAt, Type = source.Type,
        Priority = source.Priority, Status = source.Status, Tags = source.Tags,
        CompletionNote = source.CompletionNote, PlannedTime = source.PlannedTime,
        FolderId = source.FolderId, IsSystemLog = source.IsSystemLog,
        IsArchived = source.IsArchived, ArchivedAt = source.ArchivedAt, CompletedAt = source.CompletedAt,
        ReminderEnabled = source.ReminderEnabled, ReminderMinutesBefore = source.ReminderMinutesBefore,
        ReminderSentAt = source.ReminderSentAt, RepeatType = source.RepeatType,
        RepeatInterval = source.RepeatInterval, RepeatEndDate = source.RepeatEndDate,
        RecurrenceSeriesId = source.RecurrenceSeriesId, NextOccurrenceId = source.NextOccurrenceId,
        RepeatAdvanceProcessed = source.RepeatAdvanceProcessed,
        SubTasks = new(source.SubTasks.Select(x => new SubTaskRecord
        {
            Id = x.Id, Title = x.Title, Note = x.Note, IsCompleted = x.IsCompleted,
            CreatedAt = x.CreatedAt, CompletedAt = x.CompletedAt
        }))
    };

    private static void RestoreEvent(EventRecord target, EventRecord source)
    {
        target.Title = source.Title; target.Content = source.Content; target.OccurredAt = source.OccurredAt;
        target.Type = source.Type; target.Priority = source.Priority; target.Status = source.Status;
        target.Tags = source.Tags; target.CompletionNote = source.CompletionNote; target.PlannedTime = source.PlannedTime;
        target.FolderId = source.FolderId; target.IsArchived = source.IsArchived; target.ArchivedAt = source.ArchivedAt;
        target.CompletedAt = source.CompletedAt; target.ReminderEnabled = source.ReminderEnabled;
        target.ReminderMinutesBefore = source.ReminderMinutesBefore; target.ReminderSentAt = source.ReminderSentAt;
        target.RepeatType = source.RepeatType; target.RepeatInterval = source.RepeatInterval;
        target.RepeatEndDate = source.RepeatEndDate; target.RecurrenceSeriesId = source.RecurrenceSeriesId;
        target.NextOccurrenceId = source.NextOccurrenceId; target.RepeatAdvanceProcessed = source.RepeatAdvanceProcessed;
        target.SubTasks.Clear();
        foreach (var item in source.SubTasks)
            target.SubTasks.Add(new SubTaskRecord
            {
                Id = item.Id, Title = item.Title, Note = item.Note, IsCompleted = item.IsCompleted,
                CreatedAt = item.CreatedAt, CompletedAt = item.CompletedAt
            });
    }

    private static ProgramRecord CopyProgram(ProgramRecord source) => new()
    {
        Id = source.Id, Name = source.Name, Category = source.Category, TargetType = source.TargetType,
        Target = source.Target, Arguments = source.Arguments, WorkingDirectory = source.WorkingDirectory,
        RunAsAdministrator = source.RunAsAdministrator, SkipIfRunning = source.SkipIfRunning,
        CreatedAt = source.CreatedAt, LastLaunchedAt = source.LastLaunchedAt, LaunchCount = source.LaunchCount
    };

    private static void RestoreProgram(ProgramRecord target, ProgramRecord source)
    {
        target.Name = source.Name; target.Category = source.Category; target.TargetType = source.TargetType;
        target.Target = source.Target; target.Arguments = source.Arguments; target.WorkingDirectory = source.WorkingDirectory;
        target.RunAsAdministrator = source.RunAsAdministrator; target.SkipIfRunning = source.SkipIfRunning;
        target.LastLaunchedAt = source.LastLaunchedAt; target.LaunchCount = source.LaunchCount;
    }

    private static WorkModeRecord CopyWorkMode(WorkModeRecord source) => new()
    {
        Id = source.Id, Name = source.Name, Description = source.Description,
        ConfirmBeforeRun = source.ConfirmBeforeRun, StopOnFailure = source.StopOnFailure, IsPinned = source.IsPinned,
        Steps = new(source.Steps.Select(CopyWorkModeStep)), CreatedAt = source.CreatedAt,
        LastRunAt = source.LastRunAt, LastRunStatus = source.LastRunStatus, RunCount = source.RunCount
    };

    private static WorkModeStep CopyWorkModeStep(WorkModeStep source) => new()
    {
        Id = source.Id, ProgramId = source.ProgramId, ProgramName = source.ProgramName,
        DelayAfterSeconds = source.DelayAfterSeconds
    };

    private static void RestoreWorkMode(WorkModeRecord target, WorkModeRecord source)
    {
        target.Name = source.Name; target.Description = source.Description;
        target.ConfirmBeforeRun = source.ConfirmBeforeRun; target.StopOnFailure = source.StopOnFailure;
        target.IsPinned = source.IsPinned; target.LastRunAt = source.LastRunAt;
        target.LastRunStatus = source.LastRunStatus; target.RunCount = source.RunCount;
        target.Steps = new(source.Steps.Select(CopyWorkModeStep));
    }

    private static CommandRecord CopyCommand(CommandRecord source) => new()
    {
        Id = source.Id, Name = source.Name, Description = source.Description, Category = source.Category, Tags = source.Tags,
        ExecutionType = source.ExecutionType, CommandText = source.CommandText, ProgramPath = source.ProgramPath,
        BatchPath = source.BatchPath, Arguments = source.Arguments, WorkingDirectory = source.WorkingDirectory,
        RunAsAdministrator = source.RunAsAdministrator, ConfirmBeforeRun = source.ConfirmBeforeRun,
        KeepWindowOpen = source.KeepWindowOpen, CreatedAt = source.CreatedAt, LastRunAt = source.LastRunAt,
        RunCount = source.RunCount, LastRunStatus = source.LastRunStatus
    };

    private static void RestoreCommand(CommandRecord target, CommandRecord source)
    {
        target.Name = source.Name; target.Description = source.Description; target.Category = source.Category; target.Tags = source.Tags;
        target.ExecutionType = source.ExecutionType; target.CommandText = source.CommandText;
        target.ProgramPath = source.ProgramPath; target.BatchPath = source.BatchPath;
        target.Arguments = source.Arguments; target.WorkingDirectory = source.WorkingDirectory;
        target.RunAsAdministrator = source.RunAsAdministrator; target.ConfirmBeforeRun = source.ConfirmBeforeRun;
        target.KeepWindowOpen = source.KeepWindowOpen; target.LastRunAt = source.LastRunAt;
        target.RunCount = source.RunCount; target.LastRunStatus = source.LastRunStatus;
    }

    private static QuickFilterRecord CopyQuickFilter(QuickFilterRecord source) => new()
    {
        Id = source.Id, Module = source.Module, Name = source.Name, Color = source.Color, SortOrder = source.SortOrder, IsPinned = source.IsPinned,
        Category = source.Category, Tags = source.Tags, TagMatchMode = source.TagMatchMode, Status = source.Status,
        Type = source.Type, Priority = source.Priority, FavoriteMode = source.FavoriteMode,
        UnityMode = source.UnityMode, AdministratorMode = source.AdministratorMode, ReminderMode = source.ReminderMode, IsActive = source.IsActive
    };

    private static void RestoreQuickFilter(QuickFilterRecord target, QuickFilterRecord source)
    {
        target.Name = source.Name; target.Color = source.Color; target.Category = source.Category;
        target.Tags = source.Tags; target.TagMatchMode = source.TagMatchMode; target.Status = source.Status;
        target.Type = source.Type; target.Priority = source.Priority; target.FavoriteMode = source.FavoriteMode;
        target.UnityMode = source.UnityMode; target.AdministratorMode = source.AdministratorMode;
        target.ReminderMode = source.ReminderMode;
        target.IsPinned = source.IsPinned; target.IsActive = source.IsActive;
    }
}
