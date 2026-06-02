# MonitraNG Workflow Backend Implementation Plan v1

## Doküman Durumu

* Durum: Planlama
* Versiyon: 1.0
* Kapsam: Workflow Backend Implementation (Servis yapısı, Mongo koleksiyonları, RabbitMQ topolojisi, Worker, geliştirme sırası)
* Bağımlılık:

  * Workflow Engine Planı v1.1 (`planing.md`)
  * Workflow Runtime Internal Design v1.0 (`InternalDesign.md`)
  * Workflow Runtime Internal Design v1.1 (`MonitraNG Workflow Runtime Internal Design v1_1.md`)

> Bu doküman mimariyi değil, doğrudan .NET Core proje yapısını, servisleri, MongoDB koleksiyonlarını, RabbitMQ topology'sini, worker sınıflarını ve geliştirme sıralamasını tanımlar. Önceki üç dokümanı tek tutarlı plana indirger ve mevcut MonitraNG altyapısına oturtur.

---

# 0. Kesinleşen Mimari Kararlar

| Konu | Karar | Gerekçe |
|---|---|---|
| Persistence | **Hibrit**: Worker → doğrudan Mongo driver. Definition/Version CRUD → MngWorkflow.Api → doğrudan Mongo | Yüksek frekanslı runtime state için HTTP round-trip ve atomiklik/optimistic locking riski elenir |
| Delay / Schedule | **MngScheduler (Quartz)** uzun delay + schedule için; **kısa delay (< ~1 dk) motor-içi bucket kuyruklarıyla** | Mevcut altyapı; cron dakika granülaritesi kısa delay'de yetersiz |
| Execution granularity | **Per-node**: her node ayrı queue mesajı, context her adımda persist | Retry/DLQ/replay/debug/parallel zaten node granülaritesinde |
| Multi-tenancy | Tüm koleksiyon ve mesajlar **domain-scoped** (`domainId`), routing key'lerde `{domainId}` prefix | Tüm MonitraNG domain-scoped çalışır |
| Servis sınırları | İki servis: `MngWorkflow.Api` + `MngWorkflow.Worker` | Worker stateless, horizontal scale |
| Expression Engine | **Jint** (sandbox: timeout + statement limiti, read-only context) | Tek motorla koşul + transform; güvenlik kontrol altında |
| Inline node optimizasyonu | İleri faza ertelendi (Faz 1 saf per-node) | Önce basit ve doğru |

---

# 1. Çözüm / Proje Yapısı

```text
MngWorkflow/
  Core/
    MngWorkflow.Domain/          # Entities, enums, value objects, sabitler
    MngWorkflow.Application/      # Interfaces, contracts, node abstractions, expression, registry
  Infrastructure/
    MngWorkflow.Infrastructure/   # Mongo repo'ları, RabbitMQ publisher/consumer, secret, scheduler client
  Presentation/
    MngWorkflow.Api/              # CRUD, publish, run, approvals, run-history, debug (mevcut)
    MngWorkflow.Worker/           # YENİ: queue consumer + node execution engine (Generic Host / BackgroundService)
  Tests/
    MngWorkflow.Tests/
```

* `MngWorkflow.Worker` yeni host projesi: `BackgroundService` tabanlı, stateless → docker/k8s'te N replica.
* Mevcut iskeletteki `Class1.cs` placeholder'ları gerçek tiplerle değiştirilir.

---

# 2. Domain Modeli

Önceki dokümanlardaki tutarsızlıklar giderilerek:

## WorkflowDefinition

`__dataId`, `domainId`, `key`, `name`, `category`, `currentVersion`, `currentVersionId`

## WorkflowVersion

`__dataId`, `workflowId`, `domainId`, `version`, `status` (Draft/Published/Archived), `nodes[]`, `edges[]`, `publishedAt`

## WorkflowInstance

`__dataId`, `workflowId`, `workflowVersionId`, `domainId`, `status` (Running/Waiting/Completed/Failed/Cancelled), `currentNodes[]`, `executionContext`, `correlationId`, `triggerType`, `triggerData`, **`revision`** (optimistic concurrency — yeni), `startedAt`, `finishedAt`

## NodeExecution

`__dataId`, `instanceId`, `domainId`, `nodeId`, `attempt`, `status`, `output`, `errorMessage`, `startedAt`, `finishedAt`

