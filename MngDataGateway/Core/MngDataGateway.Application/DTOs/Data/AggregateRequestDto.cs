using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Request DTO for POST /api/data/{datasetName}/aggregate endpoint
    /// </summary>
    public class AggregateRequestDto
    {
        /// <summary>
        /// MongoDB aggregate pipeline array
        /// </summary>
        public List<object> Pipeline { get; set; } = new();
    }
}

