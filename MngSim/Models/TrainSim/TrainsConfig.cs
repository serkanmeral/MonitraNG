namespace MngSim.Models.TrainSim;

/// <summary>Uygulama açılışında yüklenecek tren listesi (trains-config.json).</summary>
public class TrainsConfig
{
    public List<TrainConfigEntry> Trains { get; set; } = new();
}

public class TrainConfigEntry
{
    public string TrainId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RouteId { get; set; } = "";
    public int DurationMinutes { get; set; }
    /// <summary>true ise uygulama açılışında yolculuk otomatik başlatılır.</summary>
    public bool AutoStart { get; set; } = true;
}
