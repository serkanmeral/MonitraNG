using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MngExtensions.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/version")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return Ok(new
        {
            Product = "MngExtensions API",
            Version = informational ?? version?.ToString() ?? "1.0.0",
            AssemblyVersion = version?.ToString() ?? "1.0.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    [HttpGet("short")]
    public IActionResult Short()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "1.0.0";
        return Ok(new { Version = version });
    }
}
