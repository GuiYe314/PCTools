using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class BackupService
{
    private readonly string _dataFile;
    private readonly string _backupDirectory;

    public BackupService(string dataFile)
    {
        _dataFile = dataFile;
        _backupDirectory = Path.Combine(Path.GetDirectoryName(dataFile)!, "Backups");
    }

    public BackupInfo? CreateBackup(int retentionCount, bool dailyOnly)
    {
        if (!File.Exists(_dataFile)) return null;
        Directory.CreateDirectory(_backupDirectory);
        if (dailyOnly && Directory.EnumerateFiles(_backupDirectory, $"data-{DateTime.Today:yyyyMMdd}-*.json").Any())
        {
            Cleanup(retentionCount);
            return null;
        }

        var destination = Path.Combine(_backupDirectory, $"data-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        File.Copy(_dataFile, destination, false);
        Cleanup(retentionCount);
        AppLogger.Info($"数据备份完成：{destination}");
        return ToInfo(destination);
    }

    public IReadOnlyList<BackupInfo> GetBackups()
    {
        if (!Directory.Exists(_backupDirectory)) return [];
        return Directory.EnumerateFiles(_backupDirectory, "data-*.json")
            .Select(ToInfo).OrderByDescending(x => x.CreatedAt).ToList();
    }

    public void Restore(BackupInfo backup)
    {
        var fullBackupPath = Path.GetFullPath(backup.FilePath);
        var fullBackupDirectory = Path.GetFullPath(_backupDirectory) + Path.DirectorySeparatorChar;
        if (!fullBackupPath.StartsWith(fullBackupDirectory, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullBackupPath))
            throw new InvalidOperationException("备份文件不存在或不属于本应用。");
        if (File.Exists(_dataFile)) File.Copy(_dataFile, _dataFile + ".before-restore", true);
        File.Copy(fullBackupPath, _dataFile, true);
        AppLogger.Info($"已从备份恢复数据：{fullBackupPath}");
    }

    private void Cleanup(int retentionCount)
    {
        retentionCount = Math.Clamp(retentionCount, 1, 100);
        foreach (var file in Directory.EnumerateFiles(_backupDirectory, "data-*.json")
                     .Select(ToInfo).OrderByDescending(x => x.CreatedAt).Skip(retentionCount))
            File.Delete(file.FilePath);
    }

    private static BackupInfo ToInfo(string path)
    {
        var file = new FileInfo(path);
        return new BackupInfo { FilePath = file.FullName, FileName = file.Name, CreatedAt = file.LastWriteTime, Size = file.Length };
    }
}
