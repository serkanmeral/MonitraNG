using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// PDU benzeri sentetik metrikler: gerilim, akım, güç, sıcaklık, priz sayısı ve priz durumları.
/// </summary>
public class PduMetricGenerator : IPduMetricGenerator
{
    private static readonly Random Rnd = new();
    private static readonly object RndLock = new();

    public PduSnmpValues Generate(VirtualDevice device)
    {
        var voltage = (uint)NextInt(220, 235);
        var currentX10 = (uint)NextInt(50, 250);
        var power = (uint)NextInt(1000, 5000);
        var temp = NextInt(20, 40);
        var outletCount = SnmpPduOids.DefaultOutletCount;
        var outlets = new int[outletCount];
        for (int i = 0; i < outletCount; i++)
            outlets[i] = Rnd.Next(0, 2);

        return new PduSnmpValues
        {
            DeviceName = device.Name,
            InputVoltage = voltage,
            InputCurrentX10 = currentX10,
            ActivePowerW = power,
            Temperature = temp,
            OutletCount = outletCount,
            OutletStatus = outlets
        };
    }

    private static int NextInt(int min, int max)
    {
        lock (RndLock)
        {
            return Rnd.Next(min, max + 1);
        }
    }
}
