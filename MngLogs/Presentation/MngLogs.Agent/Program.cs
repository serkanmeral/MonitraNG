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

// Windows Service / GPO install: working directory is often System32; pin to exe folder for wwwroot.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MngLogsAgentSettings>(
    builder.Configuration.GetSection(MngLogsAgentSettings.SectionName));

var earlySettings = builder.Configuration.GetSection(MngLogsAgentSettings.SectionName).Get<MngLogsAgentSettings>()
                    ?? new MngLogsAgentSettings();
var logDirectory = ResolveLogDirectory(earlySettings.System.DataDirectory);
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDirectory, "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = MngLogs.Agent.AgentServiceInfo.ServiceName;
});

builder.Services.AddSingleton<IAgentConfigStore, AgentConfigStore>();
builder.Services.AddSingleton<ILocalUiPinAuth, LocalUiPinAuth>();
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
builder.Services.AddSingleton<IEventLogPackageCatalogStore, EventLogPackageCatalogStore>();
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
builder.Services.AddSingleton<EventLogCursorService>();
builder.Services.AddHostedService<HeartbeatProducerWorker>();
builder.Services.AddHostedService<PackageCatalogSyncWorker>();
builder.Services.AddHostedService<EventLogCollectorWorker>();
builder.Services.AddHostedService<ServiceWatchWorker>();
builder.Services.AddHostedService<OutboundShipperWorker>();

// Bind Kestrel to local UI only (field agent UI).
// Prefer DataDirectory/system.json (GPO/MSI writes here) over appsettings defaults.
string uiHost;
int uiPort;
{
    var system = earlySettings.System;
    var dataDirHint = string.IsNullOrWhiteSpace(system.DataDirectory)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MngLogs",
            "Agent")
        : system.DataDirectory.Trim();
    var systemJsonPath = Path.Combine(dataDirHint, "system.json");
    if (File.Exists(systemJsonPath))
    {
        try
        {
            var fromDisk = System.Text.Json.JsonSerializer.Deserialize<SystemConfig>(
                File.ReadAllText(systemJsonPath),
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            if (fromDisk is not null)
                system = fromDisk;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read {Path}; using appsettings system config", systemJsonPath);
        }
    }

    uiHost = string.IsNullOrWhiteSpace(system.LocalUiHost) ? "127.0.0.1" : system.LocalUiHost;
    uiPort = system.LocalUiPort <= 0 ? 5092 : system.LocalUiPort;

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
    "Starting MngLogs Agent service={Service} base={Base}. Local UI http://{Host}:{Port}/ logs={LogDir}",
    MngLogs.Agent.AgentServiceInfo.ServiceName,
    AppContext.BaseDirectory,
    uiHost,
    uiPort,
    logDirectory);

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

static string ResolveLogDirectory(string? configuredDataDirectory)
{
    if (!string.IsNullOrWhiteSpace(configuredDataDirectory))
        return Path.Combine(configuredDataDirectory.Trim(), "logs");

    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    if (string.IsNullOrWhiteSpace(programData))
        programData = Path.Combine(Path.GetTempPath(), "MngLogs");

    return Path.Combine(programData, "MngLogs", "Agent", "logs");
}

public partial class Program;
