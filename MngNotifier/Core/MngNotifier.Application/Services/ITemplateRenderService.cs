using MngNotifier.Application.Models;

namespace MngNotifier.Application.Services;

public interface ITemplateRenderService
{
    Task<RenderedMailContent> RenderAsync(
        TemplateRenderRequest request,
        string bearerToken,
        CancellationToken cancellationToken = default);
}
