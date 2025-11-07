using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// DTO for updating data
    /// </summary>
    public class UpdateDataDto
    {
        /// <summary>
        /// Fields to update
        /// Key: field name, Value: new value
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }
}

