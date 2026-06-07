using MngOperations.Application.Contracts.Notifications;

namespace MngOperations.Application.Interfaces;

public interface IMngNotifiersClient
{
    Task<SendMailResult> SendMailAsync(SendMailRequest request, CancellationToken cancellationToken = default);

    Task<SendMailResult> SendTemplateAsync(
        SendTemplateRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default);
}
