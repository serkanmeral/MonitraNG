# Odak Sipariş — Devam noktası (checkpoint)

**Son güncelleme:** 16 Haziran 2026 (gece)  
**Durum:** ✅ Faz 1 kalite deploy · ✅ Faz 1b PO PDF + export (kod) · ✅ PO migrasyon POC (7 paket DG) · ⏳ Sunum walkthrough

> **⭐ KALDIĞIMIZ YER:** **Faz 1b** tamamlandı (local + commit): PO PDF paneli (modal önizleme, DG `file` alanı), liste **Dışa aktar** (CSV/Excel), legacy PO migrasyon scriptleri + **7 paket POC** Odak DG'de. **mngui deploy** bu oturumda yapıldı. **Yarın:** müşteri sunumu (demo paketleri aşağıda) · fiyat alanları · tam PO migrasyon (uploads sync).

---

## Mimari karar (güncel)

```
Legacy MySQL (kalite) / SQL dump
    → DG POST
        odak_musteriler
        odak_is_paketleri   (+ poDocument file alanı)
        odak_siparis_kalemleri
        odak_ncr              (501 kayıt)
        odak_capa             (25 kayıt)

Hub UI: native dialog (MO köprüsü yok) — NCR/CAPA + PO PDF
MO (op_work_items): sonraki faz
```

Detay: [MIMARI_KARAR.md](./MIMARI_KARAR.md) · [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md)

---

## Migrasyon sonuçları (Odak test)

| Kaynak | DG | Not |
|--------|-----|-----|
| Paketler | **824** | 1 paket (`"9"`) SQL tuple bozuk |
| Kalemler | **2759** / 2767 (~%99,7) | Otomatik kalan: 0 |
| NCR | **501** | 57 paketsiz (legacy) |
| CAPA | **25** | 17 pakette; 9 açık |
| Müşteriler | **89** | +2 seed |
| PO PDF (migrasyon POC) | **7 paket** | `legacy-po-pdf-migration-report.json` |

---

## Müşteri sunumu — demo paketleri (17 Haziran 2026)

**Liste:** http://192.168.20.20:3000/apps/odak-siparis/packages

### Kalite (NCR / CAPA)

| Paket | Durum | NCR | CAPA | Deep link |
|-------|--------|-----|------|-----------|
| **2023-027** | Açık | 3 | 2 | `?expand=02e23dc7-4148-4277-a4c9-1479824f3a32&tab=quality` |
| **2021-049** | Açık | 10 | 1 | `?expand=7d715f76-8dd8-4732-ad5c-4eddd36151ec&tab=quality` |
| **2021-043** | Kapalı | 5 | 3 | `?expand=f347226c-241d-4d03-880e-1be847f66eeb&tab=quality` (**Tümü** sekmesi) |
| **2023-018** | Kapalı | 3 | 1 | `?expand=4ee02a9e-1539-4f26-bcbd-b8bf34d75578&tab=quality` |

### PO PDF (migrasyon POC)

| Paket | PDF | PO rev. | Deep link (Özet) |
|-------|-----|---------|------------------|
| **2022-012** | 2022-012.pdf | — | `?expand=da050de4-fbd7-431f-a2fa-a108e8989b98` |
| **2021-067** | 2021-067_2.pdf | 2 | `?expand=e638f4df-72a7-4fe5-943a-3d30adf53e12` |
| **2023-077** | 2023-077.pdf | — | `?expand=3e00d36e-96d6-480b-8a11-e42c25e1ed0a` |

Diğer POC: 2022-013 … 2022-016 (hepsi kapalı, PO yüklü). **Not:** PO ile NCR/CAPA aynı pakette yok; sunumda ayrı paketlerle göster.

---

## Oturumda tamamlanan işler (16 Haziran 2026 — gece)

### Faz 1b — PO PDF

- [x] Dataset: `odak_is_paketleri.poDocument` (`fieldType: file`)
- [x] UI: `OdakSiparisPoDocumentPanel` — liste, modal önizleme, yükle/kaydet/indir
- [x] `odakSiparisPoService.ts` — DG file sözleşmesi `{ content, originalFileName }`
- [x] Önizleme: blob + `application/pdf` MIME (`typedBlobForPreview`, OC ekleri ile paylaşımlı)
- [x] Expand özet: iki sütun (özet + PO kartı)

