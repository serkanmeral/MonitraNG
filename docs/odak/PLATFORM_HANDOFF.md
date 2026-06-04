# Platform Handoff — UI modülleri ve SIEM öncesi durum

**Son güncelleme:** 4 Haziran 2026  
**Git:** `main` @ **`6c4ecbf`** — `feat(ui): Alarm Center, Automation Center, and workflow admin`  
**Push:** commit lokal; `origin/main`'e push edilmediyse önce `git push` yapın.

Bu dosya, **platform UI / operatör modülleri** chat'inden ayrılırken kaldığımız yeri tek yerde toplar. **SIEM Faz 1 implementasyonu** ayrı chat'te yürütülür — bkz. [monitoring/SIEM_FAZ1_HANDOFF.md](./monitoring/SIEM_FAZ1_HANDOFF.md).

---

## 1. Tek cümlede durum

Checkpoint **C1–C7 SIEM-ready ✅**. Operatör UI üç modüle ayrıldı: **Operasyon** (görev + onay inbox), **Alarm Merkezi** (açık alarmlar + tespit kuralları), **Otomasyon Merkezi** (iş akışı tanım/editör W1). Smoke: `test-operator-smoke.ps1` + `run-checkpoint-e2e.ps1` (10 script) **PASS** (4 Haz 2026).

---

## 2. UI modül haritası

| Modül | Menü header | Route'lar | Bileşenler |
|-------|-------------|-----------|------------|
| **Operasyon** | Operasyon | `/apps/operation-core/workspace` | OC workspace, kanban, WI profil |
| | | `/apps/operation-core/approvals` | `OcApprovalsExplorer` |
| | | `/apps/operation-core/admin/*` | Tanımlamalar (sistem, workspace, scheduled jobs) |
| **Alarm Merkezi** | Alarm Merkezi | `/apps/alarm-center/alarms` | `AcAlarmsExplorer` |
| | | `/apps/alarm-center/rules` | `AcAlarmRulesExplorer` |
| **Otomasyon Merkezi** | Otomasyon Merkezi | `/apps/automation-center/workflows` | `AcWorkflowsExplorer` |
| | | `/apps/automation-center/workflows/[id]` | `AcWorkflowEditor` (form: nodes/edges/triggers) |

**Eski route'lar** (`/apps/operation-core/admin/alarms`, `alarm-rules`, `approvals`, `workflows`) → redirect.

**i18n:** `alarmCenter.*`, `automationCenter.workflows.*`, onaylar `operationCore.adminApprovals.*`

**Menü patch (Odak DG `@side_menu`):**

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\patch-oc-side-menu.ps1
.\docs\odak\alarm\scripts\patch-alarm-center-side-menu.ps1
.\docs\odak\automation\scripts\patch-automation-side-menu.ps1
```

---

## 3. Tamamlanan işler (bu chat hattı)

| # | Konu | Durum |
|---|------|--------|
| C6 | MngReactor native `monitra.observations`, bridge kapalı | ✅ |
| C7 | Checkpoint E2E 10 script | ✅ |
| UI-W1 | Workflow list + form tabanlı draft editör, publish, test run | ✅ |
| UI-MENU | Otomasyon / Alarm / OC menü ayrımı | ✅ commit `6c4ecbf` |
| Deploy | Odak `mngui` + menü patch | ✅ |

---

## 4. Sıradaki (bu chat'e dönünce — SIEM dışı)

| Öncelik | İş | Not |
|---------|-----|-----|
| **UI-W2** | Vue Flow canvas tasarımcı | Otomasyon Merkezi editör; API aynı |
| **P4 tam** | Reactor `POST /api/v1/mqtt/publish` + gerçek `block_ip` | `DevLogOnly=true` kaldır |
| **Alarm UI** | Açık alarmlarda kaynak filtresi (metrik/SIEM/AI) | SIEM verisi geldikten sonra anlamlı |
| **OC perf** | Pano query dedup / cold start | [operationcore/mngoperations/DEVAM.md](./operationcore/mngoperations/DEVAM.md) |
| **Prod deploy** | Ayrı commit bekliyor | `docs/odak/proddeploy/` (working tree'de, commit edilmedi) |

**SIEM Faz 1** → ayrı chat; [monitoring/SIEM_FAZ1_HANDOFF.md](./monitoring/SIEM_FAZ1_HANDOFF.md), [monitoring/HANDOFF.md](./monitoring/HANDOFF.md).

---

## 5. Smoke / deploy (hatırlatma)

```powershell
# UI deploy
pwsh -Command "& '.\scripts\odak\sync-odak-source.ps1' -Paths @('Mng.Ui')"
pwsh -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngui -NoCache

# Smoke
.\scripts\odak\test-operator-smoke.ps1
.\scripts\odak\run-checkpoint-e2e.ps1
```

`-Paths` için `-Command` kullanın (`-File` array param kırılır). Kritik fix sonrası `-NoCache` zorunlu.

---

## 6. Referanslar

| Doküman | İçerik |
|---------|--------|
| [PLATFORM_CHECKPOINT.md](./PLATFORM_CHECKPOINT.md) | C1–C7 SIEM-ready |
| [workflow/DEVAM.md](./workflow/DEVAM.md) | MngWorkflow backend + P4 |
| [alarm/DEVAM.md](./alarm/DEVAM.md) | MngAlarm motor |
| [automation/README.md](./automation/README.md) | Otomasyon Merkezi UI |
| [deploy/README.md](./deploy/README.md) | Test sunucu deploy |

---

## 7. Bu chat'e dönüş prompt'u

Aşağıdaki bloğu yeni oturumda yapıştırın:

```markdown
# MonitraNG — Platform UI handoff (kaldığımız yer)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- Ana handoff: `docs/odak/PLATFORM_HANDOFF.md`
- Checkpoint: `docs/odak/PLATFORM_CHECKPOINT.md` — C1–C7 SIEM-ready ✅
- Git: `main` @ `6c4ecbf` (UI modül ayrımı; push durumunu kontrol et)

## UI modülleri (Odak menü)
- **Operasyon:** workspace + `/apps/operation-core/approvals` (onay inbox)
- **Alarm Merkezi:** `/apps/alarm-center/alarms`, `/apps/alarm-center/rules`
- **Otomasyon Merkezi:** `/apps/automation-center/workflows` (+ form editör W1)

## Tamamlanan
- Workflow W1 (list + draft editor, publish, test run)
- Menü/route refaktör + redirect'ler + patch script'ler
- Smoke: `test-operator-smoke.ps1` + `run-checkpoint-e2e.ps1` PASS

## SIEM
- SIEM Faz 1 **ayrı chat'te** — bu oturumda SIEM kodu yok; gerekirse sadece `docs/odak/monitoring/SIEM_FAZ1_HANDOFF.md` oku.

## Sıradaki (önerilen)
1. UI-W2 — Vue Flow canvas (Otomasyon Merkezi)
2. P4 tam — Reactor mqtt/publish, gerçek block_ip
3. Alarm Merkezi — kaynak filtresi (SIEM sonrası)

## Bu oturumda ne yapmak istiyorum?
[Kendi cümleni buraya yaz]
```
