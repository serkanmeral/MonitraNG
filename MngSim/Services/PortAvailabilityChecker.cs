using System.Net;
using System.Net.Sockets;

namespace MngSim.Services;

public class PortAvailabilityChecker : IPortAvailabilityChecker
{
    /// <summary>
    /// Portun boş olup olmadığını kontrol eder. Bağlanmayı dener; bağlantı kabul edilirse port meşgul.
    /// (Bind yapmıyoruz, böylece hemen ardından dinleyici açılırken OS portu serbest bırakmış olur.)
    /// </summary>
    public PortCheckResult CheckPorts(IEnumerable<int> ports)
    {
        var busy = new List<(int Port, string Error)>();
        foreach (var port in ports.Distinct())
        {
            if (!IsPortFree(port))
                busy.Add((port, "Port kullanımda."));
        }
        return new PortCheckResult { BusyPorts = busy };
    }

    private static bool IsPortFree(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(1));
            return false; // Bağlandıysa port meşgul
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return true; // Reddedildi = kimse dinlemiyor = port boş
        }
        catch (AggregateException ex) when (ex.InnerException is SocketException sex && sex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return true;
        }
        catch
        {
            return true; // Zaman aşımı vb. = muhtemelen boş
        }
    }
}
