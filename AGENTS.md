# PCTools 开发代理说明

本文件是本仓库中 Codex 及其他开发代理的长期工作约定。进入仓库后，应先阅读本文件，再阅读根目录 `README.md`、`CHANGELOG.md`、`JuDianWorkbench/README.md` 和最近的 Git 提交。

## 项目目标

“聚点工作台”是一个 Windows 本地 WPF 效率工具。它将常用文件夹、任务、快捷命令、Unity 项目入口、GitHub 热门项目以及系统/网络工具集中在一个应用中。项目当前重视：本地优先、低依赖、数据安全、清晰的中文界面和可验证的功能变更。

## 当前技术基线

- 主项目：`JuDianWorkbench/JuDianWorkbench.csproj`
- 测试项目：`JuDianWorkbench.FunctionalTests/JuDianWorkbench.FunctionalTests.csproj`
- 文件共享服务：`JuDianFileShare.Server/JuDianFileShare.Server.csproj`
- 文件共享测试：`JuDianFileShare.Tests/JuDianFileShare.Tests.csproj`
- 目标框架：`net10.0-windows`
- UI：WPF + XAML
- 可空引用类型和隐式 using：启用
- 外部 NuGet 包：当前无
- 应用数据结构版本：`AppData.SchemaVersion = 7`
- 默认分支：`main`

## 架构与职责

### 启动和界面

- `App.xaml`：全局颜色、控件样式、转换器和主题资源。
- `App.xaml.cs`：单实例互斥锁、全局异常记录、退出后重启。
- `MainWindow.xaml`：工作台、文件夹、任务、命令中心、GitHub、系统与网络、设置七个页面。
- `MainWindow.xaml.cs`：UI 事件、确认对话框、窗口导航以及服务调用。业务数据状态尽量留在 `MainViewModel`，系统边界操作留在 `Services`。
- 其他 `*Window.xaml(.cs)`：编辑或管理弹窗；取消编辑时主窗口使用快照还原对象。

### 状态和业务编排

- `ViewModels/MainViewModel.cs` 持有 `AppData`，负责加载、迁移、保存、集合刷新、搜索筛选、快捷筛选、任务重复推进、提醒判定、GitHub 快照及翻译缓存。
- `ViewModels/ObservableObject.cs` 提供 `INotifyPropertyChanged`。
- `MainViewModel` 中的 `Folders`、`Events`、`Commands` 等是面向界面的过滤集合；真实持久化集合位于私有 `_data`。

### 数据模型

- `AppData`：持久化根对象，包含文件夹、任务、GitHub 快照、命令、快捷筛选和设置。
- `AppSettings`：应用名称、开机启动、备份、网卡、GitHub 筛选和功能显示设置。
- `FolderRecord` / `FolderTreeNode` / `UnityProjectInfo`：文件夹登记、延迟目录树和 Unity 检测结果。
- `EventRecord` / `SubTaskRecord` / `ReminderOption`：任务、子任务、截止时间、提醒、归档和重复序列。
- `CommandRecord` / `CommandLaunchResult`：快捷命令配置和启动结果。
- `QuickFilterRecord`：文件夹、任务、命令三个模块的持久化组合筛选。
- `GitHubProjectRecord` / `GitHubTrendSnapshot` / `GitHubTrendResult`：热门仓库结果、排名比较和缓存。
- `SystemNetworkSnapshot` / `NetworkAdapterInfo` / `IpConfigurationRequest`：系统网络展示与 IP 配置请求。

计算型展示属性通常标记 `[JsonIgnore]`，避免把环境派生状态写入数据文件。

### 服务边界

