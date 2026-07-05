using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Models;

namespace MngDocument.Infrastructure.Services;

public interface ITemplateLetterheadApplier
{
    Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel letterhead,
        byte[]? designDocxBytes,
        string? bearerToken,
        CancellationToken ct = default);
}

public sealed class TemplateLetterheadApplier : ITemplateLetterheadApplier
{
    private readonly IDomainLogoProvider _logoProvider;

    public TemplateLetterheadApplier(IDomainLogoProvider logoProvider)
    {
        _logoProvider = logoProvider;
    }

    public async Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel letterhead,
        byte[]? designDocxBytes,
        string? bearerToken,
        CancellationToken ct = default)
    {
        if (!letterhead.Enabled)
            return docxBytes;

        if (designDocxBytes is { Length: > 0 })
        {
            var merged = LetterheadDesignMerger.ApplyHeader(docxBytes, designDocxBytes);
            if (LetterheadDesignMerger.HasAppliedHeader(merged))
                return merged;
        }

        DomainLogoResult? logo = null;
        if (letterhead.ShowLogo)
            logo = await _logoProvider.GetCurrentDomainLogoAsync(bearerToken, ct);

        return LetterheadInjector.Apply(docxBytes, new LetterheadApplyRequest
        {
            Letterhead = letterhead,
            DocumentName = documentName,
            LogoBytes = logo?.Bytes,
            LogoExtension = logo?.Extension ?? ".png"
        });
    }
}

public static class TemplateDraftGuard
{
    public static void EnsureDraft(DmDocumentTemplate template)
    {
        if (string.Equals(template.status, Domain.Constants.TemplateStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            throw Application.Exceptions.DocumentException.Validation(
                "TEMPLATE_PUBLISHED",
                "Published templates cannot be modified.",
                "Yayınlanmış şablonlar düzenlenemez.");
        }
    }
}
