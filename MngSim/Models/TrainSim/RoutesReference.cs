namespace MngSim.Models.TrainSim;

public class RoutesReference
{
    public List<StationRef> Stations { get; set; } = new();
    public List<RouteRef> Routes { get; set; } = new();
}

public class StationRef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Lon { get; set; }
    public double Lat { get; set; }
}

public class RouteRef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FromStationId { get; set; } = "";
    public string ToStationId { get; set; } = "";
    public int DurationMinutes { get; set; }
    public int WaitAtEndMinutes { get; set; }
}
