using System.Collections.Generic;
using System.Threading.Tasks;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Main data service orchestrator
    /// </summary>
    public interface IDataService
    {
        /// <summary>
        /// Create new data
        /// </summary>
        Task<Dictionary<string, object>> CreateAsync(
            string datasetName,
            Dictionary<string, object> data,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Get data by ID
        /// </summary>
        Task<Dictionary<string, object>?> GetByIdAsync(
            string datasetName,
            string dataId,
            string databaseName);

        /// <summary>
        /// List data with pagination
        /// </summary>
        Task<(List<Dictionary<string, object>> data, long totalCount)> ListAsync(
            string datasetName,
            string databaseName,
            int skip = 0,
            int limit = 50);

        /// <summary>
        /// Update data
        /// </summary>
        Task<Dictionary<string, object>?> UpdateAsync(
            string datasetName,
            string dataId,
            Dictionary<string, object> updates,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Delete data (soft delete)
        /// </summary>
        Task<bool> DeleteAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Restore deleted data
        /// </summary>
        Task<bool> RestoreAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null);
    }
}

