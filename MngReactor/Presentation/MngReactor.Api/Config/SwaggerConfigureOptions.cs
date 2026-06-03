using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace MngReactor.Api.Config;

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
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "MngReactor API",
                Version = description.ApiVersion.ToString(),
                Description = "Monitoring data ingestion and config sync service for MonitraNG platform",
                Contact = new OpenApiContact
                {
                    Name = "MonitraNG",
                    Email = "serkan.meral@isimplatform.io"
                }
            });
        }

        var openApiServerPath = _configuration["MngReactorSettings:OpenApiServerPath"];
        if (!string.IsNullOrEmpty(openApiServerPath))
        {
            options.AddServer(new OpenApiServer
            {
                Url = openApiServerPath,
                Description = "API Gateway Server"
            });
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);
    }
}
