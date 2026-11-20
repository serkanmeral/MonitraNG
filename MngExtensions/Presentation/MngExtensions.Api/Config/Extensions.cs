using System.Net;
using System.Reflection;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MngExtensions.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MngExtensions.Api.Config;

public static class Extensions
{
    public static void InitSerilog(this WebApplicationBuilder builder)
    {
        var log = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        Log.Logger = log;
        builder.Services.AddSingleton<Serilog.ILogger>(log);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(log);
        builder.Host.UseSerilog(log);

        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0] ?? "1.0.0";
        log.Information("MngExtensions starting. Version {Version}", version);
    }

    public static void InitWebApp(this WebApplicationBuilder builder, MngExtensionsSettings settings)
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

        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://127.0.0.1:3000",
                    "http://abt.localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod());
        });
    }

    public static void InitOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddTransient<Microsoft.Extensions.Options.IConfigureOptions<SwaggerGenOptions>, SwaggerConfigureOptions>();
    }

    public static void InitAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var authority = builder.Configuration["Jwt:Authority"];
                if (!string.IsNullOrWhiteSpace(authority))
                    options.Authority = authority;
                options.RequireHttpsMetadata = builder.Configuration.GetValue("Jwt:RequireHttpsMetadata", false);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = true,
                    SignatureValidator = (token, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token),
                    NameClaimType = "preferred_username"
                };
            });

        builder.Services.AddAuthorization();
    }

    public static void UseApplicationSettings(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        var enableSwagger = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger");
        if (enableSwagger)
        {
            var versions = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            app.UseSwagger(c => c.RouteTemplate = "api-docs/{documentName}/swagger.json");
            app.UseSwaggerUI(c =>
            {
                foreach (var description in versions.ApiVersionDescriptions.OrderByDescending(d => d.ApiVersion))
                {
                    c.SwaggerEndpoint($"/api-docs/{description.GroupName}/swagger.json", $"MngExtensions API {description.GroupName}");
                }

                c.RoutePrefix = "swagger";
                c.DocumentTitle = "MngExtensions API";
            });

            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngExtensions API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
            });
        }

        app.MapControllers();
    }
}

public sealed class SwaggerConfigureOptions : Microsoft.Extensions.Options.IConfigureOptions<SwaggerGenOptions>
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
                Title = "MngExtensions API",
                Version = description.ApiVersion.ToString(),
                Description = "Kuruma özel prosedürler. İlk modül kapı geçişidir."
            });
        }

        var serverPath = _configuration["MngExtensionsSettings:OpenApiServerPath"];
        if (!string.IsNullOrWhiteSpace(serverPath))
            options.AddServer(new OpenApiServer { Url = serverPath, Description = "API Gateway" });
    }
}
