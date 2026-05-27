# MngOperations Faz 1 Mimari ve Runtime Modeli

## 1. Genel Bakış

MngOperations, klasik bir task manager uygulaması değil;
MonitraNG platformunun operasyonel karar ve iş yönetim motorudur.

Bu yapı:

* Monitoring
* Security
* Maintenance
* Workflow
* Approval
* AI
* Automation
* Reporting

modüllerinin ortak operasyon katmanı olarak çalışacaktır.

MngOperations temel olarak:

```text
Runtime configurable operational intelligence platform
```

mantığıyla tasarlanmaktadır.

Sistem:

```text
hardcoded business logic
```

yaklaşımından uzak durarak:

```text
metadata + runtime rules + runtime context
```

yaklaşımını kullanacaktır.

---

# 2. Temel Mimari Yaklaşım

## 2.1 Backend decides, UI renders

UI tarafında business logic çalıştırılmayacaktır.

UI:

* state kararları
* permission kararları
* readonly kararları
* visibility kararları
* transition kararları
* notification kararları

vermeyecektir.

Tüm runtime kararları backend tarafından üretilecektir.

UI sadece:

```text
RuntimeContext
```

render edecektir.

---

# 3. RuntimeContext Yaklaşımı

MngOperations içinde ekran bazlı runtime context üretilecektir.

## 3.1 RuntimeContextBase

Tüm context tiplerinin ortak temel modeli.

## 3.2 Context Tipleri

```text
RuntimeContextBase
 ├─ FormRuntimeContext
 ├─ ProfileRuntimeContext
 ├─ BoardRuntimeContext
 └─ DashboardRuntimeContext
```

---

# 4. Workspace Modeli

## 4.1 Workspace Tanımı

Workspace operasyonel domain’i temsil eder.

Örnek:

```text
SOC
NOC
Maintenance
Operations
Approvals
```

## 4.2 Workspace Prefix

WorkItem kimlikleri workspace seviyesinde üretilecektir.

Örnek:

```text
TSK-00001
SOC-00022
NOC-00412
```

Önerilen alanlar:

```json
{
  "keyPrefix": "TSK",
  "keyFormat": "{PREFIX}-{SEQ:D5}",
  "sequenceStart": 1
}
```

## 4.3 Workspace Tree

Model tree destekleyecek şekilde hazırlanacaktır.

Örnek:

```text
Operations
 ├─ SOC
 ├─ NOC
 └─ Maintenance
```

Ancak Faz 1’de workspace yapısı tek seviyeli kullanılacaktır.

Önerilen alanlar:

```json
{
  "parentWorkspaceId": null,
  "path": "/operations/soc",
  "level": 1
}
```

---

# 5. Board Modeli

## 5.1 Board Tanımı

Board:

```text
operational view context
```

olarak ele alınacaktır.

Board sadece Kanban değildir.

## 5.2 Faz 1 Board Tipleri

```text
list
kanban
list+kanban
```

Örnek:

```json
{
  "supportedViewTypes": ["list", "kanban"],
  "defaultViewType": "kanban"
}
```

## 5.3 Board RuntimeContext

BoardRuntimeContext:

* columns
* visibleFields
* filters
* actions
* supportedViewTypes

bilgilerini taşıyacaktır.

---

# 6. Form ve Profile Modeli

## 6.1 Form

Form:

```text
WorkItem create/edit input layout
```

olarak kullanılacaktır.

Özellikler:

* Workspace bazlı
* Bir workspace içinde birden fazla form olabilir
* Dinamik layout desteği
* Modal içinde açılabilir
* Modal size bilgisi vardır
* Timeline içermez

## 6.2 Profile

Profile:

```text
WorkItem detail / operational cockpit
```

olarak kullanılacaktır.

Özellikler:

* Tam sayfa açılır
* Breadcrumb header içerir
* Sağ sidebar içerir
* Timeline/history içerir
* Transition action’ları içerir
* Comments paneli içerir
* Attachments paneli içerir
* Activity/timeline paneli içerir

---

# 7. Dynamic Layout Modeli

Form ve profile ekranları dinamik layout mantığında oluşturulacaktır.

## 7.1 Layout Özellikleri

Desteklenecek:

* satır
* sütun
* colSpan
* rowSpan
* field grouping
* section grouping

Örnek:

```text
Genel Bilgiler
Teknik Bilgiler
Çözüm Bilgileri
```

