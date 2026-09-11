using System.Windows;
using JuDianWorkbench.Models;
using JuDianWorkbench.ViewModels;

namespace JuDianWorkbench;

public partial class DataBackupWindow : Window
{
    private readonly MainViewModel _viewModel;

    public DataBackupWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        RefreshList();
    }

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var backup = _viewModel.CreateBackup();
        MessageBox.Show(this, backup is null ? "当前没有可备份的数据。" : "备份创建成功。", "数据备份", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshList();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BackupInfo backup }) return;
        if (MessageBox.Show(this, $"确定恢复备份“{backup.FileName}”？\n当前数据会先保存为 data.json.before-restore。", "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _viewModel.RestoreBackup(backup);
            App.RestartAfterShutdown();
        }
        catch (Exception ex)
        {
            Services.AppLogger.Error("恢复数据失败", ex);
            MessageBox.Show(this, "恢复失败，详细信息已写入日志。", "恢复失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshList() => DataContext = _viewModel.GetBackups();
}
