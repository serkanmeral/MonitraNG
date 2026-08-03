using MngReactor.Application.Contracts.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventWindowsParseSampleService
{
    Task<SecEventWindowsParseSampleResponse> GetSamplesAsync(
        string domain,
        SecEventWindowsParseSampleRequest request,
        CancellationToken cancellationToken = default);
}
