using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Models;

namespace MngDocument.Infrastructure.Services;

public interface ITemplateLetterheadApplier
{
    Task<byte[]> ApplyAsync(
        byte[] docxBytes,
        string documentName,
        TemplateLetterheadModel letterhead,
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
        string? bearerToken,
        CancellationToken ct = default)
    {
        if (!letterhead.Enabled)
            return docxBytes;

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

public static class TemplateParameterMapper
{
    public static TemplateParameterDto ToDto(TemplateParameterModel model) => new()
    {
        Key = model.Key,
        Label = model.Label,
        DataType = model.DataType,
        ValueSourceMode = model.ValueSourceMode,
        Incremental = model.Incremental is null
            ? null
            : new TemplateIncrementalOptionsDto
            {
                Format = model.Incremental.Format,
                StartValue = model.Incremental.StartValue,
                IncrementStep = model.Incremental.IncrementStep,
                ScopeKey = model.Incremental.ScopeKey,
                ResetPolicy = model.Incremental.ResetPolicy
            },
        SourceBinding = model.SourceBinding is null
            ? null
            : new TemplateSourceBindingDto
            {
                RegionKind = model.SourceBinding.RegionKind,
                ParagraphIndex = model.SourceBinding.ParagraphIndex,
                OriginalText = model.SourceBinding.OriginalText,
                CharStart = model.SourceBinding.CharStart,
                CharEnd = model.SourceBinding.CharEnd
            }
    };

    public static TemplateParameterModel ToModel(TemplateParameterDto dto) => new()
    {
        Key = dto.Key,
        Label = dto.Label,
        DataType = dto.DataType,
        ValueSourceMode = dto.ValueSourceMode,
        Incremental = dto.Incremental is null
            ? null
            : new TemplateIncrementalModel
            {
                Format = dto.Incremental.Format,
                StartValue = dto.Incremental.StartValue,
                IncrementStep = dto.Incremental.IncrementStep,
                ScopeKey = dto.Incremental.ScopeKey,
                ResetPolicy = dto.Incremental.ResetPolicy
            },
        SourceBinding = dto.SourceBinding is null
            ? null
            : new TemplateSourceBindingModel
            {
                RegionKind = dto.SourceBinding.RegionKind,
                ParagraphIndex = dto.SourceBinding.ParagraphIndex,
                OriginalText = dto.SourceBinding.OriginalText,
                CharStart = dto.SourceBinding.CharStart,
                CharEnd = dto.SourceBinding.CharEnd
            }
    };
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
