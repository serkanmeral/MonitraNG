using Microsoft.AspNetCore.Mvc;
using MngSim.Models;
using MngSim.Services;

namespace MngSim.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ISimulatorConfigService _configService;

    public ConfigController(ISimulatorConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public ActionResult<SimulatorConfig?> Get()
    {
        var config = _configService.GetConfig();
        if (config == null)
            return Ok((SimulatorConfig?)null);
        return Ok(config);
    }

    [HttpPost]
    public IActionResult Post([FromBody] SimulatorConfig config)
    {
        if (config == null)
            return BadRequest();
        _configService.SetConfig(config);
        return Ok(new { ok = true });
    }
}
