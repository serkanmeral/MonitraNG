using MngSim.Models.TrainSim;

namespace MngSim.Services.TrainSim;

public interface ITrainPositionService
{
    bool IsEnabled { get; }
    /// <summary>Rota listesi (dropdown için).</summary>
    IReadOnlyList<RouteRef> GetAvailableRoutes();
    /// <summary>Kayıtlı tüm trenleri döner.</summary>
    IReadOnlyList<TrainListItem> GetTrains();
    /// <summary>Tren ekler (henüz başlamamış).</summary>
    bool AddTrain(string trainId, string name, string routeId, int durationMinutes);
    /// <summary>Treni listeden kaldırır.</summary>
    bool RemoveTrain(string trainId);
    /// <summary>Trenin yolculuğunu başlatır.</summary>
    bool StartTrain(string trainId);
    TrainsPositionsResponse GetAllPositions(bool includeSensors = false);
    TrainPositionDto? GetPosition(string trainId, bool includeSensors = false);
    void SetSensorOverrides(string trainId, TrainSensorsOverride? overrides);
}
