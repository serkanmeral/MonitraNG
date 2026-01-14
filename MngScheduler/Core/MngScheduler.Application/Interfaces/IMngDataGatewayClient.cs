namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Client interface for MngDataGateway dataset API
/// Used for User Job CRUD operations
/// </summary>
public interface IMngDataGatewayClient
{
    /// <summary>
    /// Create data in dataset
    /// </summary>
    Task<T> CreateAsync<T>(string datasetName, T data, string? token = null) where T : class;

    /// <summary>
    /// Get data from dataset (with query)
    /// </summary>
    Task<IEnumerable<T>> GetAsync<T>(string datasetName, string? query = null, string? token = null) where T : class;

    /// <summary>
    /// Get single data by ID from dataset
    /// </summary>
    Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null) where T : class;

    /// <summary>
    /// Update data in dataset
    /// </summary>
    Task<T> UpdateAsync<T>(string datasetName, string id, T data, string? token = null) where T : class;

    /// <summary>
    /// Delete data from dataset
    /// </summary>
    Task<bool> DeleteAsync(string datasetName, string id, string? token = null);
}
