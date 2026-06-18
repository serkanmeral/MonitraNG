# Odak Uretim — Devam noktasi (checkpoint)

**Son guncelleme:** 12 Haziran 2026 (gece — oturum kapanisi)  
**Durum:** NCR/CAPA seed + board UI performans paketi **commit + push + deploy** tamam. Kullanici dogrulamasi: **her sey yolunda**.  
**Son commit:** `68974b4` — `fix(ui): dedupe board list network requests on open`  
**Onceki commitler (ayni oturum):** `774d823` (NCR/CAPA seed + loading panel) · `2bdd432` (board switch stale guard) · `730e585` (paralel load + lazy relation)  
**Deploy saglik:** `gateway=200 ui=200 oc_live=200` (mngui Odak test, 12 Haz ~00:41)

> **⭐ KALDIĞIMIZ YER (12 Haz 2026 — mola):** Odak Uretim workspace'inde **NCR + CAPA** kuyruklari seed ile guncellendi; board gecisinde **loading panel** ve **stale list race** duzeltildi; board liste acilisinda **network tekrarlari** giderildi (context kataloglari kullaniliyor, cift `list` yok). **Yarın buradan devam:** asagidaki **Sonraki adimlar** + bilinen acik noktalar. **Yeni chat:** bu dosya + `../operationcore/mngoperations/DEVAM.md` + gerekirse `../diagnostic/DEVAM.md`.

---

## Workspace / ortam

| Alan | Deger |
|------|--------|
| Workspace | Odak Uretim — `9f9cc085-81c7-4a92-9fa2-357ad5c654cd` |
| Uretim panosu (liste) | `75ec624c-a8be-4131-b072-9408ace1fd32` |
| NCR Kuyrugu | `fbc470c2-01a4-4992-b45a-bd1d099f59ab` |
| CAPA Kuyrugu | `bcd054f1-2e8b-44cd-8cca-718e3038d88b` |
| Referans kapali emir | ODF-0011 — `cf7fffb8-a3f2-44dc-b0a7-5d4ea382534a` |
| Gateway | http://192.168.20.20:5040 |
| UI (Odak test) | http://192.168.20.20:3000 |

---

## Bu oturumda tamamlananlar (11–12 Haziran 2026)

### NCR / CAPA seed (`774d823`)

| Konu | Sonuc |
|------|--------|
| Kalite kuyrugu | **NCR Kuyrugu** (kanban, 5 kolon) |
| Yeni board | **CAPA Kuyrugu** |
| Gecisler / profil | NCR containment, disposition gerekceleri; profil actions + layout ust bolumler |
| Otomasyon seed | `ncrSource: final`, `affectedQty` ← `rejectedQty` duzeltmesi |
| Migrasyon | `Find-OrCreate-ByNames` ile eski board adindan gecis |
| Sync | Odak seed json guncellendi (`odak-uretim-seed.json`) |

Script: `docs/odak/is_surecleri/scripts/seed-operation-core-odak-uretim.ps1`

### Board UI — gecis ve performans (`2bdd432`, `730e585`, `68974b4`)

| Konu | Dosya / mekanizma |
|------|-------------------|
| Loading panel | `OcBoardPanel` — context temizleme, `listBootstrapPending`, i18n |
| Stale list race | `boardDataEpoch` + `loadBoardListPage(expectedBoardId)` |
| Paralel yukleme | `loadBoard` + ilk `list` → `Promise.all`; kanban `?view=kanban` atlanir |
| Lazy relation | Filtre paneli acilinca `ensureRelationOptions` |
| Board cache | `loadBoardsForWorkspace` force kaldirildi (dashboard atamasi haric) |
| Network dedupe | Context `catalogs` varken DG states/priorities/types atlanir; `ocGetWorkspace` inflight birlestirme; prefetch sonrasi `lastSignature` = `buildListRequest()` |

**Beklenen network (board list tikla):** `board context` + tek `list` + (varsa) `op_dashboards` — DG katalog/workspace tekrarlari **olmamali**.

### Diagnostic (Odak)

Raporlar: `docs/odak/diagnostic/reports/oc_pages_odak_uretim_list_20260611_235331.json`, `oc_pages_odak_uretim_ncr_20260611_235448.json`, `di_pages_odak_20260611_235448.json`

---

## Bilinen / acik noktalar

1. **NCR anahtar formati** — ornek `ODF-0024` (`NCR-…` prefix beklenmiyor olabilir; dogrulanmadi)
2. **assignee sablonu** — `{{source.assignee}}` otomasyonda cozulmemis gorunebilir
3. **NCR kapaninca** — parent ODF `resume_from_hold` ve NCR→CAPA otomasyonu bu tur **kapsam disi** birakildi
4. **Kanban performans** — kolon basina ayri sorgu; 9 kolonlu uretim panosunda yavas (liste/profil iyilesti)
5. **NCR/CAPA form yardim** — ertelendi
6. **Workspace otomasyonu (SW-A0…)** — planlama tamam; **implementasyon baslamadi** ([WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md))

---

## Sonraki adimlar (oncelik)

### Oncelik 1 — Uretim / kalite is akisi

1. NCR profil ve gecislerin tarayicida tam lifecycle testi (contain → disposition → kapat)
2. Seed veri tutarliligi: NCR key prefix, assignee cozumu (gerekirse seed veya otomasyon duzeltmesi)
3. NCR kapaninca ODF resume — is kurali / otomasyon karari (musteri ile netlestir)

### Oncelik 2 — Workspace otomasyonu implementasyonu

| Faz | Is |
|-----|-----|
| SW-A0 | `op_workspace_automations` dataset + generator + setup |
| SW-A1 | MO `WorkspaceAutomationService` + transition hook |
| SW-A2 | UI Otomatik isler sekmesi |
| SW-A3 | Odak seed (NCR otomasyonu) + E2E |
| SW-A4 | Simule et, activity, i18n |

### Oncelik 3 — Performans (opsiyonel)

1. Kanban kolon sorgulari — batch veya MO tarafinda optimizasyon
2. Tarayici waterfall ile board acilis regresyon kontrolu (`68974b4` sonrasi)

---

## Kurulum / deploy (Odak test)

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1
```

UI deploy:
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui
```

---

## Yeni chat — yapistirilacak ozet

```
Odak Uretim DEVAM.md ⭐ (12 Haz 2026) okuyarak devam et.

Tamamlanan: NCR/CAPA seed, board loading panel, stale list fix, board liste network dedupe (68974b4 deploy OK).

Siradaki: NCR lifecycle tarayici testi; SW-A0 workspace otomasyonu; bilinen seed/otomasyon aciklari (NCR key, assignee).

Odak: http://192.168.20.20:3000 · ws 9f9cc085-… · uretim board 75ec624c-… · NCR fbc470c2-…
```

---

## Ilgili dokumanlar

- [README.md](./README.md)
- [referans/ODAK_URETIM_WORKSPACE_TASLAK.md](./referans/ODAK_URETIM_WORKSPACE_TASLAK.md)
- [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md)
- [../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md](../operationcore/mngoperations/WORKSPACE_AUTOMATION_PLANNING.md)
- [../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md](../operationcore/ui/OC_UI_WORKSPACE_AUTOMATIONS.md)
- [../diagnostic/DEVAM.md](../diagnostic/DEVAM.md)
