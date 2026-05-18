using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.DTOs.Events;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Notification Service implementation with error logging to MongoDB
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IRabbitMqService _rabbitMqService;
        private readonly IMongoClient _mongoClient;

        public NotificationService(
            ILogger<NotificationService> logger,
            IRabbitMqService rabbitMqService,
            IMongoClient mongoClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        }

        public async Task PublishDataCreatedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            object data,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var eventPayload = BuildEventPayload(
                eventType: "dataset.data.created",
                domainName: domainName,
                databaseName: databaseName,
                schema: schema,
                data: data,
                userId: userId,
                userEmail: userEmail,
                ipAddress: ipAddress);

            await PublishEventAsync(domainName, schema.DatasetName, "created", eventPayload);
        }

        public async Task PublishDataUpdatedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            object data,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var eventPayload = BuildEventPayload(
                eventType: "dataset.data.updated",
                domainName: domainName,
                databaseName: databaseName,
                schema: schema,
                data: data,
                userId: userId,
                userEmail: userEmail,
                ipAddress: ipAddress);

            await PublishEventAsync(domainName, schema.DatasetName, "updated", eventPayload);
        }

        public async Task PublishDataDeletedEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var data = new { __dataId = dataId };

            var eventPayload = BuildEventPayload(
                eventType: "dataset.data.deleted",
                domainName: domainName,
                databaseName: databaseName,
                schema: schema,
                data: data,
                userId: userId,
                userEmail: userEmail,
                ipAddress: ipAddress);

            await PublishEventAsync(domainName, schema.DatasetName, "deleted", eventPayload);
        }

        public async Task PublishDataRestoredEventAsync(
            string domainName,
            string databaseName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var data = new { __dataId = dataId };

            var eventPayload = BuildEventPayload(
                eventType: "dataset.data.restored",
                domainName: domainName,
                databaseName: databaseName,
                schema: schema,
                data: data,
                userId: userId,
                userEmail: userEmail,
                ipAddress: ipAddress);

            await PublishEventAsync(domainName, schema.DatasetName, "restored", eventPayload);
        }

        private static readonly HashSet<string> MonitoringSyncDatasets = new(StringComparer.OrdinalIgnoreCase)
            { "mon_engines", "mon_agents", "mon_assets" };

        private async Task PublishEventAsync(string domainName, string datasetName, string operation, DataEventDto eventPayload)
        {
            // Fire & Forget - çalıştır ama hata user'ı etkilemesin
            _ = Task.Run(async () =>
            {
                try
                {
                    var routingKey = $"dataset.{datasetName}.{operation}";
                    await _rabbitMqService.PublishDataEventAsync(domainName, routingKey, eventPayload);

                    if (MonitoringSyncDatasets.Contains(datasetName))
                    {
                        await _rabbitMqService.PublishMonitoringSyncEventAsync(domainName, datasetName, operation, eventPayload);
                    }

                    _logger.LogInformation(
                        "Event published successfully: {EventType} for dataset {DatasetName}",
                        eventPayload.EventType, datasetName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to publish event: {EventType} for dataset {DatasetName}",
                        eventPayload.EventType, datasetName);

                    // Log to MongoDB @notification_errors collection
                    await LogNotificationErrorAsync(domainName, datasetName, operation, eventPayload, ex);
                }
            });

            await Task.CompletedTask;
        }

        private DataEventDto BuildEventPayload(
            string eventType,
            string domainName,
            string databaseName,
            DatasetSchema schema,
            object data,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            return new DataEventDto
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                EventVersion = "1.0",
                Timestamp = DateTime.UtcNow,
                
                Source = new EventSourceDto
                {
                    Service = "MngDataGateway",
                    Instance = Environment.MachineName,
                    Version = "1.0.0"
                },
                
                Domain = new EventDomainDto
                {
                    Name = domainName,
                    DatabaseName = databaseName
                },
                
                Dataset = new EventDatasetDto
                {
                    Name = schema.DatasetName,
                    CategoryCode = schema.DatasetCategoryCode,
                    CollectionName = schema.CollectionName
                },
                
                Data = data,
                
                Actor = new EventActorDto
                {
                    UserId = userId,
                    Email = userEmail,
                    DomainName = domainName,
                    IpAddress = ipAddress
                },
                
                Metadata = new EventMetadataDto
                {
                    CorrelationId = Guid.NewGuid().ToString()
                }
            };
        }

        private async Task LogNotificationErrorAsync(
            string domainName,
            string datasetName,
            string operation,
            DataEventDto eventPayload,
            Exception ex)
        {
            try
            {
                var database = _mongoClient.GetDatabase("monitra_system");
                var collection = database.GetCollection<BsonDocument>("@notification_errors");

                var errorDocument = new BsonDocument
                {
                    { "domainName", domainName },
                    { "datasetName", datasetName },
                    { "operation", operation },
                    { "eventId", eventPayload.EventId },
                    { "eventType", eventPayload.EventType },
                    { "exchange", $"monitra.data.events.{domainName.ToLowerInvariant()}" },
                    { "routingKey", $"dataset.{datasetName}.{operation}" },
                    { "error", ex.Message },
                    { "stackTrace", ex.StackTrace ?? string.Empty },
                    { "timestamp", DateTime.UtcNow },
                    { "payload", BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(eventPayload)) }
                };

                await collection.InsertOneAsync(errorDocument);

                _logger.LogInformation(
                    "Notification error logged to @notification_errors for event {EventId}",
                    eventPayload.EventId);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx,
                    "Failed to log notification error to MongoDB - Original error: {OriginalError}",
                    ex.Message);
            }
        }
    }
}

