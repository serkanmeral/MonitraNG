using MngLogs.Agent.Configuration;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.Transport;
using MngLogs.Agent.Workers;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.Metrics;
using Serilog;

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
builder.Services.AddHostedService<EventLogCollectorWorker>();
builder.Services.AddHostedService<ServiceWatchWorker>();
builder.Services.AddHostedService<OutboundShipperWorker>();

// Bind Kestrel to local UI only (field agent UI).
{
    var early = builder.Configuration.GetSection(MngLogsAgentSettings.SectionName).Get<MngLogsAgentSettings>()
                ?? new MngLogsAgentSettings();
    var host = string.IsNullOrWhiteSpace(early.System.LocalUiHost) ? "127.0.0.1" : early.System.LocalUiHost;
    var port = early.System.LocalUiPort <= 0 ? 5092 : early.System.LocalUiPort;
    builder.WebHost.UseUrls($"http://{host}:{port}");
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapLocalUi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mnglogs-agent" }));
app.MapFallbackToFile("index.html");

Log.Information(
    "Starting MngLogs Agent (Windows Service capable). Local UI on configured loopback port. Field install via MSI later.");
app.Run();

public partial class Program;
