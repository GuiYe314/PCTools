using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;
using Microsoft.Win32;

namespace JuDianWorkbench;

public partial class ProgramEditWindow : Window
{
    public string[] TargetTypes => WorkModeService.TargetTypes;

    public ProgramEditWindow(ProgramRecord program, Window owner, bool isNew)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = program;
        DialogTitle.Text = isNew ? "添加启动项" : "编辑启动项";
        Title = DialogTitle.Text;
        UpdateTargetType();
    }

    private void TargetType_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateTargetType();

    private void UpdateTargetType()
    {
        if (TargetLabel is null || ProgramOptionsPanel is null) return;
        var type = TargetTypeComboBox.SelectedItem?.ToString() ?? "程序";
        TargetLabel.Text = type switch { "网页" => "网页地址", "文件夹" => "文件夹路径", _ => "程序路径" };
        BrowseTargetButton.Visibility = type == "网页" ? Visibility.Collapsed : Visibility.Visible;
        ProgramOptionsPanel.Visibility = type == "程序" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProgramRecord program) return;
        if (program.TargetType == "文件夹")
        {
            var folder = new OpenFolderDialog { Title = "选择文件夹", Multiselect = false };
            if (folder.ShowDialog(this) == true) program.Target = folder.FolderName;
        }
        else
        {
            var file = new OpenFileDialog { Title = "选择程序", Filter = "应用程序 (*.exe)|*.exe|所有文件 (*.*)|*.*", CheckFileExists = true };
            if (file.ShowDialog(this) == true) program.Target = file.FileName;
        }
        RefreshDataContext();
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProgramRecord program) return;
        var dialog = new OpenFolderDialog { Title = "选择工作目录", Multiselect = false };
        if (dialog.ShowDialog(this) == true) program.WorkingDirectory = dialog.FolderName;
        RefreshDataContext();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProgramRecord program) return;
        var error = WorkModeService.ValidateProgram(program);
        if (error is not null) { MessageBox.Show(this, error, "程序库", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DialogResult = true;
    }

    private void RefreshDataContext()
    {
        var value = DataContext;
        DataContext = null;
        DataContext = value;
    }
}
