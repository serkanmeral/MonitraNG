using Microsoft.AspNetCore.Mvc;
using MngSim.Models;
using MngSim.Services;

namespace MngSim.Controllers;

public record DeviceMetricDto(string Name, string Value, string? Unit);

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly ISimulatorConfigService _configService;
    private readonly IHostMetricGenerator _hostMetricGenerator;
    private readonly IPduMetricGenerator _pduMetricGenerator;
    private readonly SnmpTemplateRegistry _snmpRegistry;

    public DeviceController(
        ISimulatorConfigService configService,
        IHostMetricGenerator hostMetricGenerator,
        IPduMetricGenerator pduMetricGenerator,
        SnmpTemplateRegistry snmpRegistry)
    {
        _configService = configService;
        _hostMetricGenerator = hostMetricGenerator;
        _pduMetricGenerator = pduMetricGenerator;
        _snmpRegistry = snmpRegistry;
    }

    /// <summary>Cihaz bilgileri ve endpoint (IP, port vb.)</summary>
    [HttpGet("{id}/info")]
    public ActionResult<object> GetInfo(string id)
    {
        var (device, index) = FindDevice(id);
        if (device == null)
            return NotFound();

        var config = _configService.GetConfig();
        if (config == null)
            return NotFound();

        var endpointDisplay = device.Protocol switch
        {
            "Http" => $"http://localhost:{config.HttpBasePort + 1 + index}/metrics",
            "Snmp" => $"udp://127.0.0.1:{config.SnmpBasePort + index} (community: public, template: {device.SnmpTemplate ?? "Pdu"})",
            "Mqtt" => $"topic: mngsim/devices/{device.RoomId ?? device.Id}/metrics",
            _ => "—"
        };

        return Ok(new
        {
            id = device.Id,
            name = device.Name,
            location = device.Location,
            protocol = device.Protocol,
            snmpTemplate = device.SnmpTemplate ?? "Pdu",
            roomId = device.RoomId,
            isEnabled = device.IsEnabled ?? true,
            endpointDisplay
        });
    }

    /// <summary>Anlık metrikler (profil sayfası için; periyodik çağrılabilir)</summary>
    [HttpGet("{id}/metrics")]
    public ActionResult<object> GetMetrics(string id)
    {
        var (device, _) = FindDevice(id);
        if (device == null)
            return NotFound();

        if (device.IsEnabled == false)
            return Ok(new { collectedAt = DateTime.UtcNow, metrics = new List<DeviceMetricDto>(), isEnabled = false });

        var collectedAt = DateTime.UtcNow;
        List<DeviceMetricDto> metrics;

        if (string.Equals(device.Protocol, "Http", StringComparison.OrdinalIgnoreCase))
        {
            var items = _hostMetricGenerator.GenerateForDevice(device, collectedAt);
            metrics = items.Select(m => new DeviceMetricDto(m.CollectibleCode, m.Value?.ToString() ?? "", m.Unit)).ToList();
        }
        else if (string.Equals(device.Protocol, "Snmp", StringComparison.OrdinalIgnoreCase))
        {
            var template = _snmpRegistry.GetTemplate(device.SnmpTemplate);
            if (template == null)
                return Ok(new { collectedAt, metrics = new List<DeviceMetricDto>() });

            metrics = new List<DeviceMetricDto>();
            foreach (var oid in template.GetOrderedOids())
            {
                var data = template.GetValueForOid(oid, device);
                if (data != null)
                    metrics.Add(new DeviceMetricDto(OidToName(oid, template.Name), data.ToString() ?? "", null));
            }
        }
        else
        {
            metrics = new List<DeviceMetricDto>();
        }

        return Ok(new { collectedAt, metrics });
    }

    private (VirtualDevice? device, int index) FindDevice(string id)
    {
        var config = _configService.GetConfig();
        if (config == null) return (null, -1);
        for (int i = 0; i < config.Devices.Count; i++)
        {
            if (string.Equals(config.Devices[i].Id, id, StringComparison.OrdinalIgnoreCase))
                return (config.Devices[i], i);
        }
        return (null, -1);
    }

    private static string OidToName(string oid, string template)
    {
        if (template == "Pdu")
        {
            if (oid.EndsWith(".1")) return "deviceName";
            if (oid.EndsWith(".2")) return "inputVoltage (V)";
            if (oid.EndsWith(".3")) return "inputCurrent (x0.1A)";
            if (oid.EndsWith(".4")) return "activePowerW";
            if (oid.EndsWith(".5")) return "temperature (°C)";
            if (oid.EndsWith(".6")) return "outletCount";
            if (oid.Contains(".7.")) return "outletStatus";
        }
        if (template == "Router")
        {
            if (oid.Contains("1.1.1.0")) return "sysDescr";
            if (oid.Contains("1.1.3.0")) return "sysUpTime";
            if (oid.Contains("1.1.5.0")) return "sysName";
            if (oid.Contains("2.1.0")) return "ifNumber";
            if (oid.Contains(".10.")) return "ifInOctets";
            if (oid.Contains(".16.")) return "ifOutOctets";
        }
        return oid;
    }
}
