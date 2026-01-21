using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.DTOs.Validation;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Exceptions;
using MngDataGateway.Persistence.Extensions;
using MngDataGateway.Persistence.Services;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Main data service orchestrator
    /// Coordinates validation, processing, storage, and notification
    /// </summary>
    public class DataService : IDataService
    {
        private readonly ILogger<DataService> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IDatasetService _datasetService;
        private readonly IValidationService _validationService;
        private readonly IDataProcessService _dataProcessService;
        private readonly IDataRepository _dataRepository;
        private readonly INotificationService _notificationService;
        private readonly MngDataGateway.Application.Interfaces.IEventPublisher? _eventPublisher;
        private readonly IConfiguration _configuration;
        private readonly FilterParser _filterParser;
        private readonly SortParser _sortParser;

        public DataService(
            ILogger<DataService> logger,
            ILoggerFactory loggerFactory,
            IDatasetService datasetService,
            IValidationService validationService,
            IDataProcessService dataProcessService,
            IDataRepository dataRepository,
            INotificationService notificationService,
            IConfiguration configuration,
            FilterParser filterParser,
            SortParser sortParser,
            MngDataGateway.Application.Interfaces.IEventPublisher? eventPublisher = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _dataProcessService = dataProcessService ?? throw new ArgumentNullException(nameof(dataProcessService));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _eventPublisher = eventPublisher; // Optional - for backward compatibility
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _filterParser = filterParser ?? throw new ArgumentNullException(nameof(filterParser));
            _sortParser = sortParser ?? throw new ArgumentNullException(nameof(sortParser));
        }

        public async Task<Dictionary<string, object>> CreateAsync(
            string datasetName,
            Dictionary<string, object> data,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            try
            {
                // 1. Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // 2. Validation
                var validationResult = await _validationService.ValidateDataAsync(
                    schema, data, databaseName, isUpdate: false);

                if (!validationResult.IsValid)
                {
                    throw new DataGatewayException("Validation failed")
                    {
                        ValidationErrors = validationResult.Errors.Cast<object>().ToList()
                    };
                }

                // 3. Pre-process
                _dataProcessService.ApplyDefaultValues(schema, data);
                _dataProcessService.GenerateMetadata(schema, data, userId, userEmail, ipAddress);

                // 4. Ensure collection & indexes
                await _dataProcessService.EnsureCollectionAndIndexesAsync(schema, databaseName);

                // 5. Generate incremental fields (before transaction decision)
                await _dataProcessService.GenerateIncrementalFieldsAsync(
                    schema, data, databaseName, domainName, session: null);

                // 6. Transaction decision
                var needsTransaction = DetermineTransactionNeed(schema);

                // 7. Insert data
                if (needsTransaction)
                {
                    await InsertWithTransactionAsync(schema, data, databaseName, userId, userEmail, ipAddress);
                }
                else
                {
                    await InsertDirectAsync(schema, data, databaseName);
                }

                // 7. Publish event (async - fire & forget)
                if (ShouldPublishEvent(schema))
                {
                    // Legacy NotificationService (domain-based exchange)
                    _ = _notificationService.PublishDataCreatedEventAsync(
                        domainName, databaseName, schema, data, userId, userEmail, ipAddress);

                    // New EventPublisher (MngKeeper-style unified exchange)
                    if (_eventPublisher != null)
                    {
                        _ = PublishDataCreatedEventAsync(domainName, schema, data, userId, userEmail, ipAddress);
                    }
                }

                _logger.LogInformation(
                    "Created data in dataset {DatasetName} with __dataId: {DataId}",
                    datasetName, data.GetValueOrDefault("__dataId"));

                return data;
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create data in dataset {DatasetName}", datasetName);
                throw new DataGatewayException("Failed to create data", ex);
            }
        }

        public async Task<Dictionary<string, object>?> GetByIdAsync(
            string datasetName,
            string dataId,
            string databaseName)
        {
            var schema = await LoadSchemaAsync(datasetName, databaseName);
            var document = await _dataRepository.FindByIdAsync(databaseName, schema.CollectionName, dataId);

            if (document == null)
                return null;

            return document.ToDictionary();
        }

        public async Task<(List<Dictionary<string, object>> data, long totalCount)> ListAsync(
            string datasetName,
            string databaseName,
            int skip = 0,
            int limit = 50)
        {
            var schema = await LoadSchemaAsync(datasetName, databaseName);
            var (documents, totalCount) = await _dataRepository.FindManyAsync(
                databaseName, schema.CollectionName, skip, limit);

            var data = documents.ToDictionaryList();

            return (data, totalCount);
        }

        public async Task<Dictionary<string, object>?> UpdateAsync(
            string datasetName,
            string dataId,
            Dictionary<string, object> updates,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            try
            {
                // 1. Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // 2. Get existing data
                var existingDoc = await _dataRepository.FindByIdAsync(databaseName, schema.CollectionName, dataId);
                if (existingDoc == null)
                {
                    throw new DataGatewayException($"Data with __dataId '{dataId}' not found");
                }

                // 3. Validate updates
                var validationResult = await _validationService.ValidateDataAsync(
                    schema, updates, databaseName, isUpdate: true, dataId: dataId);

                if (!validationResult.IsValid)
                {
                    throw new DataGatewayException("Validation failed")
                    {
                        ValidationErrors = validationResult.Errors.Cast<object>().ToList()
                    };
                }

                // 4. Add history if logging mode is "self"
                if (schema.logging == "self")
                {
                    AddHistoryEntry(existingDoc, updates, userId, userEmail, ipAddress);
                }

                // 5. Update data
                var success = await _dataRepository.UpdateOneAsync(
                    databaseName, schema.CollectionName, dataId, updates);

                if (!success)
                {
                    throw new DataGatewayException("Failed to update data");
                }

                // 6. Get updated data
                var updatedDoc = await _dataRepository.FindByIdAsync(databaseName, schema.CollectionName, dataId);
                var result = updatedDoc?.ToDictionary();

                // 7. Publish event (async)
                if (ShouldPublishEvent(schema) && result != null)
                {
                    // Legacy NotificationService (domain-based exchange)
                    _ = _notificationService.PublishDataUpdatedEventAsync(
                        domainName, databaseName, schema, result, userId, userEmail, ipAddress);

                    // New EventPublisher (MngKeeper-style unified exchange)
                    if (_eventPublisher != null)
                    {
                        _ = PublishDataUpdatedEventAsync(domainName, schema, result, userId, userEmail, ipAddress);
                    }
                }

                _logger.LogInformation(
                    "Updated data in dataset {DatasetName} with __dataId: {DataId}",
                    datasetName, dataId);

                return result;
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update data in dataset {DatasetName}", datasetName);
                throw new DataGatewayException("Failed to update data", ex);
            }
        }

        public async Task<bool> DeleteAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var schema = await LoadSchemaAsync(datasetName, databaseName);
            
            // Get retention days from settings
            var settings = _configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();
            var retentionDays = settings?.DeletedData?.RetentionDays ?? 7;
            var expireAt = DateTime.UtcNow.AddDays(retentionDays);

            var success = await _dataRepository.HardDeleteAndArchiveAsync(
                databaseName, schema.CollectionName, dataId, userId, userEmail, expireAt, ipAddress);

            if (!success)
                return false;

            // Publish event (async)
            if (ShouldPublishEvent(schema))
            {
                _ = _notificationService.PublishDataDeletedEventAsync(
                    domainName, databaseName, schema, dataId, userId, userEmail, ipAddress);
            }

            _logger.LogInformation(
                "Hard-deleted and archived data in dataset {DatasetName} with __dataId: {DataId} (expires at {ExpireAt})",
                datasetName, dataId, expireAt);

            return true;
        }

        public async Task<bool> RestoreAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var schema = await LoadSchemaAsync(datasetName, databaseName);
            
            var success = await _dataRepository.RestoreFromArchiveAsync(
                databaseName, schema.CollectionName, dataId, userId, userEmail, ipAddress);

            if (!success)
                return false;

            // Publish event (async)
            if (ShouldPublishEvent(schema))
            {
                // Legacy NotificationService (domain-based exchange)
                _ = _notificationService.PublishDataRestoredEventAsync(
                    domainName, databaseName, schema, dataId, userId, userEmail, ipAddress);

                // New EventPublisher (MngKeeper-style unified exchange)
                if (_eventPublisher != null)
                {
                    _ = PublishDataRestoredEventAsync(domainName, schema, dataId, userId, userEmail, ipAddress);
                }
            }

            _logger.LogInformation(
                "Restored data from archive in dataset {DatasetName} with __dataId: {DataId}",
                datasetName, dataId);

            return true;
        }

        public async Task<BulkInsertResultDto> BulkCreateAsync(
            string datasetName,
            List<Dictionary<string, object>> items,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            try
            {
                // 1. Load schema once
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // 2. Ensure collection & indexes once
                await _dataProcessService.EnsureCollectionAndIndexesAsync(schema, databaseName);

                // 3. Validate all items first (pre-validation)
                var validationResults = new List<(int index, Dictionary<string, object> item, ValidationResult? result)>();
                var validItems = new List<(int index, Dictionary<string, object> item)>();

                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    try
                    {
                        var validationResult = await _validationService.ValidateDataAsync(
                            schema, item, databaseName, isUpdate: false);

                        if (validationResult.IsValid)
                        {
                            validItems.Add((i, item));
                        }
                        else
                        {
                            validationResults.Add((i, item, validationResult));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Validation failed for item {Index} in bulk insert", i);
                        validationResults.Add((i, item, new ValidationResult
                        {
                            IsValid = false,
                            Errors = new List<ValidationErrorDto> { new ValidationErrorDto { Message = ex.Message } }
                        }));
                    }
                }

                // 4. Process valid items (defaults, metadata, incremental fields)
                var processedItems = new List<Dictionary<string, object>>();
                var needsTransaction = DetermineTransactionNeed(schema);

                if (needsTransaction)
                {
                    // Transaction içinde tüm item'ları process et ve insert et
                    await ProcessAndInsertItemsInTransactionAsync(
                        schema, validItems, databaseName, domainName, userId, userEmail, ipAddress, processedItems);
                }
                else
                {
                    // Transaction olmadan process et
                    foreach (var (index, item) in validItems)
                    {
                        var processedItem = new Dictionary<string, object>(item);
                        _dataProcessService.ApplyDefaultValues(schema, processedItem);
                        _dataProcessService.GenerateMetadata(schema, processedItem, userId, userEmail, ipAddress);
                        await _dataProcessService.GenerateIncrementalFieldsAsync(schema, processedItem, databaseName, domainName, session: null);
                        processedItems.Add(processedItem);
                    }

                    // Bulk insert
                    if (processedItems.Any())
                    {
                        await _dataRepository.InsertManyAsync(
                            databaseName, schema.CollectionName, processedItems, session: null);
                    }
                }

                // 5. Build response
                var result = new BulkInsertResultDto
                {
                    Total = items.Count,
                    Successful = processedItems.Count,
                    Failed = validationResults.Count,
                    Items = processedItems,
                    Errors = validationResults.Select(vr => new BulkInsertErrorDto
                    {
                        Index = vr.index,
                        Item = vr.item,
                        Error = "Validation failed",
                        Details = vr.result?.Errors?.Cast<object>().ToList()
                    }).ToList()
                };

                // 6. Publish events (async - fire & forget)
                if (ShouldPublishEvent(schema))
                {
                    foreach (var item in processedItems)
                    {
                        // Legacy NotificationService (domain-based exchange)
                        _ = _notificationService.PublishDataCreatedEventAsync(
                            domainName, databaseName, schema, item, userId, userEmail, ipAddress);

                        // New EventPublisher (MngKeeper-style unified exchange)
                        if (_eventPublisher != null)
                        {
                            _ = PublishDataCreatedEventAsync(domainName, schema, item, userId, userEmail, ipAddress);
                        }
                    }
                }

                _logger.LogInformation(
                    "Bulk insert completed for dataset {DatasetName}: {Successful}/{Total} successful, {Failed} failed",
                    datasetName, processedItems.Count, items.Count, validationResults.Count);

                return result;
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk create data in dataset {DatasetName}", datasetName);
                throw new DataGatewayException("Failed to bulk create data", ex);
            }
        }

        #region Private Methods

        private async Task<DatasetSchema> LoadSchemaAsync(string datasetName, string databaseName)
        {
            var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
            if (schema == null)
            {
                throw new DataGatewayException($"Dataset '{datasetName}' not found");
            }

            // No need to fix query pipeline - it's already stored correctly in MongoDB as BsonDocument

            // Note: IsActive check can be added later if needed
            // For now, all schemas in collection are considered active

            return schema;
        }

        private bool DetermineTransactionNeed(DatasetSchema schema)
        {
            // Need transaction if:
            // 1. Has incremental field (counter must be atomic)
            // 2. Logging mode is "common" (separate collection write)
            // 3. Has relation fields (future: referential integrity)

            var hasIncrementalField = schema.fields.Any(f => f.fieldType == "incremental");
            var hasCommonLogging = schema.logging == "common";
            var hasRelations = schema.fields.Any(f => f.fieldType == "relation");

            return hasIncrementalField || hasCommonLogging || hasRelations;
        }

        private async Task InsertWithTransactionAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            try
            {
                using var session = await _dataRepository.StartSessionAsync();
                session.StartTransaction();

                try
                {
                    // Note: Incremental fields are already generated before transaction

                    // Insert data
                    await _dataRepository.InsertOneAsync(
                        databaseName, schema.CollectionName, data, session);

                    await session.CommitTransactionAsync();
                    
                    _logger.LogDebug("Transaction committed for dataset {DatasetName}", schema.DatasetName);
                }
                catch
                {
                    await session.AbortTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("Standalone") || 
                ex.Message.Contains("Transaction") || 
                ex.Message.Contains("session") ||
                ex.InnerException?.Message.Contains("Standalone") == true)
            {
                // MongoDB Standalone doesn't support transactions - fallback
                _logger.LogWarning(ex, "Transaction not supported, falling back to direct insert");
                await InsertDirectAsync(schema, data, databaseName);
            }
        }

        private async Task InsertDirectAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName)
        {
            // Note: Incremental fields are already generated before this method
            // Insert data
            await _dataRepository.InsertOneAsync(
                databaseName, schema.CollectionName, data, session: null);
        }

        private async Task ProcessAndInsertItemsInTransactionAsync(
            DatasetSchema schema,
            List<(int index, Dictionary<string, object> item)> validItems,
            string databaseName,
            string domainName,
            string userId,
            string userEmail,
            string? ipAddress,
            List<Dictionary<string, object>> processedItems)
        {
            try
            {
                using var session = await _dataRepository.StartSessionAsync();
                session.StartTransaction();

                try
                {
                    // Process each item (defaults, metadata, incremental fields)
                    foreach (var (index, item) in validItems)
                    {
                        var processedItem = new Dictionary<string, object>(item);
                        _dataProcessService.ApplyDefaultValues(schema, processedItem);
                        _dataProcessService.GenerateMetadata(schema, processedItem, userId, userEmail, ipAddress);
                        await _dataProcessService.GenerateIncrementalFieldsAsync(
                            schema, processedItem, databaseName, domainName, session);
                        processedItems.Add(processedItem);
                    }

                    // Bulk insert all processed items
                    if (processedItems.Any())
                    {
                        await _dataRepository.InsertManyAsync(
                            databaseName, schema.CollectionName, processedItems, session);
                    }

                    await session.CommitTransactionAsync();

                    _logger.LogDebug(
                        "Bulk insert transaction committed for dataset {DatasetName}: {Count} items",
                        schema.DatasetName, processedItems.Count);
                }
                catch
                {
                    await session.AbortTransactionAsync();
                    throw;
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("Standalone") ||
                ex.Message.Contains("Transaction") ||
                ex.Message.Contains("session") ||
                ex.InnerException?.Message.Contains("Standalone") == true)
            {
                // MongoDB Standalone doesn't support transactions - fallback
                _logger.LogWarning(ex, "Transaction not supported, falling back to direct insert");

                // Process and insert without transaction
                foreach (var (index, item) in validItems)
                {
                    var processedItem = new Dictionary<string, object>(item);
                    _dataProcessService.ApplyDefaultValues(schema, processedItem);
                    _dataProcessService.GenerateMetadata(schema, processedItem, userId, userEmail, ipAddress);
                    await _dataProcessService.GenerateIncrementalFieldsAsync(
                        schema, processedItem, databaseName, domainName, session: null);
                    processedItems.Add(processedItem);
                }

                // Bulk insert
                if (processedItems.Any())
                {
                    await _dataRepository.InsertManyAsync(
                        databaseName, schema.CollectionName, processedItems, session: null);
                }
            }
        }

        private bool ShouldPublishEvent(DatasetSchema schema)
        {
            // Phase 1: Simple check - publish_mode != "none"
            return schema.publish_mode != "none";
        }

        #region EventPublisher Helpers (MngKeeper-style)

        /// <summary>
        /// Publish DataCreatedEvent using EventPublisher (MngKeeper-style)
        /// </summary>
        private async Task PublishDataCreatedEventAsync(
            string domainName,
            DatasetSchema schema,
            Dictionary<string, object> data,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            if (_eventPublisher == null) return;

            try
            {
                var dataId = data.GetValueOrDefault("__dataId")?.ToString() ?? string.Empty;
                
                var @event = new MngDataGateway.Application.Events.DataCreatedEvent
                {
                    DatasetName = schema.DatasetName,
                    DataId = dataId,
                    Data = data,
                    UserId = userId,
                    UserEmail = userEmail,
                    IpAddress = ipAddress
                };

                // Use domainName as domainId (they're typically the same)
                await _eventPublisher.PublishAsync(@event, domainName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish DataCreatedEvent via EventPublisher for dataset {DatasetName}", schema.DatasetName);
            }
        }

        /// <summary>
        /// Publish DataUpdatedEvent using EventPublisher (MngKeeper-style)
        /// </summary>
        private async Task PublishDataUpdatedEventAsync(
            string domainName,
            DatasetSchema schema,
            Dictionary<string, object> data,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            if (_eventPublisher == null) return;

            try
            {
                var dataId = data.GetValueOrDefault("__dataId")?.ToString() ?? string.Empty;
                
                var @event = new MngDataGateway.Application.Events.DataUpdatedEvent
                {
                    DatasetName = schema.DatasetName,
                    DataId = dataId,
                    Data = data,
                    UserId = userId,
                    UserEmail = userEmail,
                    IpAddress = ipAddress
                };

                await _eventPublisher.PublishAsync(@event, domainName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish DataUpdatedEvent via EventPublisher for dataset {DatasetName}", schema.DatasetName);
            }
        }

        /// <summary>
        /// Publish DataDeletedEvent using EventPublisher (MngKeeper-style)
        /// </summary>
        private async Task PublishDataDeletedEventAsync(
            string domainName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            if (_eventPublisher == null) return;

            try
            {
                var @event = new MngDataGateway.Application.Events.DataDeletedEvent
                {
                    DatasetName = schema.DatasetName,
                    DataId = dataId,
                    UserId = userId,
                    UserEmail = userEmail,
                    IpAddress = ipAddress
                };

                await _eventPublisher.PublishAsync(@event, domainName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish DataDeletedEvent via EventPublisher for dataset {DatasetName}", schema.DatasetName);
            }
        }

        /// <summary>
        /// Publish DataRestoredEvent using EventPublisher (MngKeeper-style)
        /// </summary>
        private async Task PublishDataRestoredEventAsync(
            string domainName,
            DatasetSchema schema,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            if (_eventPublisher == null) return;

            try
            {
                var @event = new MngDataGateway.Application.Events.DataRestoredEvent
                {
                    DatasetName = schema.DatasetName,
                    DataId = dataId,
                    UserId = userId,
                    UserEmail = userEmail,
                    IpAddress = ipAddress
                };

                await _eventPublisher.PublishAsync(@event, domainName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish DataRestoredEvent via EventPublisher for dataset {DatasetName}", schema.DatasetName);
            }
        }

        #endregion

        private void AddHistoryEntry(
            BsonDocument existingDoc,
            Dictionary<string, object> updates,
            string userId,
            string userEmail,
            string? ipAddress)
        {
            // Convert updates to BsonDocument for storage
            var changesBson = new BsonDocument(updates.Select(kvp => 
                new BsonElement(kvp.Key, BsonValue.Create(kvp.Value))));

            var historyEntry = new Dictionary<string, object>
            {
                { "operation", "update" },
                { "userId", userId },
                { "userEmail", userEmail },
                { "timestamp", DateTime.UtcNow },
                { "ipAddress", ipAddress ?? string.Empty },
                { "changes", changesBson }
            };

            // Get existing history or create new
            var history = existingDoc.Contains("__history") && existingDoc["__history"].IsBsonArray
                ? existingDoc["__history"].AsBsonArray.Select(h => BsonTypeMapper.MapToDotNetValue(h)).ToList()
                : new List<object>();

            history.Add(historyEntry);

            updates["__history"] = history;
        }


        public async Task<QueryResultDto> QueryAsync(
            string datasetName,
            string databaseName,
            QueryOptionsDto options)
        {
            try
            {
                // Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // Get configuration
                var settings = _configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();
                var querySettings = settings?.Query ?? new QuerySettings();
                var maxDepth = options.Deep ?? querySettings.RelationExpansionMaxDepth;

                // Validate pagination
                var skip = Math.Max(0, options.Skip);
                var limit = Math.Min(Math.Max(1, options.Limit), querySettings.MaxPageSize);

                // Parse filter
                var matchFilter = _filterParser.Parse(options.Filter);

                // Parse sort
                var sortDefinition = _sortParser.Parse(options.Sort);

                // Parse fields
                List<string>? fields = null;
                if (!string.IsNullOrWhiteSpace(options.Fields))
                {
                    fields = options.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrEmpty(f))
                        .ToList();
                }

                // Search in relation collections and collect matching IDs (pre-expansion search)
                Dictionary<string, List<BsonValue>>? relationSearchIds = null;
                if (!string.IsNullOrWhiteSpace(options.Search))
                {
                    relationSearchIds = await SearchInRelationCollectionsAsync(
                        schema,
                        databaseName,
                        options.Search);
                }

                // Build pipeline
                var builder = new AggregatePipelineBuilder(
                    _loggerFactory.CreateLogger<AggregatePipelineBuilder>(),
                    schema,
                    databaseName);

                // Build base pipeline (without pagination for count)
                builder
                    .AddMatch(matchFilter)
                    .AddSearch(options.Search, relationSearchIds)  // Add search before expansion (pre-expansion search)
                    .AddRelationExpansion(options.Expand, maxDepth, 0, null)
                    .AddPersonExpansion(options.Expand)
                    .AddProject(fields, options.ShowHistory);

                var basePipeline = builder.Build();

                // Build data pipeline (with sort and pagination) - inside $facet
                // $facet içindeki pipeline'lar bağımsız çalışır, bu yüzden basePipeline'ı her branch'te tekrar eklemeliyiz
                var dataBranch = new List<BsonDocument>();
                // Base pipeline'ı data branch'e ekle
                foreach (var stage in basePipeline)
                {
                    dataBranch.Add(stage);
                }
                // Sonra sort ve pagination ekle
                if (sortDefinition != null && sortDefinition.ElementCount > 0)
                {
                    dataBranch.Add(new BsonDocument("$sort", sortDefinition));
                }
                if (skip > 0)
                {
                    dataBranch.Add(new BsonDocument("$skip", skip));
                }
                if (limit > 0)
                {
                    dataBranch.Add(new BsonDocument("$limit", limit));
                }

                // Build count pipeline - inside $facet
                // Base pipeline'ı count branch'e ekle
                var countBranch = new List<BsonDocument>();
                foreach (var stage in basePipeline)
                {
                    countBranch.Add(stage);
                }
                // Sonra count ekle
                countBranch.Add(new BsonDocument("$count", "total"));

                // Build $facet pipeline to get both data and count in one query
                // Base pipeline'ı $facet'ten önce eklemek yerine, $facet'i direkt kullanıyoruz
                // Çünkü $facet içindeki her branch bağımsız çalışır
                var facetPipeline = new List<BsonDocument>();

                // Add $facet stage with data and count branches (both include basePipeline)
                var facetStage = new BsonDocument("$facet", new BsonDocument
                {
                    { "data", new BsonArray(dataBranch) },
                    { "count", new BsonArray(countBranch) }
                });
                facetPipeline.Add(facetStage);

                // Add $unwind to extract count from array
                facetPipeline.Add(new BsonDocument("$unwind", new BsonDocument
                {
                    { "path", "$count" },
                    { "preserveNullAndEmptyArrays", true }
                }));

                // Add $project to reshape output
                facetPipeline.Add(new BsonDocument("$project", new BsonDocument
                {
                    { "data", 1 },
                    { "total", new BsonDocument("$ifNull", new BsonArray { "$count.total", 0 }) }
                }));

                // Convert pipeline to JSON objects if showQuery is true
                List<object>? pipelineJson = null;
                if (options.ShowQuery)
                {
                    pipelineJson = facetPipeline.Select(stage =>
                    {
                        var json = stage.ToJson();
                        return System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? new object();
                    }).ToList();
                }

                // Execute aggregate with $facet
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    facetPipeline);

                // Parse $facet result
                long totalCount = 0;
                List<Dictionary<string, object>> data = new();

                if (results != null && results.Any())
                {
                    var resultDoc = results.First();
                    if (resultDoc.Contains("total"))
                    {
                        totalCount = resultDoc["total"].ToInt64();
                    }
                    if (resultDoc.Contains("data") && resultDoc["data"].IsBsonArray)
                    {
                        var dataArray = resultDoc["data"].AsBsonArray;
                        data = dataArray.Select(d => d.AsBsonDocument.ToDictionary()).ToList();
                    }
                }

                _logger.LogDebug(
                    "Query executed on dataset {DatasetName}, returned {Count} documents, total: {TotalCount}",
                    datasetName, data.Count, totalCount);

                return new QueryResultDto
                {
                    Data = data,
                    TotalCount = totalCount,
                    Query = pipelineJson
                };
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query data in dataset {DatasetName}. Exception: {ExceptionType}, Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                    datasetName, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                throw new DataGatewayException("Failed to query data", ex);
            }
        }

        public async Task<QueryResultDto> QueryByIdAsync(
            string datasetName,
            string dataId,
            string databaseName,
            QueryOptionsDto options)
        {
            try
            {
                // Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // Get configuration
                var settings = _configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();
                var querySettings = settings?.Query ?? new QuerySettings();
                var maxDepth = options.Deep ?? querySettings.RelationExpansionMaxDepth;

                // Parse sort (not used for single document, but included for consistency)
                var sortDefinition = _sortParser.Parse(options.Sort);

                // Parse fields
                List<string>? fields = null;
                if (!string.IsNullOrWhiteSpace(options.Fields))
                {
                    fields = options.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrEmpty(f))
                        .ToList();
                }

                // Build pipeline
                var builder = new AggregatePipelineBuilder(
                    _loggerFactory.CreateLogger<AggregatePipelineBuilder>(),
                    schema,
                    databaseName);

                builder
                    .AddMatchById(dataId)
                    .AddRelationExpansion(options.Expand, maxDepth, 0, null)
                    .AddPersonExpansion(options.Expand)
                    .AddProject(fields, options.ShowHistory)
                    .AddSort(sortDefinition);

                var pipeline = builder.Build();

                // Convert pipeline to JSON objects if showQuery is true
                List<object>? pipelineJson = null;
                if (options.ShowQuery)
                {
                    pipelineJson = pipeline.Select(stage =>
                    {
                        var json = stage.ToJson();
                        return System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? new object();
                    }).ToList();
                }

                // Execute aggregate
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    pipeline);

                // Convert to dictionary
                var result = results.FirstOrDefault();
                var data = new List<Dictionary<string, object>>();
                if (result != null)
                {
                    data.Add(result.ToDictionary());
                }

                _logger.LogDebug(
                    "QueryById executed on dataset {DatasetName} for __dataId {DataId}",
                    datasetName, dataId);

                return new QueryResultDto
                {
                    Data = data,
                    Query = pipelineJson
                };
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query data {DataId} in dataset {DatasetName}", dataId, datasetName);
                throw new DataGatewayException("Failed to query data", ex);
            }
        }

        public async Task<QueryResultDto> QueryWithMatchAsync(
            string datasetName,
            string databaseName,
            Dictionary<string, object>? match,
            QueryOptionsDto options)
        {
            try
            {
                // Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // Get configuration
                var settings = _configuration.GetSection("MngDataGatewaySettings").Get<MngDataGatewaySettings>();
                var querySettings = settings?.Query ?? new QuerySettings();
                var maxDepth = options.Deep ?? querySettings.RelationExpansionMaxDepth;

                // Validate pagination
                var skip = Math.Max(0, options.Skip);
                var limit = Math.Min(Math.Max(1, options.Limit), querySettings.MaxPageSize);

                // Convert match dictionary to BsonDocument
                BsonDocument? matchFilter = null;
                if (match != null && match.Count > 0)
                {
                    matchFilter = new BsonDocument(match.Select(kvp =>
                        new BsonElement(kvp.Key, BsonValue.Create(kvp.Value))));
                }

                // Parse sort
                var sortDefinition = _sortParser.Parse(options.Sort);

                // Parse fields
                List<string>? fields = null;
                if (!string.IsNullOrWhiteSpace(options.Fields))
                {
                    fields = options.Fields.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrEmpty(f))
                        .ToList();
                }

                // Search in relation collections and collect matching IDs (pre-expansion search)
                Dictionary<string, List<BsonValue>>? relationSearchIds = null;
                if (!string.IsNullOrWhiteSpace(options.Search))
                {
                    relationSearchIds = await SearchInRelationCollectionsAsync(
                        schema,
                        databaseName,
                        options.Search);
                }

                // Build pipeline
                var builder = new AggregatePipelineBuilder(
                    _loggerFactory.CreateLogger<AggregatePipelineBuilder>(),
                    schema,
                    databaseName);

                builder
                    .AddMatch(matchFilter)
                    .AddSearch(options.Search, relationSearchIds)  // Add search before expansion (pre-expansion search)
                    .AddRelationExpansion(options.Expand, maxDepth, 0, null)
                    .AddPersonExpansion(options.Expand)
                    .AddProject(fields, options.ShowHistory)
                    .AddSort(sortDefinition)
                    .AddPagination(skip, limit);

                var pipeline = builder.Build();

                // Convert pipeline to JSON objects if showQuery is true
                List<object>? pipelineJson = null;
                if (options.ShowQuery)
                {
                    pipelineJson = pipeline.Select(stage =>
                    {
                        var json = stage.ToJson();
                        return System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? new object();
                    }).ToList();
                }

                // Execute aggregate
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    pipeline);

                // Convert to dictionary list
                var data = results.ToDictionaryList();

                _logger.LogDebug(
                    "QueryWithMatch executed on dataset {DatasetName}, returned {Count} documents",
                    datasetName, data.Count);

                return new QueryResultDto
                {
                    Data = data,
                    Query = pipelineJson
                };
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query data with match in dataset {DatasetName}", datasetName);
                throw new DataGatewayException("Failed to query data", ex);
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecutePredefinedQueryAsync(
            string datasetName,
            string queryName,
            string databaseName,
            Dictionary<string, object> parameters)
        {
            try
            {
                // Load schema
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // Find query definition
                var queryDef = schema.queries?.FirstOrDefault(q => q.name == queryName);
                if (queryDef == null)
                {
                    throw new DataGatewayException($"Predefined query '{queryName}' not found in dataset '{datasetName}'");
                }

                // Log parameter definitions for debugging
                _logger.LogInformation("Query '{QueryName}' parameter definitions type: {Type}, Value: {Value}", 
                    queryName, 
                    queryDef.parameters?.GetType().FullName ?? "null",
                    queryDef.parameters is MongoDB.Bson.BsonArray ? ((MongoDB.Bson.BsonArray)queryDef.parameters).ToJson() : queryDef.parameters?.ToString() ?? "null");

                // Validate and convert parameters based on type definitions
                var validatedParameters = ValidateAndConvertParameters(queryDef.parameters, parameters, queryName);
                
                _logger.LogInformation("Query '{QueryName}' validated parameters: {Parameters}", 
                    queryName, 
                    System.Text.Json.JsonSerializer.Serialize(validatedParameters));

                // Convert pipeline to BsonDocument list and replace parameters
                var pipeline = new List<BsonDocument>();
                if (queryDef.pipeline != null && queryDef.pipeline.Any())
                {
                    _logger.LogDebug("Processing {StageCount} pipeline stages for query '{QueryName}' with parameters: {Parameters}", 
                        queryDef.pipeline.Count, queryName, System.Text.Json.JsonSerializer.Serialize(parameters));
                    
                    foreach (var stage in queryDef.pipeline)
                    {
                        try
                        {
                            // Log original stage type and content
                            _logger.LogInformation("Processing stage for query '{QueryName}': Type={StageType}, IsBsonDocument={IsBsonDoc}", 
                                queryName,
                                stage?.GetType().FullName ?? "null",
                                stage is MongoDB.Bson.BsonDocument);
                            
                            if (stage is MongoDB.Bson.BsonDocument originalBsonDoc)
                            {
                                _logger.LogInformation("Stage is BsonDocument. Content: {StageJson}", originalBsonDoc.ToJson());
                                
                                // Deep clone to avoid modifying original
                                var clonedDoc = originalBsonDoc.DeepClone().AsBsonDocument;
                                
                                // Only replace parameters - no other modifications needed
                                // BsonValue types (including $sort integers) are preserved as-is
                                var stageBson = ReplaceParametersInBsonDocument(clonedDoc, parameters);
                                
                                _logger.LogInformation("Stage after parameter replacement: {StageJson}", stageBson.ToJson());
                                
                                pipeline.Add(stageBson);
                            }
                            else
                            {
                                _logger.LogWarning("Stage is NOT BsonDocument! Type: {Type}. This should not happen for predefined queries.", 
                                    stage?.GetType().FullName ?? "null");
                                
                                // This should not happen - predefined queries should be stored as BsonDocument
                                // But handle it anyway for safety
                                var stageWithParams = ReplaceParametersInObject(stage, validatedParameters);
                                
                                BsonDocument stageBson;
                                if (stageWithParams is Dictionary<string, object> dict)
                                {
                                    stageBson = DictionaryToBsonDocument(dict);
                                }
                                else
                                {
                                    var stageJson = System.Text.Json.JsonSerializer.Serialize(stageWithParams);
                                    if (string.IsNullOrWhiteSpace(stageJson) || stageJson == "{}" || stageJson == "[]")
                                    {
                                        _logger.LogWarning("Skipping empty stage for query '{QueryName}'", queryName);
                                        continue;
                                    }
                                    stageBson = BsonDocument.Parse(stageJson);
                                }
                                
                                if (stageBson.ElementCount == 0)
                                {
                                    _logger.LogWarning("Skipping empty BsonDocument stage for query '{QueryName}'", queryName);
                                    continue;
                                }
                                
                                pipeline.Add(stageBson);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process pipeline stage for query '{QueryName}'. Stage Type: {StageType}, Stage: {Stage}, Exception: {ExceptionType}, Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                                queryName, 
                                stage?.GetType().FullName ?? "null",
                                System.Text.Json.JsonSerializer.Serialize(stage), 
                                ex.GetType().Name, 
                                ex.Message,
                                ex.InnerException?.Message,
                                ex.StackTrace);
                            throw;
                        }
                    }
                }

                // Log final pipeline for debugging
                _logger.LogDebug("Final pipeline for query '{QueryName}' ({StageCount} stages):", queryName, pipeline.Count);
                for (int i = 0; i < pipeline.Count; i++)
                {
                    _logger.LogDebug("  Stage {Index}: {StageJson}", i + 1, pipeline[i].ToJson());
                }

                // Execute aggregate
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    pipeline);

                // Convert to dictionary list
                var data = results.ToDictionaryList();

                _logger.LogDebug(
                    "Predefined query '{QueryName}' executed on dataset {DatasetName}, returned {Count} documents",
                    queryName, datasetName, data.Count);

                return data;
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute predefined query '{QueryName}' in dataset {DatasetName}. Exception: {ExceptionType}, Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                    queryName, datasetName, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                throw new DataGatewayException($"Failed to execute predefined query '{queryName}'", ex);
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteRawAggregateAsync(
            string datasetName,
            string databaseName,
            List<MongoDB.Bson.BsonDocument> pipeline)
        {
            try
            {
                // Load schema to get collection name
                var schema = await LoadSchemaAsync(datasetName, databaseName);

                // Execute aggregate
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    pipeline);

                // Convert to dictionary list
                var data = results.ToDictionaryList();

                _logger.LogDebug(
                    "Raw aggregate executed on dataset {DatasetName}, returned {Count} documents",
                    datasetName, data.Count);

                return data;
            }
            catch (DataGatewayException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute raw aggregate in dataset {DatasetName}. Exception: {ExceptionType}, Message: {Message}, Inner: {InnerMessage}, StackTrace: {StackTrace}", 
                    datasetName, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                throw new DataGatewayException("Failed to execute raw aggregate", ex);
            }
        }

        /// <summary>
        /// Recursively replace parameters in an object (handles nested dictionaries and lists)
        /// </summary>
        private object ReplaceParametersInObject(object? obj, Dictionary<string, object> parameters)
        {
            if (obj == null)
                return new object();

            // Handle BsonDocument (from MongoDB)
            if (obj is MongoDB.Bson.BsonDocument bsonDoc)
            {
                var bsonDict = new Dictionary<string, object>();
                foreach (var element in bsonDoc)
                {
                    var value = MongoDB.Bson.BsonTypeMapper.MapToDotNetValue(element.Value);
                    bsonDict[element.Name] = ReplaceParametersInObject(value, parameters);
                }
                return bsonDict;
            }

            if (obj is string str)
            {
                // Check if string is a parameter placeholder
                if (str.StartsWith(":") && parameters.ContainsKey(str.Substring(1)))
                {
                    return parameters[str.Substring(1)];
                }
                return str;
            }

            if (obj is System.Text.Json.JsonElement jsonElement)
            {
                return ReplaceParametersInJsonElement(jsonElement, parameters);
            }

            if (obj is Dictionary<string, object> dict)
            {
                var result = new Dictionary<string, object>();
                foreach (var kvp in dict)
                {
                    // Skip empty keys (should not happen, but safety check)
                    if (string.IsNullOrEmpty(kvp.Key))
                    {
                        continue;
                    }
                    result[kvp.Key] = ReplaceParametersInObject(kvp.Value, parameters);
                }
                return result;
            }

            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ReplaceParametersInObject(item, parameters));
                }
                return list;
            }

            return obj;
        }

        /// <summary>
        /// Replace parameters in JsonElement
        /// Preserves numeric types correctly (especially for $sort stage with -1 and 1 values)
        /// </summary>
        private object ReplaceParametersInJsonElement(System.Text.Json.JsonElement element, Dictionary<string, object> parameters)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    {
                        var str = element.GetString() ?? string.Empty;
                        if (str.StartsWith(":") && parameters.ContainsKey(str.Substring(1)))
                        {
                            return parameters[str.Substring(1)];
                        }
                        return str;
                    }
                case System.Text.Json.JsonValueKind.Object:
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in element.EnumerateObject())
                        {
                            dict[prop.Name] = ReplaceParametersInJsonElement(prop.Value, parameters);
                        }
                        return dict;
                    }
                case System.Text.Json.JsonValueKind.Array:
                    {
                        var list = new List<object>();
                        foreach (var item in element.EnumerateArray())
                        {
                            list.Add(ReplaceParametersInJsonElement(item, parameters));
                        }
                        return list;
                    }
                case System.Text.Json.JsonValueKind.Number:
                    // Preserve as integer if possible (critical for $sort: { field: 1 } or { field: -1 })
                    if (element.TryGetInt32(out var intVal))
                    {
                        return intVal;
                    }
                    if (element.TryGetInt64(out var longVal))
                    {
                        return longVal;
                    }
                    return element.GetDouble();
                case System.Text.Json.JsonValueKind.True:
                    return true;
                case System.Text.Json.JsonValueKind.False:
                    return false;
                case System.Text.Json.JsonValueKind.Null:
                    return (object?)null;
                default:
                    return element.GetRawText();
            }
        }

        /// <summary>
        /// Replace parameters in BsonDocument recursively
        /// Only replaces parameter placeholders (":paramName"), preserves all other BsonValue types as-is
        /// </summary>
        private MongoDB.Bson.BsonDocument ReplaceParametersInBsonDocument(MongoDB.Bson.BsonDocument bsonDoc, Dictionary<string, object> parameters)
        {
            var result = new MongoDB.Bson.BsonDocument();
            
            foreach (var element in bsonDoc)
            {
                // Simply replace parameters in the value - no special handling needed
                // BsonValue types (including $sort integers) are preserved as-is
                result[element.Name] = ReplaceParametersInBsonValue(element.Value, parameters);
            }
            
            return result;
        }

        /// <summary>
        /// Replace parameters in BsonValue recursively
        /// </summary>
        private MongoDB.Bson.BsonValue ReplaceParametersInBsonValue(MongoDB.Bson.BsonValue value, Dictionary<string, object> parameters)
        {
            // Handle string values (parameter placeholders)
            if (value is MongoDB.Bson.BsonString bsonString)
            {
                var str = bsonString.Value;
                if (str.StartsWith(":") && parameters.ContainsKey(str.Substring(1)))
                {
                    var paramValue = parameters[str.Substring(1)];
                    return ConvertToBsonValue(paramValue);
                }
                return value;
            }
            
            // Handle BsonDocument (nested objects)
            if (value is MongoDB.Bson.BsonDocument bsonDoc)
            {
                return ReplaceParametersInBsonDocument(bsonDoc, parameters);
            }
            
            // Handle BsonArray (arrays)
            if (value is MongoDB.Bson.BsonArray bsonArray)
            {
                var result = new MongoDB.Bson.BsonArray();
                foreach (var item in bsonArray)
                {
                    result.Add(ReplaceParametersInBsonValue(item, parameters));
                }
                return result;
            }
            
            // Preserve all other BsonValue types as-is (numbers, booleans, null, etc.)
            // This is critical for $sort stage where 1 and -1 must remain as BsonInt32
            return value;
        }

        /// <summary>
        /// Convert Dictionary to BsonDocument recursively
        /// </summary>
        private MongoDB.Bson.BsonDocument DictionaryToBsonDocument(Dictionary<string, object> dict)
        {
            var bsonDoc = new MongoDB.Bson.BsonDocument();
            
            foreach (var kvp in dict)
            {
                bsonDoc[kvp.Key] = ConvertToBsonValue(kvp.Value);
            }
            
            return bsonDoc;
        }

        /// <summary>
        /// Convert object to BsonValue recursively
        /// Preserves numeric types correctly (especially for $sort stage with -1 and 1 values)
        /// </summary>
        private MongoDB.Bson.BsonValue ConvertToBsonValue(object? value)
        {
            if (value == null)
                return MongoDB.Bson.BsonNull.Value;

            // Handle BsonValue types directly
            if (value is MongoDB.Bson.BsonValue bsonValue)
                return bsonValue;

            // Handle numeric types FIRST (before string check) to preserve integer values
            // This is critical for $sort stage where 1 and -1 must remain as BsonInt32
            if (value is int intVal)
                return new MongoDB.Bson.BsonInt32(intVal);
            
            if (value is long longVal)
                return new MongoDB.Bson.BsonInt64(longVal);
            
            if (value is double doubleVal)
                return new MongoDB.Bson.BsonDouble(doubleVal);
            
            if (value is float floatVal)
                return new MongoDB.Bson.BsonDouble(floatVal);
            
            if (value is decimal decimalVal)
                return new MongoDB.Bson.BsonDecimal128(decimalVal);
            
            if (value is short shortVal)
                return new MongoDB.Bson.BsonInt32(shortVal);
            
            if (value is byte byteVal)
                return new MongoDB.Bson.BsonInt32(byteVal);

            // Handle boolean
            if (value is bool boolVal)
                return new MongoDB.Bson.BsonBoolean(boolVal);
            
            // Handle DateTime types
            if (value is DateTime dateTimeVal)
                return new MongoDB.Bson.BsonDateTime(dateTimeVal);
            
            if (value is DateTimeOffset dateTimeOffsetVal)
                return new MongoDB.Bson.BsonDateTime(dateTimeOffsetVal.UtcDateTime);
            
            // Handle string (check for DateTime after numeric types)
            if (value is string strValue)
            {
                // Try to parse as DateTime
                if (DateTime.TryParse(strValue, out var parsedDateTime))
                {
                    return new MongoDB.Bson.BsonDateTime(parsedDateTime);
                }
                return new MongoDB.Bson.BsonString(strValue);
            }

            // Handle JsonElement (from JSON deserialization)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElementToBsonValue(jsonElement);
            }

            // Handle Dictionary
            if (value is Dictionary<string, object> dict)
                return DictionaryToBsonDocument(dict);

            // Handle List/Array
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var bsonArray = new MongoDB.Bson.BsonArray();
                foreach (var item in enumerable)
                {
                    bsonArray.Add(ConvertToBsonValue(item));
                }
                return bsonArray;
            }

            // Fallback: serialize to JSON and parse
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return MongoDB.Bson.BsonDocument.Parse(json);
            }
            catch
            {
                // Last resort: convert to string
                return new MongoDB.Bson.BsonString(value.ToString() ?? string.Empty);
            }
        }

        /// <summary>
        /// Convert JsonElement to BsonValue, preserving numeric types correctly
        /// </summary>
        private MongoDB.Bson.BsonValue ConvertJsonElementToBsonValue(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    {
                        var str = element.GetString() ?? string.Empty;
                        // Try to parse as DateTime
                        if (DateTime.TryParse(str, out var parsedDateTime))
                        {
                            return new MongoDB.Bson.BsonDateTime(parsedDateTime);
                        }
                        return new MongoDB.Bson.BsonString(str);
                    }

                case System.Text.Json.JsonValueKind.Number:
                    // Preserve as integer if possible (critical for $sort: { field: 1 } or { field: -1 })
                    if (element.TryGetInt32(out var intVal))
                    {
                        return new MongoDB.Bson.BsonInt32(intVal);
                    }
                    if (element.TryGetInt64(out var longVal))
                    {
                        return new MongoDB.Bson.BsonInt64(longVal);
                    }
                    return new MongoDB.Bson.BsonDouble(element.GetDouble());

                case System.Text.Json.JsonValueKind.True:
                    return MongoDB.Bson.BsonBoolean.True;

                case System.Text.Json.JsonValueKind.False:
                    return MongoDB.Bson.BsonBoolean.False;

                case System.Text.Json.JsonValueKind.Null:
                    return MongoDB.Bson.BsonNull.Value;

                case System.Text.Json.JsonValueKind.Object:
                    var objDoc = new MongoDB.Bson.BsonDocument();
                    foreach (var prop in element.EnumerateObject())
                    {
                        objDoc[prop.Name] = ConvertJsonElementToBsonValue(prop.Value);
                    }
                    return objDoc;

                case System.Text.Json.JsonValueKind.Array:
                    var array = new MongoDB.Bson.BsonArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        array.Add(ConvertJsonElementToBsonValue(item));
                    }
                    return array;

                default:
                    return new MongoDB.Bson.BsonString(element.GetRawText());
            }
        }

        /// <summary>
        /// Fix $sort stage values - ensure they are integers, not booleans or documents
        /// This fixes issues where sort values are incorrectly parsed from JSON
        /// </summary>
        private void FixSortStageValues(MongoDB.Bson.BsonDocument stage)
        {
            if (stage.Contains("$sort") && stage["$sort"] is MongoDB.Bson.BsonDocument sortDoc)
            {
                var fixedSort = new MongoDB.Bson.BsonDocument();
                foreach (var sortField in sortDoc)
                {
                    var sortValue = sortField.Value;
                    
                    // If value is a BsonDocument (incorrectly parsed), use default
                    if (sortValue is MongoDB.Bson.BsonDocument)
                    {
                        _logger.LogWarning("Fixing BsonDocument in $sort field '{FieldName}', using default sort direction 1", sortField.Name);
                        fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt32(1);
                    }
                    // If value is a BsonBoolean (incorrectly parsed), convert to integer
                    else if (sortValue is MongoDB.Bson.BsonBoolean bsonBool)
                    {
                        fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt32(bsonBool.Value ? 1 : -1);
                        _logger.LogDebug("Fixed BsonBoolean to BsonInt32 in $sort field '{FieldName}': {Value} -> {IntValue}", 
                            sortField.Name, bsonBool.Value, bsonBool.Value ? 1 : -1);
                    }
                    // If value is already a number, ensure it's an integer
                    else if (sortValue is MongoDB.Bson.BsonInt32 || sortValue is MongoDB.Bson.BsonInt64)
                    {
                        fixedSort[sortField.Name] = sortValue;
                    }
                    // If value is a double, convert to integer
                    else if (sortValue is MongoDB.Bson.BsonDouble bsonDouble)
                    {
                        fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt32((int)bsonDouble.Value);
                    }
                    // Otherwise, try to preserve or use default
                    else
                    {
                        var intValue = MongoDB.Bson.BsonTypeMapper.MapToDotNetValue(sortValue);
                        if (intValue is int intVal)
                        {
                            fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt32(intVal);
                        }
                        else if (intValue is long longVal)
                        {
                            fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt64(longVal);
                        }
                        else
                        {
                            _logger.LogWarning("Fixing unexpected type in $sort field '{FieldName}': {Type}, using default sort direction 1", 
                                sortField.Name, sortValue.GetType().Name);
                            fixedSort[sortField.Name] = new MongoDB.Bson.BsonInt32(1);
                        }
                    }
                }
                stage["$sort"] = fixedSort;
            }
        }

        /// <summary>
        /// Validate and convert parameters based on type definitions
        /// Supports both new format (List<QueryParameterDefinition>) and legacy format (List<string>)
        /// </summary>
        private Dictionary<string, object> ValidateAndConvertParameters(
            object? parameterDefinitions,
            Dictionary<string, object> providedParameters,
            string queryName)
        {
            var result = new Dictionary<string, object>();

            _logger.LogInformation("ValidateAndConvertParameters called for query '{QueryName}'. ParameterDefinitions type: {Type}, IsNull: {IsNull}, IsBsonArray: {IsBsonArray}", 
                queryName, 
                parameterDefinitions?.GetType().FullName ?? "null",
                parameterDefinitions == null,
                parameterDefinitions is MongoDB.Bson.BsonArray);

            if (parameterDefinitions == null)
            {
                // No parameter definitions - return provided parameters as-is
                _logger.LogWarning("No parameter definitions for query '{QueryName}', returning provided parameters as-is", queryName);
                return providedParameters;
            }

            // Handle legacy format: List<string>
            if (parameterDefinitions is List<string> legacyParamNames)
            {
                // Validate required parameters
                var missingParams = legacyParamNames.Where(p => !providedParameters.ContainsKey(p)).ToList();
                if (missingParams.Any())
                {
                    throw new DataGatewayException($"Missing required parameters: {string.Join(", ", missingParams)}");
                }

                // Convert parameters (no type checking, just pass through)
                foreach (var paramName in legacyParamNames)
                {
                    if (providedParameters.ContainsKey(paramName))
                    {
                        result[paramName] = providedParameters[paramName];
                    }
                }

                return result;
            }

            // Handle new format: List<QueryParameterDefinition>
            if (parameterDefinitions is List<QueryParameterDefinition> newParamDefs)
            {
                foreach (var paramDef in newParamDefs)
                {
                    var paramName = paramDef.name;
                    var paramType = paramDef.type?.ToLowerInvariant() ?? "text";
                    var isRequired = paramDef.required;

                    // Check if parameter is provided
                    if (!providedParameters.ContainsKey(paramName))
                    {
                        if (isRequired)
                        {
                            throw new DataGatewayException($"Missing required parameter '{paramName}' (type: {paramType})");
                        }
                        continue; // Skip optional parameters
                    }

                    var paramValue = providedParameters[paramName];

                    // Convert parameter based on type
                    try
                    {
                        var convertedValue = ConvertParameterByType(paramValue, paramType, paramName);
                        result[paramName] = convertedValue;
                    }
                    catch (Exception ex)
                    {
                        throw new DataGatewayException(
                            $"Invalid parameter '{paramName}': Expected type '{paramType}', but received '{paramValue?.GetType().Name}'. {ex.Message}");
                    }
                }

                return result;
            }

            // Try to deserialize from BsonValue
            try
            {
                if (parameterDefinitions is MongoDB.Bson.BsonArray bsonArray)
                {
                    _logger.LogInformation("Query '{QueryName}' parameters is BsonArray with {Count} elements", queryName, bsonArray.Count);
                    var firstElement = bsonArray.FirstOrDefault();
                    if (firstElement != null)
                    {
                        _logger.LogInformation("Query '{QueryName}' first element type: {Type}", queryName, firstElement.GetType().FullName);
                        if (firstElement is MongoDB.Bson.BsonString)
                        {
                            // Legacy format: List<string>
                            _logger.LogInformation("Query '{QueryName}' using legacy format (List<string>)", queryName);
                            var bsonParamNames = bsonArray.Select(e => e.AsString).ToList();
                            var missingParams = bsonParamNames.Where(p => !providedParameters.ContainsKey(p)).ToList();
                            if (missingParams.Any())
                            {
                                throw new DataGatewayException($"Missing required parameters: {string.Join(", ", missingParams)}");
                            }
                            foreach (var paramName in bsonParamNames)
                            {
                                if (providedParameters.ContainsKey(paramName))
                                {
                                    result[paramName] = providedParameters[paramName];
                                }
                            }
                            return result;
                        }
                        else if (firstElement is MongoDB.Bson.BsonDocument)
                        {
                            // New format: List<QueryParameterDefinition>
                            _logger.LogInformation("Query '{QueryName}' using new format (List<QueryParameterDefinition>)", queryName);
                            var bsonParamDefs = new List<QueryParameterDefinition>();
                            foreach (var element in bsonArray)
                            {
                                var doc = element.AsBsonDocument;
                                bsonParamDefs.Add(new QueryParameterDefinition
                                {
                                    name = doc.GetValue("name", "").AsString,
                                    type = doc.GetValue("type", "text").AsString,
                                    description = doc.Contains("description") ? doc["description"].AsString : null,
                                    required = doc.Contains("required") ? doc["required"].AsBoolean : true
                                });
                            }
                            _logger.LogInformation("Query '{QueryName}' parsed {Count} parameter definitions: {Params}", 
                                queryName, bsonParamDefs.Count, 
                                string.Join(", ", bsonParamDefs.Select(p => $"{p.name}({p.type}, required={p.required})")));
                            return ValidateAndConvertParameters(bsonParamDefs, providedParameters, queryName);
                        }
                        else
                        {
                            _logger.LogWarning("Query '{QueryName}' BsonArray first element is neither BsonString nor BsonDocument: {Type}", 
                                queryName, firstElement.GetType().FullName);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Query '{QueryName}' BsonArray is empty", queryName);
                    }
                }
                else
                {
                    _logger.LogWarning("Query '{QueryName}' parameterDefinitions is not BsonArray: {Type}", 
                        queryName, parameterDefinitions.GetType().FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse parameter definitions for query '{QueryName}'. Type: {Type}", 
                    queryName, parameterDefinitions?.GetType().FullName ?? "null");
                throw new DataGatewayException($"Invalid parameter definitions format for query '{queryName}': {ex.Message}", ex);
            }

            // If we reach here, parameterDefinitions is in an unknown format
            _logger.LogWarning("Unknown parameter definitions format for query '{QueryName}'. Type: {Type}, Value: {Value}", 
                queryName, 
                parameterDefinitions?.GetType().FullName ?? "null",
                parameterDefinitions?.ToString() ?? "null");
            throw new DataGatewayException($"Unknown parameter definitions format for query '{queryName}'. Expected List<string> or List<QueryParameterDefinition>.");
        }

        /// <summary>
        /// Convert parameter value based on type definition
        /// </summary>
        private object ConvertParameterByType(object value, string type, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentException($"Parameter '{paramName}' cannot be null");
            }

            // Handle JsonElement (from JSON deserialization)
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElementByType(jsonElement, type, paramName);
            }

            switch (type.ToLowerInvariant())
            {
                case "text":
                    return value.ToString() ?? string.Empty;

                case "number":
                    if (value is int || value is long || value is double || value is decimal || value is float)
                    {
                        return value;
                    }
                    if (value is string strValue)
                    {
                        if (int.TryParse(strValue, out var intVal))
                            return intVal;
                        if (long.TryParse(strValue, out var longVal))
                            return longVal;
                        if (double.TryParse(strValue, out var doubleVal))
                            return doubleVal;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to number: '{strValue}'");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a number, but received '{value.GetType().Name}'");

                case "bool":
                case "boolean":
                    if (value is bool boolVal)
                    {
                        return boolVal;
                    }
                    if (value is string boolStr)
                    {
                        if (bool.TryParse(boolStr, out var parsedBool))
                            return parsedBool;
                        if (boolStr.Equals("true", StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (boolStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                            return false;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to boolean: '{boolStr}'");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a boolean, but received '{value.GetType().Name}'");

                case "datetime":
                case "date":
                    if (value is DateTime dateTimeVal)
                    {
                        return dateTimeVal;
                    }
                    if (value is DateTimeOffset dateTimeOffsetVal)
                    {
                        return dateTimeOffsetVal.UtcDateTime;
                    }
                    // Reject numeric values for datetime (they should be strings in ISO 8601 format)
                    if (value is int || value is long || value is double || value is decimal || value is float)
                    {
                        throw new ArgumentException($"Parameter '{paramName}' must be a DateTime string (ISO 8601 format), but received a number: '{value}'. Use format like '2025-01-01T00:00:00Z'");
                    }
                    if (value is string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out var parsedDateTime))
                            return parsedDateTime;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to DateTime: '{dateStr}'. Expected ISO 8601 format (e.g., '2025-01-01T00:00:00Z')");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a DateTime, but received '{value.GetType().Name}'");

                default:
                    _logger.LogWarning("Unknown parameter type '{Type}' for parameter '{ParamName}', using value as-is", type, paramName);
                    return value;
            }
        }

        /// <summary>
        /// Convert JsonElement to the specified type
        /// </summary>
        private object ConvertJsonElementByType(JsonElement jsonElement, string type, string paramName)
        {
            switch (type.ToLowerInvariant())
            {
                case "text":
                    return jsonElement.GetString() ?? string.Empty;

                case "number":
                    if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        if (jsonElement.TryGetInt32(out var intVal))
                            return intVal;
                        if (jsonElement.TryGetInt64(out var longVal))
                            return longVal;
                        if (jsonElement.TryGetDouble(out var doubleVal))
                            return doubleVal;
                    }
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var strValue = jsonElement.GetString();
                        if (int.TryParse(strValue, out var intVal))
                            return intVal;
                        if (long.TryParse(strValue, out var longVal))
                            return longVal;
                        if (double.TryParse(strValue, out var doubleVal))
                            return doubleVal;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to number: '{strValue}'");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a number, but received JsonElement with ValueKind '{jsonElement.ValueKind}'");

                case "bool":
                case "boolean":
                    if (jsonElement.ValueKind == JsonValueKind.True)
                        return true;
                    if (jsonElement.ValueKind == JsonValueKind.False)
                        return false;
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var boolStr = jsonElement.GetString();
                        if (bool.TryParse(boolStr, out var parsedBool))
                            return parsedBool;
                        if (boolStr?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                            return true;
                        if (boolStr?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
                            return false;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to boolean: '{boolStr}'");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a boolean, but received JsonElement with ValueKind '{jsonElement.ValueKind}'");

                case "datetime":
                case "date":
                    // Reject numeric values for datetime
                    if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        throw new ArgumentException($"Parameter '{paramName}' must be a DateTime string (ISO 8601 format), but received a number: '{jsonElement}'. Use format like '2025-01-01T00:00:00Z'");
                    }
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var dateStr = jsonElement.GetString();
                        if (DateTime.TryParse(dateStr, out var parsedDateTime))
                            return parsedDateTime;
                        throw new ArgumentException($"Parameter '{paramName}' cannot be converted to DateTime: '{dateStr}'. Expected ISO 8601 format (e.g., '2025-01-01T00:00:00Z')");
                    }
                    throw new ArgumentException($"Parameter '{paramName}' must be a DateTime string, but received JsonElement with ValueKind '{jsonElement.ValueKind}'");

                default:
                    // For unknown types, try to get string value
                    if (jsonElement.ValueKind == JsonValueKind.String)
                        return jsonElement.GetString() ?? string.Empty;
                    return jsonElement.ToString();
            }
        }

        /// <summary>
        /// Search in relation collections and return matching IDs grouped by field name
        /// </summary>
        private async Task<Dictionary<string, List<BsonValue>>> SearchInRelationCollectionsAsync(
            DatasetSchema schema,
            string databaseName,
            string searchTerm)
        {
            var relationSearchIds = new Dictionary<string, List<BsonValue>>();

            var relationFields = schema.fields
                .Where(f => f.fieldType == "relation" && !string.IsNullOrEmpty(f.relationDataset))
                .ToList();

            if (relationFields.Count == 0)
            {
                return relationSearchIds;
            }

            _logger.LogDebug(
                "Searching in {Count} relation collections for term: {SearchTerm}",
                relationFields.Count, searchTerm);

            // Escape special regex characters in search term
            var escapedTerm = System.Text.RegularExpressions.Regex.Escape(searchTerm);
            var regexPattern = new BsonRegularExpression(escapedTerm, "i"); // case-insensitive

            foreach (var relationField in relationFields)
            {
                try
                {
                    // Load relation schema
                    var relationSchema = await LoadSchemaAsync(relationField.relationDataset!, databaseName);

                    // Find text fields in relation schema
                    var textFields = relationSchema.fields
                        .Where(f => f.fieldType == "text" && !f.name.StartsWith("__"))
                        .ToList();

                    if (textFields.Count == 0)
                    {
                        _logger.LogDebug(
                            "No searchable text fields found in relation dataset {RelationDataset} for field {FieldName}",
                            relationField.relationDataset, relationField.name);
                        continue;
                    }

                    // Build search pipeline for relation collection
                    var searchPipeline = new List<BsonDocument>();

                    // Build $or conditions for text fields
                    var orConditions = new BsonArray();
                    foreach (var textField in textFields)
                    {
                        orConditions.Add(new BsonDocument(textField.name, regexPattern));
                    }

                    // Add $match stage
                    searchPipeline.Add(new BsonDocument("$match", new BsonDocument("$or", orConditions)));

                    // Add $project stage to only return __dataId
                    searchPipeline.Add(new BsonDocument("$project", new BsonDocument("__dataId", 1)));

                    // Execute search in relation collection
                    var relationResults = await _dataRepository.AggregateAsync(
                        databaseName,
                        relationSchema.CollectionName,
                        searchPipeline);

                    // Collect matching IDs
                    var matchingIds = relationResults
                        .Where(doc => doc.Contains("__dataId"))
                        .Select(doc => doc["__dataId"])
                        .Where(id => id != null && !id.IsBsonNull)
                        .ToList();

                    if (matchingIds.Count > 0)
                    {
                        relationSearchIds[relationField.name] = matchingIds;
                        _logger.LogDebug(
                            "Found {Count} matching documents in relation dataset {RelationDataset} for field {FieldName}",
                            matchingIds.Count, relationField.relationDataset, relationField.name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to search in relation dataset {RelationDataset} for field {FieldName}",
                        relationField.relationDataset, relationField.name);
                    // Continue with other relation fields
                }
            }

            return relationSearchIds;
        }

        #endregion
    }
}

