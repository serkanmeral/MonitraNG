using MngReactor.Api.Config;
using Serilog;

try
{
    var app = AppBootstrapper.CreateApplication(args);
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: {ex.Message}");
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Integration test icin WebApplicationFactory erisimi.</summary>
public partial class Program { }
