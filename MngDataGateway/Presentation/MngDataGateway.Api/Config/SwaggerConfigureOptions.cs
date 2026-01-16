using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace MngDataGateway.Api.Config;

/// <summary>
/// Configures Swagger options for API versioning
/// </summary>
public class SwaggerConfigureOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly IConfiguration _configuration;

    public SwaggerConfigureOptions(IApiVersionDescriptionProvider provider, IConfiguration configuration)
    {
        _provider = provider;
        _configuration = configuration;
    }

    public void Configure(SwaggerGenOptions options)
    {
        // Build Swagger documents for each API version discovered by ApiExplorer
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "MngDataGateway API",
                Version = description.ApiVersion.ToString(),
                Description = "Dynamic Data Gateway for MongoDB with schema management",
                Contact = new OpenApiContact
                {
                    Name = "MonitraNG",
                    Email = "serkan.meral@isimplatform.io"
                }
            });
        }

        // Configure Server URL from settings (for API Gateway)
        var openApiServerPath = _configuration["MngDataGatewaySettings:OpenApiServerPath"];
        if (!string.IsNullOrEmpty(openApiServerPath))
        {
            options.AddServer(new OpenApiServer
            {
                Url = openApiServerPath,
                Description = "API Gateway Server"
            });
        }

        // Include XML comments if available
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
}

