namespace MngSim.Models.TrainSim;

public class TrainPositionDto
{
    public string TrainId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; }
    /// <summary>Polling sensörleri (includeSensors=true ise dolu).</summary>
    public TrainSensorsDto? Sensors { get; set; }
}

/// <summary>Lokomotif polling sensör değerleri (REST ile periyodik okunur).</summary>
public class TrainSensorsDto
{
    public double EngineTempC { get; set; }
    public double OilPressureBar { get; set; }
    public double CoolantTempC { get; set; }
    public double BatteryVoltageV { get; set; }
    public double BrakePipePressureBar { get; set; }
    public double CabTempC { get; set; }
    public double VibrationMs2 { get; set; }
    public bool DoorClosed { get; set; }
}

public class TrainsPositionsResponse
{
    public DateTime UpdatedAt { get; set; }
    public List<TrainPositionDto> Positions { get; set; } = new();
}
