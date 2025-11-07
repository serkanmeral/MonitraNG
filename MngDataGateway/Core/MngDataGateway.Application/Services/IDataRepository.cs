using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Data repository for MongoDB operations
    /// </summary>
    public interface IDataRepository
    {
        /// <summary>
        /// Insert single document
        /// </summary>
        Task InsertOneAsync(
            string databaseName,
            string collectionName,
            Dictionary<string, object> data,
            IClientSessionHandle? session = null);

        /// <summary>
        /// Find document by __dataId
        /// </summary>
        Task<BsonDocument?> FindByIdAsync(
            string databaseName,
            string collectionName,
            string dataId,
            bool includeDeleted = false);

        /// <summary>
        /// Find documents with pagination
        /// </summary>
        Task<(List<BsonDocument> data, long totalCount)> FindManyAsync(
            string databaseName,
            string collectionName,
            int skip = 0,
            int limit = 50,
            bool includeDeleted = false);

        /// <summary>
        /// Update document by __dataId
        /// </summary>
        Task<bool> UpdateOneAsync(
            string databaseName,
            string collectionName,
            string dataId,
            Dictionary<string, object> updates,
            IClientSessionHandle? session = null);

        /// <summary>
        /// Soft delete document
        /// </summary>
        Task<bool> SoftDeleteAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Restore soft-deleted document
        /// </summary>
        Task<bool> RestoreAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Start MongoDB session for transaction
        /// </summary>
        Task<IClientSessionHandle> StartSessionAsync();
    }
}

