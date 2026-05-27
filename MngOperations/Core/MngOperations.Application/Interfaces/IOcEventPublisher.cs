using MngOperations.Application.Events;

namespace MngOperations.Application.Interfaces;

public interface IOcEventPublisher
{
    Task PublishWorkItemEventAsync(
        OcWorkItemEvent @event,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false);
}
