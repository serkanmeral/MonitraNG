using MediatR;
using Microsoft.AspNetCore.Mvc;
using MngEngine.Application.Features.Config;
using MngEngine.Application.Interfaces;
using MngEngine.Domain.Entities.Config;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfigService _configService;

    public ConfigController(IMediator mediator, IConfigService configService)
    {
        _mediator = mediator;
        _configService = configService;
    }

    [HttpPost]
    public async Task<IActionResult> ApplyConfig([FromBody] ConfigApply configText)
    {
        var request = new ConfigCommandRequest { ConfigText = configText.ConfigText };
        var res = await _mediator.Send(request);
        return Ok(res);
    }

    /// <summary>Config'i siler; Engine sıfır kurulum moduna geçer.</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteConfig()
    {
        await _configService.ClearConfigAsync();
        return Ok(new { success = true, message = "Config silindi." });
    }
}
