using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Infrastructure.Services;

public interface ILetterheadFooterApplier
{
    /// <summary>Generation-time fallback only (legacy Odak / footerBlocks). Not used for Collabora design read.</summary>
    byte[] ApplyGenerationFallback(byte[] docxBytes, LetterheadSettingsDto settings);

    (string Source, IReadOnlyList<string> PreviewLines) DescribePreview(
        LetterheadSettingsDto settings,
        byte[] rawDesignDocx);
}

public sealed class LetterheadFooterApplier : ILetterheadFooterApplier
{
    private readonly ITemplateFooterApplier _legacyApplier;
    private readonly MngDocumentSettings _settings;

    public LetterheadFooterApplier(
        ITemplateFooterApplier legacyApplier,
        IOptions<MngDocumentSettings> settings)
    {
        _legacyApplier = legacyApplier;
        _settings = settings.Value;
    }

    public byte[] ApplyGenerationFallback(byte[] docxBytes, LetterheadSettingsDto settings)
    {
        var normalized = LetterheadSettingsSerializer.Normalize(settings);
        if (!normalized.Footer.Enabled)
            return docxBytes;

        var layout = TemplateModelSerializer.ToPageLayoutModel(normalized.PageLayout)
                     ?? TemplatePageLayoutModel.CreateDefault();

        if (normalized.FooterBlocks.Count > 0)
            return GenericFooterBlockRenderer.Apply(docxBytes, normalized.FooterBlocks, layout);

        if (!_settings.LegacyOdakFooterEnabled || normalized.LegacyOdakFooter is not { Enabled: true } legacy)
            return docxBytes;

        var legacyModel = TemplateModelSerializer.ToFooterModel(legacy);
        if (legacyModel is not { Enabled: true })
            return docxBytes;

        return _legacyApplier.Apply(docxBytes, legacyModel, layout);
    }

    public (string Source, IReadOnlyList<string> PreviewLines) DescribePreview(
        LetterheadSettingsDto settings,
        byte[] rawDesignDocx)
    {
        var normalized = LetterheadSettingsSerializer.Normalize(settings);
        if (!normalized.Footer.Enabled)
            return ("disabled", Array.Empty<string>());

        if (LetterheadDesignMerger.HasFooterTableStructure(rawDesignDocx)
            || LetterheadDesignMerger.HasDesignFooter(rawDesignDocx))
        {
            var lines = new List<string>
            {
                $"{normalized.Footer.TableRows}×{normalized.Footer.TableColumns}"
            };
            if (LetterheadDesignMerger.HasDesignFooter(rawDesignDocx))
            {
                var text = ExtractFooterPlainText(rawDesignDocx);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }

            return ("design", lines);
        }

        return ("pending", new[] { $"{normalized.Footer.TableRows}×{normalized.Footer.TableColumns}" });
    }

    private static string ExtractFooterPlainText(byte[] rawDesignDocx)
    {
        using var input = new MemoryStream(rawDesignDocx, writable: false);
        using var archive = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
        var entry = DocxZipHelper.GetEntry(archive, "word/footer1.xml");
        if (entry is null)
            return string.Empty;

        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd();
        var matches = System.Text.RegularExpressions.Regex.Matches(
            xml,
            "<w:t[^>]*>([^<]*)</w:t>",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var parts = matches
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t) && t != " ")
            .Take(4)
            .ToList();
        return string.Join(" · ", parts);
    }
}
