# MngOperations — API yüzeyi

**Son güncelleme:** 29 Mayıs 2026  
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
| `DELETE` | `/work-items/{id}` | Kalıcı silme (DG delete + activity + `oc.workitem.deleted`); yetki = update/manager → 204 |
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
- `origin.sourceType` örnekleri: `monitoring`, `scheduler`, `workflow` (MngWorkflow — [Workflow Plan §13](../../workflow/Workflow%20Backend%20Implementation%20Plan%20v1.md)).
- Aynı `correlationId` ile tekrar istek → **200** + mevcut kayıt (`ALREADY_EXISTS`), yeni WI açılmaz ([PIPELINES §6.1](./PIPELINES.md)).
- İlk istek: normal create pipeline; `origin` validate.

### 2.3 `POST /work-items/{id}/transitions/{transitionKey}`

Gövde (opsiyonel): `{ "fields": { "resolution": "…" }, "comment": "…" }`

**Yanıt:** güncel work item + `availableTransitions` + güncellenmiş SLA alanları.

### 2.4 Katalog CRUD (write-through cache) — 29 May 2026

Katalog yazma işlemleri **MO üzerinden** geçer; MO hem DG'ye yazar hem de kendi cache'ini invalide eder (UI doğrudan DG'ye yazmaz → cache tutarlılığı).

| Method | Downstream path | Açıklama |
|--------|-----------------|----------|
| `POST` | `/catalogs/{source}` | Katalog kaydı oluştur |
| `PUT` | `/catalogs/{source}/{id}` | Güncelle |
| `DELETE` | `/catalogs/{source}/{id}` | Sil (kullanımda guard) |

- `{source}` ∈ `states` · `priorities` · `types` · `fields` (`OcCatalogRegistry`).
- Yetki: platform admin **veya** manager (`CatalogService.EnsureCatalogAdmin`).
- Silme: `op_work_items` / `op_workspaces` kullanım kontrolü (`UsageChecks`) — kullanımdaysa 409.
- Cache: `MetadataCache.InvalidateCatalog(dataset)` (write-through).

### 2.5 Workspace metadata cache reload — 9 Haziran 2026

Form / alan / akış tanımları DG’de güncellenir; MO runtime `IMetadataCache` TTL (~600 sn Odak) nedeniyle gecikmeli yansıyabilir.

| Method | Path | Açıklama |
|--------|------|----------|
| `POST` | `/workspaces/{workspaceId}/metadata-cache/reload` | Workspace kapsamlı in-memory cache temizliği |

- Yetki: platform admin veya workspace manager (catalog admin ile aynı çizgi).
- Yanıt: `MetadataCacheReloadResult` — silinen anahtar sayısı özeti.
- Kod: `WorkspacesController`, `MetadataCacheAdminService`, `MetadataCacheService.ReloadWorkspaceAsync`.

---

## 3. Runtime context (okuma)

| Method | Path | DTO |
|--------|------|-----|
| `GET` | `/runtime/boards/{boardId}` | `BoardRuntimeContext` |
| `POST` | `/runtime/boards/{boardId}/list` | `QueryExecuteResponse` (server-side liste: sayfalama/sıralama/filtre/arama) |
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
  "cardFieldKeys": ["title", "assignee", "priorityId", "key"],
  "listColumns": [
    { "key": "key", "sortable": true, "filterable": false },
    { "key": "stateId", "sortable": true, "filterable": true }
  ],
  "defaultSort": { "field": "lastStateChangeAt", "direction": "desc" },
  "catalogs": {
    "states":     { "<id>": { "id": "…", "name": "Açık", "color": "#…", "icon": "…" } },
    "priorities": { "<id>": { "id": "…", "name": "Yüksek", "color": "#…", "icon": "…" } },
    "types":      { "<id>": { "id": "…", "name": "Görev", "color": "#…", "icon": "…" } }
  },
  "permissions": { "canCreate": true }
}
```

- `cardFieldKeys` = board `visibleFields` (çekirdek + **pool alan key'leri**); pool alanlar liste tablosunda sütun olur.
- `listColumns` = sıralı liste sütunları + per-sütun `sortable`/`filterable` (board `config.listColumns`). Eski board'larda `visibleFields`'tan türetilir.
- `defaultSort` = liste varsayılan sıralaması (board `config.defaultSort`); kullanıcı sıralaması yoksa uygulanır.
- `catalogs` = id→ad/renk/ikon lookup (client-side join yok); **workspace kapsamına indirgenir** (state = board akış kapsamı ∪ `enabledStateIds`; priority/type = `enabled*Ids`, yoksa workspace tipleri/tüm katalog). Detay: [RUNTIME_CONTEXT §5.2](./RUNTIME_CONTEXT.md).

Kanban: [OPERATION_CORE §5.2.2](../OPERATION_CORE_IMPLEMENTATION_PLAN.md).

### 3.1.1 `POST /runtime/queries/{queryKey}/execute` yanıtı

```jsonc
{
  "dataset": "op_work_items",
  "queryKey": "wi_board_column",
  "items": [ { "id", "key", "title", "stateId", "assignee", "priorityId", "typeId",
              "fields": { "<poolKey>": "…" } } ],
  "people": { "<userId>": { "id", "name", "title", "isActive" } },
  "skip": 0, "take": 50, "total": 12
}
```

- `items[].fields` = pool alan değerleri (`extraFields`).
- `people` = person alanları (assignee/watchers + person pool) id→ad; MO Keeper cache. Detay: [RUNTIME_CONTEXT §5.3](./RUNTIME_CONTEXT.md) · [INTEGRATIONS §1.1](./INTEGRATIONS.md).

### 3.1.2 `POST /runtime/boards/{boardId}/list` (server-side liste)

İstek gövdesi (`BoardListRequest`):

```jsonc
{
  "skip": 0,
  "take": 50,
  "sort": { "field": "priorityId", "direction": "desc" },   // opsiyonel; yoksa board defaultSort
  "filters": [                                               // hepsi AND
    { "field": "stateId",   "operator": "in",       "value": "s1,s2" },
    { "field": "priorityId","operator": "ne",       "value": "p_low" },
    { "field": "title",     "operator": "contains", "value": "ödeme" }
  ],
  "search": "fatura"
}
```

Yanıt = §3.1.1 ile aynı `QueryExecuteResponse` (`items` + `people` + `total`).

- **Sunucu tarafı** sayfalama/sıralama/filtre/arama; DG `POST /data/{ds}/query` (native Mongo `match`) üzerinden, `$facet` ile toplam (`X-Total-Count`).
- **Operatörler:** `eq, ne, gt, gte, lt, lte, in, nin, contains, startsWith, endsWith`. `in/nin` virgülle ayrılır; metin operatörleri `$regex` (case-insensitive, escape'li).
- **AND birleşimi:** koşullar `$and` ile birleşir → aynı alana birden çok koşul (gelişmiş arama) ezilmez; kullanıcı `stateId` filtresi board akış kapsamıyla **kesişir** (kapsam dışına çıkamaz).
- **Güvenlik:** yalnızca `listColumns[].filterable=true` / `sortable=true` alanlar kabul edilir; `workspaceId`/`boardId` sabit kapsam (kullanıcı ezemez).

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
