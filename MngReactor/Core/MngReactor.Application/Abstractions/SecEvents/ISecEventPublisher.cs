using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventPublisher
{
    Task PublishCreatedAsync(
        string domain,
        IReadOnlyList<SecEventCreatedMessage> messages,
        CancellationToken cancellationToken = default);
}
