using System;

namespace MngDataGateway.Application.DTOs.Events
{
    /// <summary>
    /// Data event payload for RabbitMQ publishing
    /// </summary>
    public class DataEventDto
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = string.Empty;
        public string EventVersion { get; set; } = "1.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public EventSourceDto Source { get; set; } = new();
        public EventDomainDto Domain { get; set; } = new();
        public EventDatasetDto Dataset { get; set; } = new();
        public object Data { get; set; } = new();
        public EventActorDto Actor { get; set; } = new();
        public EventMetadataDto Metadata { get; set; } = new();
    }

    public class EventSourceDto
    {
        public string Service { get; set; } = "MngDataGateway";
        public string? Instance { get; set; }
        public string Version { get; set; } = "1.0.0";
    }

    public class EventDomainDto
    {
        public string Name { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class EventDatasetDto
    {
        public string Name { get; set; } = string.Empty;
        public string? CategoryCode { get; set; }
        public string? CollectionName { get; set; }
    }

    public class EventActorDto
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? DomainName { get; set; }
        public string? IpAddress { get; set; }
    }

    public class EventMetadataDto
    {
        public string? CorrelationId { get; set; }
        public string? TraceId { get; set; }
    }
}

