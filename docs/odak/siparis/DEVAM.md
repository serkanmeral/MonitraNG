# Odak Sipariş — Devam noktası (checkpoint)

**Son güncelleme:** 16 Haziran 2026 (akşam)  
**Durum:** ✅ NCR/CAPA hub UI · ✅ Yan menü · ✅ Odak `mngui` deploy · ⏳ Walkthrough · Faz 1b (PO PDF) sonra

> **⭐ KALDIĞIMIZ YER:** İş paketi listesinde **Kalite (NCR/CAPA)** expand sekmesi, deep link (`?expand=&tab=quality`), kalite kısayol butonu ve hızlı arama `clearable` null düzeltmesi **Odak test'e deploy edildi**. DG'de **501 NCR**, **25 CAPA** (17 pakette). Yan menü: İş Paketleri + **Müşteriler** eklendi/güncellendi. **Sıradaki:** walkthrough · Faz 1b PO PDF · döküman paketi (dosya yükü — ertelenmiş).

---

## Mimari karar (güncel)

```
Legacy MySQL (kalite) / SQL dump
    → DG POST
        odak_musteriler
        odak_is_paketleri
        odak_siparis_kalemleri
        odak_ncr              (501 kayıt)
        odak_capa             (25 kayıt)

Hub UI: native dialog (MO köprüsü yok) — NCR/CAPA
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

### CAPA deneme paketleri (UI walkthrough)

| Paket no | CAPA | Not |
|----------|------|-----|
| 2023-027 | 2 | Açık paket |
| 2021-043 | 3 | Kapalı — **Tümü** sekmesi |
| 2023-018 | 1 | CAPA-0023 açık |
| 2021-049 | 1 | Açık paket |

Deep link: `/apps/odak-siparis/packages?expand={dataId}&tab=quality`

---

## Oturumda tamamlanan işler (16 Haziran 2026 — akşam)

### Kalite hub (NCR/CAPA)

- [x] Dataset: `odak_ncr`, `odak_capa` + setup script
- [x] Legacy migrasyon: `migrate-legacy-ncs-to-dg.ps1` (501 NCR, 25 CAPA)
- [x] UI: `OdakSiparisQualityPanel`, NCR/CAPA panel + dialog (DG CRUD)
- [x] Expand panel **Kalite** sekmesi; URL sync (`expand`, `tab`)
- [x] Liste satırında kalite kısayolu (CertificateIcon)
- [x] i18n tr/en (`odakSiparis.quality.*`)
- [x] `normalizeNcrStatus()` — legacy encoding (Kapalı vb.)

### UI düzeltmeleri

- [x] Hızlı arama temizlenince `null.trim` hatası giderildi (`clearable` → null)

### Yan menü (Odak DG — canlı)

- [x] `patch-odak-siparis-side-menu.ps1` — filter + regex ID lookup (JSON parse sorunu)
- [x] Odak Sipariş header, İş Paketleri, **Müşteriler**

### Deploy

- [x] `mngui` Odak — 16 Haz (akşam)

---

## Hub UI dosyaları (kalite dahil)

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/pages/apps/odak-siparis/packages/index.vue` | Liste + expand + arama |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisPackageExpandPanel.vue` | Özet / kalemler / kalite |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisQualityPanel.vue` | NCR + CAPA sekmeleri |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisNcrPanel.vue` · `OdakSiparisNcrDialog.vue` | NCR |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisCapaPanel.vue` · `OdakSiparisCapaDialog.vue` | CAPA |
| `Mng.Ui/utils/odakSiparisNcrService.ts` · `odakSiparisCapaService.ts` | DG servisleri |
| `docs/odak/siparis/scripts/setup-odak-siparis-ncr-capa-datasets.ps1` | Dataset kurulum |
| `docs/odak/siparis/scripts/migrate-legacy-ncs-to-dg.ps1` | NCR/CAPA migrasyon |

**Odak UI:** http://192.168.20.20:3000/apps/odak-siparis/packages

---

## Faz ilerleme özeti

| Adım | Durum | Not |
|------|--------|-----|
| Paket/kalem migrasyon | ✅ | ~%99,7 |
| NCR/CAPA migrasyon + UI | ✅ | Deploy edildi |
| Yan menü | ✅ | Müşteriler eklendi |
| Walkthrough | ⏳ | Kullanıcı |
| Kalan 8 kalem + paket `"9"` | ⏳ | Veri |
| Faz 1b PO PDF | ⏳ | Dosya yükü öncesi ilk adım |
| Döküman paketi | 📋 | [DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md) |
| MO `workItemId` | ⏳ | Sonraki faz |

---

## Sonraki adımlar

1. **Walkthrough** — liste, arama (clear), expand, kalite NCR/CAPA
2. **Legacy Kalite karşılaştırma** — [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md)
3. Walkthrough bulguları → küçük UI düzeltmeleri
4. **Faz 1b** — müşteri PO PDF (object storage)
5. **Döküman paketi** v1 — NAS yükünü azaltmak için
6. Kalan 8 kalem + paket `"9"` manuel

---

## Script envanteri

| Script | Amaç |
|--------|------|
| `setup-odak-siparis-ncr-capa-datasets.ps1` | NCR/CAPA dataset |
| `migrate-legacy-ncs-to-dg.ps1` | Legacy ncs/cpas → DG |
| `export-legacy-ncs-from-mysql.ps1` | MySQL export |
| `patch-odak-siparis-side-menu.ps1` | Yan menü |
| `migrate-remaining-lines.ps1` | Kalan kalemler (dry-run: 0) |

**UI deploy:**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui
```

---

## Önemli notlar

- NCR/CAPA **global liste yok** — iş paketi expand → Kalite sekmesi
- Kapalı paketlerde NCR/CAPA için liste **Tümü** sekmesi gerekir
- Dosya/PDF eki henüz yok (Faz 1b+)
- `@side_menu` listesi bazen JSON string döner; patch script filter + regex kullanır

---

## Mimari dokümanlar

- [README.md](./README.md) · [FAZ_PLANI.md](./FAZ_PLANI.md) · [DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md)
- [LEGACY_KALITE_OVERVIEW.md](./LEGACY_KALITE_OVERVIEW.md)