- `JsonDataStore`：读取 JSON；保存时写 `.tmp` 后替换正式文件。
- `BackupService`：创建、轮换、枚举和恢复应用数据备份；恢复前保留 `.before-restore`。
- `AppLogger`：写入 `%LOCALAPPDATA%\JuDianWorkbench\Logs`，日志失败不应导致应用崩溃。
- `AutoStartService`：管理当前用户注册表 `Run` 项。
- `CommandExecutionService`：校验并启动 CMD、软件或 BAT/CMD；管理员模式触发 UAC。
- `UnityProjectService`：最多向下四层、最多识别 20 个 Unity 项目，并寻找匹配 Editor。
- `GitHubTrendingService`：调用 GitHub Search API，默认每次取 30 个仓库，记录限额及前后快照差异。
- `TranslationService`：调用 MyMemory，将 UTF-8 文本按 450 字节分段翻译并缓存结果。
- `SystemNetworkService`：读取系统与网卡信息，检测互联网、公网 IP，并通过提权 PowerShell/netsh 应用 IPv4 设置。

### 局域网文件共享服务

- `JuDianFileShare.Server/Program.cs`：ASP.NET Core 服务启动、密码 Cookie 鉴权、上传/下载/搜索/删除 API 和请求校验。
- `JuDianFileShare.Server/Services/FileStore.cs`：随机存储文件名、路径隔离、流式大小限制、SHA-256、JSON 原子索引及并发串行化。
- `JuDianFileShare.Server/wwwroot`：无需前端框架的浏览器界面，提供登录、拖拽上传、进度、列表、搜索、下载和删除确认。
- `JuDianFileShare.Server/appsettings.json`：监听地址、共享目录、访问密码和单文件大小限制。默认密码只用于首次启动，正式使用前必须修改。
- `JuDianFileShare.Tests`：文件存储层的离线功能测试。

## 关键行为与约束

- 数据默认位于 `%LOCALAPPDATA%\JuDianWorkbench\data.json`，不是仓库文件。
- 删除文件夹记录不能删除真实文件夹；删除命令记录不能删除程序或脚本。
- 文件夹路径变化后必须清除旧的 Unity 扫描结果。
- 重复任务仅在当前期首次完成时生成下一期，依靠 `RepeatAdvanceProcessed` 防止重复生成。
- 提醒通过 30 秒定时器检查，`ReminderSentAt` 用于去重；改变时间或提醒参数时要重置它。
- 最多保留 24 份 GitHub 筛选快照；翻译结果应跨筛选快照复用。
- 目录树单节点最多加载 500 项；Unity 搜索跳过 `.git`、`Library`、`Temp`、`obj` 等目录。
- 静态 IPv4 参数必须在提权前验证；所有系统级修改都必须保留用户确认。
- 网络调用必须有超时、取消或明确错误反馈，不得阻塞 UI 线程。
- 不得提交 API Key、Token、真实个人数据、`data.json`、日志、备份、`bin` 或 `obj`。
- 文件共享服务必须把上传文件保存在配置的共享目录内，并使用随机存储名；任何浏览器提供的文件名只能作为显示和下载名称。
- 文件共享的写操作必须同时通过密码 Cookie 和同源请求头校验；下载、删除和索引查询不得绕过鉴权。
- `JuDianFileShare.Server/Data` 属于运行数据，禁止提交。公网部署前必须改用 HTTPS、强化身份系统和限流；当前实现仅定位为可信局域网服务。

## 实施新功能的强制流程

1. 阅读相关 Model、Service、ViewModel、XAML、代码后置文件和现有测试，不凭文件名猜测行为。
2. 保持职责边界：持久化数据进 `Models`，系统/外部调用进 `Services`，集合和业务状态进 `MainViewModel`，纯界面交互进窗口代码。
3. 若改变持久化结构：
   - 提升 `AppData.SchemaVersion`；
   - 在 `MainViewModel` 的迁移路径中兼容旧数据；
   - 增加从旧版本数据加载的测试；
   - 在 `CHANGELOG.md` 明确记录数据结构变化。
4. 若新增网络、进程、注册表、文件恢复或管理员操作，必须包含参数校验、异常处理、日志和必要的用户确认。
5. 为新增行为补充或更新 `JuDianWorkbench.FunctionalTests/Program.cs`；真实联网测试应保持显式环境变量开关，默认测试必须可离线运行。
6. 完成后至少执行：

   ```powershell
   dotnet build .\JuDianWorkbench\JuDianWorkbench.csproj
   dotnet run --project .\JuDianWorkbench.FunctionalTests\JuDianWorkbench.FunctionalTests.csproj
   dotnet run --project .\JuDianFileShare.Tests\JuDianFileShare.Tests.csproj
   ```

