# Odak Uretim — Devam noktasi (checkpoint)

**Son guncelleme:** 11 Haziran 2026 (otomasyon planlama v0.2 kapanisi)  
**Durum:** Odak test perf paketi **commit + deploy** tamam. **Workspace otomasyonu planlama tamam** — implementasyon SW-A0 bekliyor.  
**Git commit (perf):** `b00d6a4` — `perf(operation-core): Odak profil/board hizlandirma ve UI runtime iyilestirmeleri`  
**Deploy saglik:** `gateway=200 ui=200 oc_live=200` (mngui Odak test)

> **⭐ KALDIĞIMIZ YER (11 Haz 2026 — planlama v0.2):** Performans ve form yardimi tamam (asagida). **Kalite uygunsuzluk → NCR** icin workspace otomasyonu planlamasi **tamamlandi** (kod yok). **Model:** WI duruma geldi → hedef board'da is olustur + alan eslemesi; varsayilan `parentItemId` (ODF→NCR). Alarm/dokuman aksiyonlari semada hazir, MVP disi. **Dokumanlar:** [WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md), [OC_UI_WORKSPACE_AUTOMATIONS.md](../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md). **Siradaki:** SW-A0…A3 (dataset → MO → UI → Odak seed/E2E). **Yeni chat:** bu dosya + `operationcore/mngoperations/DEVAM.md` + planlama dokumanlari.

---

## Workspace / ortam

| Alan | Deger |
|------|--------|
| Workspace | Odak Uretim — `9f9cc085-81c7-4a92-9fa2-357ad5c654cd` |
| Uretim panosu boardId | `75ec624c-a8be-4131-b072-9408ace1fd32` |
| Referans kapali emir | ODF-0011 — `cf7fffb8-a3f2-44dc-b0a7-5d4ea382534a` |
| Gateway | http://192.168.20.20:5040 |
| UI (Odak test) | http://192.168.20.20:3000 |

---

## Bu oturumda tamamlananlar (11 Haz 2026)

### Performans (MO + UI) — commit `b00d6a4`

| Konu | Sonuc |
|------|--------|
| profile-view | Timeline varsayilan kapali; warm P95 ~1488 ms (once ~4588 ms) |
| Board list | warm P95 ~331 ms (once ~980 ms) |
| MO cache | poolFields, relation display, person/group pool keys |
| UI | Lazy timeline, MO poolFields, displayForm, gecis dialog fix |

Diagnostic: `docs/odak/diagnostic/reports/oc_pages_odak_uretim_post_perf_20260611_205036.json`

### Form yardimi

- Yeni emir formu: `docs/odak/is_surecleri/seed/odak_uretim_yeni_emir_form_help.md`
- Patch: `docs/odak/is_surecleri/scripts/patch-odak-uretim-form-help.ps1`

### Workspace otomasyonu planlama (v0.2)

| Konu | Karar |
|------|--------|
| Ilk senaryo | `hold_quality` + uygunsuz → NCR (Kalite kuyrugu), `parentItemId`=ODF |
| UI | **Otomatik isler** sekmesi (`tab=automations`) |
| Dataset | `op_workspace_automations` (tetik + aksiyon tek kayit) |
| Alan eslemesi | Uretim alanlari → NCR alanlari (lotSerial, qualityNotes, …) |
| Iliski | Varsayilan parent-child; coklu NCR serbest (`idempotency: none`) |
| Mevcut durum | Demo NCR **manuel** (seed script) |

Tam plan: [WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md) · UI: [OC_UI_WORKSPACE_AUTOMATIONS.md](../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md)

### Daha once (v0.2 sprint)

- Create form sadelestirme, gecis zorunluluklari, kismi sevkiyat alanlari
- Referans seed ODF-0011 (kapali PO)

---

## Bilinen / acik noktalar

1. **Kanban performans** — diagnostic WARN (~4,6 sn P95); liste/profil iyilesti
2. **Sevkiyat birikimi** (`shippedQty`) — generic rule engine; MO hardcode yok
3. **Yetkilendirme** — rol matrisi musteri talebi bekliyor
4. **NCR/CAPA form yardim** — ertelendi
5. **View-only gecis UX** — buton gorunur, 403 bilinen

---

## Sonraki adimlar

### Oncelik 1 — Workspace otomasyonu implementasyonu

| Faz | Is |
|-----|-----|
| SW-A0 | `op_workspace_automations` dataset + generator + setup |
| SW-A1 | MO `WorkspaceAutomationService` + transition hook |
| SW-A2 | UI Otomatik isler sekmesi |
| SW-A3 | Odak seed (NCR otomasyonu) + E2E; manuel demo script NCR kaldir |
| SW-A4 | Simule et, activity, i18n |

### Oncelik 2 — Odak dogrulama (paralel)

1. Liste, gelismis arama, ODF-0011 profil, Yeni is Yardim dialog
2. Tam lifecycle testi (plan → uretim → kalite → sevkiyat)

---

## Kurulum (Odak test)

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1
```

Deploy UI:
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\sync-odak-source.ps1 -Paths @('Mng.Ui') }"
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\deploy-odak-apps.ps1 -Services mngui }"
```

---

## Ilgili dokumanlar

- [README.md](./README.md)
- [referans/ODAK_URETIM_WORKSPACE_TASLAK.md](./referans/ODAK_URETIM_WORKSPACE_TASLAK.md)
- [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md)
- [../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md)
- [../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md](../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md)
