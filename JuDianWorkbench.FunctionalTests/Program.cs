using JuDianWorkbench.Models;
using JuDianWorkbench.Services;
using JuDianWorkbench.ViewModels;
using System.Windows;
using System.Windows.Interop;

var testRoot = Path.Combine(Path.GetTempPath(), "JuDianWorkbenchTests", Guid.NewGuid().ToString("N"));
var managedFolder = Path.Combine(testRoot, "ManagedFolder");
Directory.CreateDirectory(managedFolder);
var dataFile = Path.Combine(testRoot, "data.json");

try
{
    var legacyDataFile = Path.Combine(testRoot, "legacy-data.json");
    await File.WriteAllTextAsync(legacyDataFile, """{"SchemaVersion":7,"Settings":{"ApplicationName":"旧版工作台"}}""");
    var migratedLegacy = new MainViewModel(new JsonDataStore(legacyDataFile));
    Assert(migratedLegacy.Settings.ShowFileShare, "旧版数据迁移后未默认显示文件共享");
    Assert(migratedLegacy.Settings.ShowWorkModes, "旧版数据迁移后未默认显示工作模式");
    Assert(!migratedLegacy.Settings.EnableShowWindowHotkey && migratedLegacy.Settings.ShowWindowHotkey == "Ctrl + Alt + J", "旧版数据迁移后的显示快捷键默认值错误");
    Assert(migratedLegacy.Settings.FileSharePort == 5080 && migratedLegacy.Settings.FileSharePassword == "change-me-now", "旧版数据迁移后文件共享默认配置错误");
    using (var migratedDocument = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(legacyDataFile)))
        Assert(migratedDocument.RootElement.GetProperty("SchemaVersion").GetInt32() == 10, "旧版数据没有迁移到结构版本 10");

    var first = new MainViewModel(new JsonDataStore(dataFile));
    var folder = new FolderRecord { Name = "收藏测试", Path = managedFolder, Purpose = "测试" };
    Assert(first.SaveFolder(folder) is null, "新增文件夹失败");
    Assert(first.SelectedFolder?.Id == folder.Id, "新增文件夹后没有自动选中记录");
    first.SetFolderFavorite(folder, true);
    Assert(first.FavoriteFolderCount == 1, "收藏计数没有更新");
    Assert(first.FavoriteFolders.Single().Id == folder.Id, "收藏列表没有同步");

    var afterFavoriteRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterFavoriteRestart.FavoriteFolderCount == 1, "重启后收藏状态丢失");
    Assert(afterFavoriteRestart.FavoriteFolders.Single().IsFavorite, "重启后收藏列表错误");
    var backup = afterFavoriteRestart.CreateBackup();
    Assert(backup is not null && File.Exists(backup.FilePath), "手动备份没有生成文件");
    afterFavoriteRestart.SetFolderFavorite(afterFavoriteRestart.FavoriteFolders.Single(), false);
    Assert(afterFavoriteRestart.FavoriteFolderCount == 0, "取消收藏没有生效");
    afterFavoriteRestart.RestoreBackup(backup!);
    var afterBackupRestore = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterBackupRestore.FavoriteFolderCount == 1, "备份恢复没有还原收藏状态");
    afterBackupRestore.SelectedFolderCategory = "项目";
    Assert(afterBackupRestore.Folders.Count == 0, "文件夹分类筛选结果错误");
    afterBackupRestore.SelectedFolderCategory = "全部分类";
    afterBackupRestore.FavoriteFoldersOnly = true;
    Assert(afterBackupRestore.Folders.Single().Id == folder.Id, "只看收藏筛选结果错误");
    afterBackupRestore.FavoriteFoldersOnly = false;

    var unityContainer = Path.Combine(testRoot, "UnityWorkspace");
    var unityProjectPath = Path.Combine(unityContainer, "Games", "DemoProject");
    Directory.CreateDirectory(Path.Combine(unityProjectPath, "Assets"));
    Directory.CreateDirectory(Path.Combine(unityProjectPath, "ProjectSettings"));
    File.WriteAllText(Path.Combine(unityProjectPath, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.10f1\r\nm_EditorVersionWithRevision: test\r\n");
    var discoveredUnityProjects = new UnityProjectService().FindProjects(unityContainer);
    Assert(discoveredUnityProjects.Count == 1, "没有从管理目录的子目录识别 Unity 项目");
    Assert(discoveredUnityProjects[0].ProjectPath == unityProjectPath, "Unity 项目路径识别错误");
    Assert(discoveredUnityProjects[0].EditorVersion == "2022.3.10f1", "Unity Editor 版本读取错误");
    var unityFolder = new FolderRecord { Name = "Unity 工作区", Path = unityContainer, Category = "项目", Tags = "Unity,开发" };
    Assert(afterBackupRestore.SaveFolder(unityFolder) is null, "Unity 管理文件夹保存失败");
    afterBackupRestore.SaveFolderReadResult(unityFolder, discoveredUnityProjects);
    Assert(unityFolder.HasUnityProjects && unityFolder.LastReadAt is not null, "文件夹读取结果没有保存");
    var unityQuickFilter = new QuickFilterRecord
    {
        Module = MainViewModel.FolderFilterModule, Name = "Unity 项目", Category = "项目",
        Tags = "Unity", UnityMode = "只看 Unity 项目"
    };
    Assert(afterBackupRestore.SaveQuickFilter(unityQuickFilter) is null, "文件夹快捷筛选标签保存失败");
    var favoriteQuickFilter = new QuickFilterRecord
    {
        Module = MainViewModel.FolderFilterModule, Name = "收藏目录", FavoriteMode = "只看收藏"
    };
    Assert(afterBackupRestore.SaveQuickFilter(favoriteQuickFilter) is null, "第二个文件夹快捷筛选标签保存失败");
    var folderFilterLayout = afterBackupRestore.GetQuickFilterLayout(MainViewModel.FolderFilterModule).Reverse().ToList();
    folderFilterLayout.Single(x => x.Id == favoriteQuickFilter.Id).IsPinned = false;
    afterBackupRestore.SaveQuickFilterLayout(MainViewModel.FolderFilterModule, folderFilterLayout);
    Assert(afterBackupRestore.FolderQuickFilters.Count == 1 && afterBackupRestore.FolderQuickFilters[0].Id == unityQuickFilter.Id, "快捷筛选固定状态没有生效");
    folderFilterLayout.Single(x => x.Id == favoriteQuickFilter.Id).IsPinned = true;
    afterBackupRestore.SaveQuickFilterLayout(MainViewModel.FolderFilterModule, folderFilterLayout);
    Assert(afterBackupRestore.FolderQuickFilters[0].Id == favoriteQuickFilter.Id, "快捷筛选排序没有生效");
    afterBackupRestore.ApplyQuickFilter(unityQuickFilter);
    Assert(afterBackupRestore.Folders.Count == 1 && afterBackupRestore.Folders[0].Id == unityFolder.Id, "文件夹快捷筛选结果错误");
    afterBackupRestore.ClearQuickFilter(MainViewModel.FolderFilterModule);

    var task = new EventRecord
    {
        Title = "归档测试",
        Content = "验证完整归档流程",
        Status = "无法完成",
        Tags = "测试,归档",
        PlannedTime = "10:30"
    };
    Assert(afterBackupRestore.SaveEvent(task) is null, "新增任务失败");
    afterBackupRestore.ArchiveTask(task);
    Assert(afterBackupRestore.Events.Count == 0, "归档任务仍显示在正常列表");
    Assert(afterBackupRestore.ArchivedTasks.Single().Id == task.Id, "归档列表没有任务");

    var afterArchiveRestart = new MainViewModel(new JsonDataStore(dataFile));
    var archived = afterArchiveRestart.ArchivedTasks.Single();
    Assert(archived.Status == "无法完成", "重启后任务状态被错误覆盖");
    Assert(afterArchiveRestart.Events.Count == 0, "重启后归档任务重新出现在正常列表");
    afterArchiveRestart.RestoreTask(archived);
    Assert(afterArchiveRestart.ArchivedTasks.Count == 0, "恢复后任务仍在归档列表");
    Assert(afterArchiveRestart.Events.Single().Id == task.Id, "恢复后任务没有回到正常列表");
    Assert(afterArchiveRestart.TaskTagFilters.Contains("测试"), "任务标签分类没有生成");
    var taskQuickFilter = new QuickFilterRecord
    {
        Module = MainViewModel.TaskFilterModule, Name = "无法完成的测试", Tags = "测试",
        Status = "无法完成", TagMatchMode = "全部标签", ReminderMode = "仅未启用提醒"
    };
    Assert(afterArchiveRestart.SaveQuickFilter(taskQuickFilter) is null, "任务快捷筛选标签保存失败");
    afterArchiveRestart.ApplyQuickFilter(taskQuickFilter);
    Assert(afterArchiveRestart.Events.Count == 1 && afterArchiveRestart.Events[0].Id == task.Id, "任务快捷筛选结果错误");
    afterArchiveRestart.ClearQuickFilter(MainViewModel.TaskFilterModule);

    var afterRestoreRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterRestoreRestart.Events.Single().Id == task.Id, "重启后恢复状态丢失");
    Assert(!afterRestoreRestart.Events.Single().IsArchived, "恢复状态没有持久化");

    var reminderTask = new EventRecord { Title = "提醒测试", OccurredAt = DateTime.Today, PlannedTime = "00:01", ReminderEnabled = true, ReminderMinutesBefore = 10 };
    Assert(afterRestoreRestart.SaveEvent(reminderTask) is null, "提醒任务保存失败");
    Assert(afterRestoreRestart.DashboardTasks.Any(x => x.Id == reminderTask.Id) && afterRestoreRestart.OpenTaskCount > 0, "工作台没有显示待办任务");
    Assert(afterRestoreRestart.GetDueReminders(DateTime.Now).Any(x => x.Id == reminderTask.Id), "到期提醒没有被识别");
    afterRestoreRestart.MarkReminderShown(reminderTask);
    Assert(afterRestoreRestart.GetDueReminders(DateTime.Now).All(x => x.Id != reminderTask.Id), "提醒去重没有生效");

    afterRestoreRestart.Settings.SelectedNetworkAdapterName = "测试网卡";
    afterRestoreRestart.Settings.ApplicationName = "研发工具台";
    afterRestoreRestart.Settings.ShowDashboard = false;
    afterRestoreRestart.Settings.ShowCommandCenter = false;
    afterRestoreRestart.Settings.ShowGitHubTrending = false;
    afterRestoreRestart.Settings.ShowFileShare = false;
    afterRestoreRestart.Settings.ShowWorkModes = false;
    afterRestoreRestart.Settings.EnableShowWindowHotkey = true;
    afterRestoreRestart.Settings.ShowWindowHotkey = "Ctrl + Shift + F9";
    afterRestoreRestart.Settings.FileSharePort = 6090;
    afterRestoreRestart.Settings.FileSharePassword = "test-password";
    afterRestoreRestart.Settings.FileShareStoragePath = Path.Combine(testRoot, "SharedFiles");
    afterRestoreRestart.SaveSettings();
    var afterSettingsRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterSettingsRestart.Settings.SelectedNetworkAdapterName == "测试网卡", "默认网卡没有持久化");
    Assert(afterSettingsRestart.Settings.ApplicationName == "研发工具台", "软件名称没有持久化");
    Assert(!afterSettingsRestart.Settings.ShowDashboard && !afterSettingsRestart.Settings.ShowCommandCenter && !afterSettingsRestart.Settings.ShowGitHubTrending && !afterSettingsRestart.Settings.ShowFileShare && !afterSettingsRestart.Settings.ShowWorkModes, "功能显示设置没有持久化");
    Assert(afterSettingsRestart.Settings.EnableShowWindowHotkey && afterSettingsRestart.Settings.ShowWindowHotkey == "Ctrl + Shift + F9", "软件显示快捷键设置没有持久化");
    Assert(afterSettingsRestart.Settings.FileSharePort == 6090 && afterSettingsRestart.Settings.FileSharePassword == "test-password" &&
           afterSettingsRestart.Settings.FileShareStoragePath == Path.Combine(testRoot, "SharedFiles"), "文件共享配置没有持久化");
    var validStaticIp = new IpConfigurationRequest
    {
        AdapterName = "测试网卡", UseDhcp = false, IpAddress = "192.168.10.20",
        SubnetMask = "255.255.255.0", Gateway = "192.168.10.1", PrimaryDns = "223.5.5.5"
    };
    Assert(SystemNetworkService.ValidateIpConfiguration(validStaticIp) is null, "有效静态 IP 被错误拒绝");
    var invalidMask = new IpConfigurationRequest
    {
        AdapterName = "测试网卡", UseDhcp = false, IpAddress = "192.168.10.20",
        SubnetMask = "255.0.255.0", Gateway = "192.168.10.1", PrimaryDns = "223.5.5.5"
    };
    Assert(SystemNetworkService.ValidateIpConfiguration(invalidMask) is not null, "不连续子网掩码未被拦截");
    Assert(SystemNetworkService.ValidateIpConfiguration(new IpConfigurationRequest { AdapterName = "测试网卡", UseDhcp = true }) is null, "DHCP 配置被错误拒绝");
    Assert(GlobalHotkeyService.TryNormalizeGesture("alt+ctrl+j", out var normalizedHotkey, out _) && normalizedHotkey == "Ctrl + Alt + J", "全局快捷键规范化错误");
    Assert(!GlobalHotkeyService.TryNormalizeGesture("J", out _, out _), "没有修饰键的全局快捷键未被拦截");
    Assert(!GlobalHotkeyService.TryNormalizeGesture("Ctrl + Alt", out _, out _), "没有主按键的全局快捷键未被拦截");
    Exception? hotkeyRegistrationError = null;
    var hotkeyThread = new Thread(() =>
    {
        try
        {
            var testWindow = new Window();
            _ = new WindowInteropHelper(testWindow).EnsureHandle();
            using var hotkeyService = new GlobalHotkeyService();
            hotkeyService.Attach(testWindow, () => { });
            Assert(hotkeyService.TryRegister("Ctrl + Alt + Shift + F24", out var registrationError), $"Windows 全局快捷键注册失败：{registrationError}");
            Assert(hotkeyService.IsRegistered, "全局快捷键注册状态错误");
            hotkeyService.Unregister();
            Assert(!hotkeyService.IsRegistered, "全局快捷键没有正确注销");
            testWindow.Close();
        }
        catch (Exception ex) { hotkeyRegistrationError = ex; }
    });
    hotkeyThread.SetApartmentState(ApartmentState.STA);
    hotkeyThread.Start();
    hotkeyThread.Join();
    if (hotkeyRegistrationError is not null) throw new InvalidOperationException("Windows 全局快捷键系统调用测试失败。", hotkeyRegistrationError);
    var systemSnapshot = await new SystemNetworkService().GetSnapshotAsync(includePublicInfo: false);
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.ComputerName), "计算机名读取失败");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.OperatingSystem), "操作系统信息读取失败");
    Assert(systemSnapshot.Memory != "未知", "物理内存信息读取失败");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.WindowsVersion), "Windows 版本详情读取失败");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.CpuDetails), "CPU 详情读取失败");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.MemoryDetails), "内存使用详情读取失败");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.Graphics) && !string.IsNullOrWhiteSpace(systemSnapshot.GraphicsDriver), "显卡信息读取结果为空");
    Assert(!string.IsNullOrWhiteSpace(systemSnapshot.Display) && !string.IsNullOrWhiteSpace(systemSnapshot.Drives) && !string.IsNullOrWhiteSpace(systemSnapshot.Uptime), "显示器、磁盘或运行时长读取结果为空");

    var recurringTask = new EventRecord
    {
        Title = "每周巡检", OccurredAt = DateTime.Today.AddDays(1), PlannedTime = "14:00",
        RepeatType = "每周", RepeatInterval = 2, ReminderEnabled = true, ReminderMinutesBefore = 60
    };
    recurringTask.SubTasks.Add(new SubTaskRecord { Title = "检查服务", Note = "记录异常", IsCompleted = true });
    Assert(afterRestoreRestart.SaveEvent(recurringTask) is null, "重复任务首次保存失败");
    recurringTask.Status = "已完成";
    Assert(afterRestoreRestart.SaveEvent(recurringTask) is null, "完成重复任务失败");
    Assert(recurringTask.NextOccurrenceId is not null, "完成后没有生成下一期任务");
    var nextOccurrence = afterRestoreRestart.Events.Single(x => x.Id == recurringTask.NextOccurrenceId);
    Assert(nextOccurrence.OccurredAt.Date == recurringTask.OccurredAt.AddDays(14).Date, "每两周的下次日期计算错误");
    Assert(nextOccurrence.SubTasks.Single().Title == "检查服务" && !nextOccurrence.SubTasks.Single().IsCompleted, "下一期子任务没有正确重置");
    var countAfterFirstAdvance = afterRestoreRestart.Events.Count;
    Assert(afterRestoreRestart.SaveEvent(recurringTask) is null, "重复任务再次保存失败");
    Assert(afterRestoreRestart.Events.Count == countAfterFirstAdvance, "同一期重复任务被重复生成");
    Assert(afterRestoreRestart.GetReminderTasks().Any(x => x.Id == nextOccurrence.Id), "下一期任务没有进入提醒视图数据");
    var afterRecurringRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterRecurringRestart.Events.Any(x => x.Id == nextOccurrence.Id), "重启后下一期任务丢失");
    Assert(afterRecurringRestart.Events.Single(x => x.Id == nextOccurrence.Id).SubTasks.Count == 1, "重启后子任务丢失");

    var previousGitHub = new GitHubTrendSnapshot
    {
        Key = "7|全部语言", FetchedAt = DateTime.Now.AddHours(-1),
        Projects = [new GitHubProjectRecord { RepositoryId = 101, FullName = "demo/old", Stars = 100, CurrentRank = 3, FirstSeenAt = DateTime.Today }]
    };
    var currentGitHub = new List<GitHubProjectRecord>
    {
        new() { RepositoryId = 101, FullName = "demo/old", Stars = 125, CurrentRank = 1 },
        new() { RepositoryId = 202, FullName = "demo/new", Stars = 80, CurrentRank = 2 }
    };
    GitHubTrendingService.CompareWithPrevious(currentGitHub, previousGitHub, DateTime.Now);
    Assert(currentGitHub[0].PreviousStars == 100 && currentGitHub[0].TrendStatus.StartsWith("排名上升"), "GitHub 排名上升识别错误");
    Assert(currentGitHub[1].TrendStatus == "新上榜", "GitHub 新上榜识别错误");
    afterRecurringRestart.ApplyGitHubTrendResult(new GitHubTrendResult { Projects = currentGitHub, FetchedAt = DateTime.Now });
    var afterGitHubRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterGitHubRestart.GitHubTrendCount == 2, "GitHub 热门榜单缓存没有持久化");
    afterGitHubRestart.GitHubProjectSearch = "demo/new";
    Assert(afterGitHubRestart.GitHubProjects.Single().RepositoryId == 202, "GitHub 项目搜索结果错误");
    var translatedProject = afterGitHubRestart.GitHubProjects.Single();
    afterGitHubRestart.SaveGitHubTranslation(translatedProject, "中文项目说明");
    var afterTranslationRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterTranslationRestart.GitHubProjects.Single(x => x.RepositoryId == 202).TranslatedDescription == "中文项目说明", "GitHub 翻译缓存没有持久化");
    var longTextSegments = TranslationService.SplitUtf8(string.Join(' ', Enumerable.Repeat("translation", 80)), 450);
    Assert(longTextSegments.Count > 1 && longTextSegments.All(x => System.Text.Encoding.UTF8.GetByteCount(x) <= 450), "翻译文本分段超过接口限制");

    var batchPath = Path.Combine(testRoot, "测试脚本.BaT");
    File.WriteAllText(batchPath, "@echo off\r\necho test\r\n");
    var cmdRecord = new CommandRecord
    {
        Name = "查看网络配置", Category = "网络", ExecutionType = "CMD 命令",
        CommandText = "ipconfig /all", Description = "查看本机网络详情", Tags = "网络,诊断"
    };
    Assert(CommandExecutionService.Validate(cmdRecord) is null, "有效 CMD 命令被错误拒绝");
    Assert(afterGitHubRestart.SaveCommand(cmdRecord) is null, "CMD 命令保存失败");
    var commandWorkingDirectory = Path.Combine(testRoot, "CommandWorkingDirectory");
    Directory.CreateDirectory(commandWorkingDirectory);
    var workingDirectoryProbe = new CommandRecord
    {
        Name = "工作目录验证", ExecutionType = "CMD 命令", CommandText = "cd > current-directory.txt",
        WorkingDirectory = commandWorkingDirectory, KeepWindowOpen = false, ConfirmBeforeRun = false
    };
    _ = new CommandExecutionService().Launch(workingDirectoryProbe);
    var workingDirectoryResult = Path.Combine(commandWorkingDirectory, "current-directory.txt");
    for (var attempt = 0; attempt < 40 && !File.Exists(workingDirectoryResult); attempt++) await Task.Delay(50);
    Assert(File.Exists(workingDirectoryResult), "CMD 命令没有在指定工作目录执行");
    Assert(string.Equals((await File.ReadAllTextAsync(workingDirectoryResult)).Trim(), commandWorkingDirectory, StringComparison.OrdinalIgnoreCase), "CMD 当前目录与指定工作目录不一致");
    var batchRecord = new CommandRecord
    {
        Name = "测试脚本", Category = "维护", ExecutionType = "BAT 脚本", BatchPath = batchPath,
        Arguments = "demo", WorkingDirectory = testRoot, Tags = "维护"
    };
    Assert(CommandExecutionService.Validate(batchRecord) is null, "混合大小写 BAT 扩展名被错误拒绝");
    Assert(afterGitHubRestart.SaveCommand(batchRecord) is null, "BAT 脚本保存失败");
    var missingProgram = new CommandRecord { Name = "缺失软件", ExecutionType = "启动软件", ProgramPath = Path.Combine(testRoot, "missing.exe") };
    Assert(CommandExecutionService.Validate(missingProgram) is not null, "不存在的软件路径未被拦截");
    Assert(afterGitHubRestart.CommandCategoryFilters.Contains("网络") && afterGitHubRestart.CommandCategoryFilters.Contains("维护"), "命令分类没有生成");
    afterGitHubRestart.CommandSearch = "ipconfig";
    Assert(afterGitHubRestart.Commands.Single().Id == cmdRecord.Id, "命令搜索结果错误");
    afterGitHubRestart.CommandSearch = string.Empty;
    afterGitHubRestart.SelectedCommandCategory = "维护";
    Assert(afterGitHubRestart.Commands.Single().Id == batchRecord.Id, "命令分类筛选错误");
    afterGitHubRestart.SelectedCommandCategory = "全部分类";
    var commandQuickFilter = new QuickFilterRecord
    {
        Module = MainViewModel.CommandFilterModule, Name = "网络诊断", Category = "网络",
        Tags = "诊断", Type = "CMD 命令"
    };
    Assert(afterGitHubRestart.SaveQuickFilter(commandQuickFilter) is null, "命令快捷筛选标签保存失败");
    afterGitHubRestart.ApplyQuickFilter(commandQuickFilter);
    Assert(afterGitHubRestart.Commands.Count == 1 && afterGitHubRestart.Commands[0].Id == cmdRecord.Id, "命令快捷筛选结果错误");
    afterGitHubRestart.RecordCommandLaunch(cmdRecord, "测试记录");
    var afterCommandRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterCommandRestart.CommandCount == 2, "重启后快捷命令丢失");
    Assert(afterCommandRestart.FolderQuickFilters.Any(x => x.Name == "Unity 项目") && afterCommandRestart.FolderQuickFilters.Any(x => x.Name == "收藏目录") &&
           afterCommandRestart.TaskQuickFilters.Any(x => x.Name == "无法完成的测试") &&
           afterCommandRestart.CommandQuickFilters.Any(x => x.Name == "网络诊断"), "快捷筛选标签没有持久化");
    Assert(afterCommandRestart.CommandQuickFilters.Single(x => x.Id == commandQuickFilter.Id).IsActive &&
           afterCommandRestart.Commands.Count == 1 && afterCommandRestart.Commands[0].Id == cmdRecord.Id, "重启后没有恢复上次选中的快捷筛选标签");
    Assert(afterCommandRestart.Folders.Single(x => x.Id == unityFolder.Id).HasUnityProjects, "重启后文件夹读取结果丢失");
    var persistedCommand = afterCommandRestart.Commands.Single(x => x.Id == cmdRecord.Id);
    Assert(persistedCommand.RunCount == 1 && persistedCommand.LastRunStatus == "测试记录", "命令执行记录没有持久化");
    afterCommandRestart.DeleteCommand(batchRecord);
    Assert(afterCommandRestart.CommandCount == 1, "快捷命令删除失败");
    Assert(afterCommandRestart.SelectedCommand?.Id == cmdRecord.Id, "删除命令后没有选中下一条可见记录");

    var executableProgram = new ProgramRecord
    {
        Name = "AI 助手", Category = "AI", TargetType = "程序",
        Target = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取测试进程路径"),
        SkipIfRunning = false
    };
    var projectFolderProgram = new ProgramRecord
    {
        Name = "Unity 项目目录", Category = "开发", TargetType = "文件夹", Target = unityProjectPath
    };
    Assert(WorkModeService.ValidateProgram(new ProgramRecord { Name = "错误网页", TargetType = "网页", Target = "ftp://example.com" }) is not null, "非 HTTP 网页地址未被拦截");
    Assert(WorkModeService.ValidateProgram(new ProgramRecord { Name = "缺失程序", TargetType = "程序", Target = Path.Combine(testRoot, "missing.exe") }) is not null, "不存在的工作模式程序路径未被拦截");
    Assert(afterCommandRestart.SaveProgram(executableProgram) is null && afterCommandRestart.SaveProgram(projectFolderProgram) is null, "程序库启动项保存失败");
    Assert(afterCommandRestart.ProgramCount == 2 && afterCommandRestart.SelectedProgram?.Id == projectFolderProgram.Id, "程序库计数或自动选择错误");
    var workMode = new WorkModeRecord { Name = "Unity AI 工作", Description = "打开 AI 和 Unity 项目", StopOnFailure = true };
    workMode.Steps.Add(new WorkModeStep { ProgramId = executableProgram.Id, ProgramName = executableProgram.Name, DelayAfterSeconds = 2 });
    workMode.Steps.Add(new WorkModeStep { ProgramId = projectFolderProgram.Id, ProgramName = projectFolderProgram.Name });
    Assert(afterCommandRestart.SaveWorkMode(workMode) is null, "工作模式保存失败");
    Assert(afterCommandRestart.WorkModeCount == 1 && afterCommandRestart.SelectedWorkMode?.Id == workMode.Id, "工作模式计数或自动选择错误");
    Assert(afterCommandRestart.PinnedWorkModes.Single().Id == workMode.Id, "固定工作模式没有出现在工作台集合");
    Assert(afterCommandRestart.DeleteProgram(executableProgram) is not null, "被工作模式引用的启动项可以被错误删除");
    var syntheticRun = new WorkModeRunSummary();
    syntheticRun.Results.Add(new(executableProgram.Name, "已启动"));
    syntheticRun.Results.Add(new(projectFolderProgram.Name, "已跳过", "测试跳过"));
    afterCommandRestart.RecordWorkModeRun(workMode, syntheticRun);
    var afterWorkModeRestart = new MainViewModel(new JsonDataStore(dataFile));
    Assert(afterWorkModeRestart.ProgramCount == 2 && afterWorkModeRestart.WorkModeCount == 1, "重启后程序库或工作模式丢失");
    var persistedMode = afterWorkModeRestart.WorkModes.Single();
    Assert(persistedMode.Steps.Select(x => x.ProgramId).SequenceEqual(new[] { executableProgram.Id, projectFolderProgram.Id }), "工作模式启动顺序没有持久化");
    Assert(persistedMode.RunCount == 1 && persistedMode.LastRunStatus.Contains("已启动 1 项"), "工作模式运行记录没有持久化");
    Assert(afterWorkModeRestart.Programs.Single(x => x.Id == executableProgram.Id).LaunchCount == 1, "工作模式没有记录已启动程序的次数");

    if (Environment.GetEnvironmentVariable("JUDIAN_LIVE_GITHUB_TEST") == "1")
    {
        var liveGitHub = await new GitHubTrendingService().FetchAsync(7, "C#");
        Assert(liveGitHub.Projects.Count > 0, "GitHub 真实接口没有返回热门项目");
        Assert(liveGitHub.Projects.All(x => x.CurrentRank > 0 && x.Url.StartsWith("https://github.com/")), "GitHub 真实数据解析错误");
    }
    if (Environment.GetEnvironmentVariable("JUDIAN_LIVE_TRANSLATION_TEST") == "1")
    {
        var translation = await new TranslationService().TranslateToChineseAsync("A fast and practical desktop development toolkit.");
        Assert(translation.Any(c => c is >= '\u3400' and <= '\u9FFF'), "真实翻译接口没有返回中文结果");
        var chunks = TranslationService.SplitUtf8(new string('a', 1000), 450);
        Assert(chunks.Count == 3 && chunks.All(x => System.Text.Encoding.UTF8.GetByteCount(x) <= 450), "翻译文本分段错误");
    }
    if (Environment.GetEnvironmentVariable("JUDIAN_LIVE_TRANSLATION_TEST") == "1")
    {
        var translated = await new TranslationService().TranslateToChineseAsync("A fast and lightweight desktop productivity tool.");
        Assert(!string.IsNullOrWhiteSpace(translated) && translated != "A fast and lightweight desktop productivity tool.", "真实翻译接口返回无效");
    }
    Console.WriteLine("PASS: 文件夹读取、Unity 项目识别、工作模式、程序库、全局显示快捷键、硬件详情、通用快捷筛选标签、任务、命令中心、GitHub 热门检测、提醒、备份、默认网卡和 IP 参数校验全部通过。");
}
finally
{
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
