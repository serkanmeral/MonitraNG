using System.Net.Sockets;
using System.Text.Json;
using MngLogs.Agent.Cli;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Linux.Cli;
using MngLogs.Agent.Linux.Journal;
using MngLogs.Agent.Linux.LocalUi;
using MngLogs.Agent.Linux.Metrics;
using MngLogs.Agent.Linux.Workers;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Metrics;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.Transport;
using MngLogs.Agent.Workers;
using Serilog;

if (LinuxAgentCli.IsCliInvocation(args))
    return await LinuxAgentCli.RunAsync(args);

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MngLogsAgentSettings>(
    builder.Configuration.GetSection(MngLogsAgentSettings.SectionName));

var earlySettings = builder.Configuration.GetSection(MngLogsAgentSettings.SectionName).Get<MngLogsAgentSettings>()
                    ?? new MngLogsAgentSettings();

if (OperatingSystem.IsLinux())
{
    if (string.IsNullOrWhiteSpace(earlySettings.System.DataDirectory))
        earlySettings.System.DataDirectory = PlatformPaths.LinuxDataDirectory;
    if (string.IsNullOrWhiteSpace(earlySettings.System.ConfigDirectory))
        earlySettings.System.ConfigDirectory = PlatformPaths.LinuxConfigDirectory;
}

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
builder.Host.UseSystemd();

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
builder.Services.AddSingleton<IHostMetricsCollector, LinuxHostMetricsCollector>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IAgentConfigStore>();
    return new JournalBookmarkStore(config.ResolveDataDirectory());
});
builder.Services.AddSingleton<JournalctlReader>();
builder.Services.AddHostedService<HeartbeatProducerWorker>();
builder.Services.AddHostedService<LinuxServiceWatchWorker>();
builder.Services.AddHostedService<LinuxJournalCollectorWorker>();
builder.Services.AddHostedService<OutboundShipperWorker>();

string uiHost;
int uiPort;
{
    var system = earlySettings.System;
    var configDirHint = string.IsNullOrWhiteSpace(system.ConfigDirectory)
        ? PlatformPaths.DefaultConfigDirectory(
            string.IsNullOrWhiteSpace(system.DataDirectory)
                ? PlatformPaths.DefaultDataDirectory()
                : system.DataDirectory.Trim())
        : system.ConfigDirectory.Trim();
    var systemJsonPath = Path.Combine(configDirHint, "system.json");
    if (File.Exists(systemJsonPath))
    {
        try
        {
            var fromDisk = JsonSerializer.Deserialize<SystemConfig>(
                File.ReadAllText(systemJsonPath),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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
            "Local UI port {Port} on {Host} is unavailable ({Detail}). {Hint}",
            uiPort,
            uiHost,
            probeDetail ?? "address in use",
            hint ?? "");
        Console.Error.WriteLine($"Local UI açılamıyor: http://{uiHost}:{uiPort}/");
        Console.Error.WriteLine("  MngLogs.Agent port set <yeniPort>");
        return 3;
    }

    builder.WebHost.UseUrls($"http://{uiHost}:{uiPort}");
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapLinuxLocalUi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mnglogs-agent", platform = "linux" }));
app.MapFallbackToFile("index.html");

Log.Information(
    "Starting MngLogs Agent (Linux) base={Base}. Local UI http://{Host}:{Port}/ logs={LogDir}",
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
    Log.Fatal(ex, "Failed to bind Local UI");
    return 3;
}

static string ResolveLogDirectory(string? configuredDataDirectory)
{
    if (!string.IsNullOrWhiteSpace(configuredDataDirectory))
        return Path.Combine(configuredDataDirectory.Trim(), "logs");

    return Path.Combine(PlatformPaths.DefaultDataDirectory(), "logs");
}

public partial class Program;
