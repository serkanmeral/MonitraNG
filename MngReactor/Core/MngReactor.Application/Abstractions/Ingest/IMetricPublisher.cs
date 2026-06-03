namespace MngReactor.Application.Abstractions.Ingest;

/// <summary>
/// RabbitMQ metrik publish için
/// </summary>
public interface IMetricPublisher
{
    Task PublishAsync(object metricDocument, string domain, CancellationToken cancellationToken = default);
}
