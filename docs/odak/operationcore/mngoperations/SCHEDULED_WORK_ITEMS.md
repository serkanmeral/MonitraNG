# MngOperations — Zamanlanmış work item oluşturma

**Son güncelleme:** 28 Mayıs 2026 (SW-4d sihirbaz + Odak deploy doğrulama)  
**Durum:** SW-0…SW-4d + SW-2/3a/b/c ✅ Odak · **SW-5** cron E2E smoke · **SW-6** backlog  
**UI:** [OC_UI_SCHEDULED_WORK_ITEMS.md](../ui/OC_UI_SCHEDULED_WORK_ITEMS.md)  
**İlişkili:** [API_SURFACE.md](./API_SURFACE.md) · [PIPELINES.md](./PIPELINES.md) §6 · [INTEGRATIONS.md](./INTEGRATIONS.md) · [PERMISSIONS_LAYERING.md](./PERMISSIONS_LAYERING.md)

---

## 1. Ürün özeti (onaylı kararlar — 28 Mayıs 2026)

| Konu | Karar |
|------|--------|
| Kapsam | **Tek workspace** altında tanım; her schedule bir workspace’e bağlı |
| Şablon | Sabit **board**, **iş tipi**, **form/layout alanları** (create şablonu bir kez kaydedilir) |
| Atama | **Tek assignee**; atanan değişince yalnızca **şablon güncellenir** (geçmiş WI’lar değişmez) |
| Tekrar | Her tetiklemede **yeni bağımsız** work item — manuel «Yeni iş» ile aynı iş kuralı, otomasyon tetikler |
| Çift kayıt | **İzinli** — aynı periyotta birden fazla WI olabilir; slot başına idempotent zorunluluk **yok** |
| Yetki | Yalnızca **domain manager** (`isManager` / `managers` grubu); normal `users` tanımlayamaz |
| İlk hedef | **Tam dikey dilim:** DG metadata + MO endpoint + MngScheduler job senkronu + Workspace tanımları UI |

**Örnek:** «Haftalık sunucu bakımı» — her Pazartesi 09:00 (Europe/Istanbul) → yeni WI, aynı board, aynı tip, aynı assignee, kayıtlı başlık/açıklama/alanlar.

---

## 2. Mimari

```text
Workspace tanımları UI
  → DG: op_work_item_schedules (şablon + cron + TZ)
  → Kayıt/güncelleme: MngScheduler User Job (cron + HTTP POST)

MngScheduler (tetik)
  → POST {gateway}/operations/api/v1/work-items/from-origin
  → Body: CreateFromOriginRequest (şablondan)

MngOperations
  → Normal create pipeline (kurallar, fieldPolicies, op_rules, key, activity, events)
  → origin.sourceType = "scheduler"
```

**Neden `from-origin`:** Faz 1’de hazır; dış/otomatik köken audit’i ve monitoring ile aynı hat.

**Neden ayrı schedule dataset:** Şablon + cron UI tek yerde; Scheduler yalnızca **tetikleyici**. WI içeriği MO’da doğrulanır (enabled type, board workspace’e ait, vb.).

---

## 3. Persist — `op_work_item_schedules` (taslak)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `workspaceId` | relation | Zorunlu |
| `name` | string | Liste adı |
| `description` | string? | |
| `isActive` | bool | Pasifken Scheduler job da pasif |
| `cronExpression` | string | MngScheduler ile aynı sözleşme |
| `timezone` | string | Örn. `Europe/Istanbul` — UI’da «Pazartesi 09:00» üretimi |
| `boardId` | relation | Sabit board |
| `typeId` | relation | Sabit iş tipi |
| `assignee` | string | Kişi id (Keeper) |
| `priorityId` | relation? | Opsiyonel |
| `title` | string | WI başlığı şablonu |
| `description` | text? | WI açıklama şablonu |
| `fields` | object? | `extraFields` / çekirdek alanlar (create payload) |
| `initialTransitionKey` | string? | Create sonrası hedef state (opsiyonel) |
| `schedulerJobId` | string? | MngScheduler `jobId` — senkron referans |
| `lastRunAt` | datetime? | Son başarılı tetik (MO veya Scheduler webhook ile) |
| `lastWorkItemId` | relation? | Son oluşan WI (UI link) |

**Index:** `workspaceId`, `isActive`, `(workspaceId, name)` unique önerilir.

