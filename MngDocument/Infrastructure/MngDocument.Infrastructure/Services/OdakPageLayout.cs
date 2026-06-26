namespace MngDocument.Infrastructure.Services;

/// <summary>Page layout defaults from ODK-COC reference DOCX (ODK-COC-23-202.docx).</summary>
internal static class OdakPageLayout
{
    internal const int DefaultMarginTopTwips = 1440;
    internal const int DefaultMarginRightTwips = 1797;
    internal const int DefaultMarginBottomTwips = 1440;
    internal const int DefaultMarginLeftTwips = 1797;
    internal const int DefaultHeaderDistanceTwips = 709;
    internal const int DefaultFooterDistanceTwips = 658;
    internal const int DefaultFooterLeftIndentTwips = -567;

    internal const int PageWidthTwips = 11910;
    internal const int PageHeightTwips = 16840;
    internal const int ColumnSpaceTwips = 708;

    internal const string SectionPropertiesXml = """
        <w:sectPr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:pgSz w:w="11910" w:h="16840"/>
          <w:pgMar w:top="1440" w:right="1797" w:bottom="1440" w:left="1797" w:header="709" w:footer="658" w:gutter="0"/>
          <w:cols w:space="708"/>
        </w:sectPr>
        """;

    internal const int FooterLeftIndentTwips = DefaultFooterLeftIndentTwips;
}
