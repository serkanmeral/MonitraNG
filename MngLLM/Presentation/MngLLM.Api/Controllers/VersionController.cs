using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace MngLLM.Api.Controllers;

/// <summary>
/// Version information controller
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Gets detailed version information about the API
    /// </summary>
    /// <returns>Version details including assembly info, build date, and dependencies</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        var buildDate = new FileInfo(assembly.Location).LastWriteTime;

        return Ok(new
        {
            Product = product ?? "MngLLM API",
            Version = informationalVersion ?? version?.ToString() ?? "1.0.0",
            AssemblyVersion = version?.ToString() ?? "1.0.0.0",
            BuildDate = buildDate,
            Company = company ?? "iSIM Platform",
            Copyright = copyright ?? "Copyright © 2025",
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Runtime = new
            {
                Framework = System.Environment.Version.ToString(),
                OS = System.Environment.OSVersion.ToString(),
                MachineName = System.Environment.MachineName,
                ProcessorCount = System.Environment.ProcessorCount
            }
        });
    }

    /// <summary>
    /// Gets simple version string
    /// </summary>
    [HttpGet("short")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetShortVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                      ?? assembly.GetName().Version?.ToString() 
                      ?? "1.0.0";

        return Ok(new { Version = version });
    }
}
