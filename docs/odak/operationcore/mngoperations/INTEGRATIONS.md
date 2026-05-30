# MngOperations — Dış entegrasyonlar

**Son güncelleme:** 29 Mayıs 2026

---

## 1. MngKeeper / Keycloak

| Konu | Davranış |
|------|----------|
| Token üretimi | **MngKeeper** `POST /api/auth/token` (UI genelde gateway `/keeper/api/auth/token`) |
| MO doğrulama | `Jwt:Authority` → Keycloak realm (ör. `…/realms/odak`); `Actors.MngKeeper` env’de |
| MO → DG | Doğrulama sonrası **aynı Bearer** forward |
| Tenant / gruplar | JWT: `domain_id`, `domain_name`, `user_groups`, `preferred_username` |
| Person id | `mng_person_id` claim → assignee ilişkisi (ileride) |
| Servis hesabı (MO içi) | Faz 1 **yok** — MO pipeline kullanıcı token’ı forward eder |
| **MngScheduler → MO** | **İstisna:** Scheduler Keeper’dan teknik kullanıcı token’ı alır → MO `from-origin` ([SCHEDULED_WORK_ITEMS §4.1](./SCHEDULED_WORK_ITEMS.md)) |

Detay: [AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md).

### 1.1 Person (kullanıcı) çözümleme — board/liste

Board listesi/kanban kartlarındaki person alanları (`assignee`, `watchers` + `fieldType ∈ persons/person` olan pool alanları), kataloglar gibi **MO tarafında** id → görünen ad olarak çözülür. UI client-side person lookup yapmaz.

| Konu | Davranış |
|------|----------|
| Tetik | Kart sorgusu (`ExecuteQuery`) anında — id'ler veri-bağımlı olduğundan context-build'de değil, sorgu yanıtında |
| Kaynak | `IKeeperDirectoryClient` → **MngKeeper** `GET api/User/{id}` (caller Bearer forward) |
| Cache | MO in-memory (`IPersonDirectory`), domain-scoped anahtar `oc:{domainId}:person:{id}`, TTL = `MetadataCache.PersonTtlSeconds` (vars. 300sn); negatif sonuç da cache'lenir |
| Çıktı | `QueryExecuteResponse.People` = `{ id → { id, name, title?, isActive? } }` (UI store'da `boardPeople` map'inde birleştirilir) |
| Ad | `FirstName LastName` → fallback `username` → `email` → `id` |
| Kapsam dışı | Person **grup** alanları (`personGroups`/`group`) — grup adı çözümü ileriki faz |

