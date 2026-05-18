using Microsoft.AspNetCore.Mvc;
using MngSim.Models;
using MngSim.Services;

namespace MngSim.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ISimulatorConfigService _configService;
    private readonly ISimulatorHostService _hostService;

    public StatusController(ISimulatorConfigService configService, ISimulatorHostService hostService)
    {
        _configService = configService;
        _hostService = hostService;
    }

    [HttpGet]
    public ActionResult<object> Get()
    {
        var config = _configService.GetConfig();
        var httpEndpoints = new List<string>();
        var snmpEndpoints = new List<string>();
        if (config != null && _hostService.IsRunning)
        {
            foreach (var (d, i) in config.Devices.Select((d, i) => (d, i)).Where(x => string.Equals(x.d.Protocol, "Http", StringComparison.OrdinalIgnoreCase)))
                httpEndpoints.Add($"http://localhost:{config.HttpBasePort + 1 + i}/metrics");
            foreach (var (d, i) in config.Devices.Select((d, i) => (d, i)).Where(x => string.Equals(x.d.Protocol, "Snmp", StringComparison.OrdinalIgnoreCase)))
                snmpEndpoints.Add($"udp://127.0.0.1:{config.SnmpBasePort + i} (OID base: 1.3.6.1.4.1.99999.1.1)");
        }

        return Ok(new
        {
            hasConfig = _configService.HasValidConfig(),
            isRunning = _hostService.IsRunning,
            lastError = _hostService.LastError,
            httpEndpoints,
            snmpEndpoints
        });
    }
}
