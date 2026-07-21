using MngLLM.Application.DTOs.Di;

namespace MngLLM.Application.Services;

public interface IDiExtractService
{
    Task<EarsivFaturaExtractDto> ExtractAsync(
        DiExtractRequestDto request,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}
