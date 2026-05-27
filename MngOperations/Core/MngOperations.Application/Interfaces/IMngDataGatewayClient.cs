namespace MngOperations.Application.Interfaces;

/// <summary>
/// MngDataGateway dataset API — Bearer forward from <see cref="IRequestContext"/>.
/// </summary>
public interface IMngDataGatewayClient
{
    Task<T> CreateAsync<T>(string datasetName, T data, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<IEnumerable<T>> GetAsync<T>(string datasetName, string? query = null, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<T> UpdateAsync<T>(string datasetName, string id, T data, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<bool> DeleteAsync(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(
        string datasetName,
        string queryName,
        Dictionary<string, object?> parameters,
        string? token = null,
        CancellationToken cancellationToken = default);
}
