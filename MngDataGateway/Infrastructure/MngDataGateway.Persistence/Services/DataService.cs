using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Exceptions;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Main data service orchestrator
    /// Coordinates validation, processing, storage, and notification
    /// </summary>
    public class DataService : IDataService
    {
        private readonly ILogger<DataService> _logger;
        private readonly IDatasetService _datasetService;
        private readonly IValidationService _validationService;
        private readonly IDataProcessService _dataProcessService;
        private readonly IDataRepository _dataRepository;
        private readonly INotificationService _notificationService;

        public DataService(
            ILogger<DataService> logger,
            IDatasetService datasetService,
            IValidationService validationService,
            IDataProcessService dataProcessService,
            IDataRepository dataRepository,
            INotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _dataProcessService = dataProcessService ?? throw new ArgumentNullException(nameof(dataProcessService));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
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

        #endregion
    }
}

