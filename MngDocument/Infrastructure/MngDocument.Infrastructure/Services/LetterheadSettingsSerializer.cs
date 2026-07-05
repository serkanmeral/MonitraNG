using System.Text.Json;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

internal static class LetterheadSettingsSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static LetterheadSettingsDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CreateDefault();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return Normalize(DeserializeFromJson(root));
        }
        catch
        {
            return CreateDefault();
        }
    }

    public static string Serialize(LetterheadSettingsDto? settings) =>
        JsonSerializer.Serialize(Normalize(settings ?? CreateDefault()), JsonOptions);

    public static LetterheadSettingsDto CreateDefault() =>
        new()
        {
            HeaderFields = new LetterheadHeaderFieldsDto
            {
                DocumentName = true,
                DocNo = true,
                GeneratedAt = true,
                CreatePerson = false
            },
            GeneralDocNo = new LetterheadGeneralDocNoDto
            {
                Enabled = true,
                Format = LetterheadBrandingDefaults.DefaultGeneralDocNoFormat,
                ScopeMode = "letterhead",
                ResetPolicy = "yearly",
                StartValue = 1,
                IncrementStep = 1
            },
            Footer = LetterheadBrandingDefaults.DefaultFooterSettings(),
            FooterBlocks = Array.Empty<FooterBlockDto>(),
            PageLayout = LetterheadBrandingDefaults.DefaultPageLayoutDto()
        };

    public static LetterheadSettingsDto Normalize(LetterheadSettingsDto settings)
    {
        var defaults = CreateDefault();
        var header = settings.HeaderFields ?? defaults.HeaderFields;
        var general = settings.GeneralDocNo ?? defaults.GeneralDocNo;
        var scopeMode = string.IsNullOrWhiteSpace(general.ScopeMode)
            ? "letterhead"
            : general.ScopeMode.Trim().ToLowerInvariant();

        if (scopeMode is not ("letterhead" or "global" or "custom"))
            scopeMode = "letterhead";

        var resetPolicy = string.IsNullOrWhiteSpace(general.ResetPolicy)
            ? "yearly"
            : general.ResetPolicy.Trim().ToLowerInvariant();

        var footer = NormalizeFooter(settings.Footer, settings.LegacyOdakFooter);
        var pageLayout = settings.PageLayout ?? defaults.PageLayout;
        var footerBlocks = NormalizeFooterBlocks(settings.FooterBlocks);
        var legacyOdak = settings.LegacyOdakFooter is null
            ? null
            : NormalizeLegacyOdakFooter(settings.LegacyOdakFooter);

        return new LetterheadSettingsDto
        {
            HeaderFields = new LetterheadHeaderFieldsDto
            {
                DocumentName = header.DocumentName,
                DocNo = header.DocNo,
                GeneratedAt = header.GeneratedAt,
                CreatePerson = header.CreatePerson
            },
            GeneralDocNo = new LetterheadGeneralDocNoDto
            {
                Enabled = general.Enabled,
                Format = string.IsNullOrWhiteSpace(general.Format)
                    ? defaults.GeneralDocNo.Format
                    : general.Format.Trim(),
                ScopeMode = scopeMode,
                ScopeKey = string.IsNullOrWhiteSpace(general.ScopeKey) ? null : general.ScopeKey.Trim(),
                ResetPolicy = resetPolicy,
                StartValue = general.StartValue > 0 ? general.StartValue : 1,
                IncrementStep = general.IncrementStep > 0 ? general.IncrementStep : 1
            },
            Footer = footer,
            LegacyOdakFooter = legacyOdak,
            FooterBlocks = footerBlocks,
            PageLayout = new TemplatePageLayoutDto
            {
                MarginTopTwips = pageLayout.MarginTopTwips > 0 ? pageLayout.MarginTopTwips : defaults.PageLayout.MarginTopTwips,
                MarginRightTwips = pageLayout.MarginRightTwips > 0 ? pageLayout.MarginRightTwips : defaults.PageLayout.MarginRightTwips,
                MarginBottomTwips = pageLayout.MarginBottomTwips > 0 ? pageLayout.MarginBottomTwips : defaults.PageLayout.MarginBottomTwips,
                MarginLeftTwips = pageLayout.MarginLeftTwips > 0 ? pageLayout.MarginLeftTwips : defaults.PageLayout.MarginLeftTwips,
                HeaderDistanceTwips = pageLayout.HeaderDistanceTwips > 0 ? pageLayout.HeaderDistanceTwips : defaults.PageLayout.HeaderDistanceTwips,
                FooterDistanceTwips = pageLayout.FooterDistanceTwips > 0 ? pageLayout.FooterDistanceTwips : defaults.PageLayout.FooterDistanceTwips,
                FooterLeftIndentTwips = pageLayout.FooterLeftIndentTwips != 0 ? pageLayout.FooterLeftIndentTwips : defaults.PageLayout.FooterLeftIndentTwips
            }
        };
    }

    private static LetterheadSettingsDto DeserializeFromJson(JsonElement root)
    {
        var defaults = CreateDefault();
        TemplateFooterDto? legacyFromOldFooter = null;
        LetterheadFooterSettingsDto? footer = null;

        if (root.TryGetProperty("footer", out var footerEl) && footerEl.ValueKind == JsonValueKind.Object)
        {
            if (footerEl.TryGetProperty("tableRows", out _) || footerEl.TryGetProperty("tableColumns", out _))
            {
                footer = JsonSerializer.Deserialize<LetterheadFooterSettingsDto>(footerEl.GetRawText(), JsonOptions)
                         ?? defaults.Footer;
            }
            else if (footerEl.TryGetProperty("showFormRevision", out _)
                     || footerEl.TryGetProperty("showOfficeColumns", out _))
            {
                legacyFromOldFooter = JsonSerializer.Deserialize<TemplateFooterDto>(footerEl.GetRawText(), JsonOptions);
                footer = new LetterheadFooterSettingsDto
                {
                    Enabled = legacyFromOldFooter?.Enabled ?? false,
                    TableRows = 2,
                    TableColumns = 2
                };
            }
            else
            {
                footer = JsonSerializer.Deserialize<LetterheadFooterSettingsDto>(footerEl.GetRawText(), JsonOptions)
                         ?? defaults.Footer;
            }
        }

        var settings = JsonSerializer.Deserialize<LetterheadSettingsDto>(root.GetRawText(), JsonOptions) ?? defaults;
        settings = new LetterheadSettingsDto
        {
            HeaderFields = settings.HeaderFields ?? defaults.HeaderFields,
            GeneralDocNo = settings.GeneralDocNo ?? defaults.GeneralDocNo,
            Footer = footer ?? settings.Footer ?? defaults.Footer,
            LegacyOdakFooter = settings.LegacyOdakFooter ?? legacyFromOldFooter,
            FooterBlocks = settings.FooterBlocks ?? Array.Empty<FooterBlockDto>(),
            PageLayout = settings.PageLayout ?? defaults.PageLayout
        };
        return settings;
    }

    private static LetterheadFooterSettingsDto NormalizeFooter(
        LetterheadFooterSettingsDto? footer,
        TemplateFooterDto? legacyOdakFooter)
    {
        var source = footer ?? LetterheadBrandingDefaults.DefaultFooterSettings();
        var rows = source.TableRows > 0 ? Math.Clamp(source.TableRows, 1, 12) : 1;
        var cols = source.TableColumns > 0 ? Math.Clamp(source.TableColumns, 1, 6) : 1;
        return new LetterheadFooterSettingsDto
        {
            Enabled = source.Enabled,
            TableRows = rows,
            TableColumns = cols
        };
    }

    private static TemplateFooterDto NormalizeLegacyOdakFooter(TemplateFooterDto footer) =>
        new()
        {
            Enabled = footer.Enabled,
            ShowFormRevision = footer.ShowFormRevision,
            ShowOfficeColumns = footer.ShowOfficeColumns,
            ShowAddresses = footer.ShowAddresses,
            ShowContacts = footer.ShowContacts,
            ShowDividerLine = footer.ShowDividerLine
        };

    private static IReadOnlyList<FooterBlockDto> NormalizeFooterBlocks(IReadOnlyList<FooterBlockDto>? blocks)
    {
        if (blocks is null || blocks.Count == 0)
            return Array.Empty<FooterBlockDto>();

        return blocks
            .Where(b => !string.IsNullOrWhiteSpace(b.Type))
            .Select(b => new FooterBlockDto
            {
                Type = b.Type.Trim().ToLowerInvariant(),
                Align = string.IsNullOrWhiteSpace(b.Align) ? null : b.Align.Trim().ToLowerInvariant(),
                Runs = NormalizeRuns(b.Runs),
                Columns = b.Columns is > 0 ? b.Columns : null,
                ColumnWidthTwips = b.ColumnWidthTwips?.Where(w => w > 0).ToList(),
                Rows = b.Rows?
                    .Select(r => new FooterTableRowDto
                    {
                        Cells = r.Cells
                            .Select(c => new FooterTableCellDto { Runs = NormalizeRuns(c.Runs) })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }

    private static IReadOnlyList<FooterRunDto> NormalizeRuns(IReadOnlyList<FooterRunDto>? runs)
    {
        if (runs is null || runs.Count == 0)
            return Array.Empty<FooterRunDto>();

        return runs
            .Where(r => !string.IsNullOrWhiteSpace(r.Text))
            .Select(r => new FooterRunDto { Text = r.Text, Bold = r.Bold })
            .ToList();
    }

    public static string ResolveScopeKey(LetterheadGeneralDocNoDto generalDocNo, string letterheadCode)
    {
        var mode = generalDocNo.ScopeMode?.Trim().ToLowerInvariant() ?? "letterhead";
        return mode switch
        {
            "global" => string.IsNullOrWhiteSpace(generalDocNo.ScopeKey)
                ? LetterheadConstants.DomainScopeKey
                : generalDocNo.ScopeKey.Trim(),
            "custom" => string.IsNullOrWhiteSpace(generalDocNo.ScopeKey)
                ? "custom"
                : generalDocNo.ScopeKey.Trim(),
            _ => $"letterhead:{letterheadCode.Trim()}"
        };
    }

    public static TemplateLetterheadModel ApplyHeaderFields(
        TemplateLetterheadModel source,
        LetterheadHeaderFieldsDto headerFields)
    {
        return new TemplateLetterheadModel
        {
            Enabled = source.Enabled,
            ShowLogo = source.ShowLogo,
            ShowDocumentName = source.ShowDocumentName && headerFields.DocumentName,
            ShowDocumentNumber = source.ShowDocumentNumber && headerFields.DocNo,
            ShowGeneratedAt = source.ShowGeneratedAt && headerFields.GeneratedAt,
            ShowCreatePerson = headerFields.CreatePerson
        };
    }
}
