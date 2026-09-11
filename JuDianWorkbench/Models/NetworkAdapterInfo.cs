namespace JuDianWorkbench.Models;

public sealed class NetworkAdapterInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public string LocalIpv4 { get; init; } = "未获取";
    public string SubnetMask { get; init; } = string.Empty;
    public string Gateway { get; init; } = "未设置";
    public string DnsServers { get; init; } = "未设置";
    public bool? IsDhcp { get; init; }

    public string IpModeText => IsDhcp switch
    {
        true => "自动获取（DHCP）",
        false => "手动静态 IP",
        _ => "未知"
    };

    public string DisplayName => $"{Name}  ·  {Status}  ·  {LocalIpv4}";
}
