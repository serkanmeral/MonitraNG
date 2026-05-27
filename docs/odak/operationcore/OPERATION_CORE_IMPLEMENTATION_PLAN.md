# Operation Core (OC) — Uygulama Planı

**Servis:** MngOperations  
**Modül:** Operation Core (OC)  
**Faz:** 1  
**Sözlük:** Workspace · WorkItem · State  

**Durum:** §5.2 tamam; **MngOperations** backend planı [mngoperations/README.md](./mngoperations/README.md); §8 MVP checklist orada.  
**Son güncelleme:** 26 Mayıs 2026  
**İlgili spec:** [operationcore_phase1.md](./operationcore_phase1.md) · [major_plan.md](./major_plan.md)

---

## 1. Amaç ve kapsam

### 1.1 Ne inşa ediyoruz?

**Operation Core (OC)**, MonitraNG [Major Plan](./major_plan.md) vizyonuna uygun **operasyonel iş yönetim omurgasıdır**. Backend servisi **MngOperations**, metadata ve runtime kurallarla çalışan bir **operasyonel karar ve iş yönetim motorudur** — klasik bir issue tracker veya Jira klonu değildir.

Bu aşamada hedef:

- Tüm Major Plan’ı tek seferde kodlamak **değil**
- Major Plan’a oturacak, ileride monitoring / security / chat / automation / **yapay zeka** ile entegre olabilecek **work management çekirdeğini** (Faz 1) inşa etmek

Tek cümlelik konum:

> OC = Major Plan’ın operasyon katmanının work management yüzü; MngOperations = bu katmanın runtime beyni.

Detaylı mimari ve runtime modeli: [operationcore_phase1.md](./operationcore_phase1.md).

### 1.2 Major Plan ile hizalama

| Major Plan alanı | OC / MngOperations karşılığı (şimdi ve ileride) |
|------------------|--------------------------------------------------|
| **4.8 Operational Workflow & Work Management** | Workspace, WorkItem, State, transition, SLA, onay/escalation zemini |
| **4.1–4.2 Monitoring & Alarm** | WorkItem `origin` (monitoring, security); alarm → iş kaydı |
| **4.3 Dashboard & Reporting** | OC dashboard, saved filter, rapor = query + template |
| **4.7 Internal Chat** | Bildirim kanalları; ileride incident collaboration |
| **8. Yapay Zeka Stratejisi — Operations** | Faz 1: AI-ready veri; Faz 2+: özet, öneri, risk tahmini |

Yapay zeka Major Plan’da ayrı bir “ek özellik” değil; operasyonel karar desteği ve otomasyonun parçasıdır. Faz 1’de model zorunlu değildir; **zengin operasyonel iz** (timeline, activity, assignment history, origin, SLA, runtime context) toplanır ([phase1 §29](./operationcore_phase1.md)).

### 1.3 Jira klonu değil — hedef sınıf farkı

| Klasik issue tracker (Jira / TM POC) | OC hedefi |
|--------------------------------------|-----------|
| Proje + issue + status | **Workspace** (operasyon domain) + **WorkItem** + **State** + **operasyonel aksiyon (transition)** |
| Kurallar UI / eklenti ağırlıklı | **Metadata + runtime rules**; karar **MngOperations** |
| Board = görünüm | **Board = operational view context** (list / kanban + runtime kolon, filtre, aksiyon) |
| Entegrasyon sonradan | **Origin** ve modül bağlantıları veri modelinde |
| AI sonradan veya yok | **AI-ready foundation** bilinçli tasarım |

**Özgün ve sofistike** yapı: [phase1](./operationcore_phase1.md) ifadesiyle *runtime configurable operational intelligence* — görev listesi uygulaması değil, metadata ile şekillenen **runtime karar motoru** ve buna bağlı UI yüzeyi.

Örnek davranış farkı: UI doğrudan DG’de `stateId` güncellemez; “şu transition’ı uygula” isteği permission → validation → persist → activity/notification pipeline’ından geçer.

### 1.4 Üst ilkeler (planlama filtresi)

Bundan sonraki tüm kararlar (Faz 1 kapsamı, API, dataset, TM geçişi) şu filtreden geçer:

