# Platform Checkpoint — SIEM öncesi tamamlanacaklar

**Kural:** Bu dokümandaki **Definition of Done** yeşil olmadan SIEM çalışmasına geçilmez.

**Son güncelleme:** 3 Haziran 2026

---

## Checkpoint kapsamı (Must)

| # | Madde | Repo | Durum |
|---|--------|------|--------|
| C1 | `oc_live=502` — nginx Docker DNS (stale upstream) | Mng.Ui | ✅ Odak `oc_live=200` |
| C2 | `GET /alarm/api/v1/alarms` (+ detail) | MngAlarm | ✅ Odak deploy |
| C3 | UI proxy: `/api/alarm/`, `/api/workflow/` | Mng.Ui nginx + Nuxt server | ✅ |
| C4 | Onay bekleyenler ekranı | Mng.Ui | ✅ |
| C5 | Açık alarmlar listesi | Mng.Ui | ✅ |
| C6 | MngReactor native `monitra.observations` + bridge kapat | MngReactor + deploy | ✅ Odak `mngreactor:latest`; bridge kapalı; `test-reactor-observation-e2e.ps1` PASS |
| C7 | Odak E2E regresyon (alarm + workflow + native obs) | scripts/odak | ✅ `run-checkpoint-e2e.ps1` (10 script) |

---

## SIEM-ready onay kriterleri (hepsi gerekli)

- [x] C1–C5 Odak'ta deploy + smoke (`test-operator-smoke.ps1` + tarayıcı)
- [x] C6 native publish + `ReactorBridge__Enabled=false`
- [x] C7 E2E scriptleri PASS (`.\scripts\odak\run-checkpoint-e2e.ps1`)
- [x] Operatör akışı: alarm listesi + onay + kurallar UI tarayıcıda doğrulandı
- [x] Bu dosyada durum **SIEM-ready ✅** işaretlendi

**SIEM-ready ✅ (3 Haz 2026)** — Sonraki kapsam: [SIEM_FAZ1_HANDOFF.md](./monitoring/SIEM_FAZ1_HANDOFF.md)

---

## Deploy (Odak)

```powershell
# C6 Reactor + worker (native observation, bridge kapalı)
pwsh -NoProfile -ExecutionPolicy Bypass -Command @"
Set-Location 'C:\Users\monitra\Dev\MonitraNG\MonitraNG'
& .\scripts\odak\sync-odak-source.ps1 -Paths @('MngReactor','ApplicationResources/mng_apps','scripts/odak')
& .\scripts\odak\deploy-odak-apps.ps1 -Services mngreactor,mngalarm-worker -NoCache
& .\scripts\odak\test-reactor-observation-e2e.ps1 -FailIfSkipped
"@
```

Detay: [deploy/README.md](./deploy/README.md) · [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./monitoring/MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md)

**Smoke:**
- `http://192.168.20.20:3000/api/operations/v1/health/live` → 200
- `.\scripts\odak\test-operator-smoke.ps1` → API + operator route shell
- Menü patch: `patch-oc-side-menu.ps1`, `patch-alarm-center-side-menu.ps1`, `patch-automation-side-menu.ps1`
- Operasyon → Bekleyen onaylar · Alarm Merkezi → Açık alarmlar / Alarm kuralları · Otomasyon Merkezi → İş Akış Yönetimi

---

## Should-have (checkpoint sonrası, SIEM ile paralel olabilir)

- ~~Kural update/delete API + admin form~~ ✅ API + Alarm Merkezi kurallar UI
- ~~`parallel.join` node~~ ✅
- ~~P4 engine.command MVP~~ ✅ (`block.ip` alias; Reactor MQTT publish)
- P4 tam: onay → block → scheduler unblock (Reactor + Engine handler)

---

## Referanslar

- [REACTOR_NATIVE_PUBLISH_HANDOFF.md](./alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md)
- [alarm/DEVAM.md](./alarm/DEVAM.md)
- [workflow/DEVAM.md](./workflow/DEVAM.md)
