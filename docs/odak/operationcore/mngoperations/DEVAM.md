# MngOperations & Operation Core UI — Devam noktası (checkpoint)

**Son güncelleme:** 28 Mayıs 2026 (gece — SW tam dikey dilim + sihirbaz)  
**Durum:** Zamanlanmış WI **SW-0…SW-4d + SW-3a/b/c + SW-2** ✅ Odak · **SW-5** cron E2E smoke · **SW-6** admin jobs backlog

Bu dosya **yeni Cursor chat**'te kaldığınız yerden devam için ana handoff'tur.

**Ana plan:** [OC_UI_ADMIN_FAZ1_PLAN.md](../ui/OC_UI_ADMIN_FAZ1_PLAN.md)

---

## Bu oturumda tamamlanan (28 May 2026)

### Backend (MngOperations + MngScheduler)

| İş | Not |
|----|-----|
| **SW-3c** | `WorkItemScheduleOrchestrationJob` — cron → Keeper token → MO `/execute` |
| **SW-2** | `POST .../work-item-schedules/{id}/execute` + `lastRunAt` / `lastWorkItemId` |
| **SW-3b** | `sync-scheduler` / `unlink-scheduler` — User Job `@scheduled_jobs` |
| **Deploy Odak** | `mngoperations`, `mngscheduler` — `OC_SCHEDULER_SYNC_USER_JOBS=true` |
| **Fix: IOptions** | `MngScheduler` `WorkItemScheduleOrchestration` IOptions'a kopyalanmıyordu → user job DG 401 |
| **Fix: sync update** | MO `PUT user/jobs` `domainId` göndermiyordu → `@scheduled_jobs` cron güncellenmiyordu; `UserJobService` token'dan bağlar |
| **Fix: sync hata** | MO update hatasını yutma kaldırıldı — UI'da senkron uyarısı görünür |

### UI (Mng.Ui)

| İş | Not |
|----|-----|
| **SW-4** | Zamanlanmış işler sekmesi — liste, dialog, «Şimdi çalıştır», «Zamanlayıcı bağlı» |
| **SW-4d** | **Zamanlama sihirbazı** `OcScheduleTimingWizard.vue` — dakika / saat / her gün / çoklu gün + gelişmiş cron |
| **Fix: hub.ts** | `reconnectHandlers` init — chat SignalR konsol hatası |
| **Fix: cron UX** | Doğrulama mesajları, kayıt sonrası scheduler sync |

### Dokümantasyon

| İş | Not |
|----|-----|
| **SW-6** | Admin Scheduled Jobs explorer backlog — [SCHEDULED_WORK_ITEMS §4.5](./SCHEDULED_WORK_ITEMS.md) |
| **OC_UI §3.2** | Sihirbaz tablosu — [OC_UI_SCHEDULED_WORK_ITEMS.md](../ui/OC_UI_SCHEDULED_WORK_ITEMS.md) |

**Commit:** Henüz yapılmadı (kullanıcı istemedi). Working tree'de MO + Scheduler + UI + docs değişiklikleri var.

---

## Odak doğrulama (referans)

| Öğe | Değer |
|-----|--------|
| Gateway | `http://192.168.20.20:5040` |
| Demo workspace | `f414462a-cd9e-427e-87e8-3cdff0502325` |
| Demo schedule `__dataId` | `82760c1b-ba39-4e78-9ade-27d404136d92` |
| Scheduler jobId | `oc-schedule-82760c1b-ba39-4e78-9ade-27d404136d92` |
| User job DB | `mng_odak.@scheduled_jobs` (**`mngkeeper` değil**) |
| UI URL | `/apps/operation-core/admin/workspace-definitions?workspaceId=...&tab=scheduled` |

**Doğrulanan:**
- `POST .../execute` → 201, WI oluşuyor (ör. `OCD-0007`)
- `sync-scheduler` → `updated: true`, cron `0 0/2 * * * ?` Mongo'da
- Scheduler log: `oc-schedule-*` Quartz'a yüklendi (`SyncUserJobs=True`)

**SW-5 kalan:** Cron ile otomatik tetik → board'da yeni WI (2 dk sihirbaz ile test; kullanıcı onayı bekleniyor).

---

