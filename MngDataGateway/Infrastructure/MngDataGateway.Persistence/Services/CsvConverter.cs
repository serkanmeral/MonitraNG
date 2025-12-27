using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Converts data dictionaries to CSV format
    /// Handles flattening of nested objects, arrays, and relation fields
    /// </summary>
    public class CsvConverter
    {
        private readonly ILogger<CsvConverter> _logger;

        public CsvConverter(ILogger<CsvConverter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Convert list of dictionaries to CSV string
        /// </summary>
        public string ConvertToCsv(List<Dictionary<string, object>> data)
        {
            if (data == null || data.Count == 0)
            {
                return string.Empty;
            }

            // Collect all possible column names from all rows
            var allColumns = new HashSet<string>();
            foreach (var row in data)
            {
                CollectColumns(row, allColumns, string.Empty);
            }

            var columns = allColumns.OrderBy(c => c).ToList();

            // Build CSV
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join(",", columns.Select(c => EscapeCsvValue(c))));

            // Data rows
            foreach (var row in data)
            {
                var values = new List<string>();
                foreach (var column in columns)
                {
                    var value = GetValue(row, column);
                    values.Add(EscapeCsvValue(value));
                }
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Collect all column names from a dictionary (recursive for nested objects)
        /// </summary>
        private void CollectColumns(Dictionary<string, object> dict, HashSet<string> columns, string prefix)
        {
            foreach (var kvp in dict)
            {
                var key = kvp.Key;

                // Skip internal fields
                if (key.StartsWith("__"))
                {
                    continue;
                }

                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

                if (kvp.Value == null)
                {
                    columns.Add(fullKey);
                }
                else if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    // Nested object - flatten recursively
                    CollectColumns(nestedDict, columns, fullKey);
                }
                else if (kvp.Value is List<object> list)
                {
                    // Array - handle as comma-separated string
                    columns.Add(fullKey);
                }
                else
                {
                    // Simple value
                    columns.Add(fullKey);
                }
            }
        }

        /// <summary>
        /// Get value from dictionary by column path (supports nested paths like "publisher.name")
        /// </summary>
        private string GetValue(Dictionary<string, object> dict, string columnPath)
        {
            if (string.IsNullOrEmpty(columnPath))
            {
                return string.Empty;
            }

            var parts = columnPath.Split('.');
            object? current = dict;

            // Navigate through nested objects
            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                {
                    return string.Empty;
                }

                if (current is Dictionary<string, object> currentDict)
                {
                    if (!currentDict.TryGetValue(parts[i], out var value))
                    {
                        return string.Empty;
                    }
                    current = value;
                }
                else
                {
                    return string.Empty;
                }
            }

            // Convert value to string
            return ConvertValueToString(current);
        }

        /// <summary>
        /// Convert value to CSV string representation
        /// </summary>
        private string ConvertValueToString(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            // Handle arrays/lists
            if (value is List<object> list)
            {
                // Convert array to comma-separated string
                var items = new List<string>();
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> itemDict)
                    {
                        // For relation arrays (like genres), extract name or first text field
                        var nameValue = itemDict.GetValueOrDefault("name")?.ToString() 
                                     ?? itemDict.Values.FirstOrDefault(v => v is string)?.ToString();
                        if (!string.IsNullOrEmpty(nameValue))
                        {
                            items.Add(nameValue);
                        }
                    }
                    else
                    {
                        items.Add(item?.ToString() ?? string.Empty);
                    }
                }
                return string.Join(", ", items);
            }

            // Handle nested objects (shouldn't happen if we flattened correctly, but handle just in case)
            if (value is Dictionary<string, object> nestedDict)
            {
                // For relation objects, prefer "name" field, otherwise first text field
                if (nestedDict.TryGetValue("name", out var nameValue))
                {
                    return nameValue?.ToString() ?? string.Empty;
                }

                // Try to find first text field
                var firstTextValue = nestedDict.Values
                    .FirstOrDefault(v => v is string && !string.IsNullOrEmpty(v.ToString()));
                return firstTextValue?.ToString() ?? string.Empty;
            }

            // Handle dates
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            // Handle booleans
            if (value is bool boolValue)
            {
                return boolValue.ToString().ToLowerInvariant();
            }

            // Default: convert to string
            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Escape CSV value (handle quotes, commas, newlines)
        /// </summary>
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // If value contains comma, quote, or newline, wrap in quotes and escape quotes
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}

