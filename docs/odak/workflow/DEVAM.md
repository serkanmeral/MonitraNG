# DEVAM — Workflow + Alarm Engine Planlama (Kaldığımız Yer)

**Son güncelleme:** 3 Haziran 2026
**Durum:** ✅ **Alarm lifecycle workflow trigger'ları** — `alarm.raised` / `alarm.updated` / `alarm.resolved` Odak E2E geçti (3 Haz 2026)

> **Agent / geliştirme kuralları:** [§12 Geliştirme, test ve deploy kuralları](#12-geliştirme-test-ve-deploy-kuralları-agent)

---

## 1. Tek cümlede durum

**Faz 0–6+ kodlandı.** Faz 6: OC WorkItem create/transition. **Faz 6.1:** `workitem.update` (PATCH). **MO entegrasyon:** `op_rules` action `startWorkflow`. **Alarm seam:** Reactor metric bridge + lifecycle triggers + native observation consumer. **SLA Faz 2:** breach scan → workflow. **parallel.fork MVP** Odak E2E ✅. **Sıradaki:** MngReactor native publish, P3 SIEM spike (external repo), `parallel.join`.

---

## 2. Bu oturumda üretilen / güncellenen dökümanlar

| Dosya | Durum | İçerik |
|-------|-------|--------|
| `Workflow Backend Implementation Plan v1.md` | Güncellendi | **§13** Operation Core entegrasyonu eklendi |
| `DEVAM.md` | Güncellendi | OC checklist, Faz 0/1 planlama detayı, sonraki adımlar |
| `docs/odak/operationcore/README.md` | Güncellendi | Workflow çapraz bağlantısı |
| `docs/odak/operationcore/mngoperations/INTEGRATIONS.md` | Güncellendi | MngWorkflow satırı + `sourceType: workflow` |
| `DEVAM.md` §12 | Güncellendi | Geliştirme, test ve deploy kuralları (agent) |
| **Kod (Faz 0/1)** | ✅ | `MngWorkflow.Worker`, engine, Mongo, RabbitMQ, 4 node, dev smoke API |
| **Kod (Faz 2)** | ✅ | Definition/Version CRUD, publish, run history API, graph validator, domain accessor |
| **Kod (Faz 3)** | ✅ | Retry bucket kuyrukları, DLQ, Jint expression, HTTP 4xx/5xx retry ayrımı |
| **Kod (Faz 4)** | ✅ | Event Trigger (`oc.events`), trigger projeksiyon, filterExpression, dev simulate |
| **Kod (Faz 5)** | ✅ | `approval.wait`, resume API, `@workflow_secrets`, HTTP `{{secrets.*}}` |
| **Kod (Faz 5.5)** | ✅ | `delay.wait`, delay bucket + resume consumer, scheduler hooks, schedule trigger sync |
| **Kod (Faz 6)** | ✅ | `workitem.create`, `workitem.transition`, MO client, context template resolver |
| **Kod (Faz 6.1)** | ✅ | `workitem.update` (PATCH) |
| **Kod (MO)** | ✅ | `op_rules` action `startWorkflow` → `POST /workflow/api/v1/runs` |
| **Alarm §15** | ✅ | Kararlar kapatıldı — [alarm/DEVAM.md](../alarm/DEVAM.md) |

**Önceki oturumlar (özet):**

| Dosya | İçerik |
|-------|--------|
| `Workflow Backend Implementation Plan v1.md` (ilk) | §0–§12 Workflow backend planı |
| `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md` | Platform geneli Alarm & Rule Engine |
| `docs/odak/monitoring/SIEM_PLANNING.md` | Korelasyon = Alarm Engine kararı |

> Orijinal tasarım taslakları (`InternalDesign.md`, `...v1_1.md`, `planing.md`) bilinçli olarak değiştirilmedi — düşünce/karar geçmişi.

---

## 3. Kilitli kararlar — Workflow Engine

| # | Konu | Karar |
|---|------|-------|
| 1 | Persistence | Hibrit: Worker → doğrudan Mongo; Definition/Version CRUD (Api) → doğrudan Mongo |
| 2 | Delay/Schedule | MngScheduler (Quartz) uzun delay+schedule; kısa delay (<~1dk) motor-içi bucket kuyrukları |
| 3 | Execution granularity | Per-node (her node ayrı mesaj, context her adımda persist); inline opt. ileriye |
| 4 | Multi-tenancy | Domain-scoped; routing key `{domainId}.*`; instance domainId ile mühürlenir |
| 5 | Servisler | `MngWorkflow.Api` + yeni `MngWorkflow.Worker` (stateless) |
| 6 | Expression engine | Jint (sandbox: timeout+limit, read-only context) |
| 7 | Validation pipeline | Mevcut `ValidationPipelineService` ile birleştirme YOK; ayrı bounded context; Jint paylaşılır |
| 8 | Trigger binding | Version içinde `triggers[]` + indeksli `@workflow_triggers`; many-to-many |
| 9 | Retry | Sabit delay-bucket kuyrukları (5s/30s/2m/10m) + DLX; ≤15dk üst sınır |
| 10 | Webhook auth | Domain-scoped opak key + HMAC imza (`@workflow_secrets`) |
| 11 | Yetki | MngKeeper izinleri + IPermissionEvaluator; worker service identity (`IMngKeeperAuthClient` deseni) |
| 12 | NextEdges | Tekil `NextEdgeType` → çoğul `NextEdges` (If/Switch/Parallel uyumu) |
| 13 | OC sınırı | WI içi senkron otomasyon = `op_rules` (MO); çok adımlı/modüller arası = Workflow — birleştirilmez ([Implementation Plan §13.1](./Workflow%20Backend%20Implementation%20Plan%20v1.md)) |

İlk teknik hedef: **Manual → If → HTTP → Log** uçtan uca (Faz 1). OC entegrasyonu Faz 4–6'da.

---

## 4. Kilitli kararlar — Alarm & Rule Engine

| # | Konu | Karar |
|---|------|-------|
| 1 | Konumlandırma | Platform geneli Alarm & Rule Engine (major §4.2); SIEM korelasyonu = bir kural ailesi |
| 2 | Sınır | Tespit/alarm üretir; aksiyon/orkestrasyon Workflow'a ait (`planing.md` §2) |
| 3 | AI | Ayrı scorer servis(ler); çıktı = sinyal event (kind=signal) → motoru besler. **Implementasyon ⏸️** — [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md) |
| 4 | IFTTT | `MONITORING_WORKFLOW.md` bölündü: tespit→Alarm Engine, aksiyon→Workflow; superseded |
| 5 | Runtime | Stream + state (workflow'dan farklı); partition'lı tüketim + Mongo checkpoint |
| 6 | Seam | Alarm event → `mng.alarms` exchange → Workflow Event Trigger tüketir |

---

## 5. Kilitli kararlar — Operation Core × Workflow

| # | Konu | Karar |
|---|------|-------|
| 1 | WI oluşturma kapısı | `POST /operations/api/v1/work-items/from-origin` — tek write entegrasyon noktası |
| 2 | Idempotency | `origin.correlationId` = workflow `instance.correlationId` (retry/DLQ güvenli) |
| 3 | origin.sourceType | `"workflow"` (mevcut: `monitoring`, `scheduler`) |
| 4 | Event tüketimi | Event Trigger → exchange `oc.events`, routing `{domainId}.oc.workitem.*` |
| 5 | Kimlik | MO çağrıları `IMngKeeperAuthClient` service token (Scheduler deseni ile aynı) |
| 6 | Zamanlanmış WI | Scheduler → `from-origin` **doğrudan**; workflow Schedule Trigger ile ikileme **yok** |
| 7 | OC Faz 2+ backlog | `op_rules` action `startWorkflow`; SLA breach → workflow — motor hazır olunca |

Detay: [Implementation Plan §13](./Workflow%20Backend%20Implementation%20Plan%20v1.md).

---

## 6. Açık kararlar

**Workflow Faz 0–4:** Büyük mimari karar kalmadı.

**Alarm Engine §15:** ✅ Kapatıldı — [alarm/DEVAM.md §2](../alarm/DEVAM.md)

**Workflow Faz 5–6 (OC — planlama, implementasyon öncesi):**

| # | Soru | Öneri |
|---|------|-------|
| W-OC1 | `UpdateWorkItem` node: ham PATCH mi, yalnızca `ApplyTransition` mi? | Faz 6'da önce `ApplyTransition`; genel PATCH Faz 6+ |
| W-OC2 | Event Trigger `eventType` adlandırması | MO routing ile hizalı: `oc.workitem.created` / `.updated` / `.transitioned` |
| W-OC3 | `CreateWorkItem` node çıktısı context'e ne yazılır? | `outputs.workItemId`, `outputs.workItemKey`, `outputs.profileUrl` (opsiyonel) |

---

## 7. Faz 0/1 — implementasyon planlama (sıradaki kod adımları)

Faz 0/1 **OC'den bağımsız**; aşağıdaki sıra [Implementation Plan §9](./Workflow%20Backend%20Implementation%20Plan%20v1.md) ile uyumlu.

### Faz 0 — Worker iskelet

| # | Görev | Kabul |
|---|-------|-------|
| 0.1 | `MngWorkflow.Worker` Generic Host projesi | ✅ |
| 0.2 | Mongo bağlantı + `@workflow_*` koleksiyon declare/index | ✅ |
| 0.3 | RabbitMQ: exchange `mng.workflow`, kuyruk `workflow.execution` | ✅ |
| 0.4 | Domain context (`domainName` → `mng_{domain}` DB) | ✅ |

### Faz 1 — Runtime core + 4 node

| # | Görev | Kabul |
|---|-------|-------|
| 1.1 | Domain model + Mongo repo'lar | ✅ |
| 1.2 | `IWorkflowNode` registry + DI | ✅ |
| 1.3 | Node'lar: ManualTrigger, If, HttpRequest, WriteLog | ✅ |
| 1.4 | Per-node engine + idempotency `(instanceId, nodeId, attempt)` | ✅ |
| 1.5 | **Uçtan uca:** Manual → If → HTTP → Log | ✅ Odak smoke 3 Haz 2026 |

**Dev smoke (Odak):**

```powershell
$r = Invoke-RestMethod -Method Post -Uri "http://192.168.20.20:5040/workflow/api/v1/dev/runs/smoke" `
  -ContentType "application/json" -Body '{"domainName":"odak","eventValue":10}'
Invoke-RestMethod -Uri "http://192.168.20.20:5040/workflow/api/v1/dev/runs/$($r.instanceId)/executions?domainName=odak"
```

**Faz 1 dışı (bilinçli erteleme):** Event Trigger, Approval, WorkItem node'ları, UI designer.

### Faz 2 — Definition CRUD + publish + run history

| # | Görev | Kabul |
|---|-------|-------|
| 2.1 | `@workflow_definitions` + `@workflow_versions` domain repo'ları | ✅ |
| 2.2 | Definition CRUD (`GET/POST/PUT /api/v1/definitions`) | ✅ |
| 2.3 | Version draft CRUD + publish (`/versions/{id}/publish`, önceki Published→Archived) | ✅ |
| 2.4 | Run API: `POST /api/v1/runs`, `GET /api/v1/runs`, `GET /api/v1/runs/{instanceId}` | ✅ |
| 2.5 | Graph validator (entry node, edge referansları) | ✅ |
| 2.6 | Domain accessor (`X-Domain-Name` / JWT `domain_name`) | ✅ |
| 2.7 | **Uçtan uca:** create → version → publish → run → history | ✅ Odak E2E 3 Haz 2026 |

**Faz 2 E2E (Odak, gateway):**

```powershell
$base = "http://192.168.20.20:5040/workflow/api/v1"
$hdr = @{ "X-Domain-Name" = "odak"; "Content-Type" = "application/json" }
# 1 POST /definitions  2 POST /definitions/{id}/versions  3 POST /versions/{id}/publish
# 4 POST /runs  5 GET /runs/{instanceId}  → 4 node Success
```

**Not:** API JSON body'deki `config`/`triggerData` değerleri Mongo'ya yazılmadan önce `JsonElement`→primitif normalize edilir (`WorkflowJsonNormalizer`).

### Faz 3 — Retry / DLQ / Jint expression

| # | Görev | Kabul |
|---|-------|-------|
| 3.1 | Retry bucket kuyrukları: `workflow.retry.5s`, `.30s`, `.2m`, `.10m` (TTL + DLX → `system.workflow.exec`) | ✅ |
| 3.2 | Başarısız node → bucket kuyruğu (attempt bazlı gecikme); limit aşımı → `workflow.deadletter` | ✅ |
| 3.3 | `NodeExecutionResult.Retryable` — HTTP 4xx kalıcı fail, 5xx retry | ✅ |
| 3.4 | Jint sandbox (`event`, `variables`, `outputs`); If node `expression` config | ✅ |
| 3.5 | Idempotency + optimistic concurrency (Faz 1'den devam) | ✅ |
| 3.6 | **Odak:** Jint If E2E (4 node Success), retry 3× fail → instance Failed | ✅ 3 Haz 2026 |

**If node — Jint modu:**

```json
{ "expression": "event.value > 5" }
```

**Retry topolojisi:** `PublishRetryAsync` → bucket kuyruğu → TTL dolunca DLX ile `workflow.execution`'a geri döner.

### Faz 4 — Event Trigger (`oc.events`)

| # | Görev | Kabul |
|---|-------|-------|
| 4.1 | Version `triggers[]` + `@workflow_triggers` projeksiyon (publish sync) | ✅ |
| 4.2 | Worker: `workflow.event.inbound` ← `oc.events` (`*.oc.workitem.*`) | ✅ |
| 4.3 | Jint `filterExpression` + event payload → instance başlatma | ✅ |
| 4.4 | Dev simulate: `POST /workflow/api/v1/dev/triggers/simulate` | ✅ |
| 4.5 | **Odak E2E:** publish + simulate → event trigger run (3 node Success) | ✅ 3 Haz 2026 |
| 4.6 | `mng.alarms` bind (`*.alarm.#`) + routing normalize | ✅ 3 Haz 2026 |
| 4.7 | **Odak E2E:** alarm rule → observation → workflow (3 node Success) | ✅ 3 Haz 2026 |
| 4.8 | **Odak E2E:** `alarm.updated` / `alarm.resolved` Event Trigger | ✅ 3 Haz 2026 |

**Faz 4 dışı (erteleme):** ~~Schedule/Delay (MngScheduler)~~ → Faz 5.5'te tamamlandı; HTTP webhook trigger hâlâ ertelemede.

### Faz 5 — Approval / Waiting / Resume + Secret resolver

| # | Görev | Kabul |
|---|-------|-------|
| 5.1 | `approval.wait` node → `@workflow_approvals` + instance `Waiting` | ✅ |
| 5.2 | `POST /api/v1/approvals/{id}/decide` → resume (`approved`/`rejected` edge) | ✅ |
| 5.3 | `@workflow_secrets` + AES-GCM + `{{secrets.key}}` HTTP resolve | ✅ |
| 5.4 | **Odak E2E:** manual → approval → approve → log (4 node Success) | ✅ 3 Haz 2026 |

**Approval node config:**

```json
{ "type": "approval.wait", "config": { "approverGroup": "SecurityAdmins" } }
```

Edges: `approved` / `rejected` from approval node.

### Faz 5.5 — Schedule / Delay trigger (MngScheduler)

| # | Görev | Kabul |
|---|-------|-------|
| 5.5.1 | `delay.wait` node + `WorkflowResumeService` (approval ile paylaşımlı resume) | ✅ |
| 5.5.2 | Delay bucket kuyrukları (`workflow.delay.*`) → DLX → `workflow.resume` | ✅ |
| 5.5.3 | `WorkflowResumeConsumer` + `PublishDelayResumeAsync` | ✅ |
| 5.5.4 | Hook: `POST /api/v1/hooks/resume/delay` (scheduler callback; AllowAnonymous) | ✅ |
| 5.5.5 | Schedule trigger: publish sync → MngScheduler user job (`wf-schedule-{workflowId}`) | ✅ |
| 5.5.6 | Hook: `POST /api/v1/hooks/schedule/run` + dev `POST /dev/triggers/schedule/simulate` | ✅ |
| 5.5.7 | `IWorkflowSchedulerClient` + `IWorkflowKeeperAuthClient` (service token) | ✅ |
| 5.5.8 | **Odak E2E:** manual → delay 5s (bucket) → log (3 node Success, ~5s) | ✅ 3 Haz 2026 |
| 5.5.9 | **Odak E2E:** publish → MngScheduler job → Quartz cron → hook → run (2 node Success) | ✅ 3 Haz 2026 |
| 5.5.10 | MngScheduler `HttpJob`: user job DG okuması için Keeper service token (OC deseni) | ✅ 3 Haz 2026 |

**Delay node config:**

```json
{ "type": "delay.wait", "config": { "delaySeconds": 5 } }
```

**Eşik:** `Engine.DelaySchedulerThresholdSeconds` (varsayılan 60) — altı bucket, üstü MngScheduler one-shot cron job.

**Delay bucket topolojisi:** `workflow.delay.5s/.30s/.2m/.10m` → DLX `system.workflow.resume` → `workflow.resume` kuyruğu.

**Scheduler ayarları** (`MngWorkflowSettings.Scheduler`):

| Alan | Açıklama |
|------|----------|
| `BaseUrl` | MngScheduler API (`http://mngscheduler:5090`) |
| `HookBaseUrl` | HTTP job hedefi (`http://mngworkflow:5085`) |
| `ServiceAccount` | Keeper token (user job CRUD) |

**Odak compose** (`docker-compose.production.yml` → `mngworkflow`):

```yaml
MngWorkflowSettings__Scheduler__BaseUrl=http://mngscheduler:5090
MngWorkflowSettings__Scheduler__HookBaseUrl=http://mngworkflow:5085
MngWorkflowSettings__Scheduler__ServiceAccount__DomainName=odak
MngWorkflowSettings__Scheduler__ServiceAccount__Username=odak_admin
MngWorkflowSettings__Scheduler__ServiceAccount__Password=...
```

**Schedule cron E2E (Odak, 3 Haz 2026):**

```text
publish (triggers[].type=schedule, cron=0 * * * * ?)
  → MngScheduler job wf-schedule-{workflowId}
  → Quartz :00 → POST /api/v1/hooks/schedule/run
  → manual_1 → log_1 (2 node Success, instance Completed)
```

Doğrulama: `GET /scheduler/api/v1/user/jobs/wf-schedule-{workflowId}` → `totalExecutionCount≥1`, `lastExecution.status=success`, `responseCode=202`.

### Faz 6 — Operation Core WorkItem node'ları

| # | Görev | Kabul |
|---|-------|-------|
| 6.1 | `workitem.create` → `POST /operations/api/v1/work-items/from-origin` (`sourceType=workflow`) | ✅ |
| 6.2 | `workitem.transition` → `POST .../work-items/{id}/transitions/{key}` | ✅ |
| 6.3 | `IWorkflowOperationsClient` + Keeper service token | ✅ |
| 6.4 | `IWorkflowContextTemplateResolver` (`{{instance.*}}`, `{{outputs.*}}`, `{{event.*}}`) | ✅ |
| 6.5 | **Odak E2E:** manual → create → start_progress → log (4 node Success) | ✅ 3 Haz 2026 |

**Create node config:**

```json
{
  "type": "workitem.create",
  "config": {
    "workspaceId": "f414462a-…",
    "typeId": "b00b8480-…",
    "title": "Workflow E2E {{instance.correlationId}}",
    "initialTransitionKey": "start_progress"
  }
}
```

**Transition node config:**

```json
{
  "type": "workitem.transition",
  "config": {
    "workItemId": "{{outputs.create_1.workItemId}}",
    "transitionKey": "start_progress",
    "comment": "workflow faz6 e2e"
  }
}
```

Node output: `workItemId`, `workItemKey`, `stateId`; create'de ek `alreadyExists` (idempotent retry).

**Odak compose:** `MngWorkflowSettings__Operations__BaseUrl=http://mngoperations:5086` (+ mevcut Scheduler service account).

### Faz 6.1 — `workitem.update`

| # | Görev | Kabul |
|---|-------|-------|
| 6.1.1 | `workitem.update` → `PATCH /operations/api/v1/work-items/{id}` | ✅ |
| 6.1.2 | Config: `workItemId`, `title`, `description`, `assignee`, `priorityId`, `boardId`, `fields` (template) | ✅ |
| 6.1.3 | **Odak E2E:** manual → create → update → log (4 node Success) | ✅ 3 Haz 2026 |

### MO — `op_rules` → `startWorkflow`

| # | Görev | Kabul |
|---|-------|-------|
| MO.1 | Action `startWorkflow` (`workflowId`, opsiyonel `triggerData`) | ✅ |
| MO.2 | `IMngWorkflowClient` + `MngOperationsSettings__Workflow__BaseUrl` | ✅ |
| MO.3 | Automation phase side-effect (inline, 202 Accepted) | ✅ |
| MO.4 | **Odak E2E:** WI create → rule → workflow instance Completed | ✅ 3 Haz 2026 |

**Rule action örneği:**

```json
{
  "type": "startWorkflow",
  "workflowId": "0b95c624a410472d824ec13ea5482d34",
  "triggerType": "op_rules"
}
```

---

## 8. OC entegrasyon yol haritası (workflow fazları)

| Workflow fazı | OC etkisi | MO değişikliği |
|---------------|-----------|----------------|
| 0–1 | Yok | Hayır |
| 2–3 | Yok | Hayır |
| 4 | `oc.events` Event Trigger bind | Hayır (MO zaten publish ediyor) |
| 5 | Approval (SIEM onaylı müdahale) | Hayır |
| 6 | `CreateWorkItem` / `ApplyTransition` node'ları | ✅ Faz 6 |

**Faz 6 ilk OC kabul senaryosu:**

```text
Manual Trigger (veya oc.workitem.created Event Trigger)
  ↓ If (severity == critical)
  ↓ Create WorkItem (from-origin → SOC workspace)
  ↓ ApplyTransition (triage_open)
  ↓ Write Log
```

---

## 9. Sonraki adım seçenekleri

1. MngReactor repo: doğrudan `monitra.observations` publish — [REACTOR_NATIVE_PUBLISH_HANDOFF.md](../alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md)
2. ~~Alarm Faz 2 — correlation window, scheduled validation.~~ ✅
3. ~~Workflow motor — `parallel.fork` MVP + engine fix (çoklu branch tamamlanmadan Completed olmaz).~~ ✅ Odak E2E: `test-parallel-fork-e2e.ps1`
4. Idempotent create retry testi (`alreadyExists`).
5. `parallel.join` node (fork sonrası birleşim — backlog).

### P4-A — alarm → onay → aksiyon (minimal) ✅

| # | Görev | Kabul |
|---|-------|-------|
| P4.1 | Correlation alarm (`auth_failure_p4_e2e`) → `alarm.raised` Event Trigger + `filterExpression` | ✅ |
| P4.2 | Workflow: log context → `approval.wait` → `workitem.create` → log | ✅ |
| P4.3 | **Odak E2E:** `scripts/odak/test-alarm-approval-e2e.ps1` | ✅ 3 Haz 2026 |

### P4-B — engine komut kanalı (MVP) ✅

| # | Görev | Kabul |
|---|-------|-------|
| P4.4 | `engine.command` + `block.ip` node — MQTT topic via Reactor `/api/v1/mqtt/publish` | ✅ |
| P4.5 | Odak stub: `EngineCommand__DevLogOnly=true` (Reactor deploy sonrası kapatılır) | ✅ |
| P4.6 | **Odak E2E:** `scripts/odak/test-p4-engine-command-e2e.ps1` | ✅ deploy sonrası |

**Topic:** `monitoring/{domain}/engine/{engineId}/command` · **Komutlar:** `block_ip`, `unblock_ip`, genel `command` config

**Workflow key (Odak):** `alarm-p4-approval-e2e` · **Alarm matchKey:** `auth_failure_p4_e2e`

### parallel.fork MVP ✅

| # | Görev | Kabul |
|---|-------|-------|
| PF.1 | `parallel.fork` node — `config.branches` ile çoklu edgeKey | ✅ |
| PF.2 | Engine: tüm branch'ler bitmeden instance `Completed` olmaz | ✅ |
| PF.3 | **Odak E2E:** `scripts/odak/test-parallel-fork-e2e.ps1` | ✅ 3 Haz 2026 |

**Backlog:** `parallel.join` (fork birleşimi), workflow metrics.

**Ertelenen (SIEM §8 tam — bileşenler hazır olunca):**

| Madde | Bağımlılık |
|-------|------------|
| ~~Block IP / Unblock + TTL~~ | ✅ node MVP; gerçek Engine handler + Reactor deploy |
| Onay kartı UI | Mng.Ui güvenlik paneli |
| `sec_events` ingest (P3) | MngReactor SIEM Faz 1 — [SIEM_FAZ1_HANDOFF.md](../monitoring/SIEM_FAZ1_HANDOFF.md) |
| HTTP stub / firewall API | SIEM Faz 3 |

---

| # | Görev | Kabul |
|---|-------|-------|
| SLA.1 | `RuleTriggers`: `WorkItemSlaResponseBreached`, `WorkItemSlaResolveBreached` | ✅ |
| SLA.2 | `POST /operations/api/v1/sla/scan-breaches?workspaceId=` (manager/admin) | ✅ |
| SLA.3 | DG query `wi_sla_*_breach` → `op_rules` automation → `startWorkflow` | ✅ |
| SLA.4 | Idempotency: `sla.responseBreachNotifiedAt` / `resolveBreachNotifiedAt` | ✅ |
| SLA.5 | **Odak E2E:** `scripts/odak/test-sla-breach-workflow-e2e.ps1` | ✅ 3 Haz 2026 |
| SLA.6 | **Scheduler:** `sync-scheduler` + `oc-sla-scan-*` cron orchestration | ✅ |
| SLA.7 | **Odak E2E:** `test-sla-breach-scheduler-e2e.ps1` | ✅ 3 Haz 2026 |
| OC-E1 | **Odak E2E:** `oc.workitem.created` Event Trigger — `test-oc-workitem-created-e2e.ps1` | ✅ 3 Haz 2026 |

**Scheduler:** `POST /operations/api/v1/sla/sync-scheduler?workspaceId=` → MngScheduler User Job → cron → `scan-breaches`.

---

## 10. Mimari özet (dört katman)

```text
Engine/Reactor (topla+normalize)
  → Alarm & Rule Engine (tespit→alarm)
  → Workflow Engine (orkestrasyon)
  → Operation Core (WI komutları — from-origin / transition API)
       ↑ inline op_rules (senkron, aynı istek — workflow değil)
```

Seam'ler RabbitMQ üzerinden gevşek bağlı (`mng.alarms`, `oc.events`). Tespit ↔ orkestrasyon ↔ WI komutları ayrı bounded context'ler.

---

## 11. İlgili dökümanlar

**Workflow**

- [Workflow Backend Implementation Plan v1](./Workflow%20Backend%20Implementation%20Plan%20v1.md) — §13 Operation Core
- Orijinal taslaklar: `InternalDesign.md`, `MonitraNG Workflow Runtime Internal Design v1_1.md`, `planing.md`

**Operation Core**

- [operationcore/README.md](../operationcore/README.md)
- [API_SURFACE.md](../operationcore/mngoperations/API_SURFACE.md) — `from-origin`
- [INTEGRATIONS.md](../operationcore/mngoperations/INTEGRATIONS.md) — `oc.events`, Scheduler/Workflow
- [PIPELINES.md](../operationcore/mngoperations/PIPELINES.md) — idempotency §6.1
- [RULE_ENGINE.md](../operationcore/mngoperations/RULE_ENGINE.md) — `op_rules` vs workflow sınırı

**Deploy / agent**

- [../deploy/README.md](../deploy/README.md) — Odak senkron + docker compose (§12 kuralları buradan türetilir)

**Alarm / SIEM**

- `docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md`
- `docs/odak/monitoring/SIEM_PLANNING.md`, `docs/odak/monitoring/DEVAM.md`
- Major vizyon: `docs/odak/operationcore/major_plan.md` §4.2, §4.8

---

## 12. Geliştirme, test ve deploy kuralları (agent)

Bu bölüm, Workflow (ve genel backend) geliştirme oturumlarında agent'ın uyması gereken **sabit kuralları** tanımlar.

### 12.1 Test stratejisi

| Konu | Kural |
|------|-------|
| **Doğrulama ortamı** | Backend değişiklikleri **Odak sunucusunda** (`192.168.20.20`) deploy edilerek test edilir — yalnızca lokal build/run yeterli sayılmaz. |
| **Referans** | Deploy akışı: [../deploy/README.md](../deploy/README.md) |

### 12.2 Backend deploy — agent yapabilir

Agent, backend servis değişikliklerinden sonra **kullanıcıdan ayrı onay beklemeden** Odak sunucusuna deploy yapabilir.

**Standart akış** (repo kökünden, **`pwsh` zorunlu**):

```powershell
# 1) Kaynak senkronu (yalnızca değişen servis + gerekirse mng_apps)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths MngWorkflow,ApplicationResources/mng_apps

# 2) Build + konteyner yenileme
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngworkflow
```

| Not | Açıklama |
|-----|----------|
| Compose servis adı | `mngworkflow` (host port **5085**) |
| Kritik fix / cache şüphesi | `-NoCache` ekle |
| SSH kimlik bilgisi | `.env.odak.local` veya `scripts/odak/local-credentials.ps1` — bkz. deploy README §1.2 |
| Worker ayrı host olursa | İleride `mngworkflow-worker` vb. compose adı bu bölüme eklenir |

Diğer backend servisleri için aynı pattern; tam liste: [deploy/README.md §4](../deploy/README.md).

**Deploy sonrası doğrulama (örnek):**

| Kontrol | URL / komut |
|---------|-------------|
| Workflow health | `http://192.168.20.20:5085/health` |
| Gateway | `http://192.168.20.20:5040/health` |

### 12.3 UI deploy — agent yapamaz (onay zorunlu)

| Konu | Kural |
|------|-------|
| **`Mng.Ui` deploy** | Kullanıcının **açık talebi ve onayı olmadan** `sync-odak-source.ps1 -Paths Mng.Ui` veya `-Services mngui` **çalıştırılmaz**. |
| **Gerekçe** | UI deploy uzun sürer; tarayıcı cache etkiler; kullanıcı kendi terminal/akışında yönetir. |
| **İstisna** | Kullanıcı «UI'ı deploy et» veya eşdeğeri açıkça söylerse → deploy README §3 izlenir. |

### 12.4 Yerel UI / dev sunucusu — agent açmaz

| Konu | Kural |
|------|-------|
| **`npm run dev` / Nuxt dev server** | Agent, Cursor içinde UI çalıştırmak için **arka planda terminal açmaz**. |
| **Kullanıcı tercihi** | UI geliştirme ve yerel önizleme kullanıcının **kendi terminal uygulamasında** yürütülür. |
| **Agent rolü** | UI kodu yazabilir; çalıştırma/deploy kullanıcı onayına bırakılır (§12.3). |

### 12.5 Özet (tek bakışta)

```text
Backend değişikliği  →  sync + deploy (agent, otomatik OK)  →  sunucuda test
UI değişikliği       →  kod yaz (agent)  →  deploy/dev YALNIZCA kullanıcı onayıyla
Yerel UI sunucusu    →  agent terminal açmaz
```

### 12.6 Performans ilkeleri (Faz 0/1 — uygulanan)

| Alan | Karar | Gerekçe |
|------|-------|---------|
| Mongo | Singleton `IMongoClient`; DB cache `ConcurrentDictionary`; idempotency **projection** (`AnyAsync` + limit 1) | Connection pool; tekrarlayan DB handle maliyeti yok |
| RabbitMQ | Singleton connection; tek publish channel + lock; consumer `BasicQos prefetch=16` | Channel thread-safety; kontrollü paralellik |
| Node'lar | **Singleton** stateless node'lar; registry tek seferlik dictionary | Scoped allocation / resolve maliyeti yok |
| JSON | Statik `JsonSerializerOptions` (camelCase) | Hot path allocation azaltma |
| HTTP node | `IHttpClientFactory` + `ResponseHeadersRead` + timeout | Socket reuse; tam body buffer'dan kaçınma |
| If (Faz 1/3) | Yapılandırılmış karşılaştırma **veya** Jint `expression` | Faz 1: field/operator; Faz 3: sandboxed JS |
| Index | Lazy domain başına bir kez; unique `(instanceId, nodeId, attempt)` | Worker cold start; idempotent replay hızlı |
| Worker / API | Ayrı konteyner (`mngworkflow` + `mngworkflow-worker`) | API latency worker load'dan izole |

**Henüz yok (backlog):** `parallel.join`, workflow metrics, MngReactor native observation publish (MonitraNG consumer hazır).

**AI-ready (R5):** Nested event/alarm context normalize sonrası korunur — `WorkflowJsonNormalizerR5Tests`. AI node sözleşmesi: [AI_NODE_EXTENSION_SPEC.md](./AI_NODE_EXTENSION_SPEC.md) (implementasyon AI-3, P4 sonrası).
