using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Parser for RESTful filter query parameter
    /// Format: ?filter=field1:operator:value,field2:operator:value
    /// Operators: eq, ne, gt, gte, lt, lte, in, nin, contains, startsWith, endsWith
    /// </summary>
    public class FilterParser
    {
        private readonly ILogger<FilterParser> _logger;

        public FilterParser(ILogger<FilterParser> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parse filter string into MongoDB match filter
        /// </summary>
        public BsonDocument? Parse(string? filterString)
        {
            if (string.IsNullOrWhiteSpace(filterString))
                return null;

            var filters = filterString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (filters.Length == 0)
                return null;

            var matchFilter = new BsonDocument();

            foreach (var filter in filters)
            {
                try
                {
                    var parts = filter.Split(':', 3);
                    if (parts.Length != 3)
                    {
                        _logger.LogWarning("Invalid filter format: {Filter}. Expected format: field:operator:value", filter);
                        continue;
                    }

                    var fieldName = parts[0].Trim();
                    var operatorStr = parts[1].Trim().ToLower();
                    var valueStr = parts[2].Trim();

                    var condition = BuildCondition(fieldName, operatorStr, valueStr);
                    if (condition != null)
                    {
                        matchFilter[fieldName] = condition;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse filter: {Filter}", filter);
                }
            }

            return matchFilter.ElementCount > 0 ? matchFilter : null;
        }

        /// <summary>
        /// Build MongoDB condition from operator and value
        /// </summary>
        private BsonValue? BuildCondition(string fieldName, string operatorStr, string valueStr)
        {
            return operatorStr switch
            {
                "eq" => ParseValue(valueStr),
                "ne" => new BsonDocument("$ne", ParseValue(valueStr)),
                "gt" => new BsonDocument("$gt", ParseValue(valueStr)),
                "gte" => new BsonDocument("$gte", ParseValue(valueStr)),
                "lt" => new BsonDocument("$lt", ParseValue(valueStr)),
                "lte" => new BsonDocument("$lte", ParseValue(valueStr)),
                "in" => new BsonDocument("$in", ParseArrayValue(valueStr)),
                "nin" => new BsonDocument("$nin", ParseArrayValue(valueStr)),
                "contains" => new BsonDocument("$regex", new BsonString(valueStr)).Add("$options", "i"),
                "startswith" => new BsonDocument("$regex", new BsonString($"^{valueStr}")).Add("$options", "i"),
                "endswith" => new BsonDocument("$regex", new BsonString($"{valueStr}$")).Add("$options", "i"),
                _ => null
            };
        }

        /// <summary>
        /// Parse value string to BsonValue (try number, bool, then string)
        /// </summary>
        private BsonValue ParseValue(string valueStr)
        {
            // Try number
            if (int.TryParse(valueStr, out var intValue))
                return intValue;
            if (long.TryParse(valueStr, out var longValue))
                return longValue;
            if (double.TryParse(valueStr, out var doubleValue))
                return doubleValue;

            // Try bool
            if (bool.TryParse(valueStr, out var boolValue))
                return boolValue;

            // Try DateTime
            if (DateTime.TryParse(valueStr, out var dateValue))
                return dateValue;

            // Default to string
            return valueStr;
        }

        /// <summary>
        /// Parse array value (comma-separated or JSON array)
        /// </summary>
        private BsonArray ParseArrayValue(string valueStr)
        {
            var array = new BsonArray();

            // Try to parse as JSON array first
            if (valueStr.TrimStart().StartsWith("["))
            {
                try
                {
                    var bsonDoc = BsonDocument.Parse($"{{ \"array\": {valueStr} }}");
                    if (bsonDoc.Contains("array") && bsonDoc["array"].IsBsonArray)
                    {
                        return bsonDoc["array"].AsBsonArray;
                    }
                }
                catch
                {
                    // Fall through to comma-separated parsing
                }
            }

            // Parse as comma-separated values
            var values = valueStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var value in values)
            {
                array.Add(ParseValue(value.Trim()));
            }

            return array;
        }
    }
}

