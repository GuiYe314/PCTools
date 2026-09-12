using System.Collections.ObjectModel;
using System.Diagnostics;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;

namespace JuDianWorkbench.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly JsonDataStore _store;
    private readonly BackupService _backupService;
    private readonly AppData _data;
    private FolderRecord? _selectedFolder;
    private EventRecord? _selectedEvent;
    private string _folderSearch = string.Empty;
    private string _eventSearch = string.Empty;
    private string _selectedTaskTag = "全部标签";
    private string _selectedFolderCategory = "全部分类";
    private string _selectedFolderStatus = "全部状态";
    private bool _favoriteFoldersOnly;
    private readonly List<GitHubProjectRecord> _allGitHubProjects = [];
    private GitHubProjectRecord? _selectedGitHubProject;
    private string _gitHubProjectSearch = string.Empty;
    private string _gitHubLastUpdatedText = "尚未检测";
    private CommandRecord? _selectedCommand;
    private ProgramRecord? _selectedProgram;
    private WorkModeRecord? _selectedWorkMode;
    private string _commandSearch = string.Empty;
    private string _selectedCommandCategory = "全部分类";

    public const string FolderFilterModule = "Folders";
    public const string TaskFilterModule = "Tasks";
    public const string CommandFilterModule = "Commands";

    public ObservableCollection<FolderRecord> Folders { get; } = [];
    public ObservableCollection<FolderRecord> FavoriteFolders { get; } = [];
    public ObservableCollection<EventRecord> Events { get; } = [];
    public ObservableCollection<EventRecord> DashboardTasks { get; } = [];
    public ObservableCollection<EventRecord> ArchivedTasks { get; } = [];
    public IReadOnlyList<string> TaskTagFilters { get; private set; } = ["全部标签"];
    public ObservableCollection<FolderTreeNode> FolderTree { get; } = [];
    public ObservableCollection<GitHubProjectRecord> GitHubProjects { get; } = [];
    public ObservableCollection<CommandRecord> Commands { get; } = [];
    public ObservableCollection<ProgramRecord> Programs { get; } = [];
    public ObservableCollection<WorkModeRecord> WorkModes { get; } = [];
    public ObservableCollection<WorkModeRecord> PinnedWorkModes { get; } = [];
    public ObservableCollection<QuickFilterRecord> FolderQuickFilters { get; } = [];
    public ObservableCollection<QuickFilterRecord> TaskQuickFilters { get; } = [];
    public ObservableCollection<QuickFilterRecord> CommandQuickFilters { get; } = [];
    public IReadOnlyList<string> CommandCategoryFilters { get; private set; } = ["全部分类"];
    public string[] FolderCategories { get; } = ["项目", "文档", "素材", "备份", "归档", "软件", "临时文件", "其他"];
    public string[] FolderCategoryFilters { get; } = ["全部分类", "项目", "文档", "素材", "备份", "归档", "软件", "临时文件", "其他"];
    public string[] FolderStatusFilters { get; } = ["全部状态", "正常", "路径失效"];
    public string[] EventTypes { get; } = ["普通任务", "工作任务", "个人任务", "提醒任务"];
    public string[] EventPriorities { get; } = ["普通", "重要", "紧急"];
    public string[] EventStatuses { get; } = ["未完成", "已完成", "无法完成", "已丢弃"];
    public string[] RepeatTypes { get; } = ["不重复", "每天", "每周", "每月", "每年"];
    public int[] RepeatIntervals { get; } = Enumerable.Range(1, 30).ToArray();
    public int[] BackupRetentionOptions { get; } = [7, 14, 30, 60];
    public GitHubTrendPeriod[] GitHubPeriodOptions { get; } = [new("近 7 天", 7), new("近 30 天", 30), new("近 90 天", 90)];
    public string[] GitHubLanguages { get; } = ["全部语言", "C#", "C++", "Python", "JavaScript", "TypeScript", "Java", "Go", "Rust", "Kotlin", "Swift", "PHP", "PowerShell"];
    public string[] CommandTypes => CommandExecutionService.SupportedTypes;

    public MainViewModel(JsonDataStore? store = null)
    {
        _store = store ?? new JsonDataStore();
        _backupService = new BackupService(_store.DataFilePath);
        _data = _store.Load();
        MigrateAndNormalizeData();
        RefreshFolders();
        RebuildTaskTags();
        RefreshEvents();
        RebuildCommandCategories();
        RefreshCommands();
        RefreshPrograms();
        RefreshWorkModes();
        RefreshQuickFilterCollections();
        LoadCachedGitHubProjects();
    }

    public FolderRecord? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            _selectedFolder = value;
            RaisePropertyChanged();
            RefreshFolderTree();
        }
    }

    public EventRecord? SelectedEvent
    {
        get => _selectedEvent;
        set { _selectedEvent = value; RaisePropertyChanged(); }
    }

    public string FolderSearch
    {
        get => _folderSearch;
        set { _folderSearch = value; RaisePropertyChanged(); RefreshFolders(); }
    }

    public string EventSearch
    {
        get => _eventSearch;
        set { _eventSearch = value; RaisePropertyChanged(); RefreshEvents(); }
    }

    public string SelectedTaskTag
    {
        get => _selectedTaskTag;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "全部标签" : value;
            if (_selectedTaskTag == normalized) return;
            _selectedTaskTag = normalized;
            DeactivateQuickFilters(TaskFilterModule);
            RaisePropertyChanged();
            RefreshEvents();
        }
    }

    public string SelectedFolderCategory
    {
        get => _selectedFolderCategory;
        set { if (_selectedFolderCategory == value) return; _selectedFolderCategory = value; DeactivateQuickFilters(FolderFilterModule); RaisePropertyChanged(); RefreshFolders(); }
    }

    public string SelectedFolderStatus
    {
        get => _selectedFolderStatus;
        set { if (_selectedFolderStatus == value) return; _selectedFolderStatus = value; DeactivateQuickFilters(FolderFilterModule); RaisePropertyChanged(); RefreshFolders(); }
    }

    public bool FavoriteFoldersOnly
    {
        get => _favoriteFoldersOnly;
        set { if (_favoriteFoldersOnly == value) return; _favoriteFoldersOnly = value; DeactivateQuickFilters(FolderFilterModule); RaisePropertyChanged(); RefreshFolders(); }
    }

    public GitHubProjectRecord? SelectedGitHubProject
    {
        get => _selectedGitHubProject;
        set { _selectedGitHubProject = value; RaisePropertyChanged(); }
    }

    public CommandRecord? SelectedCommand
    {
        get => _selectedCommand;
        set { _selectedCommand = value; RaisePropertyChanged(); }
    }

    public ProgramRecord? SelectedProgram
    {
        get => _selectedProgram;
        set { _selectedProgram = value; RaisePropertyChanged(); }
    }

    public WorkModeRecord? SelectedWorkMode
    {
        get => _selectedWorkMode;
        set { _selectedWorkMode = value; RaisePropertyChanged(); }
    }

    public string CommandSearch
    {
        get => _commandSearch;
        set { _commandSearch = value; RaisePropertyChanged(); RefreshCommands(); }
    }

    public string SelectedCommandCategory
    {
        get => _selectedCommandCategory;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "全部分类" : value;
            if (_selectedCommandCategory == normalized) return;
            _selectedCommandCategory = normalized;
            DeactivateQuickFilters(CommandFilterModule);
            RaisePropertyChanged();
            RefreshCommands();
        }
    }

    public string GitHubProjectSearch
    {
        get => _gitHubProjectSearch;
        set { _gitHubProjectSearch = value; RaisePropertyChanged(); RefreshGitHubProjects(); }
    }

    public int SelectedGitHubPeriodDays
    {
        get => Settings.GitHubTrendPeriodDays;
        set
        {
            var normalized = value is 7 or 30 or 90 ? value : 7;
            if (Settings.GitHubTrendPeriodDays == normalized) return;
            Settings.GitHubTrendPeriodDays = normalized;
            _store.Save(_data);
            RaisePropertyChanged();
            LoadCachedGitHubProjects();
        }
    }

    public string SelectedGitHubLanguage
    {
        get => Settings.GitHubTrendLanguage;
        set
        {
            var normalized = GitHubLanguages.Contains(value) ? value : "全部语言";
            if (Settings.GitHubTrendLanguage == normalized) return;
            Settings.GitHubTrendLanguage = normalized;
            _store.Save(_data);
            RaisePropertyChanged();
            LoadCachedGitHubProjects();
        }
    }

    public string GitHubLastUpdatedText
    {
        get => _gitHubLastUpdatedText;
        private set { _gitHubLastUpdatedText = value; RaisePropertyChanged(); }
    }

    public int GitHubTrendCount => _allGitHubProjects.Count;
    public int GitHubNewCount => _allGitHubProjects.Count(x => x.TrendStatus == "新上榜");
    public int CommandCount => _data.Commands.Count;
    public int ProgramCount => _data.Programs.Count;
    public int WorkModeCount => _data.WorkModes.Count;
    public int PinnedWorkModeCount => _data.WorkModes.Count(x => x.IsPinned);

    public int FolderCount => _data.Folders.Count;
    public int FavoriteFolderCount => _data.Folders.Count(x => x.IsFavorite);
    public int InvalidFolderCount => _data.Folders.Count(x => !x.Exists);
    public int EventCount => _data.Events.Count(x => !x.IsArchived);
    public int OpenTaskCount => _data.Events.Count(x => !x.IsArchived && x.Status == "未完成");
    public int OverdueTaskCount => _data.Events.Count(x => !x.IsArchived && x.IsOverdue);
    public int ArchivedTaskCount => _data.Events.Count(x => x.IsArchived);
    public AppSettings Settings => _data.Settings;

    public void NewFolder() => SelectedFolder = new FolderRecord();

    public string? SaveFolder(FolderRecord folder)
    {
        folder.Path = folder.Path.Trim();
        folder.Name = folder.Name.Trim();
        if (string.IsNullOrWhiteSpace(folder.Path)) return "请选择文件夹路径。";
        if (!Directory.Exists(folder.Path)) return "指定的文件夹不存在或当前没有访问权限。";
        if (string.IsNullOrWhiteSpace(folder.Name))
            folder.Name = Path.GetFileName(folder.Path.TrimEnd(Path.DirectorySeparatorChar));
        if (_data.Folders.Any(x => x.Id != folder.Id && string.Equals(x.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
            return "这个文件夹已经加入管理。";

        var existing = _data.Folders.FirstOrDefault(x => x.Id == folder.Id);
        folder.UpdatedAt = DateTime.Now;
        if (existing is null) _data.Folders.Add(folder);
        SaveAndRefresh();
        AppLogger.Info(existing is null ? $"添加文件夹：{folder.Name}" : $"修改文件夹：{folder.Name}");
        SelectedFolder = folder;
        return null;
    }

    public void DeleteFolder(FolderRecord folder)
    {
        _data.Folders.RemoveAll(x => x.Id == folder.Id);
        foreach (var item in _data.Events.Where(x => x.FolderId == folder.Id)) item.FolderId = null;
        AppLogger.Info($"移除文件夹管理记录：{folder.Name}");
        SaveAndRefresh();
        SelectedFolder = Folders.FirstOrDefault();
    }

    public void SetFolderFavorite(FolderRecord folder, bool isFavorite)
    {
        var stored = _data.Folders.FirstOrDefault(x => x.Id == folder.Id);
        if (stored is null) return;
        stored.IsFavorite = isFavorite;
        stored.UpdatedAt = DateTime.Now;
        _store.Save(_data);
        RefreshFolders();
        SelectedFolder = stored;
        AppLogger.Info($"{(isFavorite ? "收藏" : "取消收藏")}文件夹：{stored.Name}");
    }

    public void SaveFolderReadResult(FolderRecord folder, IReadOnlyList<UnityProjectInfo> projects)
    {
        var stored = _data.Folders.FirstOrDefault(x => x.Id == folder.Id);
        if (stored is null) return;
        stored.DetectedUnityProjects = projects.ToList();
        stored.LastReadAt = DateTime.Now;
        stored.ReadStatus = projects.Count == 0 ? "读取完成，未发现 Unity 项目" : $"读取完成，发现 {projects.Count} 个 Unity 项目";
        stored.UpdatedAt = DateTime.Now;
        _store.Save(_data);
        RefreshFolders();
        SelectedFolder = stored;
    }

    public void ClearFolderReadResult(FolderRecord folder)
    {
        folder.DetectedUnityProjects = [];
        folder.LastReadAt = null;
        folder.ReadStatus = "路径已修改，请重新读取";
    }

    public (int Normal, int Invalid) CheckAllFolderPaths()
    {
        RefreshFolders();
        var normal = _data.Folders.Count(x => x.Exists);
        return (normal, _data.Folders.Count - normal);
    }

    public void OpenFolder(FolderRecord folder)
    {
        if (!folder.Exists) throw new DirectoryNotFoundException(folder.Path);
        Process.Start(new ProcessStartInfo("explorer.exe", folder.Path) { UseShellExecute = true });
        folder.LastOpenedAt = DateTime.Now;
        AppLogger.Info($"打开文件夹：{folder.Name}");
        SaveAndRefresh();
    }

    public void NewEvent() => SelectedEvent = new EventRecord();

    public string? SaveEvent(EventRecord record)
    {
        record.Title = record.Title.Trim();
        if (string.IsNullOrWhiteSpace(record.Title)) return "请输入任务标题。";
        if (!TimeOnly.TryParse(record.PlannedTime, out _)) return "计划时间格式不正确，请输入类似 09:30 的时间。";
        record.RepeatInterval = Math.Clamp(record.RepeatInterval, 1, 30);
        if (!RepeatTypes.Contains(record.RepeatType)) record.RepeatType = "不重复";
        if (record.RepeatEndDate is not null && record.RepeatEndDate.Value.Date < record.OccurredAt.Date)
            return "重复结束日期不能早于当前任务的计划日期。";
        foreach (var subTask in record.SubTasks)
        {
            subTask.Title = subTask.Title.Trim();
            subTask.Note = subTask.Note.Trim();
            if (string.IsNullOrWhiteSpace(subTask.Title)) return "子任务标题不能为空。";
            if (subTask.IsCompleted) subTask.CompletedAt ??= DateTime.Now;
            else subTask.CompletedAt = null;
        }
        if (record.Status == "已完成") record.CompletedAt ??= DateTime.Now;
        else record.CompletedAt = null;
        var existing = _data.Events.FirstOrDefault(x => x.Id == record.Id);
        if (existing is null) _data.Events.Add(record);
        var nextOccurrence = CreateNextOccurrenceIfNeeded(record);
        SaveAndRefresh(rebuildTaskTags: true);
        SelectedEvent = record;
        if (nextOccurrence is not null)
            AppLogger.Info($"重复任务生成下一期：{record.Title}，计划 {nextOccurrence.PlannedDateTimeText}");
        return null;
    }

    public void DeleteEvent(EventRecord record)
    {
        _data.Events.RemoveAll(x => x.Id == record.Id);
        SaveAndRefresh(rebuildTaskTags: true);
        SelectedEvent = Events.FirstOrDefault();
    }

    public void ArchiveTask(EventRecord record)
    {
        record.IsArchived = true;
        record.ArchivedAt = DateTime.Now;
        AppLogger.Info($"归档任务：{record.Title}");
        SaveAndRefresh(rebuildTaskTags: true);
        SelectedEvent = Events.FirstOrDefault();
    }

    public void RestoreTask(EventRecord record)
    {
        record.IsArchived = false;
        record.ArchivedAt = null;
        AppLogger.Info($"恢复归档任务：{record.Title}");
        SaveAndRefresh(rebuildTaskTags: true);
    }

    public void SaveSettings() => _store.Save(_data);

    public string? SaveCommand(CommandRecord record)
    {
        var error = CommandExecutionService.Validate(record);
        if (error is not null) return error;
        var existing = _data.Commands.FirstOrDefault(x => x.Id == record.Id);
        if (existing is null) _data.Commands.Add(record);
        _store.Save(_data);
        RebuildCommandCategories();
        RefreshCommands();
        SelectedCommand = record;
        AppLogger.Info(existing is null ? $"添加快捷命令：{record.Name}" : $"修改快捷命令：{record.Name}");
        return null;
    }

    public void DeleteCommand(CommandRecord record)
    {
        _data.Commands.RemoveAll(x => x.Id == record.Id);
        _store.Save(_data);
        RebuildCommandCategories();
        RefreshCommands();
        SelectedCommand = Commands.FirstOrDefault();
        RaisePropertyChanged(nameof(CommandCount));
        AppLogger.Info($"删除快捷命令：{record.Name}");
    }

    public void RecordCommandLaunch(CommandRecord record, string status)
    {
        record.LastRunAt = DateTime.Now;
        record.RunCount++;
        record.LastRunStatus = status;
        _store.Save(_data);
        RefreshCommands();
        SelectedCommand = record;
        RaisePropertyChanged(nameof(CommandCount));
    }

    public string? SaveProgram(ProgramRecord program)
    {
        var error = WorkModeService.ValidateProgram(program);
        if (error is not null) return error;
        if (_data.Programs.Any(x => x.Id != program.Id && x.Name.Equals(program.Name, StringComparison.OrdinalIgnoreCase)))
            return "程序库中已经存在同名启动项。";
        var existing = _data.Programs.FirstOrDefault(x => x.Id == program.Id);
        if (existing is null) _data.Programs.Add(program);
        foreach (var step in _data.WorkModes.SelectMany(x => x.Steps).Where(x => x.ProgramId == program.Id)) step.ProgramName = program.Name;
        _store.Save(_data);
        RefreshPrograms();
        RefreshWorkModes();
        SelectedProgram = program;
        RaisePropertyChanged(nameof(ProgramCount));
        AppLogger.Info(existing is null ? $"添加程序库启动项：{program.Name}" : $"修改程序库启动项：{program.Name}");
        return null;
    }

    public string? DeleteProgram(ProgramRecord program)
    {
        var usedBy = _data.WorkModes.Where(x => x.Steps.Any(step => step.ProgramId == program.Id)).Select(x => x.Name).ToList();
        if (usedBy.Count > 0) return $"该启动项正被工作模式“{string.Join("、", usedBy)}”使用，请先从模式中移除。";
        _data.Programs.RemoveAll(x => x.Id == program.Id);
        _store.Save(_data);
        RefreshPrograms();
        SelectedProgram = Programs.FirstOrDefault();
        RaisePropertyChanged(nameof(ProgramCount));
        AppLogger.Info($"删除程序库启动项：{program.Name}");
        return null;
    }

    public void RecordProgramLaunch(ProgramRecord program)
    {
        program.LastLaunchedAt = DateTime.Now;
        program.LaunchCount++;
        _store.Save(_data);
        RefreshPrograms();
        SelectedProgram = program;
    }

    public string? SaveWorkMode(WorkModeRecord mode)
    {
        var error = WorkModeService.ValidateMode(mode, _data.Programs);
        if (error is not null) return error;
        if (_data.WorkModes.Any(x => x.Id != mode.Id && x.Name.Equals(mode.Name, StringComparison.OrdinalIgnoreCase)))
            return "已经存在同名工作模式。";
        var existing = _data.WorkModes.FirstOrDefault(x => x.Id == mode.Id);
        if (existing is null) _data.WorkModes.Add(mode);
        _store.Save(_data);
        RefreshWorkModes();
        SelectedWorkMode = mode;
        RaisePropertyChanged(nameof(WorkModeCount));
        RaisePropertyChanged(nameof(PinnedWorkModeCount));
        AppLogger.Info(existing is null ? $"添加工作模式：{mode.Name}" : $"修改工作模式：{mode.Name}");
        return null;
    }

    public void DeleteWorkMode(WorkModeRecord mode)
    {
        _data.WorkModes.RemoveAll(x => x.Id == mode.Id);
        _store.Save(_data);
        RefreshWorkModes();
        SelectedWorkMode = WorkModes.FirstOrDefault();
        RaisePropertyChanged(nameof(WorkModeCount));
        RaisePropertyChanged(nameof(PinnedWorkModeCount));
        AppLogger.Info($"删除工作模式：{mode.Name}");
    }

    public void RecordWorkModeRun(WorkModeRecord mode, WorkModeRunSummary summary)
    {
        mode.LastRunAt = DateTime.Now;
        mode.LastRunStatus = summary.StatusText;
        mode.RunCount++;
        for (var index = 0; index < summary.Results.Count && index < mode.Steps.Count; index++)
        {
            if (summary.Results[index].Status != "已启动") continue;
            var program = _data.Programs.FirstOrDefault(x => x.Id == mode.Steps[index].ProgramId);
            if (program is null) continue;
            program.LastLaunchedAt = DateTime.Now;
            program.LaunchCount++;
        }
        _store.Save(_data);
        RefreshPrograms();
        RefreshWorkModes();
        SelectedWorkMode = mode;
    }

    public string? SaveQuickFilter(QuickFilterRecord filter)
    {
        filter.Name = filter.Name?.Trim() ?? string.Empty;
        filter.Tags = filter.Tags?.Trim() ?? string.Empty;
        filter.Color = string.IsNullOrWhiteSpace(filter.Color) ? "#566FEB" : filter.Color;
        if (string.IsNullOrWhiteSpace(filter.Name)) return "请输入快捷筛选标签名称。";
        if (filter.Module is not (FolderFilterModule or TaskFilterModule or CommandFilterModule)) return "快捷筛选所属模块无效。";
        if (_data.QuickFilters.Any(x => x.Id != filter.Id && x.Module == filter.Module && x.Name.Equals(filter.Name, StringComparison.OrdinalIgnoreCase)))
            return "当前页面已经存在同名的快捷筛选标签。";
        var existing = _data.QuickFilters.FirstOrDefault(x => x.Id == filter.Id);
        if (existing is null)
        {
            filter.SortOrder = _data.QuickFilters.Where(x => x.Module == filter.Module).Select(x => x.SortOrder).DefaultIfEmpty(-1).Max() + 1;
            _data.QuickFilters.Add(filter);
        }
        _store.Save(_data);
        RefreshQuickFilterCollections();
        RefreshModule(filter.Module);
        AppLogger.Info($"{(existing is null ? "添加" : "修改")}快捷筛选标签：{filter.Module}/{filter.Name}");
        return null;
    }

    public void DeleteQuickFilter(QuickFilterRecord filter)
    {
        _data.QuickFilters.RemoveAll(x => x.Id == filter.Id);
        _store.Save(_data);
        RefreshQuickFilterCollections();
        RefreshModule(filter.Module);
        AppLogger.Info($"删除快捷筛选标签：{filter.Module}/{filter.Name}");
    }

    public void ApplyQuickFilter(QuickFilterRecord filter)
    {
        foreach (var item in _data.QuickFilters.Where(x => x.Module == filter.Module)) item.IsActive = item.Id == filter.Id;
        ResetManualFilters(filter.Module);
        _store.Save(_data);
        RefreshQuickFilterCollections();
        RefreshModule(filter.Module);
    }

    public void ClearQuickFilter(string module)
    {
        DeactivateQuickFilters(module);
        ResetManualFilters(module);
        _store.Save(_data);
        RefreshQuickFilterCollections();
        RefreshModule(module);
    }

    public IReadOnlyList<string> GetAvailableTags(string module) => module switch
    {
        FolderFilterModule => _data.Folders.SelectMany(x => SplitTags(x.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
        TaskFilterModule => _data.Events.SelectMany(x => SplitTags(x.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
        CommandFilterModule => _data.Commands.SelectMany(x => SplitTags(x.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
        _ => []
    };

    public IReadOnlyList<QuickFilterRecord> GetQuickFilterLayout(string module) => _data.QuickFilters
        .Where(x => x.Module == module).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        .Select(x => new QuickFilterRecord
        {
            Id = x.Id, Module = x.Module, Name = x.Name, Color = x.Color, SortOrder = x.SortOrder, IsPinned = x.IsPinned,
            Category = x.Category, Tags = x.Tags, TagMatchMode = x.TagMatchMode, Status = x.Status,
            Type = x.Type, Priority = x.Priority, FavoriteMode = x.FavoriteMode,
            UnityMode = x.UnityMode, AdministratorMode = x.AdministratorMode, ReminderMode = x.ReminderMode
        }).ToList();

    public void SaveQuickFilterLayout(string module, IReadOnlyList<QuickFilterRecord> layout)
    {
        for (var index = 0; index < layout.Count; index++)
        {
            var source = layout[index];
            var stored = _data.QuickFilters.FirstOrDefault(x => x.Id == source.Id && x.Module == module);
            if (stored is null) continue;
            stored.SortOrder = index;
            stored.IsPinned = source.IsPinned;
            if (!stored.IsPinned) stored.IsActive = false;
        }
        _store.Save(_data);
        RefreshQuickFilterCollections();
        RefreshModule(module);
        AppLogger.Info($"更新快捷筛选标签布局：{module}");
    }

    public void ApplyGitHubTrendResult(GitHubTrendResult result)
    {
        var key = GitHubTrendingService.BuildSnapshotKey(SelectedGitHubPeriodDays, SelectedGitHubLanguage);
        var previous = _data.GitHubTrendSnapshots.FirstOrDefault(x => x.Key == key);
        var projects = result.Projects.ToList();
        var knownTranslations = _data.GitHubTrendSnapshots.SelectMany(x => x.Projects)
            .Where(x => !string.IsNullOrWhiteSpace(x.TranslatedDescription))
            .GroupBy(x => x.RepositoryId).ToDictionary(x => x.Key, x => x.First().TranslatedDescription);
        foreach (var project in projects.Where(x => string.IsNullOrWhiteSpace(x.TranslatedDescription)))
            if (knownTranslations.TryGetValue(project.RepositoryId, out var translation)) project.TranslatedDescription = translation;
        GitHubTrendingService.CompareWithPrevious(projects, previous, result.FetchedAt);
        _data.GitHubTrendSnapshots.RemoveAll(x => x.Key == key);
        _data.GitHubTrendSnapshots.Add(new GitHubTrendSnapshot { Key = key, FetchedAt = result.FetchedAt, Projects = projects });
        foreach (var old in _data.GitHubTrendSnapshots.OrderByDescending(x => x.FetchedAt).Skip(24).ToList())
            _data.GitHubTrendSnapshots.Remove(old);
        _store.Save(_data);
        LoadCachedGitHubProjects();
    }

    public void SaveGitHubTranslation(GitHubProjectRecord project, string translatedDescription)
    {
        project.TranslatedDescription = translatedDescription.Trim();
        project.IsTranslationVisible = true;
        foreach (var cached in _data.GitHubTrendSnapshots.SelectMany(x => x.Projects).Where(x => x.RepositoryId == project.RepositoryId))
            cached.TranslatedDescription = project.TranslatedDescription;
        _store.Save(_data);
    }

    public BackupInfo? CreateBackup(bool dailyOnly = false) =>
        _backupService.CreateBackup(Settings.BackupRetentionCount, dailyOnly);

    public IReadOnlyList<BackupInfo> GetBackups() => _backupService.GetBackups();

    public void RestoreBackup(BackupInfo backup) => _backupService.Restore(backup);

    public IReadOnlyList<EventRecord> GetDueReminders(DateTime now) => _data.Events
        .Where(x => !x.IsArchived && x.Status == "未完成" && x.ReminderEnabled && x.ReminderSentAt is null)
        .Where(x => now >= x.DueAt.AddMinutes(-x.ReminderMinutesBefore))
        .OrderBy(x => x.DueAt).ToList();

    public IReadOnlyList<EventRecord> GetReminderTasks() => _data.Events
        .Where(x => !x.IsArchived && x.ReminderEnabled)
        .OrderBy(x => x.DueAt)
        .ToList();

    public void MarkReminderShown(EventRecord task)
    {
        task.ReminderSentAt = DateTime.Now;
        _store.Save(_data);
    }

    public string FolderName(Guid? id) => id is null ? "未关联" : _data.Folders.FirstOrDefault(x => x.Id == id)?.Name ?? "原文件夹已移除";

    private void RefreshFolders()
    {
        var selectedId = SelectedFolder?.Id;
        var query = _data.Folders.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(FolderSearch))
            query = query.Where(x => $"{x.Name} {x.Path} {x.Purpose} {x.Category} {x.Tags}".Contains(FolderSearch, StringComparison.OrdinalIgnoreCase));
        if (SelectedFolderCategory != "全部分类") query = query.Where(x => x.Category == SelectedFolderCategory);
        if (SelectedFolderStatus != "全部状态") query = query.Where(x => x.StatusText == SelectedFolderStatus);
        if (FavoriteFoldersOnly) query = query.Where(x => x.IsFavorite);
        if (ActiveQuickFilter(FolderFilterModule) is { } quickFilter)
        {
            if (quickFilter.Category != "不限") query = query.Where(x => x.Category.Equals(quickFilter.Category, StringComparison.OrdinalIgnoreCase));
            if (quickFilter.Status != "不限") query = query.Where(x => x.StatusText == quickFilter.Status);
            if (quickFilter.FavoriteMode == "只看收藏") query = query.Where(x => x.IsFavorite);
            if (quickFilter.FavoriteMode == "不看收藏") query = query.Where(x => !x.IsFavorite);
            if (quickFilter.UnityMode == "只看 Unity 项目") query = query.Where(x => x.HasUnityProjects);
            if (quickFilter.UnityMode == "不看 Unity 项目") query = query.Where(x => !x.HasUnityProjects);
            query = ApplyTagFilter(query, quickFilter, x => x.Tags);
        }
        Folders.Clear();
        foreach (var item in query.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Name)) Folders.Add(item);
        FavoriteFolders.Clear();
        foreach (var item in _data.Folders.Where(x => x.IsFavorite).OrderBy(x => x.Name)) FavoriteFolders.Add(item);
        RaiseSummaryProperties();
        SelectedFolder = Folders.FirstOrDefault(x => x.Id == selectedId) ?? Folders.FirstOrDefault();
    }

    private void RefreshEvents()
    {
        var selectedId = SelectedEvent?.Id;
        var query = _data.Events.Where(x => !x.IsArchived);
        if (!string.IsNullOrWhiteSpace(EventSearch))
            query = query.Where(x => $"{x.Title} {x.Content} {x.Type} {x.Status} {x.Tags}".Contains(EventSearch, StringComparison.OrdinalIgnoreCase));
        if (SelectedTaskTag != "全部标签")
            query = query.Where(x => SplitTags(x.Tags).Contains(SelectedTaskTag, StringComparer.OrdinalIgnoreCase));
        if (ActiveQuickFilter(TaskFilterModule) is { } quickFilter)
        {
            if (quickFilter.Status != "不限") query = query.Where(x => x.Status == quickFilter.Status);
            if (quickFilter.Type != "不限") query = query.Where(x => x.Type == quickFilter.Type);
            if (quickFilter.Priority != "不限") query = query.Where(x => x.Priority == quickFilter.Priority);
            if (quickFilter.ReminderMode == "仅启用提醒") query = query.Where(x => x.ReminderEnabled);
            if (quickFilter.ReminderMode == "仅未启用提醒") query = query.Where(x => !x.ReminderEnabled);
            if (quickFilter.ReminderMode == "仅已逾期") query = query.Where(x => x.IsOverdue);
            query = ApplyTagFilter(query, quickFilter, x => x.Tags);
        }
        Events.Clear();
        foreach (var item in query.OrderByDescending(x => x.OccurredAt)) Events.Add(item);
        DashboardTasks.Clear();
        foreach (var item in _data.Events.Where(x => !x.IsArchived && x.Status == "未完成").OrderBy(x => x.DueAt).Take(6)) DashboardTasks.Add(item);
        ArchivedTasks.Clear();
        foreach (var item in _data.Events.Where(x => x.IsArchived).OrderByDescending(x => x.ArchivedAt)) ArchivedTasks.Add(item);
        RaisePropertyChanged(nameof(EventCount));
        RaisePropertyChanged(nameof(OpenTaskCount));
        RaisePropertyChanged(nameof(OverdueTaskCount));
        RaisePropertyChanged(nameof(ArchivedTaskCount));
        SelectedEvent = Events.FirstOrDefault(x => x.Id == selectedId) ?? Events.FirstOrDefault();
    }

    private void RefreshFolderTree()
    {
        FolderTree.Clear();
        if (SelectedFolder?.Exists == true) FolderTree.Add(new FolderTreeNode(SelectedFolder.Path));
    }

    private void SaveAndRefresh(bool rebuildTaskTags = false)
    {
        _store.Save(_data);
        RefreshFolders();
        if (rebuildTaskTags) RebuildTaskTags();
        RefreshEvents();
    }

    private void RaiseSummaryProperties()
    {
        RaisePropertyChanged(nameof(FolderCount));
        RaisePropertyChanged(nameof(FavoriteFolderCount));
        RaisePropertyChanged(nameof(InvalidFolderCount));
    }

    private void MigrateAndNormalizeData()
    {
        var changed = _data.Events.RemoveAll(x => x.IsSystemLog) > 0 || _data.SchemaVersion < 10;
        _data.GitHubTrendSnapshots ??= [];
        _data.Commands ??= [];
        _data.QuickFilters ??= [];
        _data.Programs ??= [];
        _data.WorkModes ??= [];
        foreach (var mode in _data.WorkModes) mode.Steps ??= [];
        foreach (var folder in _data.Folders) folder.DetectedUnityProjects ??= [];
        foreach (var group in _data.QuickFilters.GroupBy(x => x.Module))
        {
            var activeFound = false;
            foreach (var filter in group.OrderBy(x => x.SortOrder))
            {
                if (!filter.IsPinned && filter.IsActive) { filter.IsActive = false; changed = true; }
                else if (filter.IsActive && activeFound) { filter.IsActive = false; changed = true; }
                else if (filter.IsActive) activeFound = true;
            }
        }
        if (string.IsNullOrWhiteSpace(_data.Settings.ApplicationName)) { _data.Settings.ApplicationName = "聚点工作台"; changed = true; }
        else
        {
            var normalizedName = _data.Settings.ApplicationName.Trim();
            if (normalizedName.Length > 24) normalizedName = normalizedName[..24];
            if (_data.Settings.ApplicationName != normalizedName) { _data.Settings.ApplicationName = normalizedName; changed = true; }
        }
        if (_data.Settings.GitHubTrendPeriodDays is not (7 or 30 or 90)) { _data.Settings.GitHubTrendPeriodDays = 7; changed = true; }
        if (!GitHubLanguages.Contains(_data.Settings.GitHubTrendLanguage)) { _data.Settings.GitHubTrendLanguage = "全部语言"; changed = true; }
        if (_data.Settings.FileSharePort is < 1024 or > 65535) { _data.Settings.FileSharePort = 5080; changed = true; }
        if (string.IsNullOrWhiteSpace(_data.Settings.FileSharePassword)) { _data.Settings.FileSharePassword = "change-me-now"; changed = true; }
        if (!GlobalHotkeyService.TryNormalizeGesture(_data.Settings.ShowWindowHotkey, out var normalizedHotkey, out _))
        {
            _data.Settings.EnableShowWindowHotkey = false;
            _data.Settings.ShowWindowHotkey = "Ctrl + Alt + J";
            changed = true;
        }
        else if (_data.Settings.ShowWindowHotkey != normalizedHotkey)
        {
            _data.Settings.ShowWindowHotkey = normalizedHotkey;
            changed = true;
        }
        foreach (var task in _data.Events)
        {
            task.SubTasks ??= [];
            if (!RepeatTypes.Contains(task.RepeatType)) { task.RepeatType = "不重复"; changed = true; }
            if (task.RepeatInterval < 1) { task.RepeatInterval = 1; changed = true; }
            if (task.Status is "无需处理" or "待处理") { task.Status = "未完成"; changed = true; }
            if (task.Type is "工作记录" or "问题记录" or "配置修改" or "待办事项")
            {
                task.Type = "普通任务";
                changed = true;
            }
        }
        if (_data.SchemaVersion < 10) _data.SchemaVersion = 10;
        if (changed) _store.Save(_data);
    }

    private void RebuildCommandCategories()
    {
        var categories = _data.Commands.Select(x => string.IsNullOrWhiteSpace(x.Category) ? "常用" : x.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var desired = new[] { "全部分类" }.Concat(categories).ToList();
        if (CommandCategoryFilters.SequenceEqual(desired, StringComparer.OrdinalIgnoreCase)) return;
        if (!desired.Contains(_selectedCommandCategory, StringComparer.OrdinalIgnoreCase)) _selectedCommandCategory = "全部分类";
        CommandCategoryFilters = desired;
        RaisePropertyChanged(nameof(CommandCategoryFilters));
        RaisePropertyChanged(nameof(SelectedCommandCategory));
    }

    private void RefreshCommands()
    {
        var selectedId = SelectedCommand?.Id;
        var query = _data.Commands.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(CommandSearch))
            query = query.Where(x => $"{x.Name} {x.Description} {x.Category} {x.Tags} {x.ExecutionType} {x.TargetSummary}".Contains(CommandSearch, StringComparison.OrdinalIgnoreCase));
        if (SelectedCommandCategory != "全部分类") query = query.Where(x => x.Category.Equals(SelectedCommandCategory, StringComparison.OrdinalIgnoreCase));
        if (ActiveQuickFilter(CommandFilterModule) is { } quickFilter)
        {
            if (quickFilter.Category != "不限") query = query.Where(x => x.Category.Equals(quickFilter.Category, StringComparison.OrdinalIgnoreCase));
            if (quickFilter.Type != "不限") query = query.Where(x => x.ExecutionType == quickFilter.Type);
            if (quickFilter.AdministratorMode == "仅管理员命令") query = query.Where(x => x.RunAsAdministrator);
            if (quickFilter.AdministratorMode == "仅普通命令") query = query.Where(x => !x.RunAsAdministrator);
            query = ApplyTagFilter(query, quickFilter, x => x.Tags);
        }
        Commands.Clear();
        foreach (var item in query.OrderBy(x => x.Category).ThenBy(x => x.Name)) Commands.Add(item);
        SelectedCommand = Commands.FirstOrDefault(x => x.Id == selectedId) ?? Commands.FirstOrDefault();
        RaisePropertyChanged(nameof(CommandCount));
    }

    private void RefreshPrograms()
    {
        var selectedId = SelectedProgram?.Id;
        Programs.Clear();
        foreach (var item in _data.Programs.OrderBy(x => x.Category).ThenBy(x => x.Name)) Programs.Add(item);
        SelectedProgram = Programs.FirstOrDefault(x => x.Id == selectedId) ?? Programs.FirstOrDefault();
        RaisePropertyChanged(nameof(ProgramCount));
    }

    private void RefreshWorkModes()
    {
        var selectedId = SelectedWorkMode?.Id;
        foreach (var step in _data.WorkModes.SelectMany(x => x.Steps))
            if (_data.Programs.FirstOrDefault(x => x.Id == step.ProgramId) is { } program) step.ProgramName = program.Name;
        WorkModes.Clear();
        foreach (var item in _data.WorkModes.OrderByDescending(x => x.IsPinned).ThenBy(x => x.Name)) WorkModes.Add(item);
        PinnedWorkModes.Clear();
        foreach (var item in WorkModes.Where(x => x.IsPinned).Take(6)) PinnedWorkModes.Add(item);
        SelectedWorkMode = WorkModes.FirstOrDefault(x => x.Id == selectedId) ?? WorkModes.FirstOrDefault();
        RaisePropertyChanged(nameof(WorkModeCount));
        RaisePropertyChanged(nameof(PinnedWorkModeCount));
    }

    private void LoadCachedGitHubProjects()
    {
        var key = GitHubTrendingService.BuildSnapshotKey(SelectedGitHubPeriodDays, SelectedGitHubLanguage);
        var snapshot = _data.GitHubTrendSnapshots.FirstOrDefault(x => x.Key == key);
        _allGitHubProjects.Clear();
        if (snapshot is not null) _allGitHubProjects.AddRange(snapshot.Projects.OrderBy(x => x.CurrentRank));
        GitHubLastUpdatedText = snapshot is null ? "当前筛选尚未检测" : $"上次检测：{snapshot.FetchedAt:yyyy-MM-dd HH:mm}";
        RefreshGitHubProjects();
        RaisePropertyChanged(nameof(GitHubTrendCount));
        RaisePropertyChanged(nameof(GitHubNewCount));
    }

    private void RefreshGitHubProjects()
    {
        var selectedId = SelectedGitHubProject?.RepositoryId;
        var query = _allGitHubProjects.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(GitHubProjectSearch))
            query = query.Where(x => $"{x.FullName} {x.Description} {x.Owner} {x.Language} {x.Topics}".Contains(GitHubProjectSearch, StringComparison.OrdinalIgnoreCase));
        GitHubProjects.Clear();
        foreach (var item in query.OrderBy(x => x.CurrentRank)) GitHubProjects.Add(item);
        SelectedGitHubProject = GitHubProjects.FirstOrDefault(x => x.RepositoryId == selectedId) ?? GitHubProjects.FirstOrDefault();
    }

    private EventRecord? CreateNextOccurrenceIfNeeded(EventRecord record)
    {
        if (record.Status != "已完成" || !record.IsRepeating || record.RepeatAdvanceProcessed) return null;
        record.RepeatAdvanceProcessed = true;
        var nextDate = record.RepeatType switch
        {
            "每天" => record.OccurredAt.AddDays(record.RepeatInterval),
            "每周" => record.OccurredAt.AddDays(7 * record.RepeatInterval),
            "每月" => record.OccurredAt.AddMonths(record.RepeatInterval),
            "每年" => record.OccurredAt.AddYears(record.RepeatInterval),
            _ => record.OccurredAt
        };
        if (record.RepeatEndDate is not null && nextDate.Date > record.RepeatEndDate.Value.Date) return null;

        var seriesId = record.RecurrenceSeriesId ?? record.Id;
        record.RecurrenceSeriesId = seriesId;
        var next = new EventRecord
        {
            Title = record.Title,
            Content = record.Content,
            OccurredAt = nextDate,
            Type = record.Type,
            Priority = record.Priority,
            Status = "未完成",
            Tags = record.Tags,
            PlannedTime = record.PlannedTime,
            FolderId = record.FolderId,
            ReminderEnabled = record.ReminderEnabled,
            ReminderMinutesBefore = record.ReminderMinutesBefore,
            RepeatType = record.RepeatType,
            RepeatInterval = record.RepeatInterval,
            RepeatEndDate = record.RepeatEndDate,
            RecurrenceSeriesId = seriesId,
            SubTasks = new(record.SubTasks.Select(x => new SubTaskRecord { Title = x.Title, Note = x.Note }))
        };
        record.NextOccurrenceId = next.Id;
        _data.Events.Add(next);
        return next;
    }

    private void RebuildTaskTags()
    {
        var selected = SelectedTaskTag;
        var tags = _data.Events.Where(x => !x.IsArchived).SelectMany(x => SplitTags(x.Tags)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var desired = new[] { "全部标签" }.Concat(tags).ToList();
        if (TaskTagFilters.SequenceEqual(desired, StringComparer.OrdinalIgnoreCase)) return;
        _selectedTaskTag = desired.Contains(selected, StringComparer.OrdinalIgnoreCase) ? selected : "全部标签";
        TaskTagFilters = desired;
        RaisePropertyChanged(nameof(TaskTagFilters));
        RaisePropertyChanged(nameof(SelectedTaskTag));
    }

    private static IEnumerable<string> SplitTags(string tags) =>
        (tags ?? string.Empty).Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private QuickFilterRecord? ActiveQuickFilter(string module) => _data.QuickFilters.FirstOrDefault(x => x.Module == module && x.IsActive);

    private static IEnumerable<T> ApplyTagFilter<T>(IEnumerable<T> query, QuickFilterRecord filter, Func<T, string> tagsSelector)
    {
        var requiredTags = SplitTags(filter.Tags).ToList();
        if (requiredTags.Count == 0) return query;
        return query.Where(item =>
        {
            var itemTags = SplitTags(tagsSelector(item)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return filter.TagMatchMode == "全部标签"
                ? requiredTags.All(itemTags.Contains)
                : requiredTags.Any(itemTags.Contains);
        });
    }

    private void DeactivateQuickFilters(string module)
    {
        var changed = false;
        foreach (var item in _data.QuickFilters.Where(x => x.Module == module && x.IsActive)) { item.IsActive = false; changed = true; }
        if (changed) { _store.Save(_data); RefreshQuickFilterCollections(); }
    }

    private void ResetManualFilters(string module)
    {
        if (module == FolderFilterModule)
        {
            _selectedFolderCategory = "全部分类"; _selectedFolderStatus = "全部状态"; _favoriteFoldersOnly = false;
            RaisePropertyChanged(nameof(SelectedFolderCategory)); RaisePropertyChanged(nameof(SelectedFolderStatus)); RaisePropertyChanged(nameof(FavoriteFoldersOnly));
        }
        else if (module == TaskFilterModule)
        {
            _selectedTaskTag = "全部标签"; RaisePropertyChanged(nameof(SelectedTaskTag));
        }
        else if (module == CommandFilterModule)
        {
            _selectedCommandCategory = "全部分类"; RaisePropertyChanged(nameof(SelectedCommandCategory));
        }
    }

    private void RefreshModule(string module)
    {
        if (module == FolderFilterModule) RefreshFolders();
        else if (module == TaskFilterModule) RefreshEvents();
        else if (module == CommandFilterModule) RefreshCommands();
    }

    private void RefreshQuickFilterCollections()
    {
        FillQuickFilters(FolderQuickFilters, FolderFilterModule);
        FillQuickFilters(TaskQuickFilters, TaskFilterModule);
        FillQuickFilters(CommandQuickFilters, CommandFilterModule);
    }

    private void FillQuickFilters(ObservableCollection<QuickFilterRecord> target, string module)
    {
        target.Clear();
        foreach (var filter in _data.QuickFilters.Where(x => x.Module == module && x.IsPinned).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)) target.Add(filter);
    }
}