---

# 8. System Fields ve Dynamic Fields

## 8.1 System Fields

Her zaman sistemde bulunan alanlardır.

Örnek:

```text
key
title
description
state
type
priority
assignee
```

## 8.2 System Panels

```text
comments
attachments
activity
timeline
```

## 8.3 Dynamic Fields

Workspace field pool’dan seçilen alanlardır.

Örnek:

```text
rootCause
location
maintenanceType
resolution
```

## 8.4 Work Item tip kategorileri

Her `op_work_item_types` kaydı **zorunlu** `category` alanı taşır (DG şema; `idx_category`).

Amaç:

* Tipleri UI’da **mantıksal gruplara** ayırmak (help desk vs operasyon)
* Raporlama, SLA eşlemesi ve workspace `enabledTypeIds` seçimini sadeleştirmek
* Hardcoded `if (type == …)` yerine metadata kullanmak

**Faz 1 önerilen enum** (serbest metin değil — UI select):

| `category` | Kullanım |
|------------|----------|
| `incident` | Olay / kesinti |
| `service_request` | Hizmet talebi |
| `problem` | Problem / kök neden |
| `change` | Değişiklik |
| `task` | Genel görev |
| `operational` | SOC/NOC/bakım operasyonu |

**Ayrım:** `op_states.category` (`open`, `in_progress`, `closed`…) state **yaşam döngüsü** semantiğidir; tip kategorisi değildir.

Referans konfigürasyon: [reference/IT_HELP_DESK_REFERENCE.md](./reference/IT_HELP_DESK_REFERENCE.md)

## 8.5 Pool alan değerleri (`extraFields`)

**Eşdeğerlik:** Konuşmalarda geçen `custom_data` = `op_work_items.extraFields` (DG şema alan adı).

### Üç katman

```text
TANIM       op_fields          key, label, fieldType, scope=pool, workspaceId?, options, …
AKTİVASYON  op_workspaces      enabledFieldIds — bu workspace hangi tanımları kullanır?
DEĞER       op_work_items      extraFields: { "storyPoints": 5, "resolutionSummary": "…" }
```

| Katman | Core (sistem) | Pool (dinamik) |
|--------|---------------|----------------|
| Tanım | `op_work_items` şeması; `op_fields` kaydı **yok** | `op_fields` (`scope: pool`) |
| UI — sistem tanımlaması | Salt okunur katalog | CRUD (global; `workspaceId` boş) |
| UI — workspace tanımlaması | — | Workspace-scoped tanım + `enabledFieldIds` (Faz 2) |
| Değer yeri | Doküman **üst seviye** kolon | Yalnızca **`extraFields[key]`** |

**Altın kural:** Pool alan değerleri üst seviyeye yazılmaz; MO create/patch `fields` gövdesini core vs pool diye ayırır.

### API sözleşmesi (MngOperations — hedef)

| Yön | Davranış |
|-----|----------|
| İstek | `{ "fields": { "storyPoints": 5 } }` |
| Persist | Core key → üst seviye; pool key → `extraFields` merge |
| RuntimeContext | UI için düzleştirilmiş alan map’i |
| Kurallar | `fields.<key>` (MO normalize eder) |

### Doğrulama (create/update)

1. Key `op_fields` tanımında var mı?
2. Tanım workspace için geçerli mi? (`workspaceId` boş veya eşleşen)
3. `enabledFieldIds` içinde mi?
4. Tip / options uyumu
5. Sonra `extraFields[key] = value`

Bilinmeyen veya devre dışı pool key → **400** (`UNKNOWN_FIELD` / `FIELD_NOT_ENABLED`).

### Örnek

* **IT Destek:** pool → `requestCategory`, `resolutionSummary` (global tanım + enabled)
* **Agile workspace:** pool → `storyPoints` (`op_fields.workspaceId` dolu, yalnızca o domain)

---

# 9. Permission Modeli

## 9.1 Genel Yaklaşım

Permission modeli:

```text
group-first
```

yaklaşımıyla ilerleyecektir.

Kullanıcı bazlı istisnalar desteklenebilir.

## 9.2 Access Token Kullanımı

MngOperations request’lerinde access token bulunacaktır.

Kullanıcı context’i token parse edilerek elde edilecektir.

Örnek token:

```json
{
  "preferred_username": "serkan.meral",
  "groups": ["maintenance-team"],
  "roles": ["manager"],
  "domain": "tenant1"
}
```

