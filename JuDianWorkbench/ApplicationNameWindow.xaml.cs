using System.Windows;

namespace JuDianWorkbench;

public partial class ApplicationNameWindow : Window
{
    public string ApplicationName { get; private set; }

    public ApplicationNameWindow(string applicationName)
    {
        InitializeComponent();
        ApplicationName = applicationName;
        ApplicationNameTextBox.Text = applicationName;
        Loaded += (_, _) => { ApplicationNameTextBox.Focus(); ApplicationNameTextBox.SelectAll(); };
    }

    private void RestoreDefault_Click(object sender, RoutedEventArgs e) => ApplicationNameTextBox.Text = "聚点工作台";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = ApplicationNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "软件名称不能为空。", "设置软件名称", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ApplicationName = name;
        DialogResult = true;
    }
}
