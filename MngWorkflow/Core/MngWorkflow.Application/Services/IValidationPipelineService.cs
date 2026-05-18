namespace MngWorkflow.Application.Services;

/// <summary>
/// Validation pipeline'ları çalıştıran servis.
/// DG HTTP validation'dan çağrılır.
/// </summary>
public interface IValidationPipelineService
{
    /// <summary>
    /// Belirtilen dataset için validation pipeline'ları çalıştırır.
    /// </summary>
    /// <param name="datasetName">Dataset adı (örn. tm_issues).</param>
    /// <param name="payload">Validate edilecek veri (create/update body).</param>
    /// <param name="domainName">JWT'den alınan domain adı.</param>
    /// <param name="authorizationHeader">JWT token (DG'den forward edilir).</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Validation sonucu.</returns>
    Task<ValidationResult> ValidateAsync(
        string datasetName,
        Dictionary<string, object> payload,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation pipeline sonucu.
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorMessage = null);
