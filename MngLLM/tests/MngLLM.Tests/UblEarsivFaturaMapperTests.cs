using System.Text;
using MngLLM.Infrastructure.Services.Di;
using Xunit;

namespace MngLLM.Tests;

public class UblEarsivFaturaMapperTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:ProfileID>EARSIVFATURA</cbc:ProfileID>
          <cbc:ID>GIB2026000000001</cbc:ID>
          <cbc:UUID>bb66d6cb-9e7f-40b4-91a1-9d9cfbb444b3</cbc:UUID>
          <cbc:IssueDate>2026-06-18</cbc:IssueDate>
          <cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty>
            <cac:Party>
              <cac:PartyIdentification>
                <cbc:ID schemeID="VKN">9251313630</cbc:ID>
              </cac:PartyIdentification>
              <cac:PartyName>
                <cbc:Name>VITANOVA ORNEK LTD</cbc:Name>
              </cac:PartyName>
            </cac:Party>
          </cac:AccountingSupplierParty>
          <cac:AccountingCustomerParty>
            <cac:Party>
              <cac:PartyIdentification>
                <cbc:ID schemeID="VKN">6340420559</cbc:ID>
              </cac:PartyIdentification>
              <cac:PartyName>
                <cbc:Name>ODAK ORNEK AS</cbc:Name>
              </cac:PartyName>
            </cac:Party>
          </cac:AccountingCustomerParty>
          <cac:LegalMonetaryTotal>
            <cbc:TaxExclusiveAmount currencyID="TRY">138942</cbc:TaxExclusiveAmount>
            <cbc:PayableAmount currencyID="TRY">166730.4</cbc:PayableAmount>
          </cac:LegalMonetaryTotal>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity>1</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount>138942</cbc:LineExtensionAmount>
            <cac:Item>
              <cbc:Name>Yazilim Danismanlik Hizmeti</cbc:Name>
            </cac:Item>
          </cac:InvoiceLine>
        </Invoice>
        """;

    [Fact]
    public void Map_SampleUbl_ReturnsEarsivFaturaFields()
    {
        var mapper = new UblEarsivFaturaMapper();
        var bytes = Encoding.UTF8.GetBytes(SampleXml);

        var result = mapper.Map(bytes, "res-1");

        Assert.Equal("earsiv_fatura", result.SchemaId);
        Assert.Equal("EARSIVFATURA", result.ProfileId);
        Assert.Equal("SATIS", result.InvoiceType);
        Assert.Equal("GIB2026000000001", result.InvoiceId);
        Assert.Equal("bb66d6cb-9e7f-40b4-91a1-9d9cfbb444b3", result.Uuid);
        Assert.Equal("2026-06-18", result.IssueDate);
        Assert.Equal("TRY", result.Currency);
        Assert.Equal(166730.4m, result.PayableAmount);
        Assert.Equal(138942m, result.TaxExclusiveAmount);
        Assert.Equal("VITANOVA ORNEK LTD", result.SupplierName);
        Assert.Equal("9251313630", result.SupplierVkn);
        Assert.Equal("ODAK ORNEK AS", result.CustomerName);
        Assert.Equal("6340420559", result.CustomerVkn);
        Assert.Equal("ubl_xml", result.Source);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("res-1", result.ResourceId);
        Assert.NotNull(result.Lines);
        Assert.Contains(result.Lines!, l => l.Name == "Yazilim Danismanlik Hizmeti");
    }
}