1. **Platform parçası** — Yarın monitoring alarmı WorkItem açabilmeli; bildirim ve event yapısı Major Plan ile uyumlu kalmalı.
2. **Metadata-first** — Workspace/tip başına `if (type == …)` ile büyüyen kod yerine `op_*` + rules.
3. **Backend decides, UI renders** — Permission, transition, field behavior UI’da kopyalanmaz; `RuntimeContext` ile sunulur.
4. **AI için veri ve bağlam** — Faz 1’de inference şart değil; audit ve timeline Major Plan operations AI’sına zemin hazırlar.
5. **Faz 1 disiplini** — Sofistike mimari; MVP sınırı net ([phase1 §31](./operationcore_phase1.md): working hours SLA, tam escalation, AI assignment vb. bilinçli ertelenir).

### 1.6 Planlama işbirliği ilkeleri

Bu belge ve OC planlama oturumlarında (insan + agent):

- **Tüm öneriler** [Major Plan](./major_plan.md) vizyonuna ve §1’deki üst çerçeveye uyumlu olmalıdır; Jira benzeri kısayol veya “sadece Faz 1’i hızlı bitir” gerekçesiyle mimari hedef zayıflatılmaz.
- **Talep / karar Major Plan ile çelişiyorsa** agent önce **açık uyarı** verir: çelişen nokta, risk (platform entegrasyonu, AI-ready iz, rule engine tutarlılığı vb.) ve mümkünse Major Plan’a uygun alternatif; onay sonrası belgeye işlenir.
- **Bilinçli istisna** (ör. geçici POC, Faz 1 daraltması) kullanıcı onayı ile §12 karar loguna **gerekçeli** yazılır.

### 1.5 Faz 1 kapsam özeti (taslak)

**Dahil (hedef):** Runtime operational core, dynamic form/profile, runtime rules & permissions, query model, dashboard, temel reporting, notifications, SLA foundation, `op_*` dataset’leri, MngOperations servisi, Mng.Ui OC ekranları.

**Hariç / sonraki fazlar:** Tam Reporting Service, gelişmiş SLA (working hours, pause, escalation engine), workflow/scheduler derin entegrasyon, AI-assisted operations, predictive analytics.

Detaylı madde listesi §8’de netleştirilecek.

---

## 2. Konumlandırma ve isimlendirme

| Katman | Ad |
|--------|-----|
| Ürün / modül | Operation Core (OC) |
| Backend servisi | MngOperations |
| Dataset öneki | `op_*` |
| UI route (öneri) | **`/apps/operation-core`** — [ui/OC_UI_PHASE1_PLAN.md](./ui/OC_UI_PHASE1_PLAN.md) |

### 2.1 Terminoloji (TM → OC)

| Eski (Task Manager) | Yeni (OC) |
|---------------------|-----------|
| Project | **Workspace** |
| Task / Issue | **WorkItem** |
| Status | **State** |
| `tm_*` | `op_*` |

### 2.2 Task Manager ile ilişki

- **Yeni sistem:** OC, Task Manager’ın devamı veya `tm_*` üzerine migrasyon değildir.
- **Veri:** `tm_*` dataset’leri taşınmaz; model workspace merkezlidir.
- **Backend:** TM DG’ye doğrudan yaslanır; OC **MngOperations zorunludur**.
- **UI:** `Mng.Ui/pages/apps/task-manager/` ve bileşenler **referans / adaptasyon** için kullanılabilir (Kanban, workspace ağacı, profil düzeni, form layout); route, store ve servis OC için ayrıdır.
- **Belge:** TM planlama arşivi: `docs/content/task_manager/TASK_MANAGER_PLANNING.md`.

### 2.3 Major Plan ile ilişki (özet)

OC, Major Plan **Faz 1 — Core Operational Intelligence Platform** içinde **4.8** maddesinin somut modülüdür. Uzun vadede Faz 2 (endüstriyel) ve Faz 3 (tam entegre Operational OS) ile aynı platformda monitoring, cybersecurity, workflow, communication ve **AI-assisted analysis** katmanlarına bağlanır.

Stratejik hedef (Major Plan): *Kurumların merkezi operasyon platformu* — sadece monitoring, sadece SIEM veya sadece task management ürünü olmak değil.

### 2.4 Backend rolü (MngOperations)

