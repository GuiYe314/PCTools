using System.Windows;
using System.Windows.Controls;
using JuDianWorkbench.Models;
using JuDianWorkbench.Services;

namespace JuDianWorkbench;

public partial class IpSettingsWindow : Window
{
    public IpConfigurationRequest? Request { get; private set; }

    public IpSettingsWindow(IReadOnlyList<NetworkAdapterInfo> adapters, NetworkAdapterInfo? selected)
    {
        InitializeComponent();
        AdapterComboBox.ItemsSource = adapters;
        AdapterComboBox.SelectedItem = selected ?? adapters.FirstOrDefault();
        ModeComboBox.SelectedIndex = (AdapterComboBox.SelectedItem as NetworkAdapterInfo)?.IsDhcp == false ? 1 : 0;
    }

    private void Adapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AdapterComboBox.SelectedItem is not NetworkAdapterInfo adapter) return;
        if (adapter.LocalIpv4 != "未获取") IpAddressTextBox.Text = adapter.LocalIpv4;
        SubnetMaskTextBox.Text = adapter.SubnetMask;
        GatewayTextBox.Text = adapter.Gateway == "未设置" ? string.Empty : adapter.Gateway.Split('、')[0];
        var dns = adapter.DnsServers.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        PrimaryDnsTextBox.Text = dns.Length > 0 && dns[0] != "未设置" ? dns[0] : string.Empty;
        SecondaryDnsTextBox.Text = dns.Length > 1 ? dns[1] : string.Empty;
        if (ModeComboBox is not null) ModeComboBox.SelectedIndex = adapter.IsDhcp == false ? 1 : 0;
    }

    private void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StaticPanel is null || ModeComboBox.SelectedItem is not ComboBoxItem item) return;
        StaticPanel.IsEnabled = item.Tag?.ToString() == "Static";
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (AdapterComboBox.SelectedItem is not NetworkAdapterInfo adapter) { ShowInfo("请选择网卡。"); return; }
        var request = new IpConfigurationRequest
        {
            AdapterName = adapter.Name,
            UseDhcp = (ModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() != "Static",
            IpAddress = IpAddressTextBox.Text.Trim(),
            SubnetMask = SubnetMaskTextBox.Text.Trim(),
            Gateway = GatewayTextBox.Text.Trim(),
            PrimaryDns = PrimaryDnsTextBox.Text.Trim(),
            SecondaryDns = SecondaryDnsTextBox.Text.Trim()
        };
        var error = SystemNetworkService.ValidateIpConfiguration(request);
        if (error is not null) { ShowInfo(error); return; }
        Request = request;
        DialogResult = true;
    }

    private void ShowInfo(string message) => MessageBox.Show(this, message, "聚点工作台", MessageBoxButton.OK, MessageBoxImage.Information);
}
