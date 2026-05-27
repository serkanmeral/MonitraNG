# MngOperations — Mimari

**Son güncelleme:** 26 Mayıs 2026

---

## 1. Solution yapısı (öneri)

MngScheduler / MngNotifier ile aynı **Clean Architecture**:

```text
MngOperations/
├── Core/
│   ├── MngOperations.Domain/          # Entities, value objects, domain exceptions
│   └── MngOperations.Application/     # Use cases, interfaces, DTOs, pipeline orchestration
├── Infrastructure/
│   ├── MngOperations.Infrastructure/  # Http clients, RabbitMQ, caching
│   └── MngOperations.Persistence/     # (Faz 1: yok veya minimal — kalıcılık DG’de)
└── Presentation/
    └── MngOperations.Api/             # Controllers, middleware, Swagger
```

**Faz 1 kalıcılık:** Tüm `op_*` verisi **MngDataGateway** üzerinden; MngOperations kendi MongoDB’si **açmaz** (operasyonel state DG’de).

---

## 2. Application katmanı modülleri

| Modül / servis | Rol |
|----------------|-----|
| `IRequestContext` | JWT → userId, username, groups, domainId, isAdmin |
| `IMetadataCache` | Workspace, state flow, form, profile, rules — scoped cache (TTL) |
| `IMngDataGatewayClient` | Dataset CRUD + predefined query execute |
| `IPermissionEvaluator` | Transition / workspace / field permission merge |
| `IFieldBehaviorResolver` | visible, readonly, required, masked merge |
| `ITransitionResolver` | Katalog + mevcut state → geçiş doğrulama |
| `IRuleEngine` | Trigger + scope → default / validation / automation |
| `IWorkItemKeyGenerator` | Prefix + sequence (DG veya dedicated counter stratejisi) |
| `ISlaCalculator` | Faz 1: policy snapshot + breach flags |
| `IWorkItemTimelineService` | Segment aç/kapat (`op_work_item_timelines`) |
| `IRuntimeContextService` | Form / Profile / Board / Dashboard / timeline / queries |
| `IWorkItemCommandService` | Create, Patch, ApplyTransition, FromOrigin, Comment |
| `INotificationOrchestrator` | inApp (`op_notifications`) + policy → MngNotifiers |
| `IMngNotifiersClient` | `POST /notifications/mail` |
| `IOcEventPublisher` | RabbitMQ publish (`oc.events`) |
| `PipelinePartialFailure` | Q5 — `PARTIAL_FAILURE` + `completedSteps[]` |

---

## 3. İstek yaşam döngüsü

```text
HTTP Request
 → CorrelationId middleware
 → JWT bearer (forward to DG / Notifier)
 → IRequestContext build
 → Controller → Application command/handler
 → Pipeline (permission → rules → DG persist → side effects)
 → Response (+ optional embedded context slice)
```

**Idempotency:** Faz 1 yalnızca `from-origin` + `origin.correlationId` lookup (Q6); genel idempotency Faz 2 ([PIPELINES §6.1](./PIPELINES.md)).

---

## 4. Hata modeli (öneri)

| HTTP | Anlam |
|------|--------|
| 400 | Validation / rule rejection / geçersiz transition |
| 403 | Permission denied |
| 404 | WorkItem / board / workspace bulunamadı |
| 409 | İş kuralı çakışması (ör. unique key) |
| 500 | Genel hata; alt kod **`PARTIAL_FAILURE`** (çok adımlı persist — Q5) |
| 502 | DG / Notifier downstream hatası (wrapped) |
| 503 | MO process unhealthy (health endpoint) |

**Health (Q3):** DG erişilemez → `/health` = **Degraded** (MO ayakta); bkz. [INTEGRATIONS.md §7](./INTEGRATIONS.md).

**`PARTIAL_FAILURE` (Q5, Faz 1):** Pipeline adımlarından biri başarısız; öncekiler DG’de **kalır** (rollback yok). Gövde: `code`, `completedSteps[]`, `failedStep`, `correlationId`. Faz 2: outbox/saga — [PIPELINES.md §9](./PIPELINES.md).

Yanıt gövdesi (Q10):

```json
{
  "code": "TRANSITION_FORBIDDEN",
  "message": "Optional technical (en)",
  "messageTr": "Opsiyonel; MO sık kodlar için",
  "correlationId": "uuid",
  "details": { },
  "completedSteps": []
}
```

- **`code`** — zorunlu, stabil (entegrasyon / UI map).
- **`messageTr`** — MO Faz 1’de yaygın kodlar; eksikse UI `code` → Türkçe.
- UI, OC ekranlarında ana çeviri kaynağı (i18n map).

---

## 5. Konfigürasyon (`MngOperationsSettings`)

```json
{
  "Server": { "Port": 5086 },
  "Actors": {
    "MngKeeper": "http://mngkeeper:5001",
    "KeycloakBaseUrl": "http://keycloak:8080/keycloak"
  },
  "DataGateway": {
    "BaseUrl": "http://mngdatagateway:5010",
    "ApiVersion": "v1"
  },
  "MngNotifiers": { "BaseUrl": "http://mngnotifier:5070" },
  "RabbitMq": { "Host": "rabbitmq", "Exchange": "oc.events" },
  "MetadataCache": { "TtlSeconds": 120 }
}
```

```json
"Jwt": {
  "Authority": "http://keycloak:8080/keycloak/realms/odak",
  "RequireHttpsMetadata": false
}
```

- **UI → MO:** Gateway `http://192.168.20.20:5040/operations/api/v1`
- **MO → DG:** Doğrudan `DataGateway:BaseUrl` — ortama göre appsettings / env
- **MO → Keeper / Keycloak:** `Actors.MngKeeper`, `Jwt:Authority` — token doğrulama; Keeper token üretimi ([AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md))
- **JWT:** Doğrulama sonrası aynı Bearer → DG **forward**; `IRequestContext` claim parse

---

## 6. Proje referansları

| Servis | Örnek |
|--------|--------|
| HttpClient + Polly | [MngScheduler MngDataGatewayClient](../../../../MngScheduler/Infrastructure/MngScheduler.Infrastructure/Clients/MngDataGatewayClient.cs) |
| Gateway route | [GATEWAY_AND_DEPLOY.md](./GATEWAY_AND_DEPLOY.md) |
| Pipeline spec | [PIPELINES.md](./PIPELINES.md), [operationcore_phase1.md §13](../operationcore_phase1.md) |
