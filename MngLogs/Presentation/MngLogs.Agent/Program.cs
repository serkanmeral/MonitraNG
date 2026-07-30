using System.Net.Sockets;
using MngLogs.Agent.Cli;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.Transport;
using MngLogs.Agent.Workers;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.Metrics;
using Serilog;

// CLI recovery mode (pin / port / status) — does not start the web host.
if (AgentCli.IsCliInvocation(args))
    return await AgentCli.RunAsync(args);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MngLogsAgentSettings>(
    builder.Configuration.GetSection(MngLogsAgentSettings.SectionName));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "MngLogs Agent";
});

builder.Services.AddSingleton<IAgentConfigStore, AgentConfigStore>();
builder.Services.AddSingleton<ILocalUiPinAuth, LocalUiPinAuth>();
builder.Services.AddSingleton<IEventLogPackageCatalogStore, EventLogPackageCatalogStore>();
builder.Services.AddSingleton<AgentRuntimeStatus>();
builder.Services.AddSingleton<IOutboundQueue>(sp =>
{
    var config = sp.GetRequiredService<IAgentConfigStore>();
    var status = sp.GetRequiredService<AgentRuntimeStatus>();
    var dir = config.ResolveDataDirectory();
    Directory.CreateDirectory(dir);
    return new ObservingOutboundQueue(new DiskOutboundQueue(dir), status);
});
builder.Services.AddHttpClient("collector", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<ICollectorClient, CollectorClient>();
builder.Services.AddSingleton<IHostMetricsCollector, HostMetricsCollector>();
builder.Services.AddSingleton<IWindowsEventLogReader>(_ =>
    OperatingSystem.IsWindows()
        ? new WindowsEventLogReader()
        : new NoOpWindowsEventLogReader());
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IAgentConfigStore>();
    return new EventLogBookmarkStore(config.ResolveDataDirectory());
});
builder.Services.AddHostedService<HeartbeatProducerWorker>();
builder.Services.AddHostedService<PackageCatalogSyncWorker>();
builder.Services.AddHostedService<EventLogCollectorWorker>();
builder.Services.AddHostedService<ServiceWatchWorker>();
builder.Services.AddHostedService<OutboundShipperWorker>();

// Bind Kestrel to local UI only (field agent UI).
string uiHost;
int uiPort;
{
    var early = builder.Configuration.GetSection(MngLogsAgentSettings.SectionName).Get<MngLogsAgentSettings>()
                ?? new MngLogsAgentSettings();
    uiHost = string.IsNullOrWhiteSpace(early.System.LocalUiHost) ? "127.0.0.1" : early.System.LocalUiHost;
    uiPort = early.System.LocalUiPort <= 0 ? 5092 : early.System.LocalUiPort;

    if (!LocalUiPortProbe.IsPortAvailable(uiHost, uiPort, out var probeDetail))
    {
        var hint = LocalUiPortProbe.FindListenerProcessHint(uiPort);
        Log.Fatal(
            "Local UI port {Port} on {Host} is unavailable ({Detail}). {Hint} " +
            "Fix: MngLogs.Agent.exe port set <newPort>   then restart the agent. " +
            "Check: MngLogs.Agent.exe port check",
            uiPort,
            uiHost,
            probeDetail ?? "address in use",
            hint ?? "");
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Local UI açılamıyor: http://{uiHost}:{uiPort}/ (port kullanımda veya bağlanılamıyor).");
        if (!string.IsNullOrWhiteSpace(probeDetail))
            Console.Error.WriteLine($"  {probeDetail}");
        if (!string.IsNullOrWhiteSpace(hint))
            Console.Error.WriteLine($"  {hint}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Kurtarma:");
        Console.Error.WriteLine("  MngLogs.Agent.exe port check");
        Console.Error.WriteLine("  MngLogs.Agent.exe port set <yeniPort>");
        Console.Error.WriteLine("  (gerekirse --data-dir \"C:\\path\\to\\data\")");
        Console.Error.WriteLine("Ardından agent’ı yeniden başlatın.");
        return 3;
    }

    builder.WebHost.UseUrls($"http://{uiHost}:{uiPort}");
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapLocalUi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mnglogs-agent" }));
app.MapFallbackToFile("index.html");

Log.Information(
    "Starting MngLogs Agent (Windows Service capable). Local UI http://{Host}:{Port}/",
    uiHost,
    uiPort);

try
{
    app.Run();
    return 0;
}
catch (IOException ex) when (ex.InnerException is SocketException ||
                             ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                             ex.Message.Contains("Failed to bind", StringComparison.OrdinalIgnoreCase))
{
    Log.Fatal(ex,
        "Failed to bind Local UI. Use: MngLogs.Agent.exe port set <newPort> && restart");
    Console.Error.WriteLine("Local UI bind başarısız (port çakışması).");
    Console.Error.WriteLine("  MngLogs.Agent.exe port set <yeniPort>");
    Console.Error.WriteLine("  sonra agent’ı yeniden başlatın.");
    return 3;
}

public partial class Program;
