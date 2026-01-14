using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// RabbitMQ event publisher interface for job execution events
/// </summary>
public interface IRabbitMqEventPublisher
{
    /// <summary>
    /// Publish job execution completed event
    /// </summary>
    Task PublishJobExecutionCompletedAsync(JobExecution execution, ScheduledJob job);
}