> Benzersiz index: **(instanceId, nodeId, attempt)** → idempotency.

## WorkflowSecret

`__dataId`, `domainId`, `key`, `encryptedValue`, `algo`

## WorkflowApproval

`__dataId`, `instanceId`, `domainId`, `nodeId`, `status`, `approverTarget`, `decidedBy`, `decidedAt`

## Düzeltilen Tutarsızlıklar

1. **NextEdges (çoğul)**: `NodeExecutionResult.NextEdgeType` (tekil) → `NextEdges` (`List<string>`). If/Switch çok-dallı ve Parallel çok-çıkışlı senaryolar tek modelle çalışır; `currentNodes[]` ile tutarlı olur.
2. **Terminoloji**: "Runtime hiçbir node'u doğrudan çağırmaz" → "Orchestrator node'u senkron çağırmaz; queue üzerinden Worker çağırır."

```csharp
public class NodeExecutionResult
{
    public bool Success { get; set; }
    public List<string> NextEdges { get; set; } = new();   // önceden: string? NextEdgeType
    public Dictionary<string, object?> Output { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool ShouldWait { get; set; }
    public string? WaitingType { get; set; }                // WaitingApproval / WaitingDelay / WaitingEvent / WaitingManualResume
}
```

---

# 3. MongoDB Koleksiyonları & Erişim

Koleksiyonlar (domain bazlı, mevcut `@` dataset konvansiyonuyla uyumlu):

```text
@workflow_definitions
@workflow_versions
@workflow_instances
@workflow_node_executions
@workflow_secrets
@workflow_approvals
```

## Erişim

* **Worker**: doğrudan `MongoClient`. Instance ilerletme **optimistic concurrency** ile:

```text
UpdateOne(
  filter: _id == id && revision == r,
  update: set currentNodes/context, inc revision)
```

  0 matched → başka worker güncellemiş → mesajı yeniden yükle/re-queue.

* **Api**: definition/version CRUD → doğrudan Mongo. (DataGateway HTTP yalnızca başka modüllerin verisini okurken kullanılır.)

## İndeksler

```text
@workflow_instances        (domainId, status)
@workflow_node_executions  (instanceId, nodeId, attempt)  UNIQUE
@workflow_approvals        (instanceId, nodeId, status)
@workflow_versions         (workflowId, version)
```

---

# 4. RabbitMQ Topolojisi

Mevcut desen: `RabbitMQ.Client` v7 async API, **topic exchange**, per-domain routing, persistent mesaj (örn. `OcEventPublisher`).

## Exchange

```text
mng.workflow   (topic, durable)
```

## Kuyruklar

> v1.1'deki 5'li kuyruk listesi sadeleştirildi.

```text
workflow.execution    -> node çalıştırma. Routing: {domainId}.workflow.exec
workflow.retry        -> TTL + DLX ile KISA retry (saniye-dakika)
workflow.deadletter   -> retry limitini aşan node'lar
```

* **Delay / Schedule**: kuyruk YOK → MngScheduler'a job kaydı. Süre dolunca MngScheduler event publish eder, Workflow EventListener resume tetikler.
* **Event trigger**: yeni omurga kurulmaz. EventListener mevcut exchange'lere bind olur:

```text
mng.topics
oc.events
mngkeeper.events
monitra.data.events.{domain}
```

## Mesaj Zarfı

```json
{
  "instanceId": "guid",
  "workflowVersionId": "guid",
  "nodeId": "node_1",
  "attempt": 1,
  "correlationId": "guid",
  "domainId": "guid"
}
```

---

# 5. Node Execution Engine (Worker)

## Per-node Döngü

```text
Dequeue (execution)
↓
Idempotency check: (instanceId, nodeId, attempt) zaten Success mü? → ack, skip
↓
Instance + Version yükle
↓
Node tipini Registry'den çöz
↓
ExecuteAsync(context, node, ct)   [timeout + cancellation]
↓
Sonuç:
  Success     → context + node_execution persist (optimistic) → NextEdges'e göre sonraki node'ları enqueue
  ShouldWait  → status = Waiting, currentNodes güncelle, ack (approval/delay/event resume bekler)
  Fail+retry  → retry kuyruğuna (attempt+1)
  Fail+bitti  → deadletter, instance = Failed
↓
ack
```

## Node Contract

```csharp
public interface IWorkflowNode
{
    string NodeType { get; }   // örn. "http.request"

    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken);
}
```

