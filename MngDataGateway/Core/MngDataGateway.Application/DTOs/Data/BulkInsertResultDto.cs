using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Result of bulk insert operation
    /// </summary>
    public class BulkInsertResultDto
    {
        /// <summary>
        /// Total number of items in the request
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Number of successfully inserted items
        /// </summary>
        public int Successful { get; set; }

        /// <summary>
        /// Number of failed items
        /// </summary>
        public int Failed { get; set; }

        /// <summary>
        /// Successfully inserted items with generated fields (__dataId, incremental fields, etc.)
        /// </summary>
        public List<Dictionary<string, object>> Items { get; set; } = new();

        /// <summary>
        /// Failed items with error details
        /// </summary>
        public List<BulkInsertErrorDto> Errors { get; set; } = new();
    }

    /// <summary>
    /// Error details for a failed item in bulk insert
    /// </summary>
    public class BulkInsertErrorDto
    {
        /// <summary>
        /// Zero-based index of the failed item in the original request
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The original item data (for reference)
        /// </summary>
        public Dictionary<string, object>? Item { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Detailed error information (validation errors, etc.)
        /// </summary>
        public List<object>? Details { get; set; }
    }
}

