using MngNotifier.Application.Models;

namespace MngNotifier.Application.Services;

public interface IMessageTemplateRenderService
{
    Task<RenderedMessageContent> RenderAsync(
        MessageTemplateRenderRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default);
}
