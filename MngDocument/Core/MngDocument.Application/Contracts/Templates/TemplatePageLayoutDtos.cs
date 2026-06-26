namespace MngDocument.Application.Contracts.Templates;

/// <summary>Page margins and header/footer distances (Word twips: 1 cm ≈ 567).</summary>
public sealed class TemplatePageLayoutDto
{
    public int MarginTopTwips { get; init; } = 1440;
    public int MarginRightTwips { get; init; } = 1797;
    public int MarginBottomTwips { get; init; } = 1440;
    public int MarginLeftTwips { get; init; } = 1797;
    public int HeaderDistanceTwips { get; init; } = 709;
    public int FooterDistanceTwips { get; init; } = 658;
    public int FooterLeftIndentTwips { get; init; } = -567;
}

public sealed class UpdateTemplatePageStructureRequest
{
    public TemplatePageLayoutDto? PageLayout { get; set; }
    public TemplateLetterheadDto? Letterhead { get; set; }
    public TemplateFooterDto? Footer { get; set; }
}