7. **每次新增、删除或改变功能时必须同步更新文档**：
   - 更新 `JuDianWorkbench/README.md` 的用户功能清单或运行说明；
   - 更新根目录 `README.md` 的主要功能、结构、依赖或使用方式（若受影响）；
   - 在根目录 `CHANGELOG.md` 的 `[Unreleased]` 下记录变更；
   - 架构、数据模型、服务边界、测试命令或开发规则变化时更新本 `AGENTS.md`。
8. 交付前检查 `git diff`，确认代码、测试和文档相互一致；不要覆盖用户无关的未提交修改。

只修改文案或格式且不改变功能时，也要在 `CHANGELOG.md` 记录有价值的用户可见变化；纯机械格式化可不记录。

## 代码风格

- 延续现有 C# 风格：文件作用域命名空间、目标类型 `new()`、集合表达式 `[]`、清晰的中文用户提示。
- 优先小而聚焦的方法；避免继续扩大 `MainWindow.xaml.cs` 和 `MainViewModel.cs`。新增独立能力时优先抽取 Service 或专门 ViewModel。
- 公共或持久化属性使用明确默认值，确保旧 JSON 缺少字段时仍可加载。
- 不吞掉本应反馈给用户的异常；仅日志自身、无权限目录枚举等明确可降级场景允许有控制的忽略。
- 外部地址应使用 HTTPS，并对将要交给 Shell 的 URL、路径和参数做来源与格式验证。

## 测试现状

`JuDianWorkbench.FunctionalTests` 是控制台式端到端业务测试，使用临时目录和独立 `data.json`。当前覆盖：

- 文件夹保存、收藏、筛选、备份和恢复
- Unity 子目录识别、Editor 版本读取和结果持久化
- 三个模块的快捷筛选、固定状态、排序和持久化
- 任务保存、归档、恢复、标签、提醒去重、子任务和重复任务
- 设置、应用名称、功能显示和默认网卡持久化
- DHCP/静态 IPv4 参数校验和系统快照基础信息
- GitHub 排名变化、缓存、搜索及翻译缓存
- 命令校验、分类、搜索、工作目录、执行记录和删除

测试会真实启动一条无交互 CMD 以验证工作目录。真实 GitHub 和翻译接口仅在相应环境变量开启时执行。

## 已知维护关注点

- `MainWindow.xaml.cs` 和 `MainViewModel.cs` 已较大，后续功能宜逐步拆分，而不是继续集中堆叠。
- JSON 加载失败当前会记录日志并返回全新数据；涉及恢复策略时需特别避免静默覆盖损坏文件。
- GitHub 查询使用未认证公共搜索接口，可能受到较低限额影响。
- 翻译依赖第三方 MyMemory 服务；错误和限额应作为可恢复状态处理。
- 网络/IP 功能仅适用于 Windows，并可能触发网络短暂中断和 UAC。
- 文件共享当前使用 HTTP 和基于密码派生值的局域网 Cookie，适合可信局域网；不要直接暴露到公网。
- 文件共享索引为单机 JSON 文件，适合轻量并发，不适合作为多实例或高并发部署。

## 跨电脑快速恢复上下文

代码同步后，先运行 `git pull`，然后让 Codex 执行：

> 阅读 AGENTS.md、README.md、CHANGELOG.md、JuDianWorkbench/README.md、最近 10 条提交和本次任务涉及的源码；先概括当前架构、相关行为、风险与验证方式，再开始修改。完成后同步更新测试和文档。

Git 只同步仓库内容，不同步 Codex 对话、本机 `%LOCALAPPDATA%` 数据、日志、备份、密钥或本地工具链。重要决策必须写进仓库文档或提交信息，不得只留在对话中。

