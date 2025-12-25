using System;
using System.Collections.Generic;
using System.Linq;
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
            SortParser sortParser)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _dataProcessService = dataProcessService ?? throw new ArgumentNullException(nameof(dataProcessService));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
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

                // 5. Transaction decision
                var needsTransaction = DetermineTransactionNeed(schema);

                // 6. Insert data
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
                    _ = _notificationService.PublishDataCreatedEventAsync(
                        domainName, databaseName, schema, data, userId, userEmail, ipAddress);
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

            return BsonDocumentToDictionary(document);
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

            var data = documents.Select(BsonDocumentToDictionary).ToList();

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
                var result = updatedDoc != null ? BsonDocumentToDictionary(updatedDoc) : null;

                // 7. Publish event (async)
                if (ShouldPublishEvent(schema) && result != null)
                {
                    _ = _notificationService.PublishDataUpdatedEventAsync(
                        domainName, databaseName, schema, result, userId, userEmail, ipAddress);
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
            
            var success = await _dataRepository.SoftDeleteAsync(
                databaseName, schema.CollectionName, dataId, userId, userEmail, ipAddress);

            if (!success)
                return false;

            // Publish event (async)
            if (ShouldPublishEvent(schema))
            {
                _ = _notificationService.PublishDataDeletedEventAsync(
                    domainName, databaseName, schema, dataId, userId, userEmail, ipAddress);
            }

            _logger.LogInformation(
                "Deleted data in dataset {DatasetName} with __dataId: {DataId}",
                datasetName, dataId);

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
            
            var success = await _dataRepository.RestoreAsync(
                databaseName, schema.CollectionName, dataId, userId, userEmail, ipAddress);

            if (!success)
                return false;

            // Publish event (async)
            if (ShouldPublishEvent(schema))
            {
                _ = _notificationService.PublishDataRestoredEventAsync(
                    domainName, databaseName, schema, dataId, userId, userEmail, ipAddress);
            }

            _logger.LogInformation(
                "Restored data in dataset {DatasetName} with __dataId: {DataId}",
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
                        schema, validItems, databaseName, userId, userEmail, ipAddress, processedItems);
                }
                else
                {
                    // Transaction olmadan process et
                    foreach (var (index, item) in validItems)
                    {
                        var processedItem = new Dictionary<string, object>(item);
                        _dataProcessService.ApplyDefaultValues(schema, processedItem);
                        _dataProcessService.GenerateMetadata(schema, processedItem, userId, userEmail, ipAddress);
                        await _dataProcessService.GenerateIncrementalFieldsAsync(schema, processedItem, databaseName, session: null);
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
                        _ = _notificationService.PublishDataCreatedEventAsync(
                            domainName, databaseName, schema, item, userId, userEmail, ipAddress);
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
                    // Generate incremental fields inside transaction
                    await _dataProcessService.GenerateIncrementalFieldsAsync(
                        schema, data, databaseName, session);

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
            // Generate incremental fields without transaction
            await _dataProcessService.GenerateIncrementalFieldsAsync(
                schema, data, databaseName, session: null);

            // Insert data
            await _dataRepository.InsertOneAsync(
                databaseName, schema.CollectionName, data, session: null);
        }

        private async Task ProcessAndInsertItemsInTransactionAsync(
            DatasetSchema schema,
            List<(int index, Dictionary<string, object> item)> validItems,
            string databaseName,
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
                            schema, processedItem, databaseName, session);
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
                        schema, processedItem, databaseName, session: null);
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

        private Dictionary<string, object> BsonDocumentToDictionary(BsonDocument document)
        {
            var dictionary = new Dictionary<string, object>();

            foreach (var element in document)
            {
                dictionary[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
            }

            return dictionary;
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

                // Build pipeline
                var builder = new AggregatePipelineBuilder(
                    _loggerFactory.CreateLogger<AggregatePipelineBuilder>(),
                    schema,
                    databaseName);

                builder
                    .AddMatch(matchFilter)
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
                var data = results.Select(BsonDocumentToDictionary).ToList();

                _logger.LogDebug(
                    "Query executed on dataset {DatasetName}, returned {Count} documents",
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
                    data.Add(BsonDocumentToDictionary(result));
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

                // Build pipeline
                var builder = new AggregatePipelineBuilder(
                    _loggerFactory.CreateLogger<AggregatePipelineBuilder>(),
                    schema,
                    databaseName);

                builder
                    .AddMatch(matchFilter)
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
                var data = results.Select(BsonDocumentToDictionary).ToList();

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

                // Validate parameters
                var requiredParams = queryDef.parameters ?? new List<string>();
                if (requiredParams.Any())
                {
                    var missingParams = requiredParams.Where(p => !parameters.ContainsKey(p)).ToList();
                    if (missingParams.Any())
                    {
                        throw new DataGatewayException($"Missing required parameters: {string.Join(", ", missingParams)}");
                    }
                }

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
                            var originalStageJson = System.Text.Json.JsonSerializer.Serialize(stage);
                            _logger.LogDebug("Original stage type: {StageType}, Content: {StageJson}", 
                                stage?.GetType().FullName ?? "null", 
                                originalStageJson);
                            
                            // Replace parameters in the stage object before serialization
                            var stageWithParams = ReplaceParametersInObject(stage, parameters);
                            
                            // Log stage after replacement (before serialization)
                            _logger.LogDebug("Stage after replacement (object): Type: {Type}, Value: {Value}", 
                                stageWithParams?.GetType().FullName ?? "null",
                                System.Text.Json.JsonSerializer.Serialize(stageWithParams));
                            
                            // Serialize stage to JSON string
                            var stageJson = System.Text.Json.JsonSerializer.Serialize(stageWithParams);
                            _logger.LogDebug("Stage after parameter replacement (JSON): {StageJson}", stageJson);
                            
                            // Validate stage is not empty
                            if (string.IsNullOrWhiteSpace(stageJson) || stageJson == "{}" || stageJson == "[]")
                            {
                                _logger.LogWarning("Skipping empty stage for query '{QueryName}'", queryName);
                                continue;
                            }
                            
                            // Parse to BsonDocument
                            var stageBson = BsonDocument.Parse(stageJson);
                            
                            // Validate BsonDocument is not empty
                            if (stageBson.ElementCount == 0)
                            {
                                _logger.LogWarning("Skipping empty BsonDocument stage for query '{QueryName}'", queryName);
                                continue;
                            }
                            
                            pipeline.Add(stageBson);
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

                // Execute aggregate
                var results = await _dataRepository.AggregateAsync(
                    databaseName,
                    schema.CollectionName,
                    pipeline);

                // Convert to dictionary list
                var data = results.Select(BsonDocumentToDictionary).ToList();

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
                var data = results.Select(BsonDocumentToDictionary).ToList();

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
                    return element.TryGetInt64(out var longVal) ? longVal : element.GetDouble();
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

        #endregion
    }
}

