using System.Windows;
using JuDianWorkbench.Models;
using Microsoft.Win32;

namespace JuDianWorkbench;

public partial class FolderEditWindow : Window
{
    public string[] Categories { get; }
    public FolderRecord Record { get; }

    public FolderEditWindow(FolderRecord record, string[] categories, bool isNew)
    {
        InitializeComponent();
        Record = record;
        Categories = categories;
        DataContext = record;
        DialogTitle.Text = isNew ? "添加文件夹" : "编辑文件夹";
        Title = DialogTitle.Text;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择要管理的文件夹", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        Record.Path = dialog.FolderName;
        if (string.IsNullOrWhiteSpace(Record.Name))
            Record.Name = System.IO.Path.GetFileName(dialog.FolderName.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        DataContext = null;
        DataContext = Record;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
