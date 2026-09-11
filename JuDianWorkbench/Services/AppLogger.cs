using System.IO;

namespace JuDianWorkbench.Services;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JuDianWorkbench", "Logs");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            if (exception is not null) line += exception + Environment.NewLine;
            lock (SyncRoot)
                File.AppendAllText(Path.Combine(LogDirectory, $"app-{DateTime.Today:yyyyMMdd}.log"), line);
        }
        catch { }
    }
}
