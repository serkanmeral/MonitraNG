using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Request DTO for POST /api/data/{datasetName}/query endpoint
    /// </summary>
    public class QueryRequestDto
    {
        /// <summary>
        /// MongoDB native match object for filtering.
        /// Example: { "$or": [ { "field1": "value1" }, { "field2": { "$gt": 10 } } ] }
        /// </summary>
        [JsonPropertyName("match")]
        public JsonElement Match { get; set; }
    }
}

