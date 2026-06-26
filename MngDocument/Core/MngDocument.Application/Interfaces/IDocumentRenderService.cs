using MngDocument.Application.Contracts.Rendering;

namespace MngDocument.Application.Interfaces;

public interface IDocumentRenderService
{
    Task<DocumentRenderingStatusDto> GetStatusAsync(CancellationToken ct = default);

    byte[] MergePlaceholders(byte[] docxBytes, IReadOnlyDictionary<string, string> values);

    Task<byte[]> ConvertDocxToPdfAsync(byte[] docxBytes, CancellationToken ct = default);

    Task<byte[]> MergeAndConvertToPdfAsync(
        byte[] docxBytes,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);
}
