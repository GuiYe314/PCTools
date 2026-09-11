using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using JuDianWorkbench.Models;
using Microsoft.Win32;

namespace JuDianWorkbench.Services;

public sealed class SystemNetworkService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<SystemNetworkSnapshot> GetSnapshotAsync(bool includePublicInfo, CancellationToken cancellationToken = default)
    {
        var adapters = GetAdapters();
        var graphics = GetGraphicsInfo();
        var localNetwork = NetworkInterface.GetIsNetworkAvailable() && adapters.Any(x => x.Status == "已连接");
        var (hasInternet, internetDetail) = await CheckInternetAsync(cancellationToken);
        var publicIp = "未查询";
        var location = "未查询";
        var isp = "未查询";

        if (includePublicInfo && hasInternet)
        {
            try
            {
                using var response = await HttpClient.GetAsync("https://ipwho.is/", cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
                var root = document.RootElement;
                if (root.TryGetProperty("success", out var success) && !success.GetBoolean())
                    throw new InvalidOperationException("公网地址服务返回失败。");
                publicIp = GetText(root, "ip", "未知");
                location = string.Join(" · ", new[]
                {
                    GetText(root, "country", string.Empty),
                    GetText(root, "region", string.Empty),
                    GetText(root, "city", string.Empty)
                }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(location)) location = "未知";
                if (root.TryGetProperty("connection", out var connection))
                    isp = GetText(connection, "isp", "未知");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                publicIp = "查询失败";
                location = "查询失败";
                isp = "查询失败";
                AppLogger.Error("查询公网 IP 和所在地失败", ex);
            }
        }
        else if (includePublicInfo)
        {
            publicIp = location = isp = "当前无法联网";
        }

        return new SystemNetworkSnapshot
        {
            HasLocalNetwork = localNetwork,
            HasInternet = hasInternet,
            InternetDetail = internetDetail,
            ComputerName = Environment.MachineName,
            OperatingSystem = RuntimeInformation.OSDescription,
            Cpu = GetCpuName(),
            Memory = GetMemoryText(),
            Architecture = $"系统 {RuntimeInformation.OSArchitecture} / 进程 {RuntimeInformation.ProcessArchitecture}",
            WindowsVersion = GetWindowsVersion(),
            CpuDetails = GetCpuDetails(),
            MemoryDetails = GetMemoryDetails(),
            Graphics = graphics.Name,
            GraphicsDriver = graphics.Driver,
            Display = $"主显示器 {GetSystemMetrics(0)} × {GetSystemMetrics(1)}",
            Motherboard = GetBiosText("BaseBoardManufacturer", "BaseBoardProduct"),
            Bios = GetBiosText("BIOSVendor", "BIOSVersion", "BIOSReleaseDate"),
            Drives = GetDrivesText(),
            Uptime = FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64)),
            PublicIp = publicIp,
            Location = location,
            Isp = isp,
            Adapters = adapters
        };
    }

    public static string? ValidateIpConfiguration(IpConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AdapterName)) return "请选择要设置的网卡。";
        if (request.UseDhcp) return null;
        if (!IsIpv4(request.IpAddress)) return "请输入有效的 IPv4 地址。";
        if (!IsValidSubnetMask(request.SubnetMask)) return "请输入有效且连续的子网掩码，例如 255.255.255.0。";
        if (!IsIpv4(request.Gateway)) return "请输入有效的默认网关。";
        if (!IsIpv4(request.PrimaryDns)) return "请输入有效的首选 DNS。";
        if (!string.IsNullOrWhiteSpace(request.SecondaryDns) && !IsIpv4(request.SecondaryDns)) return "备用 DNS 地址无效。";
        if (request.IpAddress == request.Gateway) return "IP 地址不能与默认网关相同。";
        return null;
    }

    public async Task ApplyIpConfigurationAsync(IpConfigurationRequest request)
    {
        var error = ValidateIpConfiguration(request);
        if (error is not null) throw new ArgumentException(error, nameof(request));
        if (!NetworkInterface.GetAllNetworkInterfaces().Any(x => x.Name == request.AdapterName))
            throw new InvalidOperationException("所选网卡已经不存在，请刷新后重试。");

        var adapter = ToPowerShellLiteral(request.AdapterName);
        string script;
        if (request.UseDhcp)
        {
            script = $"$ErrorActionPreference='Stop'; $adapter={adapter}; " +
                     "& netsh.exe interface ipv4 set address name=$adapter source=dhcp; if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; " +
                     "& netsh.exe interface ipv4 set dnsservers name=$adapter source=dhcp; exit $LASTEXITCODE";
        }
        else
        {
            var ip = ToPowerShellLiteral(request.IpAddress);
            var mask = ToPowerShellLiteral(request.SubnetMask);
            var gateway = ToPowerShellLiteral(request.Gateway);
            var dns1 = ToPowerShellLiteral(request.PrimaryDns);
            var dns2 = ToPowerShellLiteral(request.SecondaryDns);
            script = $"$ErrorActionPreference='Stop'; $adapter={adapter}; $ip={ip}; $mask={mask}; $gateway={gateway}; $dns1={dns1}; $dns2={dns2}; " +
                     "& netsh.exe interface ipv4 set address name=$adapter source=static address=$ip mask=$mask gateway=$gateway gwmetric=1; if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; " +
                     "& netsh.exe interface ipv4 set dnsservers name=$adapter source=static address=$dns1 validate=no; if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}; " +
                     "if($dns2){& netsh.exe interface ipv4 add dnsservers name=$adapter address=$dns2 index=2 validate=no}; exit $LASTEXITCODE";
        }

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("无法启动网络设置程序。");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"Windows 网络设置返回错误代码 {process.ExitCode}。");
    }

    private static async Task<(bool Connected, string Detail)> CheckInternetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync("https://www.msftconnecttest.com/connecttest.txt", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode && content.Contains("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase)
                ? (true, "可正常访问互联网")
                : (false, $"检测服务返回 {(int)response.StatusCode}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return (false, "连接检测超时"); }
        catch (Exception ex) when (ex is HttpRequestException or SocketException) { return (false, ex.Message); }
    }

    private static List<NetworkAdapterInfo> GetAdapters()
    {
        var result = new List<NetworkAdapterInfo>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(x => x.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                     .OrderByDescending(x => x.OperationalStatus == OperationalStatus.Up).ThenBy(x => x.Name))
        {
            try
            {
                var properties = network.GetIPProperties();
                var unicast = properties.UnicastAddresses.FirstOrDefault(x => x.Address.AddressFamily == AddressFamily.InterNetwork);
                var gateways = properties.GatewayAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork).Select(x => x.Address.ToString());
                var dns = properties.DnsAddresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(x => x.ToString());
                result.Add(new NetworkAdapterInfo
                {
                    Name = network.Name,
                    Description = network.Description,
                    Status = network.OperationalStatus == OperationalStatus.Up ? "已连接" : "未连接",
                    LocalIpv4 = unicast?.Address.ToString() ?? "未获取",
                    SubnetMask = unicast is null ? string.Empty : PrefixToSubnetMask(unicast.PrefixLength),
                    Gateway = string.Join("、", gateways) is { Length: > 0 } gateway ? gateway : "未设置",
                    DnsServers = string.Join("、", dns) is { Length: > 0 } dnsText ? dnsText : "未设置",
                    IsDhcp = unicast is null ? null : unicast.PrefixOrigin == PrefixOrigin.Dhcp
                });
            }
            catch (NetworkInformationException ex)
            {
                AppLogger.Error($"读取网卡信息失败：{network.Name}", ex);
            }
        }
        return result;
    }

    private static string PrefixToSubnetMask(int prefixLength)
    {
        if (prefixLength is < 0 or > 32) return string.Empty;
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return string.Join('.', new[] { (mask >> 24) & 255, (mask >> 16) & 255, (mask >> 8) & 255, mask & 255 });
    }

    private static bool IsIpv4(string value) =>
        IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork;

    private static bool IsValidSubnetMask(string value)
    {
        if (!IsIpv4(value)) return false;
        var bytes = IPAddress.Parse(value).GetAddressBytes();
        var mask = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        var inverted = ~mask;
        return mask != 0 && (inverted & (inverted + 1)) == 0;
    }

    private static string GetCpuName()
    {
        try
        {
            return Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")?.GetValue("ProcessorNameString")?.ToString()?.Trim()
                   ?? "未知";
        }
        catch { return "未知"; }
    }

    private static string GetMemoryText()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(status) ? $"{status.TotalPhysical / 1024d / 1024 / 1024:F1} GB" : "未知";
    }

    private static string GetMemoryDetails()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) return "未知";
        var available = status.AvailablePhysical / 1024d / 1024 / 1024;
        var used = (status.TotalPhysical - status.AvailablePhysical) / 1024d / 1024 / 1024;
        return $"已用 {used:F1} GB · 可用 {available:F1} GB · 使用率 {status.MemoryLoad}%";
    }

    private static string GetCpuDetails()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var mhz = key?.GetValue("~MHz")?.ToString();
            var speed = int.TryParse(mhz, out var value) ? $" · 约 {value / 1000d:F2} GHz" : string.Empty;
            return $"{Environment.ProcessorCount} 个逻辑处理器{speed}";
        }
        catch { return $"{Environment.ProcessorCount} 个逻辑处理器"; }
    }

    private static string GetWindowsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var product = key?.GetValue("ProductName")?.ToString() ?? RuntimeInformation.OSDescription;
            var displayVersion = key?.GetValue("DisplayVersion")?.ToString();
            var build = key?.GetValue("CurrentBuildNumber")?.ToString();
            var ubr = key?.GetValue("UBR")?.ToString();
            return $"{product}" + (string.IsNullOrWhiteSpace(displayVersion) ? string.Empty : $" {displayVersion}") +
                   (string.IsNullOrWhiteSpace(build) ? string.Empty : $" · Build {build}{(string.IsNullOrWhiteSpace(ubr) ? string.Empty : $".{ubr}")}");
        }
        catch { return RuntimeInformation.OSDescription; }
    }

    private static (string Name, string Driver) GetGraphicsInfo()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var drivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            foreach (var subKeyName in root?.GetSubKeyNames().Where(x => x.Length == 4 && x.All(char.IsDigit)) ?? [])
            {
                using var key = root!.OpenSubKey(subKeyName);
                var name = key?.GetValue("DriverDesc")?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Remote", StringComparison.OrdinalIgnoreCase)) names.Add(name);
                var version = key?.GetValue("DriverVersion")?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(version)) drivers.Add(version);
            }
        }
        catch (Exception ex) { AppLogger.Error("读取显卡信息失败", ex); }
        return (names.Count == 0 ? "未获取" : string.Join(" / ", names), drivers.Count == 0 ? "未获取" : string.Join(" / ", drivers));
    }

    private static string GetBiosText(params string[] valueNames)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            var values = valueNames.Select(x => key?.GetValue(x)?.ToString()?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            return values.Count == 0 ? "未获取" : string.Join(" · ", values);
        }
        catch { return "未获取"; }
    }

    private static string GetDrivesText()
    {
        try
        {
            return string.Join(Environment.NewLine, DriveInfo.GetDrives().Where(x => x.IsReady && x.DriveType == DriveType.Fixed).Select(x =>
                $"{x.Name.TrimEnd('\\')}  {x.TotalSize / 1024d / 1024 / 1024:F0} GB · 可用 {x.AvailableFreeSpace / 1024d / 1024 / 1024:F0} GB"));
        }
        catch { return "未获取"; }
    }

    private static string FormatUptime(TimeSpan uptime) =>
        uptime.TotalDays >= 1 ? $"{(int)uptime.TotalDays} 天 {uptime.Hours} 小时" : $"{uptime.Hours} 小时 {uptime.Minutes} 分钟";

    private static string GetText(JsonElement element, string property, string fallback) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static string ToPowerShellLiteral(string value) => $"'{value.Replace("'", "''")}'";

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
