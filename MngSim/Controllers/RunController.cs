using System.Net;
using Microsoft.AspNetCore.Mvc;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using MngSim.Services;

namespace MngSim.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RunController : ControllerBase
{
    private readonly ISimulatorHostService _hostService;

    public RunController(ISimulatorHostService hostService)
    {
        _hostService = hostService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start()
    {
        var result = await _hostService.StartAsync(HttpContext.RequestAborted);
        if (!result.Success)
            return BadRequest(new { running = false, error = result.ErrorMessage, busyPorts = result.BusyPorts });
        return Ok(new { running = _hostService.IsRunning });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await _hostService.StopAsync(HttpContext.RequestAborted);
        return Ok(new { running = _hostService.IsRunning });
    }

    /// <summary>
    /// Net-SNMP yüklü olmadan SNMP simülatörünü test etmek için: verilen port ve OID'ye GET isteği gönderir, yanıtı döndürür.
    /// Örnek: GET /api/run/test-snmp?port=11161&amp;oid=1.3.6.1.4.1.99999.1.1.2
    /// </summary>
    [HttpGet("test-snmp")]
    public async Task<IActionResult> TestSnmp([FromQuery] int port = 11161, [FromQuery] string oid = "1.3.6.1.4.1.99999.1.1.2")
    {
        if (port < 1 || port > 65535)
            return BadRequest(new { error = "port 1-65535 aralığında olmalı." });
        if (string.IsNullOrWhiteSpace(oid))
            return BadRequest(new { error = "oid gerekli." });

        try
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var variables = new List<Variable> { new Variable(new ObjectIdentifier(oid)) };
            var result = await Messenger.GetAsync(
                VersionCode.V2,
                endpoint,
                new OctetString("public"),
                variables,
                HttpContext.RequestAborted);

            var items = result.Select(v => new { oid = v.Id.ToString(), value = v.Data?.ToString() ?? "(null)", type = v.Data?.TypeCode.ToString() }).ToList();
            return Ok(new { port, requestedOid = oid, variables = items });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new { error = "İstek iptal edildi." });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "SNMP yanıtı alınamadı. Simülatör çalışıyor ve bu portta SNMP dinliyor mu?", detail = ex.Message });
        }
    }
}
