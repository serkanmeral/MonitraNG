using MngNotifier.Application.Models;

namespace MngNotifier.Application.Services;

public interface IDataGatewayTemplateClient
{
    Task<MailTemplateRecord?> GetTemplateByKeyAsync(string templateKey, string bearerToken, CancellationToken cancellationToken = default);
    Task<MessageTemplateRecord?> GetMessageTemplateByKeyAsync(string templateKey, string bearerToken, CancellationToken cancellationToken = default);
    Task<MailLayoutRecord?> GetLayoutByKeyAsync(string layoutKey, string bearerToken, CancellationToken cancellationToken = default);
    Task<MailLayoutRecord?> GetDefaultLayoutAsync(string bearerToken, CancellationToken cancellationToken = default);
}
