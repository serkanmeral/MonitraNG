using System.Text.Json;
using MngSim.Models.TrainSim;

namespace MngSim.Services.TrainSim;

public class TrainPositionService : ITrainPositionService
{
    private readonly string _basePath;
    private readonly ILogger<TrainPositionService> _logger;
    private RoutesReference? _routesRef;
    private readonly Dictionary<string, RouteGeometry> _geometriesByForwardId = new();
    private readonly Dictionary<string, (string Name, string RouteId, int DurationMinutes, DateTime? StartUtc)> _trains = new();
    private readonly Dictionary<string, TrainSensorsOverride> _sensorOverrides = new();
    private readonly object _lock = new();
    private bool _initialized;

    public bool IsEnabled { get; private set; }

    public TrainPositionService(IWebHostEnvironment env, IConfiguration config, ILogger<TrainPositionService> logger)
    {
        _logger = logger;
        var trainSimBase = config["TrainSim:DataPath"]?.Trim();
        if (!string.IsNullOrEmpty(trainSimBase))
            _basePath = Path.IsPathRooted(trainSimBase) ? trainSimBase : Path.Combine(env.ContentRootPath, trainSimBase);
        else
            _basePath = ResolveDataPath(env.ContentRootPath);
        _logger.LogInformation(
            "Tren simülasyonu veri yolu: BasePath={BasePath}, ContentRoot={ContentRoot}, routes-reference var mı={Exists}",
            _basePath,
            env.ContentRootPath,
            File.Exists(Path.Combine(_basePath, "routes-reference.json")));
    }

    private static string ResolveDataPath(string contentRootPath)
    {
        var primary = Path.Combine(contentRootPath, "Data", "TrainSim");
        if (File.Exists(Path.Combine(primary, "routes-reference.json")))
            return primary;
        var fallback = Path.Combine(AppContext.BaseDirectory, "Data", "TrainSim");
        return File.Exists(Path.Combine(fallback, "routes-reference.json")) ? fallback : primary;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                var refPath = Path.Combine(_basePath, "routes-reference.json");
                var refExists = File.Exists(refPath);
                var geomDir = Path.Combine(_basePath, "route-geometries");
                _logger.LogInformation("Tren simülasyonu init: refPath={RefPath}, File.Exists={Exists}, Directory.Exists(BasePath)={DirExists}",
                    refPath, refExists, Directory.Exists(_basePath));
                if (Directory.Exists(_basePath))
                {
                    var files = string.Join(", ", Directory.GetFiles(_basePath).Select(Path.GetFileName));
                    _logger.LogInformation("Tren simülasyonu BasePath içeriği: {Files}", files);
                    if (Directory.Exists(geomDir))
                    {
                        var geomFiles = string.Join(", ", Directory.GetFiles(geomDir).Select(Path.GetFileName));
                        _logger.LogInformation("Tren simülasyonu route-geometries içeriği: {Files}", geomFiles);
                    }
                    else
                        _logger.LogWarning("Tren simülasyonu: route-geometries klasörü yok: {Path}", geomDir);
                }
                if (!refExists)
                {
                    _logger.LogWarning("Tren simülasyonu: routes-reference.json bulunamadı, devre dışı. Beklenen: {Path}", refPath);
                    _initialized = true;
                    return;
                }
                var refJson = File.ReadAllText(refPath);
                _routesRef = JsonSerializer.Deserialize<RoutesReference>(refJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (_routesRef?.Stations == null || _routesRef.Routes == null)
                {
                    _logger.LogWarning("Tren simülasyonu: routes-reference.json geçersiz.");
                    _initialized = true;
                    return;
                }
                _logger.LogInformation("Tren simülasyonu: routes-reference yüklendi, Stations={Stations}, Routes={Routes}",
                    _routesRef.Stations.Count, _routesRef.Routes.Count);
                foreach (var route in _routesRef.Routes)
                {
                    string geomFileId = GetGeometryFileId(route.Id);
                    var geomPath = Path.Combine(geomDir, geomFileId + ".json");
                    _logger.LogInformation("Tren simülasyonu: deniyor routeId={RouteId}, geomFileId={GeomFileId}, path={Path}, exists={Exists}", route.Id, geomFileId, geomPath, File.Exists(geomPath));
                    if (!File.Exists(geomPath)) continue;
                    RouteGeometry? geom;
                    try
                    {
                        geom = LoadRouteGeometryFromFile(geomPath, _logger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Tren simülasyonu: {File} okunamadı.", geomPath);
                        continue;
                    }
                    if (geom?.Coordinates == null || geom.Coordinates.Count < 2)
                    {
                        _logger.LogWarning("Tren simülasyonu: {File} geçersiz (geom null={Null}, coords count={Count}).", geomPath, geom == null, geom?.Coordinates?.Count ?? 0);
                        continue;
                    }
                    if (!_geometriesByForwardId.ContainsKey(geomFileId))
                        _geometriesByForwardId[geomFileId] = geom;
                    _logger.LogInformation("Tren simülasyonu: {File} yüklendi, {Count} nokta.", geomPath, geom.Coordinates.Count);
                }
                if (_geometriesByForwardId.Count == 0)
                {
                    _logger.LogWarning("Tren simülasyonu: Hiç rota geometrisi yüklenemedi.");
                    _initialized = true;
                    return;
                }
                IsEnabled = true;
                _initialized = true;
                _logger.LogInformation("Tren simülasyonu yüklendi: {Count} rota.", _geometriesByForwardId.Count);
                LoadTrainsFromConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tren simülasyonu başlatılamadı.");
                _initialized = true;
            }
        }
    }

