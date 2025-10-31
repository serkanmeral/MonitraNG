using MngKeeper.Application.Interfaces;
using MngKeeper.Infrastructure.Services;
using MngKeeper.Infrastructure.Persistence.Repositories;
using MediatR;
using System.Reflection;
using MongoDB.Driver;
using Serilog;
using MngKeeper.Api.Middleware;
using HotChocolate.AspNetCore;
using MngKeeper.Api.Configuration;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure Kestrel to listen on port 5001
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5001);
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Swagger Configuration
builder.Services.AddSwaggerConfiguration();

// Add GraphQL with HotChocolate
builder.Services.AddGraphQLServer()
    .AddQueryType<MngKeeper.Api.GraphQL.Query>();

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MngKeeper.Application.Features.Domain.Commands.CreateDomain.CreateDomainCommand).Assembly));

// Add MongoDB (simplified)
builder.Services.AddSingleton<IMongoClient>(provider =>
{
    var connectionString = builder.Configuration["ConnectionStrings:MongoDB"] ?? "mongodb://localhost:27017";
    return new MongoDB.Driver.MongoClient(connectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(provider =>
{
    var client = provider.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "MngKeeper";
    return client.GetDatabase(databaseName);
});

// Add Repositories
builder.Services.AddScoped<IDomainRepository, DomainRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

        // Add Services
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IKeycloakService, KeycloakService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IJwtTokenParserService, JwtTokenParserService>();
builder.Services.AddScoped<IRabbitMqService, RabbitMqService>();
builder.Services.AddScoped<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<IRedisService, RedisService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IMqttService, MqttService>();
builder.Services.AddHttpContextAccessor();

// Add Services (commented out external dependencies for now)
// builder.Services.AddScoped<IMqttService, MqttService>();
// builder.Services.AddScoped<ICacheService, RedisCacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerConfiguration(app.Environment);
    
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

app.UseHttpsRedirection();

// Add Global Exception Handler
app.UseGlobalExceptionHandler();

// Serve static files for Swagger UI customization
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// Map GraphQL endpoint
app.MapGraphQL();

try
{
    Log.Information("Starting MngKeeper API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
