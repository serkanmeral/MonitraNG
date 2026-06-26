using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

internal static class DocxSectPrHelper
{
    internal static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static string UpsertSectionReferences(
        string documentXml,
        bool includeHeader,
        string headerRelId,
        bool includeFooter,
        string footerRelId)
    {
        var doc = XDocument.Parse(documentXml);
        var body = doc.Root?.Element(W + "body") ?? throw new InvalidOperationException("Invalid document.xml");
        var existing = body.Elements(W + "sectPr").LastOrDefault();

        var preserved = existing?.Elements()
            .Where(e => e.Name != W + "headerReference" && e.Name != W + "footerReference")
            .Select(e => new XElement(e))
            .ToList() ?? new List<XElement>();

        body.Elements(W + "sectPr").Remove();

        var sectPr = new XElement(W + "sectPr", preserved);

        if (includeHeader)
        {
            sectPr.Add(new XElement(W + "headerReference",
                new XAttribute(R + "id", headerRelId),
                new XAttribute(W + "type", "default")));
        }
        else if (existing is not null)
        {
            var headerRef = existing.Elements(W + "headerReference").FirstOrDefault();
            if (headerRef is not null)
                sectPr.Add(new XElement(headerRef));
        }

        if (includeFooter)
        {
            sectPr.Add(new XElement(W + "footerReference",
                new XAttribute(R + "id", footerRelId),
                new XAttribute(W + "type", "default")));
        }
        else if (existing is not null)
        {
            var footerRef = existing.Elements(W + "footerReference").FirstOrDefault();
            if (footerRef is not null)
                sectPr.Add(new XElement(footerRef));
        }

        if (!preserved.Any())
        {
            // ODK reference page size/margins (ODK-COC-23-202.docx)
            sectPr.Add(new XElement(W + "pgSz",
                new XAttribute(W + "w", "11910"),
                new XAttribute(W + "h", "16840")));
            sectPr.Add(new XElement(W + "pgMar",
                new XAttribute(W + "top", "1440"),
                new XAttribute(W + "right", "1797"),
                new XAttribute(W + "bottom", "1440"),
                new XAttribute(W + "left", "1797"),
                new XAttribute(W + "header", "709"),
                new XAttribute(W + "footer", "658"),
                new XAttribute(W + "gutter", "0")));
            sectPr.Add(new XElement(W + "cols",
                new XAttribute(W + "space", "708")));
        }

        body.Add(sectPr);
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append(doc.Root!.ToString(SaveOptions.DisableFormatting));
        return sb.ToString();
    }

    internal static string ApplyPageLayout(string documentXml, TemplatePageLayoutModel layout)
    {
        var doc = XDocument.Parse(documentXml);
        var body = doc.Root?.Element(W + "body") ?? throw new InvalidOperationException("Invalid document.xml");
        var existing = body.Elements(W + "sectPr").LastOrDefault();

        var preserved = existing?.Elements()
            .Where(e => e.Name != W + "pgSz" && e.Name != W + "pgMar" && e.Name != W + "cols")
            .Select(e => new XElement(e))
            .ToList() ?? new List<XElement>();

        body.Elements(W + "sectPr").Remove();

        var sectPr = new XElement(W + "sectPr", preserved);
        sectPr.Add(new XElement(W + "pgSz",
            new XAttribute(W + "w", OdakPageLayout.PageWidthTwips.ToString()),
            new XAttribute(W + "h", OdakPageLayout.PageHeightTwips.ToString())));
        sectPr.Add(new XElement(W + "pgMar",
            new XAttribute(W + "top", layout.MarginTopTwips.ToString()),
            new XAttribute(W + "right", layout.MarginRightTwips.ToString()),
            new XAttribute(W + "bottom", layout.MarginBottomTwips.ToString()),
            new XAttribute(W + "left", layout.MarginLeftTwips.ToString()),
            new XAttribute(W + "header", layout.HeaderDistanceTwips.ToString()),
            new XAttribute(W + "footer", layout.FooterDistanceTwips.ToString()),
            new XAttribute(W + "gutter", "0")));
        sectPr.Add(new XElement(W + "cols",
            new XAttribute(W + "space", OdakPageLayout.ColumnSpaceTwips.ToString())));

        body.Add(sectPr);

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append(doc.Root!.ToString(SaveOptions.DisableFormatting));
        return sb.ToString();
    }
}
