using Serilog;
using System.Reflection;
using MngEngine.Api.Extentions;
using MngEngine.Infrastructure;
using MngEngine.Persistence;
using MngEngine.Application;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Interfaces;
using System.Text.Json;
using MngEngine.Persistence.Service.HostedService;
using MngEngine.Persistence.Service.Init;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var log = builder.InitSerilog();

log.Information($"MonitraNG_Engine.Api Starting. Version {Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion} ");

builder.InitSwagger();
builder.InitWebAPP();


builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddPersistenceServices(builder.Configuration);


var app = builder.Build();

app.UseApplicationSettings();

await app.Services.GetService<IInitApplicationService>().InitApplication();

app.Run();