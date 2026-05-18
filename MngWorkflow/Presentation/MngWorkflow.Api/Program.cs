using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection(MngWorkflowSettings.SectionName).Get<MngWorkflowSettings>()
    ?? new MngWorkflowSettings();
builder.Services.Configure<MngWorkflowSettings>(builder.Configuration.GetSection(MngWorkflowSettings.SectionName));

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("version"),
        new HeaderApiVersionReader("Api-Version"),
        new UrlSegmentApiVersionReader());
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MngWorkflow API", Version = "v1" });
});

// JWT — Gateway'den forward edilen token. Authority yoksa dev modunda auth atlanır.
var jwtAuthority = builder.Configuration["Jwt:Authority"];
if (!string.IsNullOrEmpty(jwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = jwtAuthority;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = false,
                ValidateAudience = false,
            };
        });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();

// Gateway: https://localhost:5040/workflow/swagger — OpenAPI URL göreli olmalı (/workflow/swagger/v1/swagger.json)
var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "MngWorkflow API v1");
        c.DocumentTitle = "MngWorkflow API";
    });
}

// Docker / Gateway arkasında yalnızca HTTP; HTTPS yönlendirmesi container içinde sorun çıkarır.
var useJwt = !string.IsNullOrEmpty(builder.Configuration["Jwt:Authority"]);
if (useJwt)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

Log.Information("Starting MngWorkflow API");
app.Run();
