namespace JuDianWorkbench.Models;

public sealed class SystemNetworkSnapshot
{
    public bool HasLocalNetwork { get; init; }
    public bool HasInternet { get; init; }
    public string InternetDetail { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string Cpu { get; init; } = string.Empty;
    public string Memory { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string WindowsVersion { get; init; } = string.Empty;
    public string CpuDetails { get; init; } = string.Empty;
    public string MemoryDetails { get; init; } = string.Empty;
    public string Graphics { get; init; } = string.Empty;
    public string GraphicsDriver { get; init; } = string.Empty;
    public string Display { get; init; } = string.Empty;
    public string Motherboard { get; init; } = string.Empty;
    public string Bios { get; init; } = string.Empty;
    public string Drives { get; init; } = string.Empty;
    public string Uptime { get; init; } = string.Empty;
    public string PublicIp { get; init; } = "未查询";
    public string Location { get; init; } = "未查询";
    public string Isp { get; init; } = "未查询";
    public IReadOnlyList<NetworkAdapterInfo> Adapters { get; init; } = [];
}
