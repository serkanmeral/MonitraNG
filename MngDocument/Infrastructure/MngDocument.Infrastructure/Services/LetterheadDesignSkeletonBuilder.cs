using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Infrastructure.Services;

/// <summary>Assembles letterhead design DOCX skeleton (header + optional empty footer table + margins).</summary>
public static class LetterheadDesignSkeletonBuilder
{
    public static byte[] Build(
        byte[] docxBytes,
        LetterheadSettingsDto settings,
        TemplateLetterheadModel letterhead,
        string documentName,
        byte[]? logoBytes,
        string logoExtension)
    {
        var normalized = LetterheadSettingsSerializer.Normalize(settings);
        var layout = TemplateModelSerializer.ToPageLayoutModel(normalized.PageLayout)
                     ?? TemplatePageLayoutModel.CreateDefault();
        var result = docxBytes;

        if (letterhead.Enabled)
        {
            result = LetterheadInjector.Apply(result, new LetterheadApplyRequest
            {
                Letterhead = letterhead,
                DocumentName = documentName,
                LogoBytes = logoBytes,
                LogoExtension = logoExtension
            });
        }

        if (normalized.Footer.Enabled)
        {
            result = LetterheadFooterTableBuilder.ApplyEmptyTable(
                result,
                normalized.Footer.TableRows,
                normalized.Footer.TableColumns,
                layout);
        }

        return PageLayoutInjector.Apply(result, layout);
    }

    public static byte[] EnsureEditorParts(
        byte[] rawDocxBytes,
        LetterheadSettingsDto settings,
        TemplateLetterheadModel letterhead,
        string documentName,
        byte[]? logoBytes,
        string logoExtension)
    {
        var normalized = LetterheadSettingsSerializer.Normalize(settings);
        var layout = TemplateModelSerializer.ToPageLayoutModel(normalized.PageLayout)
                     ?? TemplatePageLayoutModel.CreateDefault();
        var result = rawDocxBytes;

        if (letterhead.Enabled && !LetterheadDesignMerger.HasDesignHeader(result))
        {
            result = LetterheadInjector.Apply(result, new LetterheadApplyRequest
            {
                Letterhead = letterhead,
                DocumentName = documentName,
                LogoBytes = logoBytes,
                LogoExtension = logoExtension
            });
        }

        if (normalized.Footer.Enabled && !LetterheadDesignMerger.HasFooterTableStructure(result))
        {
            result = LetterheadFooterTableBuilder.ApplyEmptyTable(
                result,
                normalized.Footer.TableRows,
                normalized.Footer.TableColumns,
                layout);
        }

        return PageLayoutInjector.Apply(result, layout);
    }
}
