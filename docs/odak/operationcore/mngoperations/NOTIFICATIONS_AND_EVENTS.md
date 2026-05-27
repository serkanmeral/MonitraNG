# MngOperations — Bildirimler vs DG `publish_mode`

**Son güncelleme:** 26 Mayıs 2026

---

## 1. İki farklı kavram (karıştırılmamalı)

| | DG `publish_mode` | OC bildirim mimarisi (MO) |
|---|-------------------|---------------------------|
| **Ne** | Dataset kaydı CRUD oldu → RabbitMQ **veri katmanı** olayı | Kullanıcıya **in-app** + **e-posta** + operasyonel anlam |
| **Kim dinler** | Hub, monitoring sync, entegrasyon (genel) | OC UI badge, kullanıcı inbox |
| **Exchange / key** | `mngdatagateway.events` veya `monitra.data.events.{domain}`; routing `dataset.op_work_items.created` | **`oc.events`**; `{domainId}.oc.workitem.transitioned` |
| **Payload** | Ham kayıt, dataset adı, dataId | workItemId, transitionKey, assignee, workspace… |
| **Dataset** | `@datasets.publish_mode` | `op_notifications`, `op_notification_policies`, `op_rules`, `op_activities` |

DG `publish_mode` **kullanıcı bildirimi değildir** — MngNotifier + `op_notifications` ayrı kanaldır ([phase1 §15–16](../operationcore_phase1.md)).

---

## 2. DG `publish_mode` değerleri

| Değer | DG davranışı |
|-------|----------------|
| `none` | CRUD sonrası RabbitMQ **yok** |
| `basic` / `full` | `DataCreated` / `DataUpdated` / … publish ([DataService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/DataService.cs)) |

`op_*` taslak export’ta çoğu `basic` — kurulumda bilinçli seçim yapılacak.

---

## 3. OC kararı (**kararlandı — 26 Mayıs 2026**)

**Evet:** Operasyonel **bildirim ve anlamlı domain olayları** MO’da; DG `publish_mode`’a **güvenmiyoruz** iş kuralı için.

### 3.1 MO sorumluluğu (Faz 1)

```text
Komut pipeline başarılı
 → op_activities (audit)
 → op_notifications (in-app, kullanıcı bazlı)
 → op_notification_policies + op_rules → MngNotifiers (email)
 → RabbitMQ oc.events ({domainId}.oc.workitem.*)
```

### 3.2 `op_*` için `publish_mode` önerisi: **`none`**

| Gerekçe |
|--------|
| Operasyonel yazma MO → DG iç HTTP; DG her PUT’ta `dataset.op_work_items.updated` yayınlardı → **gürültü** ve **anlamsız** event (transitionKey yok). |
| Tek kaynak gerçek: **`oc.events`** (Q11). |
| İstisna: Metadata doğrudan DG admin UI ile düzenlenirse ve entegrasyon ham veri değişikliği istiyorsa ilgili dataset `basic` bırakılabilir — Faz 1’de tüm `op_*` **none** yeterli. |

### 3.3 Çift publish istenirse (ileride)

Bazı tüketiciler ham DG event isteyebilir → seçili dataset `basic` + MO `oc.events`; consumer’lar **farklı** routing dinler; duplicate iş kuralı yok.

---

## 4. MO → DG yazımında `skipEventPublish`?

DG DataController’da `skipEventPublish` parametresi var (monitoring sync). MO `IMngDataGatewayClient` Faz 2’de header/query ile DG publish’i kapatabilir — Faz 1’de **`publish_mode: none`** daha basit.

---

## 5. Özet cümle

**Soru:** `publish_mode` kullanmadan kendi notification mimarimiz mi?  
**Cevap:** Kullanıcı bildirimi (**op_notifications**, policies, Notifier) **tamamen MO**; DG `publish_mode` yalnızca **ham CRUD entegrasyon event’i** — OC operasyonel yol için **`none` + `oc.events`** kombinasyonu multi-tenant ve anlamlı event için doğru ayrım.

İlgili: [INTEGRATIONS.md §3](./INTEGRATIONS.md), [PIPELINES.md](./PIPELINES.md), [phase1 §28](../operationcore_phase1.md).
