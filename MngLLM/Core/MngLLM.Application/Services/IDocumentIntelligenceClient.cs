namespace MngLLM.Application.Services;

public sealed record DiResourceContent(
    string ResourceId,
    string Name,
    string? Extension,
    string? MimeType,
    string? FilePath,
    byte[] Content);

public interface IDocumentIntelligenceClient
{
    Task<DiResourceContent> GetFileContentAsync(
        string resourceId,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}
