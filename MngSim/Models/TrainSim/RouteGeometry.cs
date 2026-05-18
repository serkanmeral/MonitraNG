using System.Text.Json.Serialization;

namespace MngSim.Models.TrainSim;

/// <summary>Rota polyline: [lon, lat] dizisi ve toplam uzunluk (m).</summary>
public class RouteGeometry
{
    [JsonPropertyName("coordinates")]
    public List<List<double>> Coordinates { get; set; } = new();

    [JsonPropertyName("length_m")]
    public double LengthM { get; set; }
}
