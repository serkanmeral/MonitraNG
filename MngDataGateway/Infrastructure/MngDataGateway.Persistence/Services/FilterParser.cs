using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

            var filters = SplitFilterClauses(filterString);
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
            var comparisonValue = NormalizeDateComparisonValue(operatorStr, valueStr);

            return operatorStr switch
            {
                "eq" when IsDateOnly(valueStr) => new BsonDocument("$regex", new BsonString($"^{Regex.Escape(valueStr)}")).Add("$options", "i"),
                "eq" => ParseValue(comparisonValue),
                "ne" => new BsonDocument("$ne", ParseValue(comparisonValue)),
                "gt" => new BsonDocument("$gt", ParseValue(comparisonValue)),
                "gte" => new BsonDocument("$gte", ParseValue(comparisonValue)),
                "lt" => new BsonDocument("$lt", ParseValue(comparisonValue)),
                "lte" => new BsonDocument("$lte", ParseValue(comparisonValue)),
                "in" => new BsonDocument("$in", ParseArrayValue(valueStr)),
                "nin" => new BsonDocument("$nin", ParseArrayValue(valueStr)),
                "contains" => new BsonDocument("$regex", new BsonString(valueStr)).Add("$options", "i"),
                "startswith" => new BsonDocument("$regex", new BsonString($"^{valueStr}")).Add("$options", "i"),
                "endswith" => new BsonDocument("$regex", new BsonString($"{valueStr}$")).Add("$options", "i"),
                _ => null
            };
        }

        /// <summary>
        /// Parse value string to BsonValue (try number, bool, then string).
        /// ISO date strings stay as strings so filters match ISO string fields in MongoDB.
        /// </summary>
        private BsonValue ParseValue(string valueStr)
        {
            // Try number
            if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                return intValue;
            if (long.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                return longValue;
            if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            // Try bool
            if (bool.TryParse(valueStr, out var boolValue))
                return boolValue;

            // Default to string (including ISO date/datetime values)
            return valueStr;
        }

        private static bool IsDateOnly(string valueStr) =>
            !string.IsNullOrWhiteSpace(valueStr)
            && DateTime.TryParseExact(
                valueStr.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);

        /// <summary>
        /// Expand calendar-day filters for ISO string datetime fields (gte/lte/gt on YYYY-MM-DD).
        /// </summary>
        private static string NormalizeDateComparisonValue(string operatorStr, string valueStr)
        {
            if (!IsDateOnly(valueStr))
                return valueStr;

            return operatorStr switch
            {
                "lte" => $"{valueStr}T23:59:59.9999999Z",
                "gt" => $"{valueStr}T23:59:59.9999999Z",
                _ => valueStr
            };
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

        /// <summary>
        /// Split filter clauses on top-level commas only (commas inside JSON array values are preserved).
        /// </summary>
        private static string[] SplitFilterClauses(string filterString)
        {
            var clauses = new List<string>();
            var current = new System.Text.StringBuilder();
            var bracketDepth = 0;

            foreach (var ch in filterString)
            {
                if (ch == '[')
                    bracketDepth++;
                else if (ch == ']' && bracketDepth > 0)
                    bracketDepth--;

                if (ch == ',' && bracketDepth == 0)
                {
                    if (current.Length > 0)
                    {
                        clauses.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                clauses.Add(current.ToString());

            return clauses
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToArray();
        }
    }
}

