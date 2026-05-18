using MngSim.Components;
using MngSim.Services;
using MngSim.Services.TrainSim;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

// API
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// HttpClient for Blazor (same-origin API çağrıları)
builder.Services.AddScoped(sp =>
{
    var ctx = sp.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
    var scheme = ctx?.Request.Scheme ?? "http";
    var host = ctx?.Request.Host ?? new HostString("localhost", 6060);
    return new HttpClient { BaseAddress = new Uri($"{scheme}://{host}") };
});

// MngSim services
builder.Services.AddSingleton<ISimulatorConfigService, SimulatorConfigService>();
builder.Services.AddSingleton<ITrainPositionService, TrainPositionService>();
builder.Services.AddSingleton<ITrainEventService, TrainEventService>();
builder.Services.AddSingleton<IHostMetricGenerator, HostMetricGenerator>();
builder.Services.AddSingleton<IPduMetricGenerator, PduMetricGenerator>();
builder.Services.AddSingleton<SnmpTemplateRegistry>();
builder.Services.AddSingleton<SnmpRequestHandler>();
builder.Services.AddSingleton<ISimulatorHostService, SimulatorHostService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Sadece HTTP, HTTPS yok — Antiforgery yalnızca Blazor/form sayfaları için; /api tile istekleri 400 vermesin
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase), appBuilder => appBuilder.UseAntiforgery());

// train-map.html'i UseStaticFiles/MapStaticAssets'tan önce wwwroot'tan sun (compressed .gz boş geliyor)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/train-map.html", StringComparison.OrdinalIgnoreCase))
    {
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var path = Path.Combine(env.WebRootPath ?? "wwwroot", "train-map.html");
        if (File.Exists(path))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(path);
            return;
        }
    }
    await next(context);
});

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// GeoServer tile proxy: /api altında, antiforgery atlanır; controller'da api/tiles yok
app.MapGet("/api/tiles/geoserver", async (HttpContext context, IConfiguration config, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("MngSim.TileProxy");
    var layer = context.Request.Query["layer"].FirstOrDefault()?.Trim();
    var zStr = context.Request.Query["z"].FirstOrDefault();
    var xStr = context.Request.Query["x"].FirstOrDefault();
    var yStr = context.Request.Query["y"].FirstOrDefault();
    logger.LogInformation("Tile isteği: layer={Layer}, z={Z}, x={X}, y={Y}", layer, zStr, xStr, yStr);

    var baseUrl = config["TrainSim:GeoServerBaseUrl"]?.Trim();
    if (string.IsNullOrEmpty(baseUrl))
    {
        logger.LogWarning("TrainSim:GeoServerBaseUrl tanımlı değil");
        return Results.NotFound();
    }

    if (string.IsNullOrEmpty(layer)) return Results.BadRequest("layer required");
    var layerName = layer switch
    {
        "railways" => "tr_rail:railways", "stations" => "tr_rail:stations", "places" => "tr_rail:places",
        "roads" => "tr_rail:roads", "waterways" => "tr_rail:waterways", "water_areas" => "tr_rail:water_areas", "landuse" => "tr_rail:landuse",
        _ => null
    };
    if (layerName == null) return Results.BadRequest("layer must be railways, stations, places, roads, waterways, water_areas or landuse");

    if (!int.TryParse(zStr, out var z) || !int.TryParse(xStr, out var x) || !int.TryParse(yStr, out var y))
        return Results.BadRequest("z,x,y integers required");

    // Leaflet (z,x,y) OSM/Web Mercator ile uyumlu: EPSG:900913 veya EPSG:3857 (aynı projeksiyon).
    var tileMatrixSet = config["TrainSim:GeoServerTileMatrixSet"]?.Trim() ?? "EPSG:900913";
    // GeoServer katmanı grid subset ile sınırlıysa Leaflet (x,y) ile GeoServer (tilecol,tilerow) farklı olabilir; ofset ile düzeltin.
    var colOffset = config.GetValue<int>("TrainSim:GeoServerTileColOffset", 0);
    var rowOffset = config.GetValue<int>("TrainSim:GeoServerTileRowOffset", 0);
    var tileCol = x + colOffset;
    var tileRow = y + rowOffset;
    var wmtsUrl = $"{baseUrl.TrimEnd('/')}/geoserver/gwc/service/wmts?request=GetTile&service=WMTS&version=1.0.0&format=image/png&tilematrixset={Uri.EscapeDataString(tileMatrixSet)}&style=&layer={Uri.EscapeDataString(layerName)}&tilematrix={Uri.EscapeDataString(tileMatrixSet)}:{z}&tilerow={tileRow}&tilecol={tileCol}";
    logger.LogInformation("GeoServer WMTS isteği: {Url}", wmtsUrl);

    try
    {
        using var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(wmtsUrl, context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
            logger.LogWarning("GeoServer yanıt hatası: StatusCode={Code}, Body(ilk 500): {Body}",
                (int)response.StatusCode, body.Length > 500 ? body[..500] + "..." : body);
            // TileOutOfRange (400) için şeffaf tile dön; harita kırılmasın, konsol 400 görmesin
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return Results.File(TileProxyStatic.TransparentPngBytes, "image/png");
            return Results.StatusCode((int)response.StatusCode);
        }
        var bytes = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
        logger.LogDebug("Tile döndü: {Count} byte", bytes.Length);
        return Results.File(bytes, "image/png");
    }
    catch (OperationCanceledException)
    {
        // İstemci sayfayı kapattı veya isteği iptal etti; exception sayfası gösterme
        return Results.StatusCode(499);
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();

// 1x1 şeffaf PNG (GeoServer 400 TileOutOfRange için boş tile)
file static class TileProxyStatic
{
    public static readonly byte[] TransparentPngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    };
}
