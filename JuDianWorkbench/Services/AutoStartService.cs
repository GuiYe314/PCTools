using Microsoft.Win32;

namespace JuDianWorkbench.Services;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "JuDianWorkbench";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("无法打开 Windows 当前用户启动项。");

        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            AppLogger.Info("已关闭开机启动");
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            throw new InvalidOperationException("无法确定程序可执行文件路径。");

        key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
        AppLogger.Info($"已开启开机启动：{executablePath}");
    }
}
