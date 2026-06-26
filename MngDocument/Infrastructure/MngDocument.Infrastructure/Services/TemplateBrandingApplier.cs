using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;

namespace MngDocument.Infrastructure.Services;

public interface ITemplateFooterApplier
{
    byte[] Apply(byte[] docxBytes, TemplateFooterModel footer, TemplatePageLayoutModel? pageLayout);
}

public sealed class TemplateFooterApplier : ITemplateFooterApplier
{
    private readonly MngDocumentSettings _settings;

    public TemplateFooterApplier(IOptions<MngDocumentSettings> settings)
    {
        _settings = settings.Value;
    }

    public byte[] Apply(byte[] docxBytes, TemplateFooterModel footer, TemplatePageLayoutModel? pageLayout)
    {
        if (!footer.Enabled)
            return docxBytes;

        var profile = _settings.FooterProfile;
        if (profile.Offices.Count == 0)
            return docxBytes;

        return FooterInjector.Apply(docxBytes, new FooterApplyRequest
        {
            Footer = footer,
            Profile = profile,
            PageLayout = pageLayout ?? TemplatePageLayoutModel.CreateDefault()
        });
    }
}

public interface ITemplateBrandingApplier
{
    Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel? letterhead,
        TemplateFooterModel? footer,
        TemplatePageLayoutModel? pageLayout,
        string? bearerToken,
        CancellationToken ct = default);
}

public sealed class TemplateBrandingApplier : ITemplateBrandingApplier
{
    private readonly ITemplateLetterheadApplier _letterheadApplier;
    private readonly ITemplateFooterApplier _footerApplier;

    public TemplateBrandingApplier(
        ITemplateLetterheadApplier letterheadApplier,
        ITemplateFooterApplier footerApplier)
    {
        _letterheadApplier = letterheadApplier;
        _footerApplier = footerApplier;
    }

    public async Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel? letterhead,
        TemplateFooterModel? footer,
        TemplatePageLayoutModel? pageLayout,
        string? bearerToken,
        CancellationToken ct = default)
    {
        var result = docxBytes;
        var layout = pageLayout ?? TemplatePageLayoutModel.CreateDefault();

        if (letterhead is { Enabled: true })
        {
            result = await _letterheadApplier.ApplyAsync(
                result,
                documentName,
                letterhead,
                bearerToken,
                ct);
        }

        if (footer is { Enabled: true })
            result = _footerApplier.Apply(result, footer, layout);

        result = PageLayoutInjector.Apply(result, layout);

        return result;
    }
}
