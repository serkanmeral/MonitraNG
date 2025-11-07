using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Incremental field service implementation
    /// Counter storage: @__counters collection
    /// Counter document: { _id: "counterKey", value: 123, lastUpdated: DateTime }
    /// </summary>
    public class IncrementalFieldService : IIncrementalFieldService
    {
        private readonly ILogger<IncrementalFieldService> _logger;
        private readonly IMongoClient _mongoClient;
        private const string COUNTERS_COLLECTION = "@__counters";

        public IncrementalFieldService(
            ILogger<IncrementalFieldService> logger,
            IMongoClient mongoClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        }

        public async Task<string> GenerateValueAsync(
            DatasetSchema schema,
            FieldDefinition field,
            Dictionary<string, object> data,
            string databaseName,
            IClientSessionHandle? session = null)
        {
            if (field.fieldType != "incremental")
                throw new ArgumentException($"Field '{field.name}' is not an incremental field", nameof(field));

            if (field.incrementalOptions == null || string.IsNullOrWhiteSpace(field.incrementalOptions.format))
                throw new InvalidOperationException($"Incremental field '{field.name}' has no format defined");

            try
            {
                // 1. Parse format and resolve placeholders
                var format = field.incrementalOptions.format;
                var resolvedPrefix = ResolvePlaceholders(format, data, out var counterPlaceholder);

                // 2. Build counter key
                var counterKey = BuildCounterKey(schema.DatasetName, field.name, resolvedPrefix);

                // 3. Get next counter value (atomic increment)
                var counterValue = await GetNextCounterValueAsync(
                    databaseName,
                    counterKey,
                    field.incrementalOptions.startValue,
                    field.incrementalOptions.incrementStep,
                    session);

                // 4. Format final value
                var finalValue = FormatValue(format, resolvedPrefix, counterValue, counterPlaceholder);

                _logger.LogDebug(
                    "Generated incremental value: {Value} (Counter: {CounterKey} = {CounterValue})",
                    finalValue, counterKey, counterValue);

                return finalValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate incremental value for field {FieldName} in dataset {DatasetName}",
                    field.name, schema.DatasetName);
                throw;
            }
        }

        /// <summary>
        /// Resolve placeholders in format template
        /// Supported: {year}, {month}, {day}, {yy}, {mm}, {dd}, {fieldName}
        /// Returns: resolved prefix and counter placeholder info
        /// </summary>
        private string ResolvePlaceholders(
            string format,
            Dictionary<string, object> data,
            out string counterPlaceholder)
        {
            var now = DateTime.UtcNow;
            var resolved = format;

            // Date placeholders
            resolved = resolved.Replace("{year}", now.Year.ToString("D4"));
            resolved = resolved.Replace("{yy}", now.ToString("yy"));
            resolved = resolved.Replace("{month}", now.Month.ToString("D2"));
            resolved = resolved.Replace("{mm}", now.Month.ToString("D2"));
            resolved = resolved.Replace("{day}", now.Day.ToString("D2"));
            resolved = resolved.Replace("{dd}", now.Day.ToString("D2"));

            // Field placeholders (e.g., {projectCode})
            foreach (var key in data.Keys)
            {
                var placeholder = $"{{{key}}}";
                if (resolved.Contains(placeholder) && data[key] != null)
                {
                    resolved = resolved.Replace(placeholder, data[key].ToString());
                }
            }

            // Extract counter placeholder (e.g., {0:D6}, {0:D4})
            var counterMatch = Regex.Match(resolved, @"\{0(:D\d+)?\}");
            counterPlaceholder = counterMatch.Success ? counterMatch.Value : "{0}";

            return resolved;
        }

        /// <summary>
        /// Build counter key for scope isolation
        /// Pattern: {datasetName}.{fieldName}.{prefix}
        /// Example: @tasks.taskNumber.TASK-202511
        /// </summary>
        private string BuildCounterKey(string datasetName, string fieldName, string resolvedPrefix)
        {
            // Extract prefix part (everything before counter placeholder)
            var counterIndex = resolvedPrefix.IndexOf("{0", StringComparison.Ordinal);
            var prefix = counterIndex > 0 ? resolvedPrefix.Substring(0, counterIndex) : string.Empty;

            // Remove trailing separators
            prefix = prefix.TrimEnd('-', '_', '.');

            if (string.IsNullOrEmpty(prefix))
                return $"{datasetName}.{fieldName}";

            return $"{datasetName}.{fieldName}.{prefix}";
        }

        /// <summary>
        /// Get next counter value with atomic increment
        /// Uses findOneAndUpdate for atomicity
        /// </summary>
        private async Task<long> GetNextCounterValueAsync(
            string databaseName,
            string counterKey,
            int startValue,
            int incrementStep,
            IClientSessionHandle? session)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(COUNTERS_COLLECTION);

            var filter = Builders<BsonDocument>.Filter.Eq("_id", counterKey);
            
            // Check if counter exists first
            var existingCounter = await collection.Find(filter).FirstOrDefaultAsync();
            
            UpdateDefinition<BsonDocument> update;
            if (existingCounter == null)
            {
                // First time - set initial value
                update = Builders<BsonDocument>.Update
                    .Set("_id", counterKey)
                    .Set("value", startValue)
                    .Set("lastUpdated", DateTime.UtcNow);
            }
            else
            {
                // Increment existing
                update = Builders<BsonDocument>.Update
                    .Inc("value", incrementStep)
                    .Set("lastUpdated", DateTime.UtcNow);
            }

            var options = new FindOneAndUpdateOptions<BsonDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            BsonDocument result;
            if (session != null)
            {
                result = await collection.FindOneAndUpdateAsync(session, filter, update, options);
            }
            else
            {
                result = await collection.FindOneAndUpdateAsync(filter, update, options);
            }

            if (result == null || !result.Contains("value"))
            {
                throw new InvalidOperationException($"Failed to increment counter '{counterKey}'");
            }

            return result["value"].ToInt64();
        }

        /// <summary>
        /// Format final value with counter
        /// Example: "TASK-{0:D6}" with counter=5 → "TASK-000005"
        /// </summary>
        private string FormatValue(string originalFormat, string resolvedPrefix, long counterValue, string counterPlaceholder)
        {
            // Extract format specifier (e.g., "D6" from "{0:D6}")
            var formatMatch = Regex.Match(counterPlaceholder, @"\{0:([^\}]+)\}");
            string formattedCounter;

            if (formatMatch.Success)
            {
                var formatSpec = formatMatch.Groups[1].Value; // "D6"
                formattedCounter = counterValue.ToString(formatSpec, CultureInfo.InvariantCulture);
            }
            else
            {
                formattedCounter = counterValue.ToString();
            }

            // Replace counter placeholder with formatted value
            return resolvedPrefix.Replace(counterPlaceholder, formattedCounter);
        }
    }
}