**Atama değişimi:** `PATCH` schedule → `assignee` güncellenir; **mevcut WI’lara dokunulmaz**.

---

## 4. MngScheduler entegrasyonu

Mevcut `ScheduledJob` ([MngScheduler ScheduledJob](../../../../MngScheduler/Core/MngScheduler.Domain/Entities/ScheduledJob.cs)):

| Alan | Kullanım |
|------|----------|
| `cronExpression` | Schedule ile aynı |
| `endpointUrl` | `{gateway}/operations/api/v1/work-items/from-origin` |
| `httpMethod` | `POST` |
| `payload` | JSON: `CreateFromOriginRequest` (şablondan üretilir) |
| `headers` | `Authorization: Bearer {access_token}`, `Content-Type: application/json` — bkz. [§4.1](#41-kimlik-doğrulama--keeper-token--mngoperations-onaylı) |
| `domainId` | Tenant |
| `isActive` | Schedule `isActive` ile senkron |

**Senkron kuralı (B modeli):**

1. Schedule **create** → MngScheduler **User Job** create → `schedulerJobId` kaydet  
2. Schedule **update** (cron, payload, active) → Scheduler job update  
3. Schedule **delete** → Scheduler job delete veya `isActive=false`

**Bugün (SW-0 + SW-4):** UI DG’ye yazar; kayıt sonrası MO `sync-scheduler` ile User Job oluşturur (**SW-3b** ✅). Cron planlanır; WI oluşturma **SW-2 + SW-3c** ile gelir.

---

### 4.1 Kimlik doğrulama — Keeper token → MngOperations (onaylı)

**Sorun:** MngScheduler tetik anında MO endpoint’ine gelerek work item oluşturur. MO (ve MO üzerinden DG) **Bearer JWT** olmadan komut kabul etmez ([AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md)). Scheduler’ın kullanıcı oturumu yoktur; **kullanıcı adı + şifre** ile önce **MngKeeper**’dan token alması gerekir.

**Onaylı akış (her cron tetiklemesinde veya «Şimdi çalıştır» eşdeğerinde):**

```text
MngScheduler (HttpJob veya OC özel job handler)
  │
  ├─ 1) POST {gateway}/keeper/api/auth/token
  │      Body: { domainName, username, password }
  │      ← teknik kullanıcı (MngScheduler konfig / secret)
  │
  ├─ 2) access_token (JWT) al
  │
  └─ 3) POST {gateway}/operations/api/v1/work-items/from-origin
         Authorization: Bearer {access_token}
         Body: CreateFromOriginRequest (op_work_item_schedules şablonundan)
              → MO create pipeline → WI + activity + events
              → origin.sourceType = "scheduler"
```

**Referans (Odak / geliştirme):** UI ve script’lerde aynı Keeper çağrısı — `docs/odak/operationcore/scripts/get-operationcore-token.ps1` (`POST /keeper/api/auth/token`, domain + username + password).

| Konu | Karar |
|------|--------|
| Token kaynağı | **MngKeeper** → Keycloak realm JWT (`access_token`) |
| MO doğrulama | Mevcut JWT middleware — ek MO değişikliği gerekmez |
| DG erişimi | MO, gelen token’ı **forward** eder; teknik kullanıcının domain’i schedule ile uyumlu olmalı |
| Teknik kullanıcı | Domain’de tanımlı servis hesabı; **manager** yetkisi veya MO’nun `from-origin` için kabul ettiği özel rol (Faz 1: manager yeterli) |
| Kimlik bilgisi nerede | **MngScheduler** appsettings / env secret — **DG job kaydına veya UI’ya yazılmaz** |
| Token job header’ında sabit mi? | **Hayır (tercih).** JWT süresi dolar; her tetikte Keeper’dan **yeni token** alınır |
| Job `headers` alanı | Runtime’da doldurulur: `Authorization: Bearer …` — sync sırasında boş veya placeholder |

**Uygulama seçenekleri (SW-3):**

| Seçenek | Açıklama |
|---------|----------|
| **A (önerilen)** | `WorkItemScheduleHttpJob` (veya HttpJob genişlemesi): tetik öncesi Keeper login → token → MO POST |
| **B** | MO `POST .../schedules/{id}/execute`: MO içinde Keeper token alır, create yapar; Scheduler yalnızca bu kısa endpoint’i çağırır (token yine MO veya Scheduler tarafında) |
| **C (Faz 1 dışı)** | Uzun ömürlü client credentials / servis JWT — Keycloak client; Keeper password grant yerine |

**Konfig örneği (MngScheduler — taslak):**

```json
{
  "MngSchedulerSettings": {
    "Actors": {
      "MngKeeper": "http://mngkeeper:5001",
      "MngOperations": "http://mngoperations:5086"
    },
    "WorkItemScheduleOrchestration": {
      "KeeperTokenPath": "/api/auth/token",
      "GatewayOperationsFromOrigin": "http://192.168.20.20:5040/operations/api/v1/work-items/from-origin",
      "ServiceAccount": {
        "DomainName": "odak",
        "Username": "odak_admin",
        "Password": "${OC_SCHEDULER_SERVICE_PASSWORD}"
      }
    }
  }
}
```

**Güvenlik notları:**

- Schedule CRUD yapan **manager** ile WI **oluşturan** teknik kullanıcı **ayrı** olabilir; audit `origin.sourceType=scheduler` + `sourceId=scheduleId` ile ayırt edilir.
- Job execution log’larında **token veya şifre** saklanmaz ([HttpJob.cs](../../../../MngScheduler/Infrastructure/MngScheduler.Infrastructure/Jobs/HttpJob.cs) yalnızca response body truncate eder).
- Token alınamazsa (401/403) → execution `failed`; `lastRunAt` güncellenmez; UI’da son çalışma boş kalır.

**SW-3 DoD (auth):** Cron tetik → Keeper token → MO 201 → WI oluşur; Scheduler execution `success`; schedule `lastRun*` güncellenir.

### 4.2 SW-3a — Odak servis hesabı (tamamlandı)

| Konu | Karar |
|------|--------|
| Faz 1 Odak teknik kullanıcı | **`odak_admin`** (ayrı `oc_scheduler_service` ertelendi) |
| Domain | `odak` |
| Keeper URL (Odak dev) | `http://192.168.20.20:5001` + `/api/auth/token` |
| MO from-origin (gateway) | `http://192.168.20.20:5040/operations/api/v1/work-items/from-origin` |
| Konfig | `MngSchedulerSettings:WorkItemScheduleOrchestration` |
| Kod | `IMngKeeperAuthClient` / `MngKeeperAuthClient` |
| Smoke | `docs/odak/operationcore/scripts/smoke-sw-scheduler-keeper-token.ps1` |

**Not:** Üretimde şifre env secret (`WorkItemScheduleOrchestration__ServiceAccount__Password`); Odak compose: `OC_SCHEDULER_*` — [`.env.odak.example`](../../../../ApplicationResources/mng_apps/.env.odak.example).

**Odak deploy (28 Mayıs 2026):**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngScheduler,ApplicationResources/mng_apps
.\scripts\odak\deploy-odak-apps.ps1 -Services mngscheduler
```
Health: `http://192.168.20.20:5090/api/v1/health`

### 4.3 SW-3b — MO orchestration → User Job senkronu (tamamlandı)

**Akış:** UI DG CRUD → ardından MO sync (manager Bearer → Scheduler forward).

| Endpoint | Açıklama |
|----------|----------|
| `POST /operations/api/v1/work-item-schedules/{id}/sync-scheduler` | DG schedule oku → MngScheduler User Job create/update → `schedulerJobId` PATCH |
| `POST /operations/api/v1/work-item-schedules/{id}/unlink-scheduler` | Silmeden önce Scheduler job delete |

**Job kimliği:** `oc-schedule-{scheduleDataId}` · **HttpJob URL (geçici):** `.../work-item-schedules/{id}/execute` (SW-2 gelene kadar 404 normal).

**MO konfig:** `MngOperationsSettings:Actors:MngScheduler`, `WorkItemSchedule:ExecuteEndpointTemplate`.

**Odak deploy:**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngOperations,ApplicationResources/mng_apps,Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations
```

**DoD:** Schedule kaydet → `schedulerJobId` dolu → Scheduler `GET /api/v1/user/jobs/{jobId}` 200.

**Önkoşul (Odak):** Domain DG'de `@scheduled_jobs` dataset — `setup-scheduler-user-job-datasets.ps1` (`forceSchema=false`).

### 4.4 SW-3c + SW-2 — Tetik + execute (kod hazır, Odak deploy bekler)

**SW-3c (MngScheduler):**
- `WorkItemScheduleOrchestrationJob` — `oc-schedule-*` user job'lar generic `HttpJob` yerine bu handler
- Cron tetik: `IMngKeeperAuthClient` → Bearer → `POST .../work-item-schedules/{id}/execute`
- `JobSync.SyncUserJobs=true` + Keeper JWT ile domain `@scheduled_jobs` okuma (`OC_SCHEDULER_SYNC_USER_JOBS`)

**SW-2 (MngOperations):**
- `POST /operations/api/v1/work-item-schedules/{id}/execute`
- Schedule şablonu → `CreateFromOriginRequest` (`origin.sourceType=scheduler`)
- Başarı sonrası `lastRunAt`, `lastWorkItemId` PATCH
- UI «Şimdi çalıştır» aynı endpoint

**Deploy sonrası DoD:** «Şimdi çalıştır» veya kısa test cron → board'da yeni WI + schedule `lastRun*` dolu.

**SW-3c deploy notu (28 May):** `AddApplicationServices` IOptions'a `WorkItemScheduleOrchestration` kopyalamıyordu → user job DG 401. Düzeltme: `ServiceRegistration.cs` + JobSync token koşulu.

**SW-3b sync güncelleme (28 May):** MO `PUT user/jobs` gövdesinde `domainId` yok → `UserJobService.UpdateJobAsync` 401; MO hatayı yutuyordu, `@scheduled_jobs.cronExpression` eski kalıyordu. Düzeltme: token'dan `domainId` bağlama + sync'te update hatasını yükselt.

### 4.5 SW-6 — Admin Scheduled Jobs sayfası (backlog, kullanıcı isteği — 28 May 2026)

**Problem:** Bugün tüm job'ları görmek için Mongo'da `mngkeeper.@scheduled_jobs` ve her domain `mng_* .@scheduled_jobs` koleksiyonlarına manuel bakmak gerekiyor. MngDomainUI yalnızca **system** job'ları listeliyor; OC `oc-schedule-*` ve diğer user job'lar görünmüyor.

**İhtiyaç:** Platform/domain **admin** erişimli tek sayfa:
- **System + user** job'ların birleşik listesi (her kaynaktan üretilmiş job'lar)
- Kaynak etiketi (OC schedule adı / workspace, system job adı), domain, cron, timezone, aktif/pasif, son çalışma, son hata
- **Manuel run** (yetkili admin; OC schedule için MO `/execute` veya Scheduler trigger API)
- İsteğe bağlı: execution geçmişi / log linki, Quartz next fire time

**Mevcut API yüzeyi:**
- System: `GET/POST .../api/scheduler/v1/system/jobs` (MngDomainUI `useScheduler.ts`)
- User job DG: domain `@scheduled_jobs` — henüz birleşik admin list API yok

**Hedef UI (öneri):** Operation Core admin (`/apps/operation-core/admin/scheduled-jobs`) veya MngDomainUI genişlemesi — epic **SW-6**; Faz 1 admin kapanışı (E1) veya SW-5 sonrası.

**DoD:** Admin Mongo'ya bakmadan tüm aktif job'ları görür; en az bir OC schedule için «Manuel çalıştır» board'da WI üretir.

---

## 5. MO API — tetik gövdesi

`POST /work-items/from-origin` ([API_SURFACE](./API_SURFACE.md)) — örnek:

```json
{
  "workspaceId": "<ws>",
  "typeId": "<type>",
  "title": "Haftalık sunucu bakımı",
  "description": "Otomatik oluşturuldu.",
  "boardId": "<board>",
  "assignee": "<userId>",
  "priorityId": "<priority>",
  "fields": { },
  "initialTransitionKey": null,
  "origin": {
    "sourceType": "scheduler",
    "sourceSystem": "MngScheduler",
    "sourceId": "<scheduleId>",
    "correlationId": "<scheduleId>:<executionId>"
  }
}
```

### 5.1 Idempotency (güncel karar)

Faz 1 `from-origin` idempotency ([PIPELINES §6.1](./PIPELINES.md)) **aynı `correlationId` → tek WI** üretir.

**Zamanlanmış işler için:** Her tetiklemede **yeni** `correlationId` (örn. `scheduleId` + `Guid` veya UTC tick) → **her seferinde yeni WI**. Geçmiş çalıştırmalar audit için `origin` içinde kalır; çift oluşturma **ürün gereksinimi**.

İsteğe bağlı: Schedule başına «son 5 dk içinde aynı schedule’dan create yok» — **yok** (ilk sürümde).

---

## 6. Yetki

| İşlem | Kim |
|--------|-----|
| Schedule CRUD (UI + API) | Domain **manager** (`isManager` / workspace manager policy) |
| Tetiklenen WI create | MO create pipeline — workspace `create` + şablon doğrulama |
| Normal kullanıcı | Schedule göremez / düzenleyemez |

Platform admin (`isAdmin`) MO genel bypass — audit ile ([PERMISSIONS_LAYERING §5.3](./PERMISSIONS_LAYERING.md)).

---

## 7. Uygulama fazları

| Faz | İş | Çıktı |
|-----|-----|--------|
| **SW-0** | Dataset `op_work_item_schedules` + DG deploy taslağı | JSON patch script |
| **SW-1** | MO: schedule CRUD (DG proxy veya doğrudan DG + permission guard) | API veya UI→DG |
| **SW-2** | MO `/execute` + `lastRun*` | ✅ Odak |
| **SW-3a** | Keeper token client + Scheduler config (`odak_admin` Odak Faz 1) | ✅ |
| **SW-3b** | Schedule CRUD → User Job senkron (MO orchestration) | ✅ Odak |
| **SW-3c** | Tetik handler: Keeper token → MO execute | ✅ Odak |
| **SW-4** | UI: Zamanlanmış işler sekmesi + DG CRUD | ✅ |
| **SW-4d** | UI: Zamanlama sihirbazı (dakika/saat/gün/çoklu gün) | ✅ Odak |
| **SW-5** | E2E: cron → WI board’da görünür | Demo kayıt → 🟡 execute OK; cron smoke |
| **SW-6** | Admin Scheduled Jobs explorer (system + user, manual run) | Backlog |

**Paralel epic’ler:** R-UI (op_rules), Board runtime — bağımsız.

---

## 8. Faz planı ile ilişki

| Belge | Önceki ifade | Güncel |
|-------|--------------|--------|
| [operationcore_phase1.md §28.3](../operationcore_phase1.md) | Workflow/Scheduler Faz 2+ | **Zamanlanmış WI** ayrı epic (SW-*); SLA job hâlâ Faz 2+ |
| [INTEGRATIONS.md §6](./INTEGRATIONS.md) | SLA / escalation HTTP | Zamanlanmış WI bu dokümana taşındı |

---

## 9. Açık teknik sorular

| # | Soru | Karar / durum |
|---|------|----------------|
| T1 | Scheduler tetik token’ı | **Kararlandı** — Keeper `POST /auth/token` (teknik kullanıcı) → `access_token` → MO Bearer ([§4.1](#41-kimlik-doğrulama--keeper-token--mngoperations-onaylı)) |
| T2 | MO schedule API mi, UI→DG mi? | SW-0: UI→DG ✅; SW-3 job sync MO veya orchestration servisi |
| T3 | Başlıkta `{date}` placeholder | İlk sürüm sabit metin; placeholder SW-1b |
| T4 | `lastRun` güncelleme | MO create sonrası PATCH schedule veya Scheduler callback |

---

## 10. Kod hedefleri (uygulama başlangıcı)

| Katman | Dosya (öneri) |
|--------|----------------|
| DG dataset | `datasets/` patch + `op_work_item_schedules` |
| MO | `WorkItemScheduleService`, `SchedulesController`, `CreateFromOrigin` schedule builder |
| Scheduler | `WorkItemScheduleHttpJob` (Keeper token + MO POST) veya HttpJob auth genişlemesi; `ISchedulerJobSync` |
| Scheduler config | `MngSchedulerSettings:WorkItemScheduleOrchestration` — domain, username, password (secret) |
| UI | `OcWorkspaceDefinitionsScheduledWorkItemsTab.vue`, `useOcWorkspaceDefinitionTabs` + `scheduled` key |

---

*Handoff: [DEVAM.md](./DEVAM.md). UI detay: [OC_UI_SCHEDULED_WORK_ITEMS.md](../ui/OC_UI_SCHEDULED_WORK_ITEMS.md).*