MngOperations stateless authorization yaklaşımı kullanacaktır.

---

# 10. Field-Level Runtime Permission

Field bazlı runtime permission desteklenecektir.

## 10.1 Faz 1 Behavior Seti

```text
visible
readonly
required
masked
```

Örnek:

```json
{
  "resolution": {
    "visible": true,
    "readonly": false,
    "required": true
  }
}
```

---

# 11. RuntimeBehaviorResolver

Field behavior merge işlemi backend tarafında yapılacaktır.

UI merge işlemi yapmayacaktır.

## 11.1 Merge Kaynakları

```text
Field Definition
Form
Profile
Workspace
Board
State
Permission
Rule
Automation
```

## 11.2 Merge Stratejisi

```text
Most Restrictive Wins
```

## 11.3 Öncelik Sırası

```text
Field Definition
 → Form/Profile
 → Workspace
 → Board
 → State
 → Permission
 → Rule
```

---

# 12. Rule Engine Yaklaşımı

MngOperations:

```text
Runtime Decision Engine
```

olarak çalışacaktır.

## 12.1 Rule Türleri

### Default Rules

Veriyi zenginleştirir.
İşlemi reddetmez.

### Validation Rules

İşlemi kabul eder veya reddeder.

---

# 13. Rule Pipeline

## 13.1 Create Pipeline

```text
Permission
 → Runtime Resolve
 → Default Rules
 → Validation Rules
 → Persist
 → Activity/Event
```

## 13.2 Transition Pipeline

```text
Permission
 → Transition Resolve
 → Pre-Validation
 → Apply Transition
 → Default Rules
 → Post-Validation
 → Persist
 → Timeline/Activity/Event
```

---

# 14. Transition Modeli

Transition:

```text
state değişimi değil,
operasyonel aksiyon
```

olarak modellenir.

## 14.1 Transition Özellikleri

Her transition:

* key
* name
* permissions
* requiredFields
* validationRules
* defaultRules
* actions
* ui metadata

bilgilerine sahip olabilir.

---

# 15. Notification Modeli

## 15.1 Activity ve Notification Ayrımı

### Activity

Operational audit log.

### Notification

Kullanıcıya özel okunma/okunmamış bildirim.

## 15.2 op_notifications

Notification kayıtları:

```text
user bazlı
isRead destekli
```

şekilde tutulacaktır.

Header notification badge unread count üzerinden beslenecektir.

---

# 16. Mail Notification Modeli

## 16.1 MngNotifiers Kullanımı

Mail gönderme altyapısı:

```text
MngNotifiers
```

servisi üzerinden yapılacaktır.

MngOperations:

* recipients resolve eder
* templateKey belirler
* mailObject oluşturur

MngNotifiers:

* SMTP
* template render
* mail body generation
* mail sending

işlerini yönetir.

## 16.2 Notification Policy

Yeni dataset:

```text
op_notification_policies
```

Örnek:

```json
{
  "eventType": "WorkItemCreated",
  "typeId": "maintenance",
  "channels": ["inApp", "email"],
  "recipients": ["assignee", "watchers"],
  "emailTemplateKey": "maintenance-created"
}
```

---

# 17. Query / Saved Filter Modeli

## 17.1 Query Definition

Teknik aggregate/query metadata’sıdır.

DG içinde tutulacaktır.

## 17.2 Saved Filter

Query definition’a referans verir.

## 17.3 Runtime Parameter Resolver

MngOperations aşağıdaki parametreleri resolve edebilir:

```text
{{currentUser}}
{{currentWorkspace}}
{{currentBoard}}
{{today}}
{{startOfWeek}}
```

---

# 18. Dashboard Runtime Modeli

Dashboard:

```text
Runtime Landing Page
```

olarak tasarlanacaktır.

## 18.1 Widget Tipleri

Faz 1:

```text
summaryCard
list
chart
activityFeed
savedFilterShortcut
```

## 18.2 Widget Query Yapısı

Widget aggregate taşımaz.

Query reference kullanır.

---

# 19. Report Runtime Modeli

## 19.1 Report Yaklaşımı

```text
Report = Query + Template + Export
```

## 19.2 Faz 1

* Manuel rapor
* Parametreli rapor
* DG predefined query kullanımı

## 19.3 Faz 2

Bağımsız:

```text
Reporting Service
```

