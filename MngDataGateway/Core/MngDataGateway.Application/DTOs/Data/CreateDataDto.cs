using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// DTO for creating data
    /// Dynamic fields based on dataset schema
    /// </summary>
    public class CreateDataDto
    {
        /// <summary>
        /// Dynamic data fields
        /// Key: field name, Value: field value
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }
}

