using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/version")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "1.0.0";

        return Ok(new
        {
            service = "MngOperations",
            version = informational.Split('+')[0],
            timestamp = DateTime.UtcNow
        });
    }
}
