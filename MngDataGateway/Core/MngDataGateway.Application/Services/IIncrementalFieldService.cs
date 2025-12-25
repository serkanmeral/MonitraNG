using System.Collections.Generic;
using System.Threading.Tasks;
using MngDataGateway.Domain.Entities;
using MongoDB.Driver;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Incremental field generation service
    /// Manages @__counters collection for auto-incrementing fields
    /// </summary>
    public interface IIncrementalFieldService
    {
        /// <summary>
        /// Generate incremental field value
        /// </summary>
        /// <param name="schema">Dataset schema</param>
        /// <param name="field">Field definition (incremental type)</param>
        /// <param name="data">Current data (for placeholder resolution)</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="session">MongoDB session for transaction support</param>
        /// <returns>Generated value (e.g., "TASK-000001")</returns>
        Task<string> GenerateValueAsync(
            DatasetSchema schema,
            FieldDefinition field,
            Dictionary<string, object> data,
            string databaseName,
            string? domainName = null,
            IClientSessionHandle? session = null);
    }
}

