using MngNotifier.Application.DTOs;

namespace MngNotifier.Application.Services;

public interface IMailProvider
{
    Task SendMailAsync(SendMailRequest request, CancellationToken cancellationToken = default);
}