geliştirilecektir.

Bu servis:

* Operations
* Monitoring
* Security

modülleri için ortak çalışacaktır.

---

# 20. SLA Modeli

Yeni dataset:

```text
op_sla_policies
```

## 20.1 Faz 1 SLA Özellikleri

* response target
* resolve target
* due date calculation
* realtime breach query

## 20.2 Faz 1’de Olmayacaklar

```text
working hours
holiday calendar
pause/resume
escalation engine
```

---

# 21. Comments / Activities / Timeline

## 21.1 Comments

İnsan iletişimi.

## 21.2 Activities

Operational audit log.

Structured tutulacaktır.

## 21.3 Timeline

```text
comments + activities
```

birleşik runtime görünümüdür.

---

# 22. Attachment Modeli

## 22.1 Faz 1 Yaklaşımı

Attachments:

```text
op_work_items içinde tutulacak
```

Ayrı dataset olmayacaktır.

DG native file field kullanılacaktır.

## 22.2 Storage

Physical storage:

```text
DG + MinIO/WebDAV
```

üzerinden yönetilecektir.

---

# 23. WorkItem Relation Modeli

## 23.1 Parent/Child

```json
{
  "parentItemId": "..."
}
```

## 23.2 Generic Links

Yeni dataset:

```text
op_links
```

Faz 1 linkType:

```text
relates_to
blocks
duplicates
```

---

# 24. Origin Modeli

WorkItem origin bilgisi taşıyacaktır.

Örnek:

```json
{
  "origin": {
    "sourceType": "monitoring",
    "sourceSystem": "MonitraNG Monitoring",
    "sourceId": "alarm-id"
  }
}
```

## 24.1 Faz 1 sourceType

```text
manual
monitoring
security
workflow
scheduler
integration
ai
```

---

# 25. Assignment Modeli

## 25.1 Faz 1

```text
single assignee
multiple assignmentGroups
```

## 25.2 Alanlar

```text
assignee
assignmentGroups
watchers
reporter
createdBy
```

---

# 26. Reporter / CreatedBy Ayrımı

## CreatedBy

Audit bilgisidir.

## Reporter

Operasyonel iletişim bilgisidir.

Aynı kişi olmak zorunda değildir.

---

# 27. Watchers Modeli

Watchers:

```text
WorkItem followers
```

olarak ele alınacaktır.

Permission sağlamaz.

Notification recipient olarak kullanılabilir.

---

# 28. Automation Modeli

## 28.1 Faz 1

```text
Event → Rule → Action
```

## 28.2 Faz 1 Action Tipleri

```text
setField
setAssignee
setAssignmentGroups
addWatcher
createNotification
sendEmailViaMngNotifiers
createActivity
```

## 28.3 Faz 2+

Workflow/Job/Scheduler entegrasyonu.

## 28.4 Faz 3+

AI-assisted automation.

---

# 29. AI Ready Architecture

## 29.1 Faz 1

AI Ready Foundation.

Toplanacak:

* state timeline
* activities
* comments
* assignment history
* relations
* SLA history
* runtime context

## 29.2 Faz 2

AI Assisted Operations.

Örnek:

* smart assignment
* AI summary
* similar issue detection
* SLA risk prediction

## 29.3 Faz 3

AI Driven Automation.

---

# 30. Genel Mimari Prensipler

## 30.1 Hardcoded Business Logic Yasak

Şu yaklaşım kullanılmayacaktır:

```text
if(type == ...)
```

Yerine:

```text
metadata + runtime rules
```

kullanılacaktır.

## 30.2 UI Business Logic İçermeyecek

UI sadece:

```text
RuntimeContext render
```

edecektir.

## 30.3 MngOperations

MngOperations:

```text
Runtime operational orchestration engine
```

olarak konumlandırılacaktır.

---

# 31. Fazlandırma Özeti

## Faz 1

* Runtime operational core
* Dynamic forms/profiles
* Runtime rules
* Runtime permissions
* Query model
* Dashboard
* Basic reporting
* Notifications
* SLA foundation
* AI-ready data model

## Faz 2

* Reporting Service
* Workflow/Scheduler integration
* Advanced SLA
* Realtime websocket infrastructure
* User dashboard personalization
* Reporting subscriptions

## Faz 3

* AI assisted operations
* AI-driven automation
* Operational intelligence graph
* Predictive analytics
* Semi-autonomous operational flows
