using MngAlarm.Application.Observations;

namespace MngAlarm.Application.Services;

public interface IObservationIngressPublisher
{
    Task PublishAsync(ObservationEnvelope envelope, CancellationToken cancellationToken = default);
}
