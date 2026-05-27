# MngOperations — API yüzeyi

**Son güncelleme:** 26 Mayıs 2026  
**Gateway prefix:** `/operations/api/v1` → downstream `/api/v1`

---

## 1. Genel kurallar

- Tüm endpoint’ler **JWT Bearer** zorunlu (gateway veya doğrudan).
- Operasyonel mutasyonlar bu API’de; metadata CRUD **DG** `/data/api/v1`.
- `stateId` doğrudan PATCH **yasak** — yalnızca `POST .../transitions/{transitionKey}`.
- Yanıtlar camelCase JSON; tarihler ISO-8601 UTC.
- Çok adımlı pipeline kısmi hata: `code: "PARTIAL_FAILURE"`, `completedSteps[]` ([PIPELINES.md §9](./PIPELINES.md)).

---

## 2. Komutlar (yazma)

| Method | Downstream path | Açıklama |
|--------|-----------------|----------|
| `POST` | `/work-items` | Create pipeline → WorkItem + activity + rules + events |
| `PATCH` | `/work-items/{id}` | Alan güncelleme (rules); `stateId` yok |
| `POST` | `/work-items/{id}/transitions/{transitionKey}` | Tam transition pipeline |
| `POST` | `/work-items/{id}/comments` | Yorum + activity |
| `POST` | `/work-items/from-origin` | Monitoring/security/scheduler kökenli oluşturma |

### 2.1 `POST /work-items` (gövde özeti)

```json
{
  "workspaceId": "…",
  "typeId": "…",
  "title": "…",
  "fields": { "location": "A1" },
  "boardId": "…",
  "assignee": "user@domain",
  "origin": null
}
```

**Yanıt:** `{ "workItem": { … }, "profileContext": { … } }` (isteğe bağlı gömülü context).

### 2.2 `POST /work-items/from-origin`

```json
{
  "workspaceId": "…",
  "typeId": "…",
  "title": "…",
  "origin": {
    "sourceType": "monitoring",
    "sourceId": "alarm-uuid",
    "correlationId": "evt-456",
    "payload": { }
  },
  "initialTransitionKey": "triage_open"
}
```

- `origin.correlationId` **zorunlu** (Q6).
- Aynı `correlationId` ile tekrar istek → **200** + mevcut kayıt (`ALREADY_EXISTS`), yeni WI açılmaz ([PIPELINES §6.1](./PIPELINES.md)).
- İlk istek: normal create pipeline; `origin` validate.

### 2.3 `POST /work-items/{id}/transitions/{transitionKey}`

Gövde (opsiyonel): `{ "fields": { "resolution": "…" }, "comment": "…" }`

**Yanıt:** güncel work item + `availableTransitions` + güncellenmiş SLA alanları.

---

## 3. Runtime context (okuma)

| Method | Path | DTO |
|--------|------|-----|
| `GET` | `/runtime/boards/{boardId}` | `BoardRuntimeContext` |
| `GET` | `/runtime/work-items/{id}/form?mode=create\|edit` | `FormRuntimeContext` |
| `GET` | `/runtime/work-items/{id}/profile` | `ProfileRuntimeContext` |
| `GET` | `/runtime/work-items/{id}/timeline?skip=&take=` | `TimelinePage` |
| `GET` | `/runtime/work-items/{id}/state-segments` | `StateSegmentsPage` (son N segment) |
| `GET` | `/runtime/dashboards/{dashboardId}` | `DashboardRuntimeContext` (widget sync execute) |
| `POST` | `/runtime/queries/{queryKey}/execute` | Parametreli query sonucu |

### 3.1 `BoardRuntimeContext` (özet alanlar)

```json
{
  "boardId": "…",
  "workspaceId": "…",
  "columns": [
    {
      "stateId": "…",
      "title": "In Progress",
      "dropEligible": true,
      "defaultTransitionKey": "start_progress",
      "alternativeTransitionKeys": []
    }
  ],
  "cardFieldKeys": ["title", "assignee", "priority"],
  "permissions": { "canCreate": true }
}
```

Kanban: [OPERATION_CORE §5.2.2](../OPERATION_CORE_IMPLEMENTATION_PLAN.md).

### 3.2 `ProfileRuntimeContext`

- `workItem` (zengin özet), `fields`, `fieldBehaviors`, `actions[]` (`op_profiles.actions` sırası)
- `header`, `sidebar`, `panels`, `layout`
- `sla`, `watchers`, `links`, `stateSegments` (embed son 5)

### 3.3 `DashboardRuntimeContext`

- `widgets[]`: tanım + `resolvedParameters` + `execution` (Faz 1 senkron query; `summaryCard` / `list` / `chart`)

### 3.4 Hata — `PARTIAL_FAILURE`

Persist sonrası side-effect hatası → HTTP **500**, `details.completedSteps[]`, `details.failedStep`, `details.correlationId`, opsiyonel `details.workItem`. Bkz. [PIPELINES.md §9](./PIPELINES.md).

---

## 4. Yardımcı endpoint’ler

| Method | Path | Not |
|--------|------|-----|
| `GET` | `/api/v1/health` | DG + RabbitMQ + Notifier ping (öneri) |
| `GET` | `/api/v1/version` | Platform standardı |

---

## 5. Versiyonlama

- URL: `/api/v1` sabit Faz 1.
- Breaking change → `v2` + gateway route.

---

## 6. OpenAPI

`MngOperations.Api` Swagger + gateway `/operations/swagger` (Workflow/Scheduler pattern).
