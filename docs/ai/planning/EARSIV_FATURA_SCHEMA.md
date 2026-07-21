# Şema: earsiv_fatura (MVP)

**SchemaId:** `earsiv_fatura`  
**Versiyon:** 1  
**Kaynak önceliği:** UBL-TR XML → (yedek) LLM / PDF metin  
**JSON Schema:** [../seeds/earsiv-fatura.schema.json](../seeds/earsiv-fatura.schema.json)

---

## Amaç

Müşteri flow’larında kullanılacak **minimum fatura alanları**.  
Keşif etiketleri (Auto-Tag) bu şemanın parçası değildir.

---

## Alanlar (çekirdek)

| Alan | Tip | Zorunlu | UBL ipucu | Flow kullanımı |
|------|-----|---------|-----------|----------------|
| `schemaId` | string | evet | sabit `earsiv_fatura` | |
| `schemaVersion` | number | evet | `1` | |
| `profileId` | string | evet | `cbc:ProfileID` | Filtre (EARSIVFATURA) |
| `invoiceType` | string | evet | `cbc:InvoiceTypeCode` | SATIS / IADE… |
| `invoiceId` | string | evet | `cbc:ID` (fatura no) | Referans / idempotency |
| `uuid` | string | evet | `cbc:UUID` | Tekil kimlik |
| `issueDate` | date (ISO) | evet | `cbc:IssueDate` | Tarih koşulu |
| `currency` | string | evet | `cbc:DocumentCurrencyCode` | |
| `payableAmount` | number | evet | `cac:LegalMonetaryTotal/cbc:PayableAmount` | **Eşik / onay** |
| `taxExclusiveAmount` | number | hayır | `cbc:TaxExclusiveAmount` | |
| `supplierName` | string | hayır | AccountingSupplierParty PartyName | |
| `supplierVkn` | string | hayır | Supplier party VKN | Tedarikçi kuralı |
| `customerName` | string | hayır | AccountingCustomerParty PartyName | |
| `customerVkn` | string | hayır | Customer party VKN | |
| `lines` | array | hayır | InvoiceLine | İleri faz |
| `source` | string | evet | `ubl_xml` \| `llm_pdf` \| `mixed` | Audit |
| `confidence` | number | evet | parse=1.0; LLM 0–1 | |

### `lines[]` öğesi (opsiyonel)

| Alan | Tip |
|------|-----|
| `lineId` | string |
| `name` | string |
| `quantity` | number |
| `lineExtensionAmount` | number |

---

## Örnek (referans paket)

Kaynak örnek: Turkcar e-arşiv paketi (`ProfileID=EARSIVFATURA`, `SATIS`, yazılım danışmanlık hizmeti).

```json
{
  "schemaId": "earsiv_fatura",
  "schemaVersion": 1,
  "profileId": "EARSIVFATURA",
  "invoiceType": "SATIS",
  "invoiceId": "GIB2026000000001",
  "uuid": "bb66d6cb-9e7f-40b4-91a1-9d9cfbb444b3",
  "issueDate": "2026-06-18",
  "currency": "TRY",
  "payableAmount": 166730.4,
  "taxExclusiveAmount": 138942,
  "supplierName": "VİTANOVA BİLİŞİM DANIŞMANLIK HİZMETLERİ SANAYİ VE TİCARET LİMİTED ŞİRKETİ",
  "supplierVkn": "9251313630",
  "customerName": "ODAK KOMPOZİT TEKNOLOJİLERİ ANONİM ŞİRKETİ",
  "customerVkn": "6340420559",
  "lines": [
    { "lineId": "1", "name": "Yazılım Danışmanlık Hizmeti" }
  ],
  "source": "ubl_xml",
  "confidence": 1.0
}
```

---

## Bilinçli dışarı (v1)

- Matbu / yabancı fatura  
- Yalnızca görüntü PDF (OCR)  
- GİB portal canlı çekim  
- Muhasebe fişine tam mapping (hesap kodu vb.)
