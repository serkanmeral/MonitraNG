using MngAdmin.Api.Config;
using MngAdmin.Application;
using MngAdmin.Application.Configuration;
using MngAdmin.Infrastructure;
using MngAdmin.Persistence;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Load environment variables
    builder.Configuration.AddEnvironmentVariables();

    var mngAdminSettings = builder.Configuration.GetSection("MngAdminSettings").Get<MngAdminSettings>();
    if (mngAdminSettings == null)
    {
        Console.WriteLine("ERROR: MngAdminSettings configuration is required!");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        throw new InvalidOperationException("MngAdminSettings configuration is required!");
    }

    Serilog.Core.Logger? log = null;
    try
    {
        log = builder.InitSerilog(mngAdminSettings);
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
    builder.InitAuthentication(mngAdminSettings);

    // HttpContextAccessor
    builder.Services.AddHttpContextAccessor();

    // HttpClient Factory
    builder.Services.AddHttpClient();

    // Memory Cache
    builder.Services.AddMemoryCache();

    // Application, Infrastructure & Persistence Services
    try
    {
        builder.Services.AddApplicationServices(mngAdminSettings);
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
        app.UseApplicationSettings(mngAdminSettings);
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
        Log.Information("Starting MngAdmin API");
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
