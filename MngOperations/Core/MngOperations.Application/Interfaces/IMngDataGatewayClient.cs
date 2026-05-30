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

    /// <summary>
    /// DG POST <c>/query</c> (native Mongo match) — sunucu tarafı filtre/sıralama/arama/sayfalama.
    /// <paramref name="match"/> JSON gövdede <c>match</c> alanına serileştirilir; query string
    /// (sort/skip/limit/search/expand) ham olarak iletilir. Toplam kayıt <c>X-Total-Count</c> header'ından okunur.
    /// REST filter DSL'inin aksine çok değerli <c>$in</c> / <c>$or</c> destekler.
    /// </summary>
    Task<DataGatewayPage> QueryPageAsync(
        string datasetName,
        object match,
        string? query = null,
        string? token = null,
        CancellationToken cancellationToken = default);
}

/// <summary>DG GET list sonucu: sayfa satırları + filtre/arama sonrası toplam kayıt sayısı.</summary>
public sealed record DataGatewayPage(
    IReadOnlyList<Dictionary<string, object?>> Items,
    long Total);
