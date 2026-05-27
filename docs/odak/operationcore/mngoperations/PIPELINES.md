# MngOperations — Komut pipeline’ları

**Son güncelleme:** 26 Mayıs 2026

---

## 1. Ortak adımlar

Tüm mutasyon pipeline’ları:

```text
1. IRequestContext + workspace scope yükle
2. Permission (workspace / transition / field)
3. Metadata resolve (form, flow, rules cache)
4. Pre-validation rules
5. Domain mutation (bellek içi model)
6. Default rules (zenginleştirme)
7. Post-validation rules
8. DG persist (sıralı)
9. Side effects: activity, timeline segment, notifications
10. Inline automation (op_rules trigger)
11. RabbitMQ publish
12. Response + context slice
```

Hata herhangi bir adımda → **kısa devre**; 8 sonrası kısmi yazma riski loglanır (Faz 1).

---

## 2. Create pipeline

```text
POST /work-items
```

| Adım | Detay |
|------|--------|
| Permission | Workspace `create` + type enabled |
| Resolve | `op_work_item_types`, workspace `enabledFieldIds`, initial state (default rule veya type default) |
| Key | `IWorkItemKeyGenerator` |
| Fields | Dynamic field validation + behaviors |
| Persist | `POST op_work_items` |
| Activity | `WorkItemCreated` → `op_activities` |
| Automation | `trigger=WorkItemCreated` scoped rules |
| Policies | `op_notification_policies` |
| Event | `oc.workitem.created` |

**Initial state:** Type/workspace default stateId; explicit `initialTransitionKey` yalnızca `from-origin` veya internal API’de.

---

## 3. PATCH pipeline

```text
PATCH /work-items/{id}
```

| Adım | Detay |
|------|--------|
| Yasak | `stateId`, `key` değişimi |
| Permission | Field-level + workspace update |
| Rules | `WorkItemUpdated` validation + default |
| Persist | DG update |
| Activity | Değişen alanlar özeti |
| Automation + publish | `oc.workitem.updated` |

---

## 4. Transition pipeline

```text
POST /work-items/{id}/transitions/{transitionKey}
```

[OPERATION_CORE §5.2.1](../OPERATION_CORE_IMPLEMENTATION_PLAN.md):

```text
transitionKey
 → katalog: key + workItem.stateId == fromStateId
 → permission merge (transition.permissions + workspace groups)
 → requiredFields (transition + field behaviors)
 → op_rules (WorkItemTransition, transitionKey ve/veya from/to kenar)
 → pre-validation
 → toStateId uygula, lastStateChangeAt, closedAt (terminal state)
 → default rules (post-state)
 → post-validation
 → persist op_work_items
 → op_activities (transitionKey, from, to, actor)
 → op_work_item_timelines: önceki segment leftAt; yeni segment enteredAt
 → SLA recalc (policy relation)
 → notifications + inline automation
 → publish oc.workitem.transitioned
 → ProfileRuntimeContext slice (availableTransitions)
```

**Yorum:** İstek gövdesinde `comment` varsa `op_comments` + activity aynı transaction sırasında.

---

## 5. Comment pipeline

```text
POST /work-items/{id}/comments
```

Permission → persist `op_comments` → activity `CommentAdded` → isteğe bağlı mention → Notifier (Faz 1 basit).

---

## 6. From-origin pipeline

`POST /work-items/from-origin` = Create + zorunlu `origin` + opsiyonel `initialTransitionKey`.

Monitoring alarm tek kayıt açma senaryosu Faz 1 hedefi ([§5.2.6](../OPERATION_CORE_IMPLEMENTATION_PLAN.md)).

### 6.1 Idempotency (Q6 — Faz 1)

Dış modül retry → çift WorkItem önlenir.

| Adım | Davranış |
|------|----------|
| 1 | `origin.correlationId` **zorunlu** (veya eşdeğer: `sourceType` + `sourceId` composite) |
| 2 | DG lookup: `op_work_items` — `origin.correlationId` (+ isteğe bağlı `origin.sourceType`) |
| 3 | **Kayıt varsa** | `200` + mevcut work item; `code: "ALREADY_EXISTS"` (opsiyonel); **yeni create yok** |
| 4 | **Kayıt yoksa** | Normal create pipeline (§2) |

**Index (öneri):** `origin.correlationId` veya compound `(origin.sourceType, origin.correlationId)` — unique değil, lookup için.

**Faz 2:** Genel `Idempotency-Key` header, outbox — tüm komutlar.

**Kapsam dışı Faz 1:** Normal `POST /work-items` (UI) idempotency zorunlu değil.

---

## 7. Read: Timeline merge

`GET /runtime/work-items/{id}/timeline`:

```text
parallel veya sequential:
  GET op_comments (workItemId)
  GET op_activities (workItemId)
→ birleştir: sort by createdAt desc
→ map to TimelineEntryDto { type, actor, text, at, meta }
```

`op_work_item_timelines` **bu endpoint’e dahil değil** — ayrı `GET .../state-segments` veya profile içi `slaTimeline` (UI planı).

---

## 8. Kanban drop (mantık)

UI drop → MO:

1. `BoardRuntimeContext` kolonundan `defaultTransitionKey` / alternatifler
2. Tek geçiş → doğrudan transition endpoint
3. Çoklu → UI seçici → seçilen key ile transition endpoint

Backend ek endpoint **gerekmez** (Faz 1).

---

## 9. Çok adımlı persist ve kısmi hata (Q5)

Transition / create gibi pipeline’lar **sıralı DG yazımı** yapar; dağıtık transaction yok.

### Faz 1 (karar)

| Konu | Karar |
|------|--------|
| Otomatik rollback (compensation) | **Yok** |
| Kullanıcı bilgilendirme | HTTP **500** (veya implementasyonda **207** — netleştirilir), `code`: **`PARTIAL_FAILURE`** |
| Gövde | `completedSteps`, `failedStep`, `correlationId`; isteğe bağlı `workItem` (son tutarlı snapshot) |

**Örnek `completedSteps` (transition):**

```json
[
  "persistWorkItem",
  "persistTimelineSegment",
  "persistActivity"
]
```

`failedStep`: `"publishRabbitMq"` — önceki adımlar DG’de kalır.

**Log:** Serilog structured — `CorrelationId`, `WorkItemId`, `completedSteps`, `failedStep` (manuel müdahale için).

**Uygulama (26 Mayıs 2026):** `WorkItemCommandService` — create / transition / patch pipeline’ları `PipelineContext` + `RunPipelineSideEffectAsync`; adım sabitleri `PipelineSteps.*`. Timeline / activity / RabbitMQ pipeline modunda `throwOnFailure: true`.

**UI:** “İşlem tamamlanamadı; şu adımlar uygulandı: …” — kullanıcı gerekirse kaydı kontrol eder veya destek.

### Faz 2 (ertelenen)

- Outbox pattern veya saga — “hep tamam ya da tutarlı geri al”
- İsteğe bağlı idempotent retry tek adım için
