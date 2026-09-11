namespace JuDianWorkbench.Models;

public sealed class IpConfigurationRequest
{
    public required string AdapterName { get; init; }
    public bool UseDhcp { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string SubnetMask { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string PrimaryDns { get; init; } = string.Empty;
    public string SecondaryDns { get; init; } = string.Empty;
}
