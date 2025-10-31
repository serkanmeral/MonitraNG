using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// Version information controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "System")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Gets detailed version information about the API
    /// </summary>
    /// <returns>Version details including assembly info, build date, and dependencies</returns>
    [HttpGet]
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
            Product = product ?? "MngKeeper API",
            Version = informationalVersion ?? version?.ToString() ?? "1.0.0",
            AssemblyVersion = version?.ToString() ?? "1.0.0.0",
            BuildDate = buildDate,
            Company = company ?? "iSIM Platform",
            Copyright = copyright ?? "Copyright © 2025",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Runtime = new
            {
                Framework = Environment.Version.ToString(),
                OS = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount
            },
            Dependencies = new
            {
                MongoDB = "7.0",
                Keycloak = "23.0.3",
                Redis = "7-alpine",
                RabbitMQ = "3-management",
                Mosquitto = "2.0"
            }
        });
    }

    /// <summary>
    /// Gets simple version string
    /// </summary>
    [HttpGet("short")]
    public IActionResult GetShortVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                      ?? assembly.GetName().Version?.ToString() 
                      ?? "1.0.0";

        return Ok(new { Version = version });
    }
}