## Registry

`IEnumerable<IWorkflowNode>` DI ile inject edilir → `Dictionary<string, IWorkflowNode>`. Yeni node = sadece DI kaydı.

```csharp
services.AddScoped<IWorkflowNode, ManualTriggerNode>();
services.AddScoped<IWorkflowNode, IfNode>();
services.AddScoped<IWorkflowNode, HttpRequestNode>();
services.AddScoped<IWorkflowNode, WriteLogNode>();
```

---

# 6. Expression Engine

* **Jint (sandboxed JS).**
* Güvenlik:

  * `Engine` başına execution timeout + statement/recursion limiti.
  * Host nesnesi expose edilmez.
  * Yalnızca read-only context enjekte edilir: `event`, `variables`, `outputs`, `user`, `system`.

Örnek ifadeler:

```text
event.riskScore > 70
event.country != "TR"
outputs.ai.score > 90
```

---

# 7. Secret & Güvenlik

* `WorkflowSecret.encryptedValue`: **AES-GCM**, anahtar config/ortam değişkeninden (ileride MngKeeper / KMS'e taşınabilir).
* Çözümleme yalnızca node çalışırken, `{{secrets.key}}` placeholder resolve edilirken yapılır.
* Secret context'e veya log'a asla yazılmaz (v1.1 §13).
* Loglarda secret / token / Authorization header maskeleme.

---

# 8. Çapraz Kesen Konular (dokümanlarda eksikti — eklendi)

* **Optimistic concurrency**: `WorkflowInstance.revision` alanı (bkz. §3).
* **Idempotency**: `(instanceId, nodeId, attempt)` unique index + execute öncesi dedup.
* **Instance-level cancel/timeout**: `workflow.cancel` API + opsiyonel global workflow timeout.
* **Long-running kimlik**: JWT expire olacağı için aksiyonlar **service identity** ile çalışır (workflow sahibi snapshot'ı + sistem token'ı). Approval'da karar veren gerçek kullanıcı ayrıca loglanır.
* **Correlation**: her instance `correlationId` taşır; tüm log/trace bununla ilişkilendirilir (Serilog enrich).

---

# 9. Geliştirme Sırası (Faz Planı)

| Faz | İçerik | Kabul Kriteri |
|---|---|---|
| **0** | Worker host projesi, Mongo bağlantısı, RabbitMQ exchange/queue declare, domain context | Worker ayağa kalkar, mesaj tüketir |
| **1** | Domain modeli + repo'lar + Registry + 4 node (ManualTrigger / If / HttpRequest / WriteLog) + per-node engine | **İlk teknik hedef**: Manual → If → HTTP → Log uçtan uca çalışır |
| **2** | Definition/Version CRUD + Publish lifecycle + Version isolation + Run / RunHistory API | UI'sız manuel run + geçmiş |
| **3** | Retry / Timeout / DeadLetter + idempotency + optimistic concurrency + Expression engine (Jint) | Dayanıklı çalıştırma |
| **4** | Trigger sistemi: Event (mevcut exchange'lere bind) + Schedule/Delay (MngScheduler) | AlarmRaised → workflow tetikleme |
| **5** | Approval + Waiting/Resume + Secret resolver | Long-running onay senaryosu |
| **6** | MonitraNG entegrasyon node'ları (Alarm / WorkItem / Notification / Block IP) + Debug / Replay | Modüller arası orkestrasyon |
| **7+** | Parallel / Join, ForEach, SubWorkflow, Compensation | Gelişmiş runtime |

---

# 10. İlk Teknik Hedef

UI geliştirmeden önce aşağıdaki senaryo uçtan uca çalışmalıdır (Faz 1 kabul kriteri):

```text
Manual Trigger
↓
If
↓
HTTP Request
↓
Write Log
```

Bu senaryo başarıyla çalıştırıldığında Runtime Core tamamlanmış kabul edilir; sonrasında UI geliştirmesine geçilir.

---

# 11. Açık Kararların Çözümü

Mevcut MonitraNG kod tabanı incelenerek netleştirilen kararlar.

## 11.1 ValidationPipeline ile İlişki

Mevcut `ValidationPipelineService`, DataGateway'in yazma öncesi **senkron** çağırdığı `fetch → assert → return` adımlı bir DSL'dir (kritik yazma yolunda, kısa ömürlü, deterministik). Queue-based async engine ile zıt paradigma.

**Karar:** Birleştirilmez. `MngWorkflow` altında iki ayrı bounded context olarak yaşar:

```text
MngWorkflow.Validation   (senkron DG validation — mevcut)
MngWorkflow.Engine       (async queue-based workflow — yeni)
```

Ortak nokta: **expression engine (Jint)** ve ileride step/node executor soyutlaması paylaşılan kütüphaneye çekilir. "Validation workflow" ileride bir trigger tipi olabilir; şimdilik kapsam dışı.

## 11.2 Trigger → Workflow Binding

Trigger'lar `WorkflowVersion` içinde saklanır:

```json
{
  "triggers": [
    { "type": "event", "config": { "eventType": "AlarmRaised" }, "filterExpression": "event.riskScore > 70", "enabled": true }
  ]
}
```

Publish anında hızlı arama için indeksli projeksiyon üretilir:

```text
@workflow_triggers  (domainId, eventType, workflowId, versionId, filterExpression)
```

Akış: EventListener event'i alır → `(domainId, eventType)` ile eşleşen trigger'ları bulur → `filterExpression`'ı (Jint) payload'a karşı değerlendirir → her eşleşme için instance başlatır.

**Many-to-many:** bir event birden fazla workflow'u tetikleyebilir; bir workflow birden fazla trigger'a sahip olabilir.

## 11.3 domainId Kaynağı (Trigger Tipine Göre)

| Trigger | domainId kaynağı |
|---|---|
| Manual | JWT claim (`domain_name` / `domain_id`, fallback `X-Domain-Name`) |
| Event | Event envelope (`Domain.Name`) / routing key `{domainId}.*` |
| HTTP webhook | Trigger kayıt anında bağlanan domain (çağırandan DEĞİL; webhook key domain-scoped) |
| Schedule / Delay | MngScheduler job `DomainId`'si, resume çağrısıyla geri taşınır |

Instance oluşturulurken `domainId` ile **mühürlenir**; tüm sonraki Mongo işlemleri ve routing key'ler bunu kullanır.

## 11.4 Retry Delay Modeli

Tek kuyrukta per-message TTL → head-of-line blocking. Bunun yerine **sabit delay-bucket kuyrukları**:

```text
workflow.retry.5s
workflow.retry.30s
workflow.retry.2m
workflow.retry.10m
```

Her biri queue-level TTL + DLX ile `workflow.execution`'a geri döner. Node'un `delaySeconds`'ı en yakın üst bucket'a yuvarlanır. Retry gecikmeleri **≤ 15 dk** üst sınırlı; daha uzun beklemeler Delay node işidir (§11.5).

## 11.5 Delay / Schedule — MngScheduler Kontratı

MngScheduler `ScheduledJob` (cron + `endpointUrl` + payload + headers + `maxExecutionCount` + `DomainId`) tetiklenince hedef URL'e HTTP atar ve `mng_scheduler_events`'e event basar. User job'lar `POST /api/v1/user/jobs` ile domain-scoped oluşturulur. Operation Core tetiklerinde `IMngKeeperAuthClient` (password grant) ile **service token** alınır.

* **Schedule Trigger (cron, tekrarlı):** Workflow.Api MngScheduler'da User Job oluşturur; `endpointUrl = {workflow başlatma webhook'u}`, `payload = { workflowId, versionId }`.
* **Delay node (tek seferlik):** `maxExecutionCount=1` tek-atışlık User Job; `endpointUrl = {resume webhook}`, `payload = { instanceId, nodeId }`. Tetiklenince auto-deactivate; periyodik temizlik tüketilmiş job'ları siler.
* **Kısa delay (< ~1 dk):** Scheduler'a verilmez; §11.4 bucket kuyruklarıyla motor içinde çözülür (**eşik ayrımı**).
* **Service identity:** Workflow da bu çağrılar ve long-running aksiyonlar için `IMngKeeperAuthClient` deseniyle service token alır. (Long-running kimlik sorununu çözer — bkz. §8.)

## 11.6 HTTP Trigger Webhook Güvenliği

Gateway MngWorkflow'u `http://mngworkflow:5085` (`/workflow` prefix) olarak expose eder.

* Endpoint: `POST /workflow/hooks/{webhookKey}` — key domain'e mühürlü, opak.
* Doğrulama: **HMAC imza** (`X-Mng-Signature`), webhook secret'ı `@workflow_secrets`'tan (GitHub/Stripe deseni).
* İç çağrılar için alternatif static bearer token modu.
* Anonim erişim yalnızca trigger'da açıkça etkinleştirilirse. Rate-limit gateway'de.

## 11.7 Yetkilendirme

Auth = MngKeeper (Keycloak/JWT). Modül bazında `IPermissionEvaluator` deseni (Operations'taki gibi).

* 10 `workflow.*` izni MngKeeper'da rol/permission olarak tanımlanır; `MngWorkflow.Api`'de `IPermissionEvaluator` (veya `[Authorize(Policy=...)]`) ile uygulanır.
* **Worker çalışma anında kullanıcı izni kontrol etmez** — yetki design/publish/run-trigger anında verilir; çalışma service identity ile yürür.
* Approval kararında: `workflow.approve` + kullanıcının `approverTarget` ile eşleşmesi kontrol edilir.

---

# 12. Monitoring / SIEM Kesişimi

İlgili: `docs/odak/monitoring/SIEM_PLANNING.md`. SIEM-hafif planının workflow'dan **iki ayrı** beklentisi vardır ve bunlar kesin olarak ayrılır.

## 12.1 Sınır Kararı

| Monitoring ihtiyacı | Workflow engine'in rolü |
|---|---|
| **Korelasyon / tespit** (SIEM §7): stateful, çok-olaylı, zaman pencereli kural (ör. 5 dk'da 10 başarısız login) | **Workflow'un işi DEĞİL.** Platform geneli **Alarm & Rule Engine** yapar (`docs/odak/alarm/ALARM_RULE_ENGINE_PLAN.md`, major §4.2) |
| **Onaylı müdahale orkestrasyonu** (SIEM §8): alert → onay → aksiyon → audit/TTL/rollback | **Tamamen workflow engine** ile karşılanır |

**Gerekçe:** Workflow engine per-instance orkestrasyon motorudur (bir trigger → bir instance → node'lar). Korelasyon ise olay akışı üzerinde sürekli kayan-pencere/groupBy/sequence/cooldown tutan bir akış-işleme (CEP) işidir. İkisini birleştirmek anti-pattern olur.

**Temiz seam:** İki sistem RabbitMQ'daki **alarm event'i** üzerinden bağlanır (`mng.alarms` exchange; Alarm Engine Plan §8). Alarm & Rule Engine alarm üretir; workflow Event Trigger ile tüketir. Gevşek bağlılık korunur.

## 12.2 Onaylı Müdahale Eşlemesi (SIEM §8 → workflow node'ları)

| SIEM §8 adımı | Workflow karşılığı |
|---|---|
| Alert (srcIp=X) | Event Trigger (alert event'ine bind — §11.2) |
| Onay bekleyen kart + operatör onayı | Approval node (WaitingApproval/Resume) |
| Engine → Firewall blok | Block IP action node — **Engine komut kanalı** üzerinden (firewall'a doğrudan değil) |
| TTL: 1 saat sonra geri al | Block IP → Delay(1s, MngScheduler) → Unblock IP (§11.5) |
| Audit (kim/ne zaman/hangi kural) | `@workflow_node_executions` + correlationId |
| Alert → incident | Create WorkItem node → MngOperations |

## 12.3 Workflow Tarafında Gereken Küçük Adaptasyonlar

* **Engine-komut aksiyon node'u:** Block IP / Unblock IP node'ları firewall API'sine doğrudan değil, MngEngine'in (MQTT) komut kanalına komut basar. Yeni node tipi değil, mevcut node'un on-prem implementasyon detayı.
* **Alert event şeması:** Korelatör alert'i `domainId` + `eventType` (ör. `SecurityAlertRaised`) + kural/srcIp/severity payload'ı taşımalı ki Event Trigger eşleştirme + `filterExpression` çalışsın.
* **Paylaşılan expression engine:** SIEM `sec_rules.match` ve workflow `filterExpression` aynı Jint motorunu kullanabilir → tek ifade dili.

> Sonuç: Monitoring'in **orkestrasyon** ihtiyacı bu planla fazlasıyla karşılanır; **tespit/korelasyon** ayrı kalır. Bu, SIEM_PLANNING.md §12.1 açık kararını kapatır (→ ayrı motor).
