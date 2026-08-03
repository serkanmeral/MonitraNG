using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MngLogs.Agent.Cli;

public static class LocalUiPortProbe
{
    public static bool IsPortAvailable(string host, int port, out string? detail)
    {
        detail = null;
        if (port is < 1 or > 65535)
        {
            detail = "Port 1–65535 aralığında olmalı.";
            return false;
        }

        try
        {
            var ip = ResolveBindAddress(host);
            var listener = new TcpListener(ip, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException ex)
        {
            detail = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static string? FindListenerProcessHint(int port)
    {
        try
        {
            foreach (var p in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
            {
                if (p.Port == port)
                    return $"Port {port} dinleniyor ({p.Address})";
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static IPAddress ResolveBindAddress(string host)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            host is "127.0.0.1" or "localhost" or "::1")
            return IPAddress.Loopback;

        if (IPAddress.TryParse(host, out var parsed))
            return parsed;

        return IPAddress.Loopback;
    }
}
