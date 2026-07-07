# MngDocument — Oturum durumu

## Son Çalışılan Konu

**8 Temmuz 2026 (gece):** Managed Office **O-0 → Pr2** tamamlandı (sheet + sunum); editör oturumları iyileştirmeleri; müşteri demo dosyaları.

**Roadmap:** [SHEET_ROADMAP.md](../odak/document_intelligence/SHEET_ROADMAP.md) · [DI_PRODUCT_ROADMAP.md §15–16](../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) · Checkpoint: [DEVAM.md](../odak/document_intelligence/DEVAM.md)

## Tamamlanan (bu oturum)

| ID | Özet |
|----|------|
| **O-0 / O-1** | `ManagedOfficeProfiles`, WOPI genelleme, `MinimalXlsxFactory` / `MinimalPptxFactory`, native API (`documents\|sheets\|presentations/native`) |
| **S1** | Yeni elektronik tablo UI + `diCreateNativeSheet` |
| **S2** | Gotenberg `export/pdf` (DOCX/XLSX/PPTX), `smoke-sheet-native-test.ps1` |
| **Pr1** | Yeni sunum UI + `diCreateNativePresentation` |
| **Pr2** | Sunum sürüm/PDF smoke, `smoke-presentation-pr2-test.ps1` |
| **D-E+** | Editör oturumları `officeKind` tür sütunu; DG toplu sorgu ile hızlı yenileme |
| **Demo** | `CollaboraDemoXlsxFactory` / `CollaboraDemoPptxFactory`, `publish-collabora-demos.ps1` |

**Deploy test:** `mngdocument` @ `192.168.20.20` ✅ · `mngui` deploy bu oturum sonunda planlandı.

## Sıradaki İşler

1. **D-BR2** — kapak sayfası kataloğu + üretimde opsiyonel seçim
2. **CoC/Activity** uçtan uca smoke (prod verisi varsa)
3. **S3 / Pr3** — şablondan sheet/sunum (senaryo netleşince)
4. **D-N1** — `document.generated` bildirim maili
5. **D4 / Managed Office prod deploy** — test doğrulandıktan sonra isteğe bağlı
6. Non-admin izin filtreleme canlı doğrulaması (DI-PERM açık borç)

## Önemli Notlar

- UI terimi: **Elektronik tablo** / **Spreadsheet** (kod içi `sheet` kalabilir).
- PDF export: Collabora menüsü + sunucu `export/pdf` ikisi de aktif.
- Demo factory’ler yalnızca `scripts/tests/MngDocument/demo/` aracıyla; production API’de yok.
- Smoke scriptleri editör oturum limiti için başta oturum temizliği yapar.

## Ortam

| Ortam | Gateway |
|-------|---------|
| **Test** | `192.168.20.20:5040` |
| **Prod** | `192.168.20.8:5040` |

## Son Güncelleme

**8 Temmuz 2026 (gece)** — O-0…Pr2 kapatıldı; test smoke geçti. Sırada D-BR2 veya CoC smoke.

## Nerede Kalmıştık

Managed Office çekirdeği (docx/xlsx/pptx) tamam. Sonraki odak: **D-BR2** (kapak) veya **CoC/Activity smoke**.
