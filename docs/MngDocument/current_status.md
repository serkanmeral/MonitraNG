# MngDocument — Oturum durumu

## Son Çalışılan Konu

**8 Temmuz 2026 (sabah):** Odak **G0–G5** generation runtime tamamlandı; XLSX sevkiyat listesi IX04’te canlı doğrulandı; kritik `DataSourceTokenResolver` JsonElement fix.

**Roadmap:** [DI_PRODUCT_ROADMAP.md §26](../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) · [SHEET_ROADMAP.md](../odak/document_intelligence/SHEET_ROADMAP.md) · Checkpoint: [DEVAM.md](../odak/document_intelligence/DEVAM.md)

## Tamamlanan (bu oturum)

| ID | Özet |
|----|------|
| **G0** | `POST /generate/run` RuntimeEnvelope; producer/context API |
| **G1** | `DgDataSourceExecutor`, `DocumentContextLoader`, context katalog |
| **G2** | DOCX `DocxTableExpander`; Activity `shipmentLines` tablo; smoke |
| **G3** | `dm_document_context_types` seed + provider |
| **G4** | `dm_data_sources` + `dm_document_producers` katalog; `dataSourceRef` |
| **G5** | XLSX renderer; `outputFormat: xlsx`; `SHIPMENT-LIST-STD`; Odak UI «Listeyi üret» |
| **G5-fix** | `DataSourceTokenResolver` JsonElement; `DocumentParameterResolver` format; `XlsxTemplateBytesResolver` fallback; `PackageShipmentLinesQueryFallback` |

**Önceki oturum (gece):** Managed Office O-0→Pr2 ✅ (sheet + sunum).

**Deploy test:** `mngdocument` + `mngui` @ `192.168.20.20` ✅ (8 Tem sabah).

## Sıradaki İşler

1. **G5+** — iş paketine sevkiyat listesi writeback (kalıcı listeleme); prod deploy
2. **G6** — work item bağlantısı ⏸️ ertelendi
3. **D-BR2** — kapak sayfası kataloğu
4. **CoC/Activity** uçtan uca smoke (prod verisi)
5. **D-N1** — `document.generated` bildirim maili
6. **S3 / Pr3** — şablondan sheet/sunum (senaryo)

## Önemli Notlar

- Sevkiyat listesi üretiminde iş paketine **writeback yok**; silme = DI `Odak/Sevkiyat/{packageNo}` klasöründen XLSX silmek yeterli.
- Her üretim yeni `dm_resources` kaydı oluşturur (idempotency yok).
- IX04 test paketi: `2d8aeb0e-6f67-4f3a-a578-21cff682ec17` — 34 sevkiyat satırı dolu.
- Smoke: `scripts/tests/MngDocument/smoke-shipment-list-xlsx-test.ps1 -PackageId <uuid>`

## Ortam

| Ortam | Gateway |
|-------|---------|
| **Test** | `192.168.20.20:5040` |
| **Prod** | `192.168.20.8:5040` |

## Son Güncelleme

**8 Temmuz 2026 (sabah)** — G0–G5 kapatıldı; IX04 XLSX doğrulandı. Sırada writeback / prod / D-BR2.

## Nerede Kalmıştık

Odak generation çekirdeği (DOCX tablo + XLSX sevkiyat listesi) tamam. Sonraki odak: **iş paketi writeback**, **prod deploy**, veya **D-BR2 / CoC smoke**.
