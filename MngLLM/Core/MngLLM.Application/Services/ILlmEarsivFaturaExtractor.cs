using MngLLM.Application.DTOs.Di;

namespace MngLLM.Application.Services;

public interface ILlmEarsivFaturaExtractor
{
    Task<EarsivFaturaExtractDto> ExtractFromTextAsync(
        string pdfText,
        string? resourceId,
        CancellationToken cancellationToken = default);
}
