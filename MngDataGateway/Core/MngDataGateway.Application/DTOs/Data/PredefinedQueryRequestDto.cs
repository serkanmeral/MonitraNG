using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Request DTO for POST /api/data/{datasetName}/queries/{queryName} endpoint
    /// Simple key-value pairs for query parameters
    /// </summary>
    public class PredefinedQueryRequestDto : Dictionary<string, object>
    {
    }
}

