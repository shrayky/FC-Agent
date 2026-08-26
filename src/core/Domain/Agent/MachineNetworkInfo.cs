using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Domain.Agent;

/// <summary>
/// Имя компьютера и список IP без loopback и link-local.
/// </summary>
public static class MachineNetworkInfo
{
    public static string HostName() => Environment.MachineName;

    public static List<string> ListAddresses()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(iface => iface.OperationalStatus == OperationalStatus.Up)
            .Where(iface => iface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(iface => iface.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address);

        return SelectAddresses(addresses);
    }

    public static List<string> SelectAddresses(IEnumerable<IPAddress> addresses)
    {
        return addresses
            .Where(IsPublicUnicast)
            .Distinct()
            .OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
            .Select(address => address.ToString())
            .ToList();
    }

    private static bool IsPublicUnicast(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return false;

        if (address.IsIPv6LinkLocal)
            return false;

        if (IsIpv4LinkLocal(address))
            return false;

        return true;
    }

    private static bool IsIpv4LinkLocal(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();
        return bytes.Length >= 2 && bytes[0] == 169 && bytes[1] == 254;
    }
}
