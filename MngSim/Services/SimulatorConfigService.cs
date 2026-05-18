using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Konfigürasyonu dosyada saklar; uygulama başlarken dosyadan yükler, her güncellemede dosyaya yazar.
/// Docker yeniden başlasa bile volume sayesinde cihaz tanımları korunur.
/// </summary>
public class SimulatorConfigService : ISimulatorConfigService
{
    private SimulatorConfig? _config;
    private readonly object _lock = new();
    private readonly string _configFilePath;
    private readonly ILogger<SimulatorConfigService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SimulatorConfigService(IConfiguration configuration, ILogger<SimulatorConfigService> logger)
    {
        _logger = logger;
        _configFilePath = configuration["MngSim:ConfigFilePath"] ?? "mngsim-config.json";
        LoadFromFile();
    }

    public SimulatorConfig? GetConfig()
    {
        lock (_lock)
        {
            return _config;
        }
    }

    public void SetConfig(SimulatorConfig config)
    {
        lock (_lock)
        {
            _config = config ?? new SimulatorConfig();
            SaveToFile();
        }
    }

    public bool HasValidConfig()
    {
        var c = GetConfig();
        return c != null && c.Devices.Count > 0
            && c.Devices.All(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Protocol));
    }

    private void LoadFromFile()
    {
        if (!File.Exists(_configFilePath))
        {
            _logger.LogInformation("Konfig dosyası yok, varsayılan (boş) config kullanılıyor: {Path}", _configFilePath);
            return;
        }
        try
        {
            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<SimulatorConfig>(json, JsonOptions);
            if (config != null)
            {
                _config = config;
                _logger.LogInformation("Konfig yüklendi: {Path}, {Count} cihaz", _configFilePath, config.Devices.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Konfig dosyası okunamadı, boş config kullanılıyor: {Path}", _configFilePath);
        }
    }

    private void SaveToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(_configFilePath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(_configFilePath, json);
            _logger.LogDebug("Konfig kaydedildi: {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Konfig dosyasına yazılamadı: {Path}", _configFilePath);
        }
    }
}