    private static RouteGeometry? LoadRouteGeometryFromFile(string path, ILogger logger)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        if (!root.TryGetProperty("coordinates", out var coordsEl))
        {
            logger.LogWarning("Tren simülasyonu: {Path} içinde 'coordinates' yok.", path);
            return null;
        }
        if (coordsEl.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("Tren simülasyonu: {Path} 'coordinates' dizi değil (kind={Kind}).", path, coordsEl.ValueKind);
            return null;
        }
        var coordinates = new List<List<double>>();
        var idx = 0;
        foreach (var point in coordsEl.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
            {
                if (idx < 3) logger.LogWarning("Tren simülasyonu: {Path} nokta[{I}] atlandı (kind={Kind}, len={Len}).", path, idx, point.ValueKind, point.ValueKind == JsonValueKind.Array ? point.GetArrayLength() : -1);
                idx++;
                continue;
            }
            var lon = point[0].GetDouble();
            var lat = point[1].GetDouble();
            coordinates.Add(new List<double> { lon, lat });
            idx++;
        }
        logger.LogInformation("Tren simülasyonu: {Path} {Count} nokta parse edildi.", path, coordinates.Count);
        var lengthM = root.TryGetProperty("length_m", out var lenEl) ? lenEl.GetDouble() : 0;
        return new RouteGeometry { Coordinates = coordinates, LengthM = lengthM };
    }

    private static string GetGeometryFileId(string routeId) => routeId switch
    {
        "ANK-IST" or "IST-ANK" => "ANK-IST",
        "ANK-KON" or "KON-ANK" => "ANK-KON",
        _ => routeId
    };

    private void LoadTrainsFromConfig()
    {
        var configPath = Path.Combine(_basePath, "trains-config.json");
        if (!File.Exists(configPath))
        {
            _logger.LogInformation("Tren simülasyonu: trains-config.json yok, varsayılan tren yüklenmeyecek.");
            return;
        }
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TrainsConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (config?.Trains == null || config.Trains.Count == 0) return;

            int added = 0, started = 0;
            lock (_lock)
            {
                foreach (var t in config.Trains)
                {
                    if (string.IsNullOrWhiteSpace(t.TrainId)) continue;
                    var id = t.TrainId.Trim();
                    if (_trains.ContainsKey(id)) continue;
                    if (!_routesRef!.Routes.Any(r => r.Id == t.RouteId)) continue;
                    if (t.DurationMinutes < 1) continue;

                    _trains[id] = (t.Name ?? id, t.RouteId, t.DurationMinutes, null);
                    added++;
                    if (t.AutoStart)
                    {
                        _trains[id] = (t.Name ?? id, t.RouteId, t.DurationMinutes, DateTime.UtcNow);
                        started++;
                    }
                }
            }
            _logger.LogInformation("Tren simülasyonu: trains-config.json yüklendi, {Added} tren eklendi, {Started} otomatik başlatıldı.", added, started);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tren simülasyonu: trains-config.json okunamadı.");
        }
    }

    public IReadOnlyList<RouteRef> GetAvailableRoutes()
    {
        EnsureInitialized();
        if (!IsEnabled || _routesRef == null) return Array.Empty<RouteRef>();
        return _routesRef.Routes;
    }

    public IReadOnlyList<TrainListItem> GetTrains()
    {
        EnsureInitialized();
        lock (_lock)
        {
            return _trains.Select(kv => new TrainListItem
            {
                TrainId = kv.Key,
                Name = kv.Value.Name,
                RouteId = kv.Value.RouteId,
                DurationMinutes = kv.Value.DurationMinutes,
                Started = kv.Value.StartUtc.HasValue,
                StartUtc = kv.Value.StartUtc
            }).ToList();
        }
    }

    public bool AddTrain(string trainId, string name, string routeId, int durationMinutes)
    {
        EnsureInitialized();
        if (!IsEnabled || _routesRef == null) return false;
        var id = (trainId ?? "").Trim();
        if (string.IsNullOrEmpty(id)) return false;
        if (durationMinutes < 1) return false;
        var route = _routesRef.Routes.FirstOrDefault(r => r.Id == routeId);
        if (route == null) return false;
        lock (_lock)
        {
            if (_trains.ContainsKey(id)) return false;
            _trains[id] = (name ?? id, routeId, durationMinutes, null);
            return true;
        }
    }

    public bool RemoveTrain(string trainId)
    {
        EnsureInitialized();
        lock (_lock)
        {
            _sensorOverrides.Remove(trainId ?? "");
            return _trains.Remove(trainId ?? "");
        }
    }

    public bool StartTrain(string trainId)
    {
        EnsureInitialized();
        lock (_lock)
        {
            if (!_trains.TryGetValue(trainId ?? "", out var t)) return false;
            if (t.StartUtc.HasValue) return true;
            _trains[trainId!] = (t.Name, t.RouteId, t.DurationMinutes, DateTime.UtcNow);
            return true;
        }
    }

    public TrainsPositionsResponse GetAllPositions(bool includeSensors = false)
    {
        EnsureInitialized();
        var response = new TrainsPositionsResponse { UpdatedAt = DateTime.UtcNow };
        if (!IsEnabled || _routesRef == null) return response;
        lock (_lock)
        {
            foreach (var (trainId, t) in _trains)
            {
                if (!t.StartUtc.HasValue) continue;
                var (pos, isMoving, speedKmh) = ComputePositionInternal(trainId, t.Name, t.RouteId, t.DurationMinutes, t.StartUtc.Value);
                if (pos != null)
                {
                    if (includeSensors)
                        pos.Sensors = BuildSensors(trainId, speedKmh, pos.Heading ?? 0, isMoving);
                    response.Positions.Add(pos);
                }
            }
        }
        return response;
    }

    public TrainPositionDto? GetPosition(string trainId, bool includeSensors = false)
    {
        EnsureInitialized();
        if (!IsEnabled || _routesRef == null) return null;
        lock (_lock)
        {
            if (!_trains.TryGetValue(trainId ?? "", out var t) || !t.StartUtc.HasValue) return null;
            var (pos, isMoving, speedKmh) = ComputePositionInternal(trainId!, t.Name, t.RouteId, t.DurationMinutes, t.StartUtc.Value);
            if (pos != null && includeSensors)
                pos.Sensors = BuildSensors(trainId!, speedKmh, pos.Heading ?? 0, isMoving);
            return pos;
        }
    }

    public void SetSensorOverrides(string trainId, TrainSensorsOverride? overrides)
    {
        if (string.IsNullOrEmpty(trainId)) return;
        lock (_lock)
        {
            if (overrides == null)
                _sensorOverrides.Remove(trainId);
            else
                _sensorOverrides[trainId] = overrides;
        }
    }

    private (TrainPositionDto? dto, bool isMoving, double speedKmh) ComputePositionInternal(string trainId, string name, string routeId, int durationMinutes, DateTime startUtc)
    {
        if (_routesRef == null) return (null, false, 0);
        var route = _routesRef.Routes.FirstOrDefault(r => r.Id == routeId);
        if (route == null) return (null, false, 0);
        var fromSt = _routesRef.Stations.FirstOrDefault(s => s.Id == route.FromStationId);
        var toSt = _routesRef.Stations.FirstOrDefault(s => s.Id == route.ToStationId);
        if (fromSt == null || toSt == null) return (null, false, 0);

        double waitSec = route.WaitAtEndMinutes * 60.0;
        double moveSec = durationMinutes * 60.0;
        double cycleSec = moveSec * 2 + waitSec * 2;
        double elapsed = (DateTime.UtcNow - startUtc).TotalSeconds;
        double tInCycle = elapsed % cycleSec;
        if (tInCycle < 0) tInCycle += cycleSec;

        double progress;
        double lat, lon;
        bool isMoving;
        double speedKmh;
        double headingDeg;
        double distKm = HaversineKm(fromSt.Lat, fromSt.Lon, toSt.Lat, toSt.Lon);
        double nominalSpeedKmh = moveSec > 0 ? distKm / (durationMinutes / 60.0) : 0;

        if (tInCycle < moveSec)
        {
            progress = Math.Min(1.0, tInCycle / moveSec);
            (lat, lon) = InterpolateStraightLine(fromSt.Lat, fromSt.Lon, toSt.Lat, toSt.Lon, progress);
            isMoving = true;
            speedKmh = nominalSpeedKmh;
            headingDeg = BearingDeg(fromSt.Lat, fromSt.Lon, toSt.Lat, toSt.Lon);
        }
        else if (tInCycle < moveSec + waitSec)
        {
            lat = toSt.Lat;
            lon = toSt.Lon;
            isMoving = false;
            speedKmh = 0;
            headingDeg = BearingDeg(fromSt.Lat, fromSt.Lon, toSt.Lat, toSt.Lon);
        }
        else if (tInCycle < moveSec * 2 + waitSec)
        {
            progress = Math.Max(0.0, 1.0 - (tInCycle - moveSec - waitSec) / moveSec);
            (lat, lon) = InterpolateStraightLine(toSt.Lat, toSt.Lon, fromSt.Lat, fromSt.Lon, 1.0 - progress);
            isMoving = true;
            speedKmh = nominalSpeedKmh;
            headingDeg = (BearingDeg(toSt.Lat, toSt.Lon, fromSt.Lat, fromSt.Lon) + 360) % 360;
        }
        else
        {
            lat = fromSt.Lat;
            lon = fromSt.Lon;
            isMoving = false;
            speedKmh = 0;
            headingDeg = (BearingDeg(toSt.Lat, toSt.Lon, fromSt.Lat, fromSt.Lon) + 360) % 360;
        }

        var dto = new TrainPositionDto
        {
            TrainId = trainId,
            RouteId = routeId,
            Lat = lat,
            Lon = lon,
            Speed = isMoving ? Math.Round(speedKmh, 1) : 0,
            Heading = Math.Round(headingDeg, 1),
            Timestamp = DateTime.UtcNow
        };
        return (dto, isMoving, speedKmh);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // km
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(dLon) * Math.Cos(lat2 * Math.PI / 180);
        var x = Math.Cos(lat1 * Math.PI / 180) * Math.Sin(lat2 * Math.PI / 180) -
                Math.Sin(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Cos(dLon);
        var deg = Math.Atan2(y, x) * 180 / Math.PI;
        return (deg + 360) % 360;
    }

    private TrainSensorsDto BuildSensors(string trainId, double speedKmh, double headingDeg, bool isMoving)
    {
        uint seed = (uint)(trainId.GetHashCode() & 0x7FFFFFFF);
        double t = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        double noise(double min, double max) => min + (double)((seed + (uint)(t / 10)) % 1000) / 1000.0 * (max - min);
        double jitter(double baseVal, double range) => baseVal + (noise(0, 1) - 0.5) * 2 * range;

        // Hız ve hareket durumuna göre anlamlı değerler
        double engineBase = isMoving ? 78 + Math.Min(14, speedKmh / 20) : 72;
        double engineTempC = jitter(engineBase, 2.5);
        double oilBar = isMoving ? 4.0 + Math.Min(1.0, speedKmh / 200) : 3.8;
        double oilPressureBar = jitter(oilBar, 0.3);
        double coolantC = engineTempC - 2 + noise(-1, 2);
        double batteryV = jitter(24.0, 0.4);
        double brakeBar = isMoving ? 5.0 : 4.9;
        double brakePipePressureBar = jitter(brakeBar, 0.15);
        double cabTempC = jitter(22.0, 1.5);
        double vibBase = isMoving ? 0.04 + Math.Min(0.08, speedKmh / 400) : 0.02;
        double vibrationMs2 = jitter(vibBase, 0.01);
        bool doorClosed = true;

        var dto = new TrainSensorsDto
        {
            EngineTempC = Math.Round(engineTempC, 1),
            OilPressureBar = Math.Round(oilPressureBar, 2),
            CoolantTempC = Math.Round(coolantC, 1),
            BatteryVoltageV = Math.Round(batteryV, 2),
            BrakePipePressureBar = Math.Round(brakePipePressureBar, 2),
            CabTempC = Math.Round(cabTempC, 1),
            VibrationMs2 = Math.Round(vibrationMs2, 3),
            DoorClosed = doorClosed
        };

        lock (_lock)
        {
            if (_sensorOverrides.TryGetValue(trainId, out var ov))
            {
                if (ov.EngineTempC.HasValue) dto.EngineTempC = ov.EngineTempC.Value;
                if (ov.OilPressureBar.HasValue) dto.OilPressureBar = ov.OilPressureBar.Value;
                if (ov.CoolantTempC.HasValue) dto.CoolantTempC = ov.CoolantTempC.Value;
                if (ov.BatteryVoltageV.HasValue) dto.BatteryVoltageV = ov.BatteryVoltageV.Value;
                if (ov.BrakePipePressureBar.HasValue) dto.BrakePipePressureBar = ov.BrakePipePressureBar.Value;
                if (ov.CabTempC.HasValue) dto.CabTempC = ov.CabTempC.Value;
                if (ov.VibrationMs2.HasValue) dto.VibrationMs2 = ov.VibrationMs2.Value;
                if (ov.DoorClosed.HasValue) dto.DoorClosed = ov.DoorClosed.Value;
            }
        }
        return dto;
    }

    private static (double lat, double lon) InterpolateStraightLine(double latFrom, double lonFrom, double latTo, double lonTo, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        return (
            latFrom + progress * (latTo - latFrom),
            lonFrom + progress * (lonTo - lonFrom)
        );
    }
}
