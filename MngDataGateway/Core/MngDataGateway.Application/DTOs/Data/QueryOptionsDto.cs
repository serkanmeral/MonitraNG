using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Data
{
    /// <summary>
    /// Query options for GET operations
    /// </summary>
    public class QueryOptionsDto
    {
        /// <summary>
        /// Pagination offset (default: 0)
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Page size (default: 50, max: 1000)
        /// </summary>
        public int Limit { get; set; } = 50;

        /// <summary>
        /// Enable relation expansion (default: true)
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <summary>
        /// Maximum depth for nested relation expansion (default: from appsettings)
        /// </summary>
        public int? Deep { get; set; }

        /// <summary>
        /// Include __history field (default: false)
        /// </summary>
        public bool ShowHistory { get; set; } = false;

        /// <summary>
        /// Return aggregate pipeline instead of data (default: false)
        /// </summary>
        public bool ShowQuery { get; set; } = false;

        /// <summary>
        /// Return dataset schema instead of data (default: false)
        /// </summary>
        public bool ShowDataset { get; set; } = false;

        /// <summary>
        /// Sort definition (MongoDB style: "field1,-field2")
        /// </summary>
        public string? Sort { get; set; }

        /// <summary>
        /// Filter definition (RESTful style: "field:operator:value")
        /// </summary>
        public string? Filter { get; set; }

        /// <summary>
        /// Field selection (comma-separated: "field1,field2,field3")
        /// </summary>
        public string? Fields { get; set; }
    }
}

