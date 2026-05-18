namespace MngSim.Models.TrainSim;

/// <summary>UI veya test için sensör değeri override; sadece dolu alanlar uygulanır.</summary>
public class TrainSensorsOverride
{
    public double? EngineTempC { get; set; }
    public double? OilPressureBar { get; set; }
    public double? CoolantTempC { get; set; }
    public double? BatteryVoltageV { get; set; }
    public double? BrakePipePressureBar { get; set; }
    public double? CabTempC { get; set; }
    public double? VibrationMs2 { get; set; }
    public bool? DoorClosed { get; set; }
}
