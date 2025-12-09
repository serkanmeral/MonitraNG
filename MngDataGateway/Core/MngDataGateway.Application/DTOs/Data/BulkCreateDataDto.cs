using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// DTO for bulk creating multiple data records
    /// </summary>
    public class BulkCreateDataDto
    {
        /// <summary>
        /// Array of data items to create
        /// Each item is a dictionary of field name-value pairs
        /// </summary>
        public List<Dictionary<string, object>> Items { get; set; } = new();
    }
}

