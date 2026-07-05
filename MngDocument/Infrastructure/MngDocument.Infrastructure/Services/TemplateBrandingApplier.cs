using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Letterheads;

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
        byte[]? letterheadDesignDocx,
        LetterheadSettingsDto? letterheadSettings,
        string? bearerToken,
        CancellationToken ct = default);
}

public sealed class TemplateBrandingApplier : ITemplateBrandingApplier
{
    private readonly ITemplateLetterheadApplier _letterheadApplier;
    private readonly ILetterheadFooterApplier _letterheadFooterApplier;
    private readonly ITemplateFooterApplier _legacyFooterApplier;

    public TemplateBrandingApplier(
        ITemplateLetterheadApplier letterheadApplier,
        ILetterheadFooterApplier letterheadFooterApplier,
        ITemplateFooterApplier legacyFooterApplier)
    {
        _letterheadApplier = letterheadApplier;
        _letterheadFooterApplier = letterheadFooterApplier;
        _legacyFooterApplier = legacyFooterApplier;
    }

    public async Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel? letterhead,
        TemplateFooterModel? footer,
        TemplatePageLayoutModel? pageLayout,
        byte[]? letterheadDesignDocx,
        LetterheadSettingsDto? letterheadSettings,
        string? bearerToken,
        CancellationToken ct = default)
    {
        var result = docxBytes;
        var layout = pageLayout ?? TemplatePageLayoutModel.CreateDefault();
        var designFooterApplied = false;

        if (letterhead is { Enabled: true })
        {
            result = await _letterheadApplier.ApplyAsync(
                result,
                documentName,
                letterhead,
                letterheadDesignDocx,
                bearerToken,
                ct);

            if (letterheadDesignDocx is { Length: > 0 }
                && (LetterheadDesignMerger.HasDesignFooter(letterheadDesignDocx)
                    || LetterheadDesignMerger.HasFooterTableStructure(letterheadDesignDocx)))
            {
                result = LetterheadDesignMerger.ApplyFooter(result, letterheadDesignDocx);
                designFooterApplied = LetterheadDesignMerger.HasFooterTableStructure(result)
                                      || LetterheadDesignMerger.HasAppliedFooter(result);
            }
        }

        if (!designFooterApplied)
        {
            if (letterheadSettings is not null)
                result = _letterheadFooterApplier.ApplyGenerationFallback(result, letterheadSettings);
            else if (footer is { Enabled: true })
                result = _legacyFooterApplier.Apply(result, footer, layout);
        }

        result = PageLayoutInjector.Apply(result, layout);

        return result;
    }
}
