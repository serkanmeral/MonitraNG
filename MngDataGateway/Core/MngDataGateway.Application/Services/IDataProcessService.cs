using System.Collections.Generic;
using System.Threading.Tasks;
using MngDataGateway.Domain.Entities;
using MongoDB.Driver;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Data processing service for pre-insert/update operations
    /// </summary>
    public interface IDataProcessService
    {
        /// <summary>
        /// Apply static default values to data
        /// </summary>
        void ApplyDefaultValues(DatasetSchema schema, Dictionary<string, object> data);

        /// <summary>
        /// Generate all incremental fields
        /// </summary>
        Task GenerateIncrementalFieldsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            IClientSessionHandle? session = null);

        /// <summary>
        /// Generate metadata for new data (__dataId, __history if needed)
        /// </summary>
        void GenerateMetadata(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string userId,
            string userEmail,
            string? ipAddress = null);

        /// <summary>
        /// Ensure collection and indexes exist (lazy creation on first insert)
        /// </summary>
        Task EnsureCollectionAndIndexesAsync(
            DatasetSchema schema,
            string databaseName);
    }
}

