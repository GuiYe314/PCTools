using System.ComponentModel;
using System.Diagnostics;
using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class WorkModeService
{
    public static readonly string[] TargetTypes = ["程序", "网页", "文件夹"];
    public static readonly int[] DelayOptions = [0, 1, 2, 3, 5, 10, 15, 30];

    public static string? ValidateProgram(ProgramRecord program)
    {
        program.Name = program.Name.Trim();
        program.Category = string.IsNullOrWhiteSpace(program.Category) ? "常用" : program.Category.Trim();
        program.Target = program.Target.Trim();
        program.Arguments = program.Arguments.Trim();
        program.WorkingDirectory = program.WorkingDirectory.Trim();
        if (string.IsNullOrWhiteSpace(program.Name)) return "请输入启动项名称。";
        if (!TargetTypes.Contains(program.TargetType)) return "请选择有效的启动项类型。";
        if (program.TargetType == "程序" && !File.Exists(program.Target)) return "请选择存在的程序文件。";
        if (program.TargetType == "文件夹" && !Directory.Exists(program.Target)) return "请选择存在的文件夹。";
        if (program.TargetType == "网页" && !IsSafeWebAddress(program.Target)) return "网页地址必须是有效的 http 或 https 地址。";
        if (program.TargetType != "程序")
        {
            program.Arguments = string.Empty;
            program.WorkingDirectory = string.Empty;
            program.RunAsAdministrator = false;
            program.SkipIfRunning = false;
        }
        else if (!string.IsNullOrWhiteSpace(program.WorkingDirectory) && !Directory.Exists(program.WorkingDirectory))
        {
            return "指定的工作目录不存在。";
        }
        return null;
    }

    public static string? ValidateMode(WorkModeRecord mode, IReadOnlyCollection<ProgramRecord> programs)
    {
        mode.Name = mode.Name.Trim();
        mode.Description = mode.Description.Trim();
        if (string.IsNullOrWhiteSpace(mode.Name)) return "请输入工作模式名称。";
        if (mode.Steps.Count == 0) return "请至少添加一个启动项。";
        if (mode.Steps.GroupBy(x => x.ProgramId).Any(x => x.Count() > 1)) return "同一个启动项不能在一个工作模式中重复添加。";
        if (mode.Steps.Any(x => programs.All(program => program.Id != x.ProgramId))) return "工作模式包含已经不存在的启动项，请编辑后重试。";
        foreach (var step in mode.Steps) step.DelayAfterSeconds = Math.Clamp(step.DelayAfterSeconds, 0, 30);
        return null;
    }

    public async Task<WorkModeStepResult> LaunchProgramAsync(ProgramRecord program, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var error = ValidateProgram(program);
        if (error is not null) return new(program.Name, "失败", error);
        try
        {
            if (program.TargetType == "程序" && program.SkipIfRunning && IsProgramRunning(program.Target))
                return new(program.Name, "已跳过", "程序已经在运行");

            var startInfo = program.TargetType switch
            {
                "程序" => new ProcessStartInfo(program.Target)
                {
                    Arguments = program.Arguments,
                    WorkingDirectory = ResolveWorkingDirectory(program),
                    UseShellExecute = true,
                    Verb = program.RunAsAdministrator ? "runas" : string.Empty
                },
                "文件夹" => CreateFolderStartInfo(program.Target),
                _ => new ProcessStartInfo(program.Target) { UseShellExecute = true }
            };
            using var process = Process.Start(startInfo);
            if (process is null) return new(program.Name, "失败", "Windows 没有启动该项目");
            await Task.Yield();
            return new(program.Name, "已启动");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(program.Name, "已跳过", "已取消管理员授权");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new(program.Name, "失败", ex.Message);
        }
    }

    public async Task<WorkModeRunSummary> LaunchModeAsync(WorkModeRecord mode, IReadOnlyCollection<ProgramRecord> programs, CancellationToken cancellationToken = default)
    {
        var summary = new WorkModeRunSummary();
        var validationError = ValidateMode(mode, programs);
        if (validationError is not null)
        {
            summary.Results.Add(new(mode.Name, "失败", validationError));
            return summary;
        }

        foreach (var step in mode.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var program = programs.FirstOrDefault(x => x.Id == step.ProgramId);
            var result = program is null
                ? new WorkModeStepResult(step.ProgramName, "失败", "启动项已经不存在")
                : await LaunchProgramAsync(program, cancellationToken);
            summary.Results.Add(result);
            if (result.Status == "失败" && mode.StopOnFailure) break;
            if (step.DelayAfterSeconds > 0 && result.Status == "已启动")
                await Task.Delay(TimeSpan.FromSeconds(step.DelayAfterSeconds), cancellationToken);
        }
        return summary;
    }

    private static string ResolveWorkingDirectory(ProgramRecord program)
    {
        if (!string.IsNullOrWhiteSpace(program.WorkingDirectory)) return program.WorkingDirectory;
        return Path.GetDirectoryName(program.Target) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static bool IsProgramRunning(string target)
    {
        var processName = Path.GetFileNameWithoutExtension(target);
        if (string.IsNullOrWhiteSpace(processName)) return false;
        using var processes = new ProcessCollection(Process.GetProcessesByName(processName));
        return processes.Count > 0;
    }

    private static bool IsSafeWebAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https") && !string.IsNullOrWhiteSpace(uri.Host);

    private static ProcessStartInfo CreateFolderStartInfo(string target)
    {
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        startInfo.ArgumentList.Add(target);
        return startInfo;
    }

    private sealed class ProcessCollection(Process[] processes) : IDisposable
    {
        public int Count => processes.Length;
        public void Dispose()
        {
            foreach (var process in processes) process.Dispose();
        }
    }
}
