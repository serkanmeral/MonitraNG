using System.Collections.Generic;
using System.Threading.Tasks;
using MngDataGateway.Application.DTOs.Data;

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
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish (e.g. monitoring sync). Useful for heartbeat-only updates.</param>
        Task<Dictionary<string, object>> CreateAsync(
            string datasetName,
            Dictionary<string, object> data,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null,
            bool skipEventPublish = false);

        /// <summary>
        /// Bulk create multiple data records
        /// </summary>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish.</param>
        Task<BulkInsertResultDto> BulkCreateAsync(
            string datasetName,
            List<Dictionary<string, object>> items,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null,
            bool skipEventPublish = false);

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
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish (e.g. monitoring sync). Useful for lastSeenAt/heartbeat updates.</param>
        Task<Dictionary<string, object>?> UpdateAsync(
            string datasetName,
            string dataId,
            Dictionary<string, object> updates,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null,
            bool skipEventPublish = false);

        /// <summary>
        /// Delete data (hard delete + archive to __deletedDatas with TTL)
        /// </summary>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish.</param>
        Task<bool> DeleteAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null,
            bool skipEventPublish = false);

        /// <summary>
        /// Restore deleted data
        /// </summary>
        /// <param name="skipEventPublish">If true, no RabbitMQ/event publish.</param>
        Task<bool> RestoreAsync(
            string datasetName,
            string dataId,
            string domainName,
            string databaseName,
            string userId,
            string userEmail,
            string? ipAddress = null,
            bool skipEventPublish = false);

        /// <summary>
        /// Query data using aggregate pipeline with all query options
        /// </summary>
        Task<QueryResultDto> QueryAsync(
            string datasetName,
            string databaseName,
            QueryOptionsDto options);

        /// <summary>
        /// Query single data by ID using aggregate pipeline with all query options
        /// </summary>
        Task<QueryResultDto> QueryByIdAsync(
            string datasetName,
            string dataId,
            string databaseName,
            QueryOptionsDto options);

        /// <summary>
        /// Query data using MongoDB native match object with all query options
        /// </summary>
        Task<QueryResultDto> QueryWithMatchAsync(
            string datasetName,
            string databaseName,
            Dictionary<string, object>? match,
            QueryOptionsDto options);

        /// <summary>
        /// Execute predefined query from dataset schema
        /// </summary>
        Task<List<Dictionary<string, object>>> ExecutePredefinedQueryAsync(
            string datasetName,
            string queryName,
            string databaseName,
            Dictionary<string, object> parameters);

        /// <summary>
        /// Execute raw MongoDB aggregate pipeline
        /// </summary>
        Task<List<Dictionary<string, object>>> ExecuteRawAggregateAsync(
            string datasetName,
            string databaseName,
            List<MongoDB.Bson.BsonDocument> pipeline);
    }
}

