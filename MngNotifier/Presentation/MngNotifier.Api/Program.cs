using Asp.Versioning;
using MngNotifier.Api.Config;
using MngNotifier.Application.Configuration;
using MngNotifier.Infrastructure;
using MngNotifier.Persistence;
using Serilog;
using System.Text.Json.Serialization;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load environment variables
    builder.Configuration.AddEnvironmentVariables();

    // Configuration
    var settings = builder.Configuration.GetSection("MngNotifierSettings").Get<MngNotifierSettings>();
    if (settings == null)
    {
        Console.WriteLine("ERROR: MngNotifierSettings configuration is required!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw new InvalidOperationException("MngNotifierSettings configuration is required!");
    }
    builder.Services.Configure<MngNotifierSettings>(builder.Configuration.GetSection("MngNotifierSettings"));

    // Serilog
    Serilog.Core.Logger? log = null;
    try
    {
        log = builder.InitSerilog(settings);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to initialize Serilog");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    try
    {
        builder.InitWebAPP();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to initialize WebApp");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        log?.Fatal(ex, "Failed to initialize WebApp");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new QueryStringApiVersionReader("version"),
            new HeaderApiVersionReader("Api-Version"),
            new UrlSegmentApiVersionReader()
        );
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // OpenAPI (Swagger + Scalar)
    builder.InitOpenApi();

    // Infrastructure Services (Mail Provider)
    try
    {
        builder.Services.AddInfrastructureServices();
        builder.Services.AddPersistenceServices();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to register services");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        log?.Fatal(ex, "Failed to register services");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    WebApplication app;
    try
    {
        app = builder.Build();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to build application");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        log?.Fatal(ex, "Failed to build application");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    try
    {
        app.UseApplicationSettings(settings);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to configure application settings");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        log?.Fatal(ex, "Failed to configure application settings");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    try
    {
        Log.Information("Starting MngNotifier API");
        app.Run();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FATAL ERROR: Application terminated unexpectedly");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        }
        Log.Fatal(ex, "Application terminated unexpectedly");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
    finally
    {
        Log.CloseAndFlush();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"CRITICAL ERROR: Application failed to start");
    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.WriteLine($"Exception Message: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
    }
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    Environment.Exit(1);
}
