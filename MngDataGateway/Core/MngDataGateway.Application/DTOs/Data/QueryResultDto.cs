using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Result DTO for query operations with optional pipeline
    /// </summary>
    public class QueryResultDto
    {
        /// <summary>
        /// Query result data (always array)
        /// </summary>
        public List<Dictionary<string, object>> Data { get; set; } = new();

        /// <summary>
        /// Aggregate pipeline (only when showQuery=true)
        /// </summary>
        public List<object>? Query { get; set; }
    }
}

