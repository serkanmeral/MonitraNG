namespace MngWorkflow.Application.Services;

/// <summary>
/// MngDataGateway API'ye erişim için client.
/// Dataset verilerini okumak (pipeline fetch, rules, templates) için kullanılır.
/// </summary>
public interface IDataGatewayClient
{
    /// <summary>
    /// Dataset'ten kayıt getirir (filter ile).
    /// </summary>
    /// <param name="datasetName">Dataset adı (örn. @wf_validation_pipelines).</param>
    /// <param name="filter">MongoDB filter (örn. dataset:eq:tm_issues).</param>
    /// <param name="domainName">Domain (JWT'den).</param>
    /// <param name="authorizationHeader">JWT token.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task<List<Dictionary<string, object>>> GetDataAsync(
        string datasetName,
        string? filter,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relation ile tek kayıt getirir (örn. tm_projects by __dataId).
    /// </summary>
    Task<Dictionary<string, object>?> GetByIdAsync(
        string datasetName,
        string dataId,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}
