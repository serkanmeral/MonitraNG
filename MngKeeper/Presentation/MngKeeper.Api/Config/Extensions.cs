using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using MngKeeper.Api.Configuration;
using MngKeeper.Api.Middleware;
using MngKeeper.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MngKeeper.Api.Config;

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

        log.Information($"MngKeeper Starting. Version {Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0] ?? "unknown"}");

        return log;
    }

    public static async Task ConfigureSeqRetentionPoliciesAsync(this WebApplication app, IConfiguration configuration)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        await SeqRetentionPolicy.ConfigureRetentionPoliciesAsync(configuration, logger);
    }

    public static void InitOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();

        // Get OpenAPI Server Path from configuration
        var openApiServerPath = builder.Configuration["MngKeeperSettings:OpenApiServerPath"];

        // Add Swagger Configuration (uses existing SwaggerConfiguration)
        builder.Services.AddSwaggerConfiguration(openApiServerPath);
    }

    //public static void InitWebApp(this WebApplicationBuilder builder, X509Certificate2? certificate)
    //{
    //    if (certificate != null)
    //    {
    //        builder.WebHost.ConfigureKestrel(options =>
    //        {
    //            options.AddServerHeader = false;
    //            // Port configuration from appsettings.json
    //        });
    //    }

    //    builder.Services.AddControllers();

    //    // Add GraphQL
    //    builder.Services.AddGraphQLServer()
    //        .AddQueryType<MngKeeper.Api.GraphQL.Query>();
    //}

    public static void InitWebAPP(this WebApplicationBuilder builder)
    {
        // Get server settings from configuration
        var serverSettings = builder.Configuration.GetSection("MngKeeperSettings:Server").Get<ServerSettings>() 
            ?? new ServerSettings();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            // Parse host - if "0.0.0.0" or "*" listen on any IP
            if (serverSettings.Host == "0.0.0.0" || serverSettings.Host == "*")
            {
                options.ListenAnyIP(serverSettings.Port);
            }
            else if (serverSettings.Host == "localhost" || serverSettings.Host == "127.0.0.1")
            {
                options.ListenLocalhost(serverSettings.Port);
            }
            else
            {
                // Specific IP address
                options.Listen(IPAddress.Parse(serverSettings.Host), serverSettings.Port);
            }

            // Log the configuration
            var logger = builder.Services.BuildServiceProvider().GetService<Serilog.ILogger>();
            logger?.Information($"Kestrel configured to listen on {serverSettings.Host}:{serverSettings.Port} ({serverSettings.Scheme})");
        });

        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler
                = ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.MaxDepth = 64;
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

        // CORS is handled by API Gateway, not needed here (backend services are internal)

        // Add services to the container
        builder.Services.AddControllers();

        // Add GraphQL
        builder.Services.AddGraphQLServer()
            .AddQueryType<MngKeeper.Api.GraphQL.Query>();
    }

    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        // Add Repositories
        services.AddScoped<MngKeeper.Application.Interfaces.IDomainRepository, MngKeeper.Infrastructure.Persistence.Repositories.DomainRepository>();
        services.AddScoped<MngKeeper.Application.Interfaces.IAuditLogRepository, MngKeeper.Infrastructure.Persistence.Repositories.AuditLogRepository>();
        services.AddScoped<MngKeeper.Application.Interfaces.IUserRepository, MngKeeper.Infrastructure.Persistence.Repositories.UserRepository>();
        services.AddScoped<MngKeeper.Application.Interfaces.IGroupRepository, MngKeeper.Infrastructure.Persistence.Repositories.GroupRepository>();
        services.AddScoped<MngKeeper.Application.Interfaces.IPasswordResetTokenRepository, MngKeeper.Infrastructure.Persistence.Repositories.PasswordResetTokenRepository>();

        // Add Services
        services.AddHttpClient();
        
        // Configure HttpClient for KeycloakService
        // BaseAddress sonda / ile bitmeli; yoksa relative path (realms/...) son segmenti siler ve 404 olur.
        services.AddHttpClient<MngKeeper.Application.Interfaces.IKeycloakService, MngKeeper.Infrastructure.Services.KeycloakService>((serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = configuration["MngKeeperSettings:Keycloak:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                var normalized = baseUrl.TrimEnd('/');
                client.BaseAddress = new Uri(normalized + "/");
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        
        // Configure HttpClient for NotifierService
        services.AddHttpClient<MngKeeper.Application.Interfaces.INotifierService, MngKeeper.Infrastructure.Services.NotifierService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IJwtTokenService, MngKeeper.Infrastructure.Services.JwtTokenService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IJwtTokenParserService, MngKeeper.Infrastructure.Services.JwtTokenParserService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IRabbitMqService, MngKeeper.Infrastructure.Services.RabbitMqService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IEventPublisher, MngKeeper.Infrastructure.Services.EventPublisher>();
        services.AddScoped<MngKeeper.Application.Interfaces.IRedisService, MngKeeper.Infrastructure.Services.RedisService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IDirectoryCache, MngKeeper.Infrastructure.Services.DirectoryCacheService>();
        services.AddScoped<MngKeeper.Application.Interfaces.ISessionService, MngKeeper.Infrastructure.Services.SessionService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IMqttService, MngKeeper.Infrastructure.Services.MqttService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IMinioService, MngKeeper.Infrastructure.Services.MinioService>();
        
        // License Services
        services.AddScoped<MngKeeper.Application.Interfaces.ILicenseEncryptionService, MngKeeper.Infrastructure.Services.LicenseEncryptionService>();
        services.AddScoped<MngKeeper.Application.Interfaces.ILicenseService, MngKeeper.Infrastructure.Services.LicenseService>();
        
        // License Background Service (daily validation)
        services.AddHostedService<MngKeeper.Infrastructure.Services.LicenseValidationBackgroundService>();
        
        // Template Repository and Service
        services.AddScoped<MngKeeper.Application.Interfaces.ITemplateRepository, MngKeeper.Infrastructure.Persistence.Repositories.TemplateRepository>();
        services.AddScoped<MngKeeper.Application.Interfaces.ITemplateService, MngKeeper.Infrastructure.Services.TemplateService>();
        
        // DataGateway Sync Service
        services.AddScoped<MngKeeper.Application.Interfaces.IDataGatewaySyncService, MngKeeper.Infrastructure.Services.DataGatewaySyncService>();

        // Directory sync (K2)
        services.AddSingleton<MngKeeper.Application.Interfaces.IDirectorySyncCoordinator, MngKeeper.Infrastructure.Services.DirectorySyncCoordinator>();
        services.AddScoped<MngKeeper.Application.Interfaces.IKeycloakToMongoSyncService, MngKeeper.Infrastructure.Services.KeycloakToMongoSyncService>();

        services.AddHttpContextAccessor();
    }

    public static void UseApplicationSettings(this WebApplication app, MngKeeperSettings settings, IWebHostEnvironment env)
    {
        // 1. Swagger/OpenAPI Documentation
        // Enable Swagger in Development or if explicitly enabled via environment variable
        var enableSwagger = env.IsDevelopment() || 
                           string.Equals(app.Configuration["EnableSwagger"], "true", StringComparison.OrdinalIgnoreCase);
        
        if (enableSwagger)
        {
            app.UseSwaggerConfiguration(env);

            // Scalar + /swagger yönlendirme (Development veya EnableSwagger=true — Odak POC)
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngKeeper API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
            });

            app.MapGet("/swagger", () => Results.Redirect("/api-docs"));
            app.MapGet("/swagger/index.html", () => Results.Redirect("/api-docs"));
        }

        // 2. Global exception handler
        app.UseGlobalExceptionHandler();

        // 5. Serve static files for Swagger UI customization
        app.UseStaticFiles();

        // 6. Serilog request logging (directory sync her zaman Information)
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex != null)
                    return Serilog.Events.LogEventLevel.Error;
                if (httpContext.Response.StatusCode >= 500)
                    return Serilog.Events.LogEventLevel.Error;
                if (httpContext.Request.Path.StartsWithSegments("/api/directory", StringComparison.OrdinalIgnoreCase))
                    return Serilog.Events.LogEventLevel.Information;
                return Serilog.Events.LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/api/directory", StringComparison.OrdinalIgnoreCase))
                    diagnosticContext.Set("DirectorySyncEndpoint", true);
            };
        });

        // 7. Routing (must be before MapControllers)
        app.UseRouting();

        // 8. JWT Claims extraction
        app.UseJwtClaims();

        // 9. Authorization
        // Note: Authorization is handled by JWT middleware, not standard ASP.NET Core authorization
        // app.UseAuthorization(); // Removed - not needed with custom JWT middleware

        // 10. Map controllers
        app.MapControllers();

        // 11. Map GraphQL endpoint
        app.MapGraphQL();
    }
}

