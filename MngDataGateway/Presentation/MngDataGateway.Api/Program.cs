using MngDataGateway.Api.Middleware;
using MngDataGateway.Application.Configuration;
using MongoDB.Driver;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

// Replace environment variable placeholders in configuration
//var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
//if (!string.IsNullOrEmpty(certPassword))
//{
//    builder.Configuration["Kestrel:Endpoints:Https:Certificate:Password"] = certPassword;
//}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MngDataGateway API",
        Version = "v1.0.0",
        Description = "Dynamic Data Gateway for MongoDB with schema management",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "iSIM Platform",
            Email = "serkan.meral@isimplatform.io"
        }
    });
    
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure Options Pattern
builder.Services.Configure<MongoDbOptions>(
    builder.Configuration.GetSection(MongoDbOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

// Add MongoDB
builder.Services.AddSingleton<IMongoClient>(provider =>
{
    var connectionString = builder.Configuration["ConnectionStrings:MongoDB"] ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});

//builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MngDataGateway API v1");
        c.RoutePrefix = "swagger";
    });
}

// HTTPS Redirection disabled for now (certificate not configured)
// app.UseHttpsRedirection();

// Add Global Exception Handler
app.UseGlobalExceptionHandler();

app.UseAuthorization();
app.MapControllers();

try
{
    Log.Information("Starting MngDataGateway API");
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