### Faz 1b — Export

- [x] İş paketi listesi **Dışa aktar** (UTF-8 CSV, Excel uyumlu, filtreler dahil)
- [x] `odakSiparisPackageExport.ts` · `filterPackagesByLineAdv` ortak

### Legacy PO migrasyon

- [x] `LegacyPoFileCommon.ps1` — CakePHP path: `{polink}{packageNo}_{poVersion}.pdf`
- [x] `export-legacy-po-candidates-from-mysql.ps1`
- [x] `migrate-legacy-po-pdf-to-dg.ps1` — POC **7/7 OK** (Odak DG)

### Refactor / düzeltmeler

- [x] `odakSiparisDateUtils.ts` — Nuxt duplicate import uyarısı giderildi
- [x] `ocAttachmentPreview.ts` — paylaşımlı MIME helper

### Deploy

- [x] Commit + push + `mngui` Odak — 16 Haz (gece)

---

## Hub UI dosyaları (Faz 1 + 1b)

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/pages/apps/odak-siparis/packages/index.vue` | Liste + export + expand |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisPackageExpandPanel.vue` | Özet / kalemler / kalite |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisPoDocumentPanel.vue` | PO PDF |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisQualityPanel.vue` | NCR + CAPA |
| `Mng.Ui/utils/odakSiparisPoService.ts` | PO upload/preview/save |
| `Mng.Ui/utils/odakSiparisPackageExport.ts` | CSV export |
| `docs/odak/siparis/scripts/migrate-legacy-po-pdf-to-dg.ps1` | Legacy PO → DG |

**Odak UI:** http://192.168.20.20:3000/apps/odak-siparis/packages

---

## Faz ilerleme özeti

| Adım | Durum | Not |
|------|--------|-----|
| Paket/kalem migrasyon | ✅ | ~%99,7 |
| NCR/CAPA migrasyon + UI | ✅ | Deploy edildi |
| Faz 1b PO PDF UI | ✅ | Deploy edildi |
| Faz 1b Export | ✅ | Deploy edildi |
| PO PDF migrasyon POC | ✅ | 7 paket; tam migrasyon bekliyor |
| Faz 1b fiyat alanları | ⏳ | Sonraki |
| Kalan 8 kalem + paket `"9"` | ⏳ | Veri |
| Döküman paketi | 📋 | [DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md) |
| MO `workItemId` | ⏳ | Sonraki faz |

---

## Sonraki adımlar (17 Haziran 2026)

1. **Müşteri sunumu** — demo paketleri (yukarı tablo)
2. Sunum geri bildirimi → küçük UX düzeltmeleri
3. **Faz 1b fiyat alanları** — birim/toplam, rol bazlı görünürlük
4. **Tam PO PDF migrasyon** — `sync-legacy-from-server` + batch `-SkipExisting`
5. Kalan 8 kalem + paket `"9"`

---

## Script envanteri

| Script | Amaç |
|--------|------|
| `migrate-legacy-po-pdf-to-dg.ps1` | Legacy PO PDF → DG `poDocument` |
| `export-legacy-po-candidates-from-mysql.ps1` | PO aday listesi |
| `migrate-legacy-ncs-to-dg.ps1` | NCR/CAPA |
| `patch-odak-siparis-side-menu.ps1` | Yan menü |

**UI deploy:**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui
```

---

## Önemli notlar

- PO PDF: DG `file` alanı; kayıt sonrası `{ path, file_name, ... }`
- Önizleme: `fetchBlobFromDataGateway` + PDF MIME (proxy URL iframe'de çalışmıyordu)
- Kapalı paketlerde kalite: **Tümü** sekmesi
- Local uploads'ta sadece ~7 MUSTERI_PO PDF var; tam migrasyon için sunucu sync gerekir

---

## Mimari dokümanlar

- [README.md](./README.md) · [FAZ_PLANI.md](./FAZ_PLANI.md) · [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md)
- [LEGACY_KALITE_OVERVIEW.md](./LEGACY_KALITE_OVERVIEW.md)
