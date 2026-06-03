using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MngAlarm.Application.Configuration;
using MngAlarm.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<MngAlarmSettings>(builder.Configuration.GetSection(MngAlarmSettings.SectionName));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "MngAlarm API", Version = "v1" }));

var jwtAuthority = builder.Configuration["Jwt:Authority"];
if (!string.IsNullOrEmpty(jwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = jwtAuthority;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new() { ValidateIssuer = false, ValidateAudience = false };
        });
}

builder.Services.AddAlarmCore(builder.Configuration);

var app = builder.Build();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "MngAlarm API v1"));
}

if (!string.IsNullOrEmpty(jwtAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

Log.Information("Starting MngAlarm API");
app.Run();
