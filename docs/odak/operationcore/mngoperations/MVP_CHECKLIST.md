# MngOperations — Faz 1 MVP checklist (backend)

**Son güncelleme:** 26 Mayıs 2026  
**UI maddeleri hariç** — UI ayrı plan.

---

## A. Proje iskeleti

- [x] `MngOperations.sln` + Clean Architecture projeleri (Persistence yok — Faz 1)
- [x] `MngOperations.Api` — health, version, Swagger
- [x] Serilog + Seq (Odak example)
- [x] `MngOperationsSettings` + `appsettings.Development.example.json`
- [x] `IMngDataGatewayClient` + `IOcEventPublisher` + `IRequestContext`
- [x] Docker + `docker-compose` (Odak overlay)
- [x] Gateway `ocelot` `/operations/api/v1` route

---

## B. Altyapı client’ları

- [x] `IMngDataGatewayClient` (CRUD + predefined query)
- [x] JWT forward / `IRequestContext` (`HttpRequestContext`)
- [x] `IMetadataCache` (memory, TTL)
- [x] `IMngNotifiersClient` (mail send)
- [x] `IOcEventPublisher` (RabbitMQ `oc.events`)

---

## C. Domain servisleri

- [x] `IPermissionEvaluator`
- [x] `IFieldBehaviorResolver`
- [x] Transition doğrulama (`StateFlowCatalog`, `StateFlowTransitionResolver`) — ayrı `ITransitionResolver` yok
- [x] `IRuleEngine` (validation + default + automation)
- [x] `IWorkItemKeyGenerator`
- [x] `ISlaCalculator` (foundation)
- [x] `IWorkItemTimelineService` (segment aç/kapat)
- [x] `IRuntimeContextService` (form, profile, board, dashboard, timeline, queries)
- [x] `QueryParameterResolver` (token çözümleme)
- [x] `INotificationOrchestrator`
- [x] `IWorkItemCommandService`

---

## D. API — komutlar

- [x] `POST /work-items`
- [x] `PATCH /work-items/{id}` (stateId yasak)
- [x] `POST /work-items/{id}/transitions/{transitionKey}`
- [x] `POST /work-items/{id}/comments`
- [x] `POST /work-items/from-origin`

---

## E. API — runtime

- [x] `GET /runtime/boards/{boardId}`
- [x] `GET /runtime/work-items/form?mode=create` + `GET .../{id}/form`
- [x] `GET /runtime/work-items/{id}/profile`
- [x] `GET /runtime/work-items/{id}/timeline`
- [x] `GET /runtime/work-items/{id}/state-segments`
- [x] `GET /runtime/dashboards/{dashboardId}`
- [x] `POST /runtime/queries/{queryKey}/execute`

---

## F. Pipeline doğrulama (Odak domain)

- [x] Demo workspace + state flow + transitions (`seed-operation-core-demo.ps1`)
- [x] Create → key üretimi
- [x] Transition → timeline segment + activity + SLA
- [x] Validation rule reject (`resolve` + boş description)
- [ ] RabbitMQ mesajı consumer’sız log doğrulama (manuel)
- [x] `-SmokeTest` script (MO doğrudan)
- [x] Gateway üzerinden smoke
- [ ] `PARTIAL_FAILURE` senaryo testi (RabbitMQ kapalı simülasyon)

---

## G. Dokümantasyon / ops

- [x] Planlama sync (`DEVAM.md`, checklist, API_SURFACE)
- [ ] `docs/content/MngOperations/` mkdocs iskelet (isteğe bağlı)
- [ ] Postman veya `scripts/tests/MngOperations/` resmi test paketi

---

## H. Bilinçli ertelenen (Faz 1 dışı)

- Metadata admin API wrapper (`/admin/*`)
- RabbitMQ consumer / queue-origin
- Working-hours SLA + escalation engine
- Distributed transaction / outbox (Q5 Faz 2)
- File upload proxy
- AI endpoints
