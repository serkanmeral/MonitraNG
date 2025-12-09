using System;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Parser for MongoDB-style sort query parameter
    /// Format: ?sort=field1,-field2,field3
    /// - prefix means descending, no prefix means ascending
    /// </summary>
    public class SortParser
    {
        private readonly ILogger<SortParser> _logger;

        public SortParser(ILogger<SortParser> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parse sort string into MongoDB sort document
        /// </summary>
        public BsonDocument? Parse(string? sortString)
        {
            if (string.IsNullOrWhiteSpace(sortString))
                return null;

            var sortFields = sortString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (sortFields.Length == 0)
                return null;

            var sortDocument = new BsonDocument();

            foreach (var field in sortFields)
            {
                try
                {
                    var trimmedField = field.Trim();
                    if (string.IsNullOrEmpty(trimmedField))
                        continue;

                    // Check if descending (starts with -)
                    if (trimmedField.StartsWith("-"))
                    {
                        var fieldName = trimmedField.Substring(1).Trim();
                        if (!string.IsNullOrEmpty(fieldName))
                        {
                            sortDocument[fieldName] = -1; // Descending
                        }
                    }
                    else
                    {
                        // Ascending
                        sortDocument[trimmedField] = 1;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse sort field: {Field}", field);
                }
            }

            return sortDocument.ElementCount > 0 ? sortDocument : null;
        }
    }
}

