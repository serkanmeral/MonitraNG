using System.Globalization;
using System.Xml.Linq;
using MngLLM.Application.DTOs.Di;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;

namespace MngLLM.Infrastructure.Services.Di;

/// <summary>
/// Maps Turkish UBL-TR e-Archive / e-Invoice XML into <see cref="EarsivFaturaExtractDto"/>.
/// Namespace-agnostic (matches on LocalName).
/// </summary>
public sealed class UblEarsivFaturaMapper : IUblEarsivFaturaMapper
{
    public EarsivFaturaExtractDto Map(byte[] xmlBytes, string? resourceId = null)
    {
        if (xmlBytes is null || xmlBytes.Length == 0)
            throw new DiExtractException("XML content is empty.", 422);

        XDocument doc;
        try
        {
            using var stream = new MemoryStream(xmlBytes);
            doc = XDocument.Load(stream, LoadOptions.None);
        }
        catch (Exception ex)
        {
            throw new DiExtractException("Invalid XML content.", 422, ex);
        }

        var root = doc.Root ?? throw new DiExtractException("XML root element missing.", 422);
        if (!string.Equals(root.Name.LocalName, "Invoice", StringComparison.OrdinalIgnoreCase))
            throw new DiExtractException($"Expected UBL Invoice root, got '{root.Name.LocalName}'.", 422);

        var profileId = RequireChild(root, "ProfileID");
        var invoiceType = RequireChild(root, "InvoiceTypeCode");
        var invoiceId = RequireChild(root, "ID");
        var uuid = RequireChild(root, "UUID");
        var issueDate = RequireChild(root, "IssueDate");
        var currency = RequireChild(root, "DocumentCurrencyCode");

        var monetary = Child(root, "LegalMonetaryTotal")
            ?? throw new DiExtractException("LegalMonetaryTotal missing.", 422);
        var payable = RequireDecimal(monetary, "PayableAmount");
        var taxExclusive = TryDecimal(ChildValue(monetary, "TaxExclusiveAmount"));

        var supplierParty = Child(root, "AccountingSupplierParty");
        var customerParty = Child(root, "AccountingCustomerParty");

        var lines = root.Elements()
            .Where(e => e.Name.LocalName == "InvoiceLine")
            .Select(MapLine)
            .Where(l => l is not null)
            .Cast<EarsivFaturaLineDto>()
            .ToList();

        return new EarsivFaturaExtractDto
        {
            SchemaId = "earsiv_fatura",
            SchemaVersion = 1,
            ProfileId = profileId,
            InvoiceType = invoiceType,
            InvoiceId = invoiceId,
            Uuid = uuid,
            IssueDate = issueDate,
            Currency = currency,
            PayableAmount = payable,
            TaxExclusiveAmount = taxExclusive,
            SupplierName = FindPartyName(supplierParty),
            SupplierVkn = FindPartyVkn(supplierParty),
            CustomerName = FindPartyName(customerParty),
            CustomerVkn = FindPartyVkn(customerParty),
            Lines = lines.Count > 0 ? lines : null,
            Source = "ubl_xml",
            Confidence = 1.0,
            ResourceId = resourceId
        };
    }

    private static EarsivFaturaLineDto? MapLine(XElement line)
    {
        var name = line.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Name")
            ?.Value?.Trim();

        return new EarsivFaturaLineDto
        {
            LineId = ChildValue(line, "ID"),
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Quantity = TryDecimal(ChildValue(line, "InvoicedQuantity")),
            LineExtensionAmount = TryDecimal(ChildValue(line, "LineExtensionAmount"))
        };
    }

    private static string? FindPartyName(XElement? partyContainer)
    {
        if (partyContainer is null) return null;
        var partyName = partyContainer.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "PartyName");
        var name = partyName is null ? null : ChildValue(partyName, "Name");
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private static string? FindPartyVkn(XElement? partyContainer)
    {
        if (partyContainer is null) return null;

        foreach (var id in partyContainer.Descendants().Where(e => e.Name.LocalName == "ID"))
        {
            var scheme = id.Attributes()
                .FirstOrDefault(a => a.Name.LocalName is "schemeID" or "schemeId")
                ?.Value;
            if (string.Equals(scheme, "VKN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scheme, "VKN_TCKN", StringComparison.OrdinalIgnoreCase))
            {
                var v = id.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }

        return null;
    }

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? ChildValue(XElement parent, string localName) =>
        Child(parent, localName)?.Value?.Trim();

    private static string RequireChild(XElement parent, string localName)
    {
        var value = ChildValue(parent, localName);
        if (string.IsNullOrWhiteSpace(value))
            throw new DiExtractException($"Required UBL field '{localName}' is missing.", 422);
        return value;
    }

    private static decimal RequireDecimal(XElement parent, string localName)
    {
        var raw = ChildValue(parent, localName);
        var parsed = TryDecimal(raw);
        if (parsed is null)
            throw new DiExtractException($"Required numeric field '{localName}' is missing or invalid.", 422);
        return parsed.Value;
    }

    private static decimal? TryDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }
}
