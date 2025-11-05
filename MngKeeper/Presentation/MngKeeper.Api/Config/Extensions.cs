using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using MngKeeper.Api.Configuration;
using MngKeeper.Api.Middleware;
using MngKeeper.Application.Configuration;
using Scalar.AspNetCore;
using Serilog;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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

    public static void InitOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();

        // Add Swagger Configuration (uses existing SwaggerConfiguration)
        builder.Services.AddSwaggerConfiguration();
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

    public static void InitWebAPP(this WebApplicationBuilder builder, X509Certificate2 certificate)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;

            options.ListenAnyIP(5001, _opt =>
            {
                _opt.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = certificate;
                });
            });
        });

        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler
                = ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.MaxDepth = 64;
        });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

        builder.Services.AddCors(l =>
        {
            l.AddPolicy("CorsPolicy", b =>
                b.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                 .WithExposedHeaders("Content-Disposition")
                );
        });


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

        // Add Services
        services.AddHttpClient();
        
        // Configure HttpClient for KeycloakService
        services.AddHttpClient<MngKeeper.Application.Interfaces.IKeycloakService, MngKeeper.Infrastructure.Services.KeycloakService>((serviceProvider, client) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var baseUrl = configuration["MngKeeperSettings:Keycloak:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<MngKeeper.Application.Interfaces.IJwtTokenService, MngKeeper.Infrastructure.Services.JwtTokenService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IJwtTokenParserService, MngKeeper.Infrastructure.Services.JwtTokenParserService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IRabbitMqService, MngKeeper.Infrastructure.Services.RabbitMqService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IEventPublisher, MngKeeper.Infrastructure.Services.EventPublisher>();
        services.AddScoped<MngKeeper.Application.Interfaces.IRedisService, MngKeeper.Infrastructure.Services.RedisService>();
        services.AddScoped<MngKeeper.Application.Interfaces.ISessionService, MngKeeper.Infrastructure.Services.SessionService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IMqttService, MngKeeper.Infrastructure.Services.MqttService>();
        services.AddScoped<MngKeeper.Application.Interfaces.IMinioService, MngKeeper.Infrastructure.Services.MinioService>();
        services.AddHttpContextAccessor();
    }

    public static void UseApplicationSettings(this WebApplication app, MngKeeperSettings settings, IWebHostEnvironment env)
    {
        // 1. Development-specific middleware
        if (env.IsDevelopment())
        {
            app.UseSwaggerConfiguration(env);

            // Add Scalar API Reference (Modern UI)
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MngKeeper API")
                    .WithTheme(ScalarTheme.Purple)
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                    .WithOpenApiRoutePattern("/api-docs/{documentName}/swagger.json");
            });
        }

        // 2. HTTPS redirection
        app.UseHttpsRedirection();

        // 3. Global exception handler
        app.UseGlobalExceptionHandler();

        // 4. Serve static files for Swagger UI customization
        app.UseStaticFiles();

        // 5. Serilog request logging
        app.UseSerilogRequestLogging();

        // 6. Authorization
        app.UseAuthorization();

        // 7. Map controllers
        app.MapControllers();

        // 8. Map GraphQL endpoint
        app.MapGraphQL();
    }
}