| Bileşen | Rol |
|---------|-----|
| **MngDataGateway** | `op_*` kalıcılık, şema, CRUD, native file field |
| **MngOperations** | Orchestration: permission merge, rule pipeline, transition gate, RuntimeContext, notification resolve, DG’ye persist koordinasyonu |
| **MngKeeper / Keycloak** | Token → kullanıcı, gruplar (stateless auth) |
| **MngNotifiers** | SMTP / template; OC alıcı + templateKey üretir |

UI iş kuralları çalıştırmaz; anlamlı aksiyonlar MngOperations API’si üzerinden yürür.

---

## 3. Mimari özet (referans)

Detay: [operationcore_phase1.md](./operationcore_phase1.md).

- Backend decides, UI renders (`RuntimeContext`)
- Metadata + runtime rules (hardcoded business logic yok)
- Group-first permission, MngNotifiers mail

_(ek notlar doldurulacak)_

---

## 4. Teknoloji ve bağımlılıklar

| Bileşen | Rol |
|---------|-----|
| MngOperations | Komut + runtime context; pipeline; inline automation ([§5.2.5](#525-api-yüzeyi-kararlandı), [§5.2.6](#526-automation-faz-1-kararlandı)) |
| MngDataGateway | `op_*` persistence; metadata config CRUD (Faz 1) |
| MngKeeper / Keycloak | Kimlik, groups, persons |
| MngNotifiers | E-posta (automation + notification policies) |
| Mng.Ui | OC ekranları |
| RabbitMQ | Faz 1: domain event **publish**; async consumer Faz 2 ([§5.2.6](#526-automation-faz-1-kararlandı)) |

---

## 5. Veri modeli (DG)

Kaynak (güncel taslak): [datasets/operationcore_datasets_phase1_draft_2026-05-26.json](./datasets/operationcore_datasets_phase1_draft_2026-05-26.json)  
Kategori: [datasets/operationcore_dataset_category.json](./datasets/operationcore_dataset_category.json) → **OperationCoreDatasets**  
Arşiv: [datasets/operationcore_datasets_phase1_current_final_2026-05-25.json](./datasets/operationcore_datasets_phase1_current_final_2026-05-25.json)

### 5.1 Dataset envanteri

| Dataset | Rol |
|---------|-----|
| `op_workspaces` | Operasyon domain; prefix, tree, groups, `enabledTypeIds` / `enabledFieldIds` |
| `op_states` | Global state kataloğu |
| `op_state_flows` | Transition kataloğu (`transitions[]` object) |
| `op_rules` | Default / validation / automation (`transitionKey` scope) |
| `op_priorities` | Öncelik kataloğu |
| `op_work_item_types` | Tip tanımları (+ isteğe bağlı `workspaceId`) |
| `op_fields` | Alan havuzu (+ isteğe bağlı `workspaceId`) |
| `op_forms` / `op_profiles` | Layout metadata |
| `op_boards` | Operational view context |
| `op_labels` | Workspace etiketleri |
| `op_work_items` | Merkez entity; 5 predefined query |
| `op_comments` / `op_activities` | Timeline merge kaynakları |
| `op_links` | İlişkiler (relates_to, blocks, duplicates) |
| `op_work_item_timelines` | State segment geçmişi |
| `op_notifications` | Kullanıcı bildirimleri |
| `op_notification_policies` | Bildirim politikaları |
| `op_sla_policies` | SLA politikaları |
| `op_dashboards` / `op_saved_filters` / `op_reports` | Görünürlük ve rapor |

Kurulum: `docs/odak/operationcore/scripts/setup-operation-core-datasets.ps1` (Odak API Gateway `http://192.168.20.20:5040`)

### 5.2 Açık tasarım soruları (sırayla netleştirilecek)

Üst çerçeve (§1–§2) onaylandıktan sonra **teker teker karar** verilecek; sonuçlar §12 karar loguna işlenecek.

| Sıra | Konu | Soru özeti | Durum |
|------|------|------------|--------|
| 1 | Transition kaynağı | Katalog vs rules vs birleşik model | **Kararlandı** → [§5.2.1](#521-transition-modeli-kararlandı) |
| 1b | Kanban sürükle-bırak | Kolon değişimi → hangi `transitionKey`? | **Kararlandı** → [§5.2.2](#522-kanban-sürükle-bırak-kararlandı) |
| 1c | `op_rules` scope | `transitionKey` vs `fromState`/`toState` kenar kuralları | **Kararlandı** → [§5.2.3](#523-op_rules-scope-kararlandı) |
| 2 | Timeline | UI akışı vs state segment geçmişi | **Kararlandı** → [§5.2.4](#524-timeline-kararlandı) |
| 3 | API yüzeyi | Command + `/runtime/*` context | **Kararlandı** → [§5.2.5](#525-api-yüzeyi-kararlandı) |
| 4 | Automation Faz 1 | Inline rules + event publish + origin API | **Kararlandı** → [§5.2.6](#526-automation-faz-1-kararlandı) |

#### 5.2.1 Transition modeli (kararlandı)

**Özet:** Geçiş **operasyonel aksiyon**; birincil tanımlayıcı **`transitionKey`**. Ham `stateId` patch yok (istisnalar §12’de ayrıca tanımlanır).

| Katman | Sorumluluk |
|--------|------------|
| **`op_state_flows.transitions[]`** | Kanonik aksiyon kataloğu: `key`, `name`, `fromStateId`, `toStateId`, `permissions`, `requiredFields`, rule referansları, `ui` metadata ([phase1 §14](./operationcore_phase1.md)). Dataset açıklamasındaki yalnızca `from→to` kenar listesi bu zengin modele evrilir. |
| **`op_rules`** | Çapraz kesen mantık ([§5.2.3](#523-op_rules-scope-kararlandı)): `trigger` = `WorkItemTransition` (ve diğerleri); scope `transitionKey` ve/veya `fromStateId`/`toStateId`. Graf tanımı **rule’da değil**, transition katalogunda. |
| **`op_profiles.actions`** | Sunum: hangi `transitionKey` buton olarak görünsün, sıra; tanımı override etmez. |

**MngOperations — Transition Resolve (Faz 1):**

```text
transitionKey isteği
 → katalogda key + mevcut stateId == fromStateId
 → permission merge (workspace + transition + group-first)
 → requiredFields / field behaviors
 → eşleşen op_rules (WorkItemTransition, key/kenar scope)
 → pre-validation → state uygula → default rules → post-validation
 → persist → activity (transitionKey, from, to, actor)
 → op_work_item_timelines segment güncelle ([§5.2.4](#524-timeline-kararlandı))
 → notifications → güncel RuntimeContext
```

**API (transition kararıyla uyumlu):**

- `POST .../work-items/{id}/transitions/{transitionKey}` — tek komut
- `GET .../work-items/{id}/profile-context` (veya eşdeğeri) — uygulanabilir aksiyonlar + UI metadata

**Faz 2+:** Aynı aksiyon birden fazla state flow’da paylaşılırsa `op_transitions` dataset’ine çıkarma değerlendirilir.

**Karar kaydı:** §12 — 26 Mayıs 2026.

#### 5.2.2 Kanban sürükle-bırak (kararlandı)

Sürükle-bırak **UX kısayolu**dır; yine **`ApplyTransition(transitionKey)`** üzerinden tam pipeline çalışır. Ham `stateId` güncellemesi yok.

| Durum | Faz 1 davranışı |
|--------|------------------|
| Mevcut state → hedef kolon (`toStateId`) için **tek** geçiş | Drop → o `transitionKey` ile otomatik apply |
| **Birden fazla** geçiş aynı `from→to` | Drop sonrası **aksiyon seçici** (modal / popover); katalog `name` + `ui` |
| Geçiş **yok** | Drop reddedilir; mesaj backend’den |

**Board / runtime:**

- `op_boards` (veya board config): isteğe bağlı `(fromStateId, toStateId)` veya kolon bazlı **`defaultTransitionKey`** (operasyonel netlik, örn. “İşlemde’ye bırak = `start_progress`”).
- **`BoardRuntimeContext`** kolon başına: `toStateId`, `dropEligible`, `defaultTransitionKey`, `alternativeTransitionKeys[]` — UI workflow tahmin etmez.

**Yapılmayan:** Çoklu geçişte sessizce “ilk” transition seçmek; sürükleyince doğrudan DG `stateId` patch.

**UI referans:** TM Kanban adaptasyonu; mantık OC store → MngOperations ([§7.3](#73-kanban-board-faz-1)).

**Karar kaydı:** §12 — 26 Mayıs 2026.

#### 5.2.3 `op_rules` scope (kararlandı)

| Kural | Karar |
|--------|--------|
| Transition **graf / aksiyon tanımı** | Yalnızca `op_state_flows.transitions[]` — rule yeni geçiş **tanımlamaz** |
| Transition tetikli kurallar | Öncelik **`transitionKey`** scope (açık, denetlenebilir) |
| Kenar kuralları | `fromStateId` + `toStateId`, `transitionKey` boş → o kenara giden **tüm** geçişlere uygulanır (ör. kapanışta `resolution` zorunlu) |
| Çakışma (aynı olay) | Key + kenar rule birlikte → **en kısıtlayan kazanır** ([phase1 §11](./operationcore_phase1.md)) |

**Karar kaydı:** §12 — 26 Mayıs 2026.

#### 5.2.4 Timeline (kararlandı)

İki ayrı kavram; isim karışıklığı önlenir:

| Kavram | İçerik | Faz 1 |
|--------|--------|--------|
| **UI Timeline** (profil akışı) | İnsan + operasyonel olaylar ([phase1 §21.3](./operationcore_phase1.md): `comments + activities`) | MngOperations **`GET .../timeline`** (veya profile-context alt kaynağı): `op_comments` + `op_activities` zamana göre **runtime merge**; ayrı dataset’e denormalize **yok** |
| **State segment geçmişi** | Hangi state’te ne kadar kalındı (SLA, AI state timeline) | **`op_work_item_timelines`** — başarılı `ApplyTransition` sonrası MngOperations **yazar/günceller** (`leftAt` önceki segment, yeni segment `enteredAt`, `transitionKey`/`transitionName`, `durationMs`) |

**`op_activities`:** Her anlamlı aksiyonda structured audit (transition dahil); UI Timeline akışında görünür.

**Yapılmayan (Faz 1):** Yorum/activity satırlarını `op_work_item_timelines`’a kopyalamak; segment geçmişini yalnızca UI merge ile türetmek (SLA sorguları zayıflar); timeline satırlarını transition dışı DG patch ile yazmak.

**İsteğe bağlı (Faz 1):** Profil akışında state geçişleri activity tipi olarak da gösterilir; SLA/detay için segment kaydına referans.

**Faz 1 zorunluluk:** `op_work_item_timelines` transition pipeline ile doldurulur (dataset mevcut export’ta tanımlı).

**Karar kaydı:** §12 — 26 Mayıs 2026.

#### 5.2.5 API yüzeyi (kararlandı)

**Model:** Mutasyonlar **komut (command)**; ekranlar **`/runtime/*` context (read)**. Operasyonel UI ana yolu DG ham CRUD değil, **MngOperations**.

**Gateway (MngWorkflow ile paralel):** `/operations/api/v1/{...}` → downstream `/api/v1/{...}`.

##### Komutlar (yazma — pipeline)

| Method | Yol | Not |
|--------|-----|-----|
| `POST` | `/work-items` | Create pipeline |
| `PATCH` | `/work-items/{id}` | Alan güncelleme (rules + permission); ham `stateId` patch yok |
| `POST` | `/work-items/{id}/transitions/{transitionKey}` | [§5.2.1](#521-transition-modeli-kararlandı) |
| `POST` | `/work-items/{id}/comments` | Yorum (aynı command pattern) |

Yanıt: güncel kayıt + ilgili **context parçası** (gereksiz round-trip azaltma).

##### Runtime context (okuma — UI render)

| Method | Yol | Context |
|--------|-----|---------|
| `GET` | `/runtime/boards/{boardId}` | `BoardRuntimeContext` |
| `GET` | `/runtime/work-items/{id}/form` | `FormRuntimeContext` (`?mode=create\|edit`) |
| `GET` | `/runtime/work-items/{id}/profile` | `ProfileRuntimeContext` |
| `GET` | `/runtime/work-items/{id}/timeline` | [§5.2.4](#524-timeline-kararlandı) merge |
| `GET` | `/runtime/dashboards/{dashboardId}` | `DashboardRuntimeContext` |

Liste/kanban: board context + saved filter / query çalıştırma (`POST /runtime/queries/{queryKey}/execute` veya eşdeğeri — parametre resolve MngOperations’ta).

##### Metadata (Faz 1)

`op_*` tanım CRUD (workspace, state flow, form, rule, …) — yönetici yapılandırma: **DG API** (mevcut platform pattern). MngOperations Faz 1’de **runtime yorum + operasyonel komutlar**; `/admin/*` wrapper sonraya bırakılabilir.

##### Bilinçli olarak yok (Faz 1 UI)

- UI → DG `PATCH @op_work_items` ile `stateId` / iş kuralı bypass
- Tek endpoint ile tüm platform context (parçalı context tercih edilir)

**Genişleme (Major Plan):** `POST /work-items/from-origin` (monitoring, security, scheduler) vb. komutlar aynı yüzeye eklenir.

**Karar kaydı:** §12 — 26 Mayıs 2026.

#### 5.2.6 Automation Faz 1 (kararlandı)

[phase1 §28](./operationcore_phase1.md) — **Event → Rule → Action**. Ayrı `op_automations` dataset yok; automation = `op_rules` (`trigger` + `actions`).

##### Katman 1 — Satır içi (Faz 1 zorunlu)

Başarılı komut pipeline sonrası (**aynı istek**), örn. `WorkItemCreated`, `WorkItemUpdated`, `WorkItemTransitioned`:

```text
→ op_rules (trigger + workspace/type/transition scope)
→ Faz 1 action tipleri: setField, setAssignee, setAssignmentGroups, addWatcher,
   createNotification, sendEmailViaMngNotifiers, createActivity
```

`op_notification_policies` aynı olayda **ayrı kanal** (inApp / email) olarak çalışabilir.

##### Katman 2 — Olay yayını (Faz 1 — ince, önerilen)

RabbitMQ **publish** (başarılı komut sonrası): örn. `oc.workitem.created`, `oc.workitem.transitioned` — payload: `workItemId`, `workspaceId`, `transitionKey`, `origin`, tenant.

- Amaç: MngHub, ileride monitoring/chat tüketicileri için sözleşme ([Major Plan §7.5](./major_plan.md)).
- Faz 1’de MngOperations **kuyruk consumer zorunlu değil**.

##### Katman 3 — Dış tetik (Faz 1 / Faz 2 ayrımı)

| Tetik | Faz |
|--------|-----|
| Monitoring / security → WorkItem | **Faz 1:** `POST /work-items/from-origin` ([§5.2.5](#525-api-yüzeyi-kararlandı)) |
| Kuyruktan otomatik iş açma / işleme | **Faz 2+** |
| MngScheduler / workflow job entegrasyonu | **Faz 2+** ([phase1 §28.3](./operationcore_phase1.md)) |
| AI-driven automation | **Faz 3+** ([phase1 §28.4](./operationcore_phase1.md)) |

##### Faz 1 dışı (bilinç)

Webhook, script, MQTT rule action; ayrı automation worker — Major Plan ile uyumlu **sonraki faz**.

**Karar kaydı:** §12 — 26 Mayıs 2026.

---

## 6. MngOperations — servis kapsamı

**Backend planlama:** [mngoperations/](./mngoperations/README.md)

| Belge | Konu |
|--------|------|
| [SERVICE_SCOPE.md](./mngoperations/SERVICE_SCOPE.md) | Sorumluluklar, sınırlar |
| [ARCHITECTURE.md](./mngoperations/ARCHITECTURE.md) | Clean Architecture, modüller |
| [DG_INTEGRATION.md](./mngoperations/DG_INTEGRATION.md) | DataGateway client |
| [API_SURFACE.md](./mngoperations/API_SURFACE.md) | REST yüzeyi |
| [PIPELINES.md](./mngoperations/PIPELINES.md) | Komut pipeline’ları |
| [RULE_ENGINE.md](./mngoperations/RULE_ENGINE.md) | `op_rules` |
| [MVP_CHECKLIST.md](./mngoperations/MVP_CHECKLIST.md) | Faz 1 backend MVP |
| [OPEN_QUESTIONS.md](./mngoperations/OPEN_QUESTIONS.md) | Netleştirilecek sorular |

### 6.1 Sorumluluklar (özet)

Runtime orchestration: transition gate, rule/permission merge, RuntimeContext, DG persist koordinasyonu, timeline segment yazımı, Notifier + RabbitMQ. Metadata CRUD Faz 1’de **DG**’de kalır. Detay: [SERVICE_SCOPE.md](./mngoperations/SERVICE_SCOPE.md).

### 6.2 API grupları

[§5.2.5](#525-api-yüzeyi-kararlandı) + [API_SURFACE.md](./mngoperations/API_SURFACE.md): **commands** + **`/runtime/*`**; gateway `/operations/api/v1`; `POST /work-items/from-origin`.

### 6.3 Runtime pipeline özeti

[PIPELINES.md](./mngoperations/PIPELINES.md):

- Create: persist → activity → [§5.2.6](#526-automation-faz-1-kararlandı) inline rules + notification policies → RabbitMQ publish
- Transition: [§5.2.1](#521-transition-modeli-kararlandı) + [§5.2.4](#524-timeline-kararlandı) — `ApplyTransition`; activity + state segment → automation + publish
- Update (`PATCH`): rules + automation + publish
- Timeline (read): [§5.2.4](#524-timeline-kararlandı) — comments + activities merge

---

## 7. UI planı (Mng.Ui)

**Detaylı plan:** [ui/OC_UI_PHASE1_PLAN.md](./ui/OC_UI_PHASE1_PLAN.md) · [ui/README.md](./ui/README.md)

Backend sözleşmesi: [RUNTIME_CONTEXT.md](./mngoperations/RUNTIME_CONTEXT.md).

### 7.1 Route yapısı

**Karar:** `/apps/operation-core` — hub, workspace, board, profil, create, dashboard. Ayrıntılar UI plan §3.

### 7.2 Task Manager’dan yeniden kullanım

| Kaynak (TM) | OC karşılığı | Karar |
|-------------|--------------|--------|
| Board kanban shell | `OcBoardKanban` | Adapt — veri MO |
| `TmNewIssueFormFields` | `OcDynamicForm` | Adapt — form context |
| `TmIssueProfileView` | Profil layout | Adapt — actions MO |
| `TmIssueComments` | Yorum paneli | Adapt — MO API |
| `taskManagerWorkflow.ts` | Transition | **Kullanma** |
| `taskManagerService` / issue CRUD | WI mutasyon | **Kullanma** |

Tam matris: [ui/OC_UI_PHASE1_PLAN.md §7](./ui/OC_UI_PHASE1_PLAN.md).

### 7.3 Kanban board (Faz 1)

- Kolon = hedef `toStateId`; drop politikası [§5.2.2](#522-kanban-sürükle-bırak-kararlandı).
- Veri: `BoardRuntimeContext` (MngOperations); kart/liste TM bileşenlerinden adaptasyon ([§7.2](#72-task-managerdan-yeniden-kullanım)).
- Profil / board toolbar: transition butonları `transitionKey` + `op_profiles.actions` sırası.

### 7.4 Profil Timeline paneli (Faz 1)

- Veri: MngOperations timeline merge ([§5.2.4](#524-timeline-kararlandı)); UI DG’ye ayrı merge yapmaz.
- State süre detayı / SLA: `op_work_item_timelines` (API veya profile alt endpoint).

### 7.5 Ekran listesi (Faz 1)

| Ekran | Route | Sprint |
|-------|-------|--------|
| Workspace explorer | `/apps/operation-core/workspace` | S1 |
| Board (kanban/list) | `.../boards/[boardId]` | S2 |
| WI profil | `.../work-items/[id]/profile` | S3–S4 |
| WI create | `.../work-items/new` | S4 |
| Dashboard | `.../dashboards/[dashboardId]` | S5 |

Uygulama sırası: [ui/OC_UI_PHASE1_PLAN.md §11](./ui/OC_UI_PHASE1_PLAN.md).

---

## 8. Fazlandırma ve kilometre taşları

### 8.1 Faz 1 — MVP

Backend maddeleri: [MVP_CHECKLIST.md](./mngoperations/MVP_CHECKLIST.md). UI maddeleri: [ui/OC_UI_PHASE1_PLAN.md §11](./ui/OC_UI_PHASE1_PLAN.md).

### 8.2 Faz 2+

_(doldurulacak — phase1 doc §31 ile hizalı)_

---

## 9. Geçiş: Task Manager → OC

### 9.1 Paralel çalışma / deprecate

_(doldurulacak)_

### 9.2 Terminoloji eşlemesi

| Eski (TM) | Yeni (OC) |
|-----------|-----------|
| Project | Workspace |
| Task / Issue | WorkItem |
| Status | State |
| `tm_*` | `op_*` |

---

## 10. Kurulum ve operasyon

- Dataset import script: _(yol — doldurulacak)_
- Gateway route: _(doldurulacak)_
- Side menu: _(doldurulacak)_
- Odak deploy notları: _(doldurulacak)_

---

## 11. Test ve kabul kriterleri

_(doldurulacak)_

---

## 12. Riskler ve karar logu

| Tarih | Konu | Karar | Not |
|-------|------|-------|-----|
| 26 May 2026 | Transition kaynağı | **Üç katman:** zengin `op_state_flows.transitions[]` (katalog, `transitionKey`); `op_rules` çapraz mantık; `op_profiles.actions` sunum. Uygulama yalnızca MngOperations `transitionKey` komutu. | §5.2.1; phase1 §14 |
| 26 May 2026 | Kanban sürükle-bırak | Tek geçiş → otomatik apply; çoklu geçiş → seçici; yok → red. İsteğe bağlı board `defaultTransitionKey`; `BoardRuntimeContext` kolon metadata. | §5.2.2; §7.3 |
| 26 May 2026 | `op_rules` scope | Graf katalogda; kurallar `transitionKey` ve/veya `from`/`to` kenar; çakışmada en kısıtlayan kazanır. | §5.2.3 |
| 26 May 2026 | Timeline | **İki katman:** UI Timeline = runtime merge (`op_comments` + `op_activities`); state segments = `op_work_item_timelines` persist on transition (Faz 1 zorunlu). | §5.2.4; phase1 §21 |
| 26 May 2026 | API yüzeyi | Command API (`work-items`, `transitions/{key}`) + `GET /runtime/*` context; gateway `/operations/api/v1`. Metadata config Faz 1 = DG. | §5.2.5; §6.2 |
| 26 May 2026 | Automation Faz 1 | **Inline** Event→Rule→Action (op_rules); **RabbitMQ publish**; origin = `POST /work-items/from-origin`. Async consumer / queue-origin Faz 2+. | §5.2.6; phase1 §28 |
| 26 May 2026 | Planlama işbirliği | Öneriler Major Plan’a uyumlu; çelişen talepte önce uyarı, bilinçli istisna §12’ye gerekçeli. | §1.6 |
| 26 May 2026 | RabbitMQ (MO) | **`oc.events`**; `{domainId}.oc.workitem.*`; tenant alanları payload’da zorunlu | `mngoperations/INTEGRATIONS.md` §3 |
| 26 May 2026 | Bildirim / DG publish | MO: `op_notifications` + policies + Notifier; `op_*` **`publish_mode: none`**; ham CRUD event yok | `mngoperations/NOTIFICATIONS_AND_EVENTS.md` |
| 26 May 2026 | Odak deploy sırası | MO + gateway smoke önce; OC UI sonra; seed `docs/odak/operationcore/scripts/seed-*.ps1` | `mngoperations/GATEWAY_AND_DEPLOY.md` §4 |

---

## 13. Checkpoint / el değiştirme

| Tarih | Durum | Sonraki adım |
|-------|--------|--------------|
| 26 May 2026 | İskelet oluşturuldu | — |
| 26 May 2026 | §1–§2 üst çerçeve yazıldı | §5.2 sıra 1: Transition |
| 26 May 2026 | §1.6 + §5.2.1 + §12 transition kararı | §5.2 sıra 1b–1c |
| 26 May 2026 | §5.2.2–5.2.3 Kanban + op_rules scope | §5.2 sıra 2: Timeline |
| 26 May 2026 | §5.2.4 Timeline | §5.2 sıra 3: API yüzeyi |
| 26 May 2026 | §5.2.5 API yüzeyi | §5.2 sıra 4: Automation Faz 1 |
| 26 May 2026 | §5.2 tamamlandı (1–4) | §5.1 dataset envanteri veya §8 Faz 1 MVP maddeleri |
| 26 May 2026 | MngOperations plan klasörü | `docs/odak/operationcore/mngoperations/*.md` — backend plan; UI ayrı; OPEN_QUESTIONS |

---

## Ekler

- _(ek linkler, diyagramlar)_
