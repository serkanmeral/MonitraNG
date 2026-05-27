using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using MngOperations.Api.Filters;
using MngOperations.Application.Configuration;
using Microsoft.OpenApi.Models;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MngOperations.Api.Config;

public static class Extensions
{
    public static Serilog.Core.Logger InitSerilog(this WebApplicationBuilder builder)
    {
        var log = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Services.AddSingleton<Serilog.ILogger>(log);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(log);
        builder.Host.UseSerilog(log);

        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0] ?? "1.0.0";

        log.Information("MngOperations starting. Version {Version}", version);
        return log;
    }

    public static void InitWebApp(this WebApplicationBuilder builder, MngOperationsSettings settings)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            if (settings.Server.Host is "0.0.0.0" or "*")
                options.ListenAnyIP(settings.Server.Port);
            else if (settings.Server.Host is "localhost" or "127.0.0.1")
                options.ListenLocalhost(settings.Server.Port);
            else
                options.Listen(IPAddress.Parse(settings.Server.Host), settings.Server.Port);
        });

        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<OperationCoreExceptionFilter>();
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
    }

    public static void InitOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
    }

    public static void InitAuthentication(this WebApplicationBuilder builder)
    {
        var jwtAuthority = builder.Configuration["Jwt:Authority"];
        if (string.IsNullOrEmpty(jwtAuthority))
            return;

        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = jwtAuthority;
                options.RequireHttpsMetadata = builder.Configuration.GetValue("Jwt:RequireHttpsMetadata", false);
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    SignatureValidator = (token, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token)
                };
            });

        builder.Services.AddAuthorization();
    }

    public static void UseApplicationPipeline(this WebApplication app, MngOperationsSettings settings)
    {
        app.UseSerilogRequestLogging();
        app.UseRouting();

        var enableSwagger = app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("EnableSwagger");

        if (enableSwagger)
        {
            app.UseSwagger(c => c.RouteTemplate = "api-docs/{documentName}/swagger.json");
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/api-docs/v1/swagger.json", "MngOperations API v1");
                c.RoutePrefix = "swagger";
                c.DocumentTitle = "MngOperations API";
            });
        }

        if (!string.IsNullOrEmpty(app.Configuration["Jwt:Authority"]))
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapControllers();
    }
}

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
                Title = "MngOperations API",
                Version = description.ApiVersion.ToString(),
                Description = "Operation Core orchestration API"
            });
        }

        var serverPath = _configuration["MngOperationsSettings:OpenApiServerPath"];
        if (!string.IsNullOrEmpty(serverPath))
        {
            options.AddServer(new OpenApiServer { Url = serverPath, Description = "API Gateway" });
        }
    }
}
