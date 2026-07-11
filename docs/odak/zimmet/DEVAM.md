# Zimmet — Kaldığımız Yer

**Son güncelleme:** 11 Temmuz 2026 (oturum kapanışı)  
**Durum:** ✅ Uçtan uca çekirdek süreç Odak test’te hazır (GIR → demirbaş → verme → iade → raporlar → DI belgeler)  
**Sıradaki (önerilen):** OC geçişinden otomatik tutanak · demirbaş expand zimmet WI geçmişi · (isteğe bağlı) F4 satınalma / hurda

**Plan:** [PLAN.md](./PLAN.md) · **Özet:** [README.md](./README.md) · **Durum:** [current_status.md](./current_status.md)

---

## ⭐ Kaldığımız yer (11 Temmuz 2026)

Bu oturumda zimmet dikeyi **operasyon + rapor + DI** ile kapatıldı:

1. **OC-1 / OC-2** — `updateDatasetRows`: teslimde zimmetle, iadede depoya dön  
2. **Çoklu demirbaş** — verme `demirbasIds`, iade `demirbasId` (multi) + profil chip görünümü  
3. **Reporting** — Zimmet kategorisi: depo/ürün/grup + demirbaş envanteri + garanti + personel özeti  
4. **DI** — personel dökümü (XLSX), teslim/iade tutanakları (**DOCX**); “DI’da aç” yeni sekme  
5. **UI** — dataset tablo seçici, picker chip, `openDiResourceInNewTab`

**Ortam:** Odak `192.168.20.20` · UI local veya `mngui` deploy  
**Browse:** `/apps/reporting/browse` → **Zimmet**

---

## Son oturum özeti

| Alan | Sonuç |
|------|--------|
| AF-1 GIR → demirbaş | ✅ `createDatasetRows` + `sequence` |
| ZIM verme | ✅ multi picker + OC-1 `deliver` |
| ZIM iade | ✅ multi + profil özet + OC-2 `receive_return` |
| Raporlar | ✅ 6 rapor seed (`seed-zimmet-reporting-all.ps1`) |
| DI belgeler | ✅ 3 şablon (PERSONEL / TESLIM / IADE) |
| Profil layout | ✅ personel + demirbaş chip’leri görünür |

### Ertelemeler (bilinçli)

- OC transition sonrası otomatik tutanak (şimdi rapor satırından)  
- Demirbaş expand → zimmet WI geçmişi sekmesi  
- Garanti “bugüne kadar” varsayılan filtre  
- Hurda / kayıp ayrı akış, F4 satınalma, AF ürün görseli, TP-2 kolon filter UI  

---

## Tamamlanan işler (birikimli)

### F0 — Dataset + Automated Forms ✅

Script: `scripts/setup-zimmet-datasets-and-forms.ps1`  
Demirbaş AF → `listView.reportId = rpt_zimmet_demirbaslar`

### F1 — Master seed ✅

Script: `scripts/seed-zimmet-master-data.ps1` · `seed/zimmet_master_ids.json`

### F2–F3 — Operation Core ✅

| Workspace | Prefix | Tipler |
|-----------|--------|--------|
| Zimmet Depo | `GIR` | Depo girişi |
| Personel Zimmet | `ZIM` | Zimmet verme, Zimmet iade |

Script: `scripts/seed-operation-core-zimmet.ps1` · `seed/zimmet-oc-seed.json`

### AF-1 / OC-1 / OC-2 ✅

| Kural seed | Geçiş | Etki |
|------------|-------|------|
| `zimmet-rule-gir-create-demirbas.json` | GIR kapanış | demirbaş üret |
| `zimmet-rule-zim-deliver-update-demirbas.json` | `deliver` | `durum=zimmetli` |
| `zimmet-rule-zim-return-update-demirbas.json` | `receive_return` | `durum=depoda`, clear zimmet alanları |

### Reporting + DI ✅

| Script / seed | Açıklama |
|---------------|----------|
| `seed-zimmet-reporting-all.ps1` | 6 rapor + DI şablon çağrısı |
| `seed-zimmet-reporting-*.json` | Katalog tanımları |
| `seed-zimmet-reporting-document-templates.json` | DI şablon meta |
| `ReportingZimmetTemplateXlsxFactory` | Personel dökümü XLSX |
| `ReportingZimmetTutanakDocxFactory` | Teslim / iade DOCX |

**Rapor id’leri:** `rpt_zimmet_depolar`, `rpt_zimmet_urunler`, `rpt_zimmet_urun_gruplari`, `rpt_zimmet_demirbaslar`, `rpt_zimmet_garanti`, `rpt_zimmet_personel`  
**DI kodları:** `RPT_ZIMMET_PERSONEL`, `RPT_ZIMMET_TESLIM`, `RPT_ZIMMET_IADE`

---

## Nasıl denerim

1. Token: `.\docs\odak\operationcore\scripts\get-operationcore-token.ps1`  
2. Raporlar (idempotent): `.\docs\odak\zimmet\scripts\seed-zimmet-reporting-all.ps1`  
3. UI: `/apps/reporting/browse` → Zimmet → Personel özeti / Demirbaş envanteri  
4. Expand satır → teslim/iade tutanağı → **DI’da aç** (yeni sekme)  
5. OC: Personel Zimmet → verme / iade WI akışı  

---

## Sıradaki adaylar

| Öncelik | Konu |
|---------|------|
| 1 | OC `deliver` / `receive_return` sonrası tutanak üretimi (veya profil aksiyonu) |
| 2 | Demirbaş rapor expand → zimmet WI geçmişi (child tab) |
| 3 | F4 satınalma / hurda / AF görsel (PLAN §10) |

---

## Önemli notlar

- Seed ID’leri **Odak test**’e özgü; prod’da yeniden üretilir.  
- `receive_return` zorunlu alanları boş (formda zaten dolu; geçişte picker kilitlenmesin diye).  
- Profil layout hem `demirbasIds` hem `demirbasId` içerir; boş alanlar gizlenir.  
- UI deploy: `sync-odak-source.ps1 -Paths Mng.Ui` + `deploy-odak-apps.ps1 -Services mngui -NoCache` ([deploy/README](../deploy/README.md)).
