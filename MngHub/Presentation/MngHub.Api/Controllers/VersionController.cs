using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace MngHub.Api.Controllers;

/// <summary>
/// Controller for retrieving application version information
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Get complete version information
    /// </summary>
    [HttpGet]
    public IActionResult GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        
        var informationalVersion = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var assemblyVersion = assembly?.GetName().Version?.ToString();
        var fileVersion = assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var product = assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var company = assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var copyright = assembly?.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        var description = assembly?.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        var title = assembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

        return Ok(new
        {
            version = informationalVersion?.Split('+')[0] ?? "unknown",
            informationalVersion = informationalVersion ?? "unknown",
            assemblyVersion = assemblyVersion ?? "unknown",
            fileVersion = fileVersion ?? "unknown",
            product = product ?? "MngHub API",
            company = company ?? "Serkan MERAL",
            copyright = copyright ?? "Copyright © 2025 Serkan MERAL",
            description = description,
            title = title ?? "MngHub API",
            buildDate = GetBuildDate(assembly),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        });
    }

    /// <summary>
    /// Get simplified version information
    /// </summary>
    [HttpGet("simple")]
    public IActionResult GetSimpleVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown";
        
        return Ok(new
        {
            version = version,
            product = "MngHub API"
        });
    }

    private static DateTime? GetBuildDate(Assembly? assembly)
    {
        if (assembly == null)
            return null;

        try
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location))
                return null;

            var fileInfo = new FileInfo(location);
            return fileInfo.LastWriteTime;
        }
        catch
        {
            return null;
        }
    }
}

