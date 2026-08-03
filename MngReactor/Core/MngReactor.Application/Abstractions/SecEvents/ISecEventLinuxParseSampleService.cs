using MngReactor.Application.Contracts.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventLinuxParseSampleService
{
    Task<SecEventLinuxParseSampleResponse> GetSamplesAsync(
        string domain,
        SecEventLinuxParseSampleRequest request,
        CancellationToken cancellationToken = default);
}
