using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MngEngine.Infrastructure.Internal;

/// <summary>
/// Lokal makine IP adresini tespit eder (Engine status için).
/// Dns, NetworkInterface ve Socket fallback ile Docker/WSL uyumlu.
/// </summary>
public static class HostAddressHelper
{
    private static string? _cached;

    /// <summary>
    /// İlk IPv4 adresini döner (loopback hariç, varsa).
    /// Docker, WSL ve çoklu ağ kartında daha güvenilir çalışır.
    /// </summary>
    public static string? GetLocalAddress()
    {
        if (_cached != null) return _cached;

        _cached = GetFromNetworkInterfaces();
        if (_cached != null) return _cached;

        _cached = GetFromDns();
        if (_cached != null) return _cached;

        _cached = GetFromSocket();
        return _cached;
    }

    private static string? GetFromNetworkInterfaces()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        return addr.Address.ToString();
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static string? GetFromDns()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static string? GetFromSocket()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep)
                return ep.Address.ToString();
        }
        catch { /* ignore */ }
        return null;
    }
}