> **İleriki faz — Keeper Redis + toplu endpoint.** Keeper bugün kullanıcı **profilini Redis'te cache'lemiyor**: `GetUsers` doğrudan Mongo'dan okur; Redis yalnızca session, lisans/user-count ve domain-bootstrap için kullanılır (`domain:{d}:users:{id}` yalnızca kuruluş anındaki admin için yazılır, CRUD'da güncellenmez). Bu yüzden MO'nun Keeper'ın iç Redis keyspace'inden doğrudan okuması **uygulanabilir/önerilir değil** (eksik/bayat veri + sıkı bağ). Doğru çözüm Keeper tarafında: (1) Redis destekli kullanıcı profili cache'i (CRUD'da tutarlı invalidation), (2) `POST api/User/by-ids` toplu endpoint'i. MO bu toplu endpoint'i tek istekte çağırır (şu an id başına çağrılıyor); MO'nun kısa-TTL in-memory cache'i ikinci savunma hattı olarak kalır. API sözleşmesi korunur, Redis hızı kazanılır.

---

## 2. MngNotifiers (e-posta)

[phase1 §16](../operationcore_phase1.md):

MO sorumluluğu:

- Alıcı listesi resolve (`assignee`, `watchers`, `groups`, explicit)
- `templateKey` seçimi (`op_notification_policies` veya rule action)
- `POST` MngNotifiers mail API (Scheduler benzeri client)

MO **SMTP yapmaz**.

**Uygulama (Faz 1):** `IMngNotifiersClient` → `POST /api/v1/notifications/mail`; `INotificationOrchestrator` → `op_notification_policies` + `op_notifications` (in-app) + rule side-effect’ler.

---

## 3. RabbitMQ (Faz 1 publish — Q11 kararlandı)

Platform ile hizalı ([MngKeeper EventPublisher](../../../../MngKeeper/Infrastructure/MngKeeper.Infrastructure/Services/EventPublisher.cs): exchange `mngkeeper.events`, routing **`{domainId}.{eventType}`**).

**Karar (26 May 2026):** Exchange **`oc.events`**; routing **`{domainId}.oc.workitem.*`**; payload’da `domainId` + `domainName` zorunlu. DG `publish_mode` ile karıştırılmaz → [NOTIFICATIONS_AND_EVENTS.md](./NOTIFICATIONS_AND_EVENTS.md).

### Karar (multi-tenant disiplin)

| Konu | Uygun hareket |
|------|----------------|
| **Exchange** | Ayrı topic: **`oc.events`** (modül sınırı — Scheduler `mng_scheduler_events` gibi) |
| **Routing key** | **`{domainId}.oc.workitem.created`** — tenant önek **routing’de zorunlu** |
| **Payload** | `domainId` + `domainName` **zorunlu** (çift kontrol) |
| **Hub exchange’i paylaşmak** | Publish için **önerilmez**; Hub Faz 2’de `oc.events`’ten **`{domainId}.oc.#`** bind ile **tüketir** |

Yalnızca `oc.workitem.created` (domainId’siz routing) → tüm tenant mesajları aynı kuyruğa düşer; consumer’da filtre gerekir → **platform disiplinine aykırı**.

### Routing key örnekleri

| Olay | Routing key |
|------|-------------|
| Create | `{domainId}.oc.workitem.created` |
| Update | `{domainId}.oc.workitem.updated` |
| Transition | `{domainId}.oc.workitem.transitioned` |

`domainId` = JWT `domain_id` (Keeper Mongo id).

### Payload (zorunlu tenant alanları)

```json
{
  "eventId": "uuid",
  "occurredAt": "2026-05-26T12:00:00Z",
  "domainId": "6a0f8fc4-…",
  "domainName": "odak",
  "workspaceId": "…",
  "workItemId": "…",
  "workItemKey": "TSK-00001",
  "transitionKey": null,
  "actor": "odak_admin"
}
```

**Consumer (Faz 2):** Kuyruk binding örn. `oc.events` + `{domainId}.oc.#` — yalnızca ilgili domain mesajları.

Publish hata → log; ana işlem rollback edilmez (Q5).

---

## 4. MngHub / SignalR (Faz 2)

Faz 1: UI polling veya mevcut hub pattern OC’ye özel kanal sonra.

MO event publish → Hub bridge değerlendirilir.

---

## 5. Monitoring / Security (Faz 1 sınırı)

| Entegrasyon | Faz |
|-------------|-----|
| `POST /work-items/from-origin` | **Faz 1** |
| Alarm otomatik subscribe | Faz 2 |
| SIEM ticket sync | Faz 2+ |

---

## 6. MngScheduler / MngWorkflow

| Konu | Durum | Belge |
|------|--------|--------|
| **Zamanlanmış work item** (cron → `from-origin`) | SW-0/4 ✅ · SW-2/3 planlandı | [SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md) |
| **Scheduler → MO kimlik** | **Kararlandı** | Keeper token → Bearer → MO ([SCHEDULED_WORK_ITEMS §4.1](./SCHEDULED_WORK_ITEMS.md)) |
| SLA kontrolü, escalation job | Faz 2+ | scheduler HTTP → MO veya DG query |

### 6.1 Zamanlanmış WI — token akışı (özet)

MngScheduler’ın MO’da WI oluşturabilmesi için **MngKeeper** oturum token’ı gerekir (UI ile aynı hat):

```text
Scheduler (tetik)
  → POST /keeper/api/auth/token  (domainName + username + password — teknik kullanıcı, secret config)
  → access_token
  → POST /operations/api/v1/work-items/from-origin  (Authorization: Bearer …)
```

- MO mevcut JWT doğrulamasını kullanır; Faz 1’de ayrı «servis API key» yok.
- Kimlik bilgileri **Scheduler konfigünde**; `op_work_item_schedules` veya job `payload` içinde **tutulmaz**.
- Her tetikte **yeni token** (JWT süresi); job header’ına sabit uzun ömürlü token gömülmez.

Faz 1: SLA due alanları transition/create’te hesaplanır; SLA cron job yok.

---

## 7. Health check (Q3 — kararlandı)

`GET /api/v1/health` — `[AllowAnonymous]` (gateway probe).

| Bileşen | Zorunluluk | Down olduğunda |
|---------|------------|----------------|
| **MO process** | Zorunlu | **Unhealthy** (503) |
| **MngDataGateway** | Operasyonel bağımlılık | **Degraded** — MO ayakta; health gövdesinde `dataGateway: degraded` |
| **RabbitMQ** | Faz 1 ince | **Degraded** (publish yapılamaz; komutlar sync çalışabilir) |
| **MngNotifiers** | Opsiyonel | **Degraded** (e-posta gecikir; in-app notification DG üzerinden) |

**Karar (26 May 2026):** DG (veya RabbitMQ) erişilemez → genel durum **`Degraded`**, **`Unhealthy` değil**. Orchestrator/container MO’yu gereksiz yeniden başlatmaz; operasyonel endpoint’ler runtime’da DG hatası döner (502/503).

**Örnek gövde:**

```json
{
  "status": "Degraded",
  "components": {
    "self": "Healthy",
    "dataGateway": { "status": "Degraded", "message": "Unreachable" },
    "rabbitMq": { "status": "Healthy" },
    "mngNotifiers": { "status": "Healthy" }
  }
}
```

HTTP: `200` + `Degraded` (ASP.NET HealthChecks pattern) veya `503` yalnızca `Unhealthy` için — implementasyonda `200` + status alanı tercih edilebilir (Scheduler ile hizalanırken netleştirilir).
