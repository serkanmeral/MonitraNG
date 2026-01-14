using MngScheduler.Api.Config;
using MngScheduler.Application;
using MngScheduler.Application.Configuration;
using MngScheduler.Infrastructure;
using MngScheduler.Persistence;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load environment variables
    builder.Configuration.AddEnvironmentVariables();

    var mngSchedulerSettings = builder.Configuration.GetSection("MngSchedulerSettings").Get<MngSchedulerSettings>();
    if (mngSchedulerSettings == null)
    {
        Console.WriteLine("ERROR: MngSchedulerSettings configuration is required!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw new InvalidOperationException("MngSchedulerSettings configuration is required!");
    }

    Serilog.Core.Logger? log = null;
    try
    {
        log = builder.InitSerilog(mngSchedulerSettings);
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
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.QueryStringApiVersionReader("version"),
            new Asp.Versioning.HeaderApiVersionReader("Api-Version"),
            new Asp.Versioning.UrlSegmentApiVersionReader()
        );
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    builder.InitOpenApi();
    builder.InitAuthentication(mngSchedulerSettings);

    // HttpContextAccessor
    builder.Services.AddHttpContextAccessor();

    // HttpClient Factory
    builder.Services.AddHttpClient();

    // Memory Cache
    builder.Services.AddMemoryCache();

    // Application, Infrastructure & Persistence Services
    try
    {
        builder.Services.AddApplicationServices(mngSchedulerSettings);
        builder.Services.AddInfrastructureServices(mngSchedulerSettings);
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
        app.UseApplicationSettings(mngSchedulerSettings);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Failed to configure application settings");
        Console.WriteLine($"Exception: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        Log.Fatal(ex, "Failed to configure application settings");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw;
    }

    try
    {
        Log.Information("Starting MngScheduler API");
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
