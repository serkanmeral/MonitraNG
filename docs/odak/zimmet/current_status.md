# Zimmet — current_status

**Son güncelleme:** 11 Temmuz 2026 (commit `6403d345` + `mngui` deploy)  
**Ortam:** Odak test (`192.168.20.20`)  
**Servis odağı:** MngOperations + Mng.Ui + MngDocument + Reporting / DI

---

## Son çalışılan konu

Zimmet dikeyi kapanış: **OC-1/OC-2**, çoklu iade, **Reporting katalog**, **DI tutanaklar (DOCX)** + “DI’da aç” yeni sekme.  
Kod: `6403d345` → GitHub `main`. UI Odak deploy: `mngui` (ui=200).

---

## Tamamlanan işler (bu oturum / birikim)

- AF-1: GIR → `createDatasetRows` + `sequence`
- OC-1: `deliver` → demirbaş `zimmetli` (multi `demirbasIds`)
- OC-2: `receive_return` → demirbaş `depoda` (multi `demirbasId`)
- Dataset tablo seçici + chip (`displayFields`)
- İade profil layout (personel + demirbaş görünür)
- Reporting: 6 Zimmet raporu + AF demirbaş `reportId`
- DI: `RPT_ZIMMET_PERSONEL` (xlsx), `RPT_ZIMMET_TESLIM` / `IADE` (docx)
- UI: `openDiResourceInNewTab` (runner / expand / child)

### Ertelemeler

- OC geçişinden otomatik belge
- Expand zimmet WI geçmişi
- TP-2 kolon filter, F4, hurda, AF görsel

---

## Devam eden / sıradaki

```text
GIR ✅ → Demirbaş ✅ → Verme ✅ → İade ✅ → Raporlar ✅ → DI ✅
         ➡️ sıradaki: OC tutanak otomasyonu veya expand geçmiş
```

| ID | Konu | Not |
|----|------|-----|
| **DI-OC** | Transition → tutanak | Rapor satırı yerine WI aksiyonu |
| **RPT-HIST** | Expand child WI | Audit |
| **F4** | Satınalma | PLAN §10 |

---

## Önemli notlar / komutlar

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\zimmet\scripts\seed-zimmet-reporting-all.ps1
# DI şablon yenileme:
.\docs\odak\zimmet\scripts\seed-zimmet-reporting-document-templates.ps1 -Replace
```

- Spec: `docs/odak/operationcore/mngoperations/CREATE_DATASET_ROWS_ACTION_SPEC.md`
- Picker: `docs/odak/operationcore/ui/OC_UI_DATASET_TABLE_PICKER.md`
- Devam: [DEVAM.md](./DEVAM.md)
