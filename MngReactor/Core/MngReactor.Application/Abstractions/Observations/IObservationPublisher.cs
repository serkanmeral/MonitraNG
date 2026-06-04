using MngReactor.Application.Observations;

namespace MngReactor.Application.Abstractions.Observations;

/// <summary>
/// Publishes flat metric observations to <c>monitra.observations</c> for MngAlarm consumption.
/// </summary>
public interface IObservationPublisher
{
    Task PublishAsync(
        string domainId,
        string domainName,
        string collectibleCode,
        double value,
        IReadOnlyDictionary<string, string?>? dimensions = null,
        DateTime? timestamp = null,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes SIEM sec_event as kind=event observation for MngAlarm correlation rules.</summary>
    Task PublishSecEventAsync(
        SecEventObservationPayload payload,
        CancellationToken cancellationToken = default);
}
