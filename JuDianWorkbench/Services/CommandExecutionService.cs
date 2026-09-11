using System.Diagnostics;
using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class CommandExecutionService
{
    public static readonly string[] SupportedTypes = ["CMD 命令", "启动软件", "BAT 脚本"];

    public static string? Validate(CommandRecord record)
    {
        record.Name = record.Name?.Trim() ?? string.Empty;
        record.Category = string.IsNullOrWhiteSpace(record.Category) ? "常用" : record.Category.Trim();
        record.Tags = record.Tags?.Trim() ?? string.Empty;
        record.Description = record.Description?.Trim() ?? string.Empty;
        record.ExecutionType = record.ExecutionType?.Trim() ?? string.Empty;
        record.CommandText = record.CommandText?.Trim() ?? string.Empty;
        record.ProgramPath = record.ProgramPath?.Trim() ?? string.Empty;
        record.BatchPath = record.BatchPath?.Trim() ?? string.Empty;
        record.Arguments = record.Arguments?.Trim() ?? string.Empty;
        record.WorkingDirectory = record.WorkingDirectory?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(record.Name)) return "请输入按钮名称。";
        if (!SupportedTypes.Contains(record.ExecutionType)) return "请选择有效的执行类型。";
        if (!string.IsNullOrWhiteSpace(record.WorkingDirectory) && !Directory.Exists(record.WorkingDirectory))
            return "指定的工作目录不存在。";
        return record.ExecutionType switch
        {
            "CMD 命令" when string.IsNullOrWhiteSpace(record.CommandText) => "请输入 CMD 命令。",
            "启动软件" when !File.Exists(record.ProgramPath) => "请选择存在的软件或可执行文件。",
            "BAT 脚本" when !File.Exists(record.BatchPath) => "请选择存在的 BAT 或 CMD 脚本。",
            "BAT 脚本" when !new[] { ".bat", ".cmd" }.Contains(Path.GetExtension(record.BatchPath), StringComparer.OrdinalIgnoreCase) => "脚本文件必须是 .bat 或 .cmd。",
            _ => null
        };
    }

    public CommandLaunchResult Launch(CommandRecord record)
    {
        var error = Validate(record);
        if (error is not null) throw new ArgumentException(error, nameof(record));

        ProcessStartInfo startInfo;
        if (record.ExecutionType == "启动软件")
        {
            startInfo = new ProcessStartInfo
            {
                FileName = record.ProgramPath,
                Arguments = record.Arguments,
                WorkingDirectory = ResolveWorkingDirectory(record, Path.GetDirectoryName(record.ProgramPath)),
                UseShellExecute = true
            };
        }
        else
        {
            var command = record.ExecutionType == "BAT 脚本"
                ? $"call \"{record.BatchPath}\" {record.Arguments}".TrimEnd()
                : record.CommandText;
            var workingDirectory = ResolveWorkingDirectory(record, record.ExecutionType == "BAT 脚本" ? Path.GetDirectoryName(record.BatchPath) : null);
            var cmdPath = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrWhiteSpace(cmdPath) || !File.Exists(cmdPath)) cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            startInfo = new ProcessStartInfo
            {
                FileName = cmdPath,
                Arguments = $"/d /{(record.KeepWindowOpen ? "k" : "c")} cd /d \"{workingDirectory}\" && {command}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
        }

        if (record.RunAsAdministrator) startInfo.Verb = "runas";
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows 没有启动该命令。");
        return new CommandLaunchResult(process.Id, record.ExecutionType == "CMD 命令" ? record.CommandText : startInfo.FileName);
    }

    private static string ResolveWorkingDirectory(CommandRecord record, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(record.WorkingDirectory)) return record.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(fallback) && Directory.Exists(fallback)) return fallback;
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
