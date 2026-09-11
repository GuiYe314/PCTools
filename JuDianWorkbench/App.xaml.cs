using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using JuDianWorkbench.Services;

namespace JuDianWorkbench;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private static bool _restartRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "JuDianWorkbench.SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out var isFirstInstance);
        _ownsSingleInstanceMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            MessageBox.Show("聚点工作台已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Error("发生未处理异常", args.ExceptionObject as Exception);
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("界面发生异常", e.Exception);
        MessageBox.Show("操作没有完成，详细信息已写入运行日志。", "发生错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex) _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
        if (_restartRequested && !string.IsNullOrWhiteSpace(Environment.ProcessPath))
            Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true });
    }

    public static void RestartAfterShutdown()
    {
        _restartRequested = true;
        Current.Shutdown();
    }
}
