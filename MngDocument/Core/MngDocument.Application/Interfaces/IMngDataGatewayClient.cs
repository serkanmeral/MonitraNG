namespace MngDocument.Application.Interfaces;

/// <summary>
/// MngDataGateway dataset API istemcisi — Bearer çağıranın token'ı ile forward edilir.
/// MngDocument kalıcılık sahibi değildir; tüm <c>dm_*</c> okuma/yazma DG üzerinden yapılır.
/// </summary>
public interface IMngDataGatewayClient
{
    Task<T> CreateAsync<T>(string datasetName, object payload, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<IReadOnlyList<T>> QueryAsync<T>(string datasetName, string? query = null, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<T> UpdateAsync<T>(string datasetName, string id, object payload, string? token = null, CancellationToken cancellationToken = default)
        where T : class;

    Task<bool> DeleteAsync(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// DG POST <c>/query</c> (native Mongo match) — filtre/sıralama/arama/sayfalama.
    /// <paramref name="match"/> gövdedeki <c>match</c> alanına serileştirilir; query string
    /// (sort/skip/limit/search) ham olarak iletilir. Toplam kayıt <c>X-Total-Count</c>'tan okunur.
    /// </summary>
    Task<DataGatewayPage> QueryPageAsync(
        string datasetName,
        object match,
        string? query = null,
        string? token = null,
        CancellationToken cancellationToken = default);

    /// <summary>DG POST <c>/data/{dataset}/queries/{queryName}</c> — predefined pipeline.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteNamedQueryAsync(
        string datasetName,
        string queryName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string? token = null,
        CancellationToken cancellationToken = default);

    /// <summary>DG <c>GET files/download?filePath=</c> — MinIO binary içerik.</summary>
    Task<byte[]> DownloadFileAsync(string filePath, string? token = null, CancellationToken cancellationToken = default);
}

/// <summary>DG list sonucu: sayfa satırları + filtre/arama sonrası toplam kayıt sayısı.</summary>
public sealed record DataGatewayPage(
    IReadOnlyList<Dictionary<string, object?>> Items,
    long Total);
