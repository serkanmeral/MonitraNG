using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Infrastructure.Services;

/// <summary>PR-1 iskelet — PR-4'te RabbitMQ publish.</summary>
public sealed class SecEventPublisherPlaceholder : ISecEventPublisher
{
    public Task PublishCreatedAsync(
        string domain,
        IReadOnlyList<SecEventCreatedMessage> messages,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
