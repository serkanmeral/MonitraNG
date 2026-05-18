namespace MngSim.Models.TrainSim;

public class TrainListItem
{
    public string TrainId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RouteId { get; set; } = "";
    public int DurationMinutes { get; set; }
    public bool Started { get; set; }
    public DateTime? StartUtc { get; set; }
}