## Deploy (Odak)

```powershell
# Repo kökünden
.\scripts\odak\sync-odak-source.ps1
.\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations,mngscheduler,mngui
```

`.env`: `OC_SCHEDULER_SYNC_USER_JOBS=true` (ör. `.env.odak.example`).

---

## Sıradaki işler (güncel sıra)

| # | Epic | Hedef |
|---|------|--------|
| **1** | **SW-5** | Cron E2E smoke (sihirbaz «Her 2 dk» → 2–4 dk bekle → board + `lastRun*`) |
| **2** | **SW-6** | Admin Scheduled Jobs sayfası (tüm job'lar, manuel run) — §4.5 |
| **3** | **A1** | Kurallar R-Plus | [OC_UI_RULES_FAZ1.md](../ui/OC_UI_RULES_FAZ1.md) |
| **4** | **C** | SLA plan | [SLA_FAZ1_PLAN.md](./SLA_FAZ1_PLAN.md) |
| **5** | **D1** | Board tanımları | [OC_UI_ADMIN_FAZ1_PLAN.md §Epic D](../ui/OC_UI_ADMIN_FAZ1_PLAN.md) |
| **6** | **E1** | Admin kapanış + yetki | aynı §Epic E |
| **7** | **F** | Operasyonel runtime | admin kapandıktan sonra |

**Stratejik sıra:** SW-5 kapat → SW-6 veya SLA → Board admin → Admin kapanış → operasyonel UI.

---

## Workspace tanımları — sekmeler

| Üst sekme | Faz 1 admin |
|-----------|-------------|
| Genel | 🟡 yetki grupları |
| Değerler | ✅ |
| Akışlar | 🟡 |
| Formlar | ✅ |
| Board'lar | 🟡 |
| Politikalar | ✅ |
| Kurallar | ✅ |
| **Zamanlanmış işler** | ✅ UI + sihirbaz; MO/Scheduler Odak |

---

## Görsel ilerleme

```text
[✓] Kurallar + Politikalar admin UX
[✓] SW-0 … SW-4d (dataset, MO execute, Scheduler sync, UI sihirbaz)
[🟡] SW-5 cron E2E (manuel execute OK)
[ ] SW-6 admin jobs explorer
[ ] SLA → Board admin → Admin kapanış
[⏸] Operasyonel board/profil
```

---

## Önemli dosyalar

| Alan | Dosyalar |
|------|----------|
| MO sync/execute | `WorkItemScheduleSyncService.cs`, `WorkItemScheduleExecuteService.cs`, `WorkItemSchedulesController.cs` |
| Scheduler | `WorkItemScheduleOrchestrationJob.cs`, `JobSyncService.cs`, `UserJobService.cs`, `ServiceRegistration.cs` (Application) |
| UI sihirbaz | `OcScheduleTimingWizard.vue`, `ocScheduleCron.ts`, `OcWorkspaceScheduleDialog.vue` |
| Docs | [SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md), [OC_UI_SCHEDULED_WORK_ITEMS.md](../ui/OC_UI_SCHEDULED_WORK_ITEMS.md) |

---

## Doküman indeksi

| Doküman | Rol |
|---------|-----|
| **DEVAM.md** | **Bu handoff** |
| [SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md) | SW backend + §4.5 SW-6 |
| [OC_UI_SCHEDULED_WORK_ITEMS.md](../ui/OC_UI_SCHEDULED_WORK_ITEMS.md) | SW UI + sihirbaz + test |
| [OC_UI_ADMIN_FAZ1_PLAN.md](../ui/OC_UI_ADMIN_FAZ1_PLAN.md) | Admin Faz 1 plan |

---

## Yeni chat — ilk mesaj (kopyala-yapıştır)

> `docs/odak/operationcore/mngoperations/DEVAM.md` oku. Zamanlanmış WI: SW-0…SW-4d ve SW-2/3 Odak'ta deploy edildi; sırada **SW-5** cron E2E smoke ve **SW-6** admin jobs sayfası. Demo schedule `82760c1b-ba39-4e78-9ade-27d404136d92`, workspace `f414462a-cd9e-427e-87e8-3cdff0502325`.

---

*Son güncelleme: 28 Mayıs 2026.*
