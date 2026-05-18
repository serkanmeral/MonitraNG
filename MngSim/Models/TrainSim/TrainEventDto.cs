namespace MngSim.Models.TrainSim;

/// <summary>Event log ve API yanıtı için.</summary>
public class TrainEventDto
{
    public string TrainId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? Zone { get; set; }
    public string? Severity { get; set; }
    public double? SpeedKmh { get; set; }
    public string? DoorId { get; set; }
    public double? EngineTempC { get; set; }
    public double? VibrationMs2 { get; set; }
}

/// <summary>POST /api/trains/{id}/events gövdesi.</summary>
public class TrainEventPublishRequest
{
    public string EventType { get; set; } = "";
    public string? Zone { get; set; }
    public string? Severity { get; set; }
    public double? SpeedKmh { get; set; }
    public string? DoorId { get; set; }
}
