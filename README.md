# PCTools / 聚点工作台

PCTools 当前包含“聚点工作台”（`JuDianWorkbench`）：一个面向 Windows 的本地桌面效率工具，用于集中管理文件夹、任务、快捷命令、Unity 项目、GitHub 热门项目以及系统和网络信息。

## 技术栈

- C# / .NET 10
- WPF / XAML
- Windows 桌面应用（`net10.0-windows`）
- 无外部运行时包依赖
- JSON 本地持久化

## 仓库结构

```text
PCTools/
├─ AGENTS.md                         # Codex/开发代理必须遵守的项目规则
├─ CHANGELOG.md                      # 面向版本和功能的变更记录
├─ README.md                         # 项目入口说明
├─ JuDianWorkbench/                  # WPF 主程序
│  ├─ Models/                        # 持久化数据与界面展示模型
│  ├─ Services/                      # 文件、网络、进程及系统服务
│  ├─ ViewModels/                    # 主状态、筛选和业务编排
│  ├─ MainWindow.xaml(.cs)           # 主界面与交互事件
│  └─ *Window.xaml(.cs)              # 编辑、归档、提醒等弹窗
└─ JuDianWorkbench.FunctionalTests/  # 独立控制台功能测试
```

更细的架构、数据流、维护约束和跨电脑接手流程见 [AGENTS.md](AGENTS.md)。主程序内已有的完整功能清单见 [JuDianWorkbench/README.md](JuDianWorkbench/README.md)。

## 主要功能

- 文件夹登记、分类、标签、收藏、搜索、路径检查和目录树浏览
- 向下识别 Unity 项目、读取 Editor 版本并用匹配版本启动
- 任务、子任务、状态、标签、优先级、归档、提醒和重复任务
- CMD 命令、应用程序、BAT/CMD 脚本的一键执行与执行记录
- 文件夹、任务和命令中心的可保存快捷筛选
- GitHub 近 7/30/90 天热门项目、语言筛选、趋势快照和中文翻译
- 本机、网卡、公网 IP、所在地和运营商信息检测
- DHCP/静态 IPv4 设置（应用时需要 Windows 管理员授权）
- 软件名称、功能入口、开机启动和每日数据备份设置

## 数据与日志

- 数据：`%LOCALAPPDATA%\JuDianWorkbench\data.json`
- 备份：`%LOCALAPPDATA%\JuDianWorkbench\Backups\data-*.json`
- 日志：`%LOCALAPPDATA%\JuDianWorkbench\Logs\app-YYYYMMDD.log`
- 恢复前副本：`%LOCALAPPDATA%\JuDianWorkbench\data.json.before-restore`

当前数据结构版本为 `7`。持久化使用临时文件加替换的方式完成原子写入。

## 构建与运行

在仓库根目录执行：

```powershell
dotnet build .\JuDianWorkbench\JuDianWorkbench.csproj
dotnet run --project .\JuDianWorkbench\JuDianWorkbench.csproj
```

运行功能测试：

```powershell
dotnet run --project .\JuDianWorkbench.FunctionalTests\JuDianWorkbench.FunctionalTests.csproj
```

测试默认不调用真实 GitHub 或翻译服务。需要进行对应的联网检查时，可设置 `JUDIAN_LIVE_GITHUB_TEST=1` 或 `JUDIAN_LIVE_TRANSLATION_TEST=1`。

## 跨电脑继续开发

开始前：

```powershell
git pull
```

然后对 Codex 说：

> 阅读根目录 AGENTS.md、README.md、CHANGELOG.md、JuDianWorkbench/README.md 和最近 10 条 Git 提交，概括当前状态与规则后继续开发。

结束前运行构建和测试，并提交所有代码及文档更新：

```powershell
git add .
git commit -m "清楚描述本次修改"
git push
```

Codex 对话不属于 Git 仓库；跨电脑恢复上下文应以仓库文档、源码和提交历史为准。

