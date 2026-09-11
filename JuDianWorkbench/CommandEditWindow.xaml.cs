using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;
using Microsoft.Win32;

namespace JuDianWorkbench;

public partial class CommandEditWindow : Window
{
    public string[] CommandTypes => CommandExecutionService.SupportedTypes;

    public CommandEditWindow(CommandRecord record, Window owner, bool isNew)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = record;
        DialogTitle.Text = isNew ? "添加快捷命令" : "编辑快捷命令";
        Title = DialogTitle.Text;
        UpdateTypePanels();
    }

    private void ExecutionType_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateTypePanels();

    private void UpdateTypePanels()
    {
        if (CmdPanel is null) return;
        var type = ExecutionTypeComboBox.SelectedItem?.ToString() ?? "CMD 命令";
        CmdPanel.Visibility = type == "CMD 命令" ? Visibility.Visible : Visibility.Collapsed;
        ProgramPanel.Visibility = type == "启动软件" ? Visibility.Visible : Visibility.Collapsed;
        BatchPanel.Visibility = type == "BAT 脚本" ? Visibility.Visible : Visibility.Collapsed;
        ArgumentsPanel.Visibility = type == "CMD 命令" ? Visibility.Collapsed : Visibility.Visible;
        KeepWindowCheckBox.IsEnabled = type != "启动软件";
    }

    private void BrowseProgram_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandRecord record) return;
        var dialog = new OpenFileDialog { Title = "选择要启动的软件", Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) record.ProgramPath = dialog.FileName;
        RefreshDataContext();
    }

    private void BrowseBatch_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandRecord record) return;
        var dialog = new OpenFileDialog { Title = "选择 BAT 或 CMD 脚本", Filter = "命令脚本 (*.bat;*.cmd)|*.bat;*.cmd", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) record.BatchPath = dialog.FileName;
        RefreshDataContext();
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandRecord record) return;
        var dialog = new OpenFolderDialog { Title = "选择命令工作目录", Multiselect = false };
        if (dialog.ShowDialog(this) == true) record.WorkingDirectory = dialog.FolderName;
        RefreshDataContext();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandRecord record) return;
        var error = CommandExecutionService.Validate(record);
        if (error is not null)
        {
            MessageBox.Show(this, error, "快捷命令", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void RefreshDataContext()
    {
        var record = DataContext;
        DataContext = null;
        DataContext = record;
    }
}
