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

            var clauses = new List<(string Field, BsonValue Condition)>();

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
                        clauses.Add((fieldName, condition));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse filter: {Filter}", filter);
                }
            }

            return BuildMatchDocument(clauses);
        }

        private static BsonDocument? BuildMatchDocument(List<(string Field, BsonValue Condition)> clauses)
        {
            if (clauses.Count == 0)
                return null;

            var andParts = new BsonArray();

            foreach (var group in clauses.GroupBy(c => c.Field, StringComparer.Ordinal))
            {
                var conditions = group.Select(g => g.Condition).ToList();
                if (conditions.Count == 1)
                {
                    andParts.Add(new BsonDocument(group.Key, conditions[0]));
                    continue;
                }

                if (TryMergeOperatorDocuments(conditions, out var merged))
                {
                    andParts.Add(new BsonDocument(group.Key, merged));
                    continue;
                }

                foreach (var condition in conditions)
                {
                    andParts.Add(new BsonDocument(group.Key, condition));
                }
            }

            if (andParts.Count == 0)
                return null;

            if (andParts.Count == 1)
                return andParts[0].AsBsonDocument;

            return new BsonDocument("$and", andParts);
        }

        /// <summary>
        /// Merge operator docs on the same field (e.g. gte + lte → single range condition).
        /// </summary>
        private static bool TryMergeOperatorDocuments(IReadOnlyList<BsonValue> conditions, out BsonDocument merged)
        {
            merged = new BsonDocument();
            foreach (var condition in conditions)
            {
                if (!condition.IsBsonDocument)
                    return false;

                foreach (var element in condition.AsBsonDocument.Elements)
                {
                    if (!element.Name.StartsWith("$", StringComparison.Ordinal))
                        return false;
                    if (merged.Contains(element.Name))
                        return false;
                    merged[element.Name] = element.Value;
                }
            }

            return merged.ElementCount > 0;
        }

        /// <summary>
        /// Build MongoDB condition from operator and value
        /// </summary>
        private BsonValue? BuildCondition(string fieldName, string operatorStr, string valueStr)
        {
            var comparisonValue = NormalizeDateComparisonValue(operatorStr, valueStr);
            var comparisonOperand = ParseComparisonOperand(operatorStr, comparisonValue, valueStr);

            return operatorStr switch
            {
                "eq" when IsDateOnly(valueStr) => new BsonDocument("$regex", new BsonString($"^{Regex.Escape(valueStr)}")).Add("$options", "i"),
                "eq" => ParseValue(comparisonValue),
                "ne" => new BsonDocument("$ne", comparisonOperand),
                "gt" => new BsonDocument("$gt", comparisonOperand),
                "gte" => new BsonDocument("$gte", comparisonOperand),
                "lt" => new BsonDocument("$lt", comparisonOperand),
                "lte" => new BsonDocument("$lte", comparisonOperand),
                "in" => new BsonDocument("$in", ParseArrayValue(valueStr)),
                "nin" => new BsonDocument("$nin", ParseArrayValue(valueStr)),
                "contains" => new BsonDocument("$regex", new BsonString(valueStr)).Add("$options", "i"),
                "startswith" => new BsonDocument("$regex", new BsonString($"^{valueStr}")).Add("$options", "i"),
                "endswith" => new BsonDocument("$regex", new BsonString($"{valueStr}$")).Add("$options", "i"),
                _ => null
            };
        }

        /// <summary>
        /// Datetime fields are stored as BSON Date in MongoDB; range operators must use BsonDateTime.
        /// Plain date (YYYY-MM-DD) and ISO datetime strings are parsed for gte/lte/gt/lt.
        /// </summary>
        private BsonValue ParseComparisonOperand(string operatorStr, string comparisonValue, string rawValue)
        {
            if (operatorStr is "gt" or "gte" or "lt" or "lte"
                && TryParseFilterDateTime(comparisonValue, out var dateTime))
            {
                return new BsonDateTime(dateTime);
            }

            return ParseValue(comparisonValue);
        }

        private static bool TryParseFilterDateTime(string valueStr, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(valueStr))
                return false;

            var trimmed = valueStr.Trim();

            if (DateTime.TryParseExact(
                    trimmed,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateOnly))
            {
                utc = DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
                return true;
            }

            if (!IsIsoDateTime(trimmed))
                return false;

            if (!DateTime.TryParse(
                    trimmed,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return false;
            }

            utc = parsed.Kind switch
            {
                DateTimeKind.Utc => parsed,
                DateTimeKind.Local => parsed.ToUniversalTime(),
                _ => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
            };
            return true;
        }

        private static bool IsIsoDateTime(string valueStr) =>
            !string.IsNullOrWhiteSpace(valueStr)
            && valueStr.Contains('T', StringComparison.Ordinal)
            && DateTime.TryParse(
                valueStr.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _);

        /// <summary>
        /// Parse value string to BsonValue (try number, bool, then string).
        /// </summary>
        private BsonValue ParseValue(string valueStr)
        {
            if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                return intValue;
            if (long.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                return longValue;
            if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            if (bool.TryParse(valueStr, out var boolValue))
                return boolValue;

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
                "lte" => $"{valueStr}T23:59:59.999Z",
                "gt" => $"{valueStr}T23:59:59.999Z",
                "gte" => $"{valueStr}T00:00:00.000Z",
                _ => valueStr
            };
        }

        /// <summary>
        /// Parse array value (comma-separated or JSON array)
        /// </summary>
        private BsonArray ParseArrayValue(string valueStr)
        {
            var array = new BsonArray();

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
