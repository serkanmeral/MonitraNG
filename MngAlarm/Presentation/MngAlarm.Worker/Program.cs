using MngAlarm.Application.Configuration;
using MngAlarm.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.Configure<MngAlarmSettings>(builder.Configuration.GetSection(MngAlarmSettings.SectionName));
builder.Services.AddAlarmWorker(builder.Configuration);

var host = builder.Build();
Log.Information("Starting MngAlarm Worker");
await host.RunAsync();
