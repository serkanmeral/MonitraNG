using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Contracts.Letterheads;

/// <summary>Default letterhead page identity (margins + platform footer table defaults).</summary>
public static class LetterheadBrandingDefaults
{
    public const string DefaultGeneralDocNoFormat = "{yyyy}-{0:D4}";

    public static LetterheadFooterSettingsDto DefaultFooterSettings() => new()
    {
        Enabled = false,
        TableRows = 1,
        TableColumns = 1
    };

    public static LetterheadFooterSettingsDto DefaultOdakSeedFooterSettings() => new()
    {
        Enabled = true,
        TableRows = 2,
        TableColumns = 2
    };

    public static TemplateFooterDto DefaultLegacyFooterDto() => new()
    {
        Enabled = true,
        ShowFormRevision = true,
        ShowOfficeColumns = true,
        ShowAddresses = true,
        ShowContacts = true,
        ShowDividerLine = false
    };

    public static TemplateFooterDto DefaultFooterDto() => DefaultLegacyFooterDto();

    public static TemplatePageLayoutDto DefaultPageLayoutDto() => new();

    /// <summary>Reference Odak corporate footer as generic blocks (tenant seed; not runtime appsettings).</summary>
    public static IReadOnlyList<FooterBlockDto> DefaultOdakFooterBlocks() =>
    [
        new FooterBlockDto
        {
            Type = "paragraph",
            Align = "both",
            Runs =
            [
                new FooterRunDto { Text = "F86 Rev04 30.11.2022" }
            ]
        },
        new FooterBlockDto
        {
            Type = "table",
            Columns = 2,
            Rows =
            [
                new FooterTableRowDto
                {
                    Cells =
                    [
                        new FooterTableCellDto { Runs = [new FooterRunDto { Text = "Merkez Ofis", Bold = true }] },
                        new FooterTableCellDto { Runs = [new FooterRunDto { Text = "Üretim", Bold = true }] }
                    ]
                },
                new FooterTableRowDto
                {
                    Cells =
                    [
                        new FooterTableCellDto
                        {
                            Runs =
                            [
                                new FooterRunDto
                                {
                                    Text = "Cinnah Caddesi No:11/5 06690 Çankaya – Ankara/TÜRKİYE"
                                }
                            ]
                        },
                        new FooterTableCellDto
                        {
                            Runs =
                            [
                                new FooterRunDto
                                {
                                    Text = "Ostim OSB Mh. 1227 Cd. No: 6/C 06374 Ostim -Ankara-/TÜRKİYE"
                                }
                            ]
                        }
                    ]
                }
            ]
        },
        new FooterBlockDto
        {
            Type = "table",
            Columns = 2,
            Rows =
            [
                new FooterTableRowDto
                {
                    Cells =
                    [
                        new FooterTableCellDto
                        {
                            Runs =
                            [
                                new FooterRunDto { Text = "Tel: +90 312 466 19 22     Faks: +90 312 426 85 14" }
                            ]
                        },
                        new FooterTableCellDto
                        {
                            Runs =
                            [
                                new FooterRunDto { Text = "Tel: +90 312 354 90 81     Faks: +90 312 354 90 82" }
                            ]
                        }
                    ]
                }
            ]
        }
    ];
}
