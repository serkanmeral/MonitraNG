using MngWorkflow.Application.Configuration;
using MngWorkflow.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Services.Configure<MngWorkflowSettings>(builder.Configuration.GetSection(MngWorkflowSettings.SectionName));
builder.Services.AddWorkflowWorker(builder.Configuration);

var host = builder.Build();

Log.Information("Starting MngWorkflow Worker");
await host.RunAsync();
