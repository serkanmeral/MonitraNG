using MngSim.Models.TrainSim;

namespace MngSim.Services.TrainSim;

public interface ITrainEventService
{
    bool IsConnected { get; }
    Task PublishAsync(string trainId, TrainEventDto payload, CancellationToken ct = default);
    IReadOnlyList<TrainEventDto> GetRecentEvents(int maxCount = 100);
}
