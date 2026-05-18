# Workflow Sistemi — Planlama Belgesi

**Tarih:** 25 Şubat 2026  
**Durum:** Planlama — **MngRules** (IFTTT tarzı) ile devam kararı alındı  
**Kararlar:** MngWorkflow ayrı servis | Task Manager: DG + Workflow (MngTaskManager yok) | Veri: DG dataset, `@` prefix | Validation API: `POST /validate/{dataset}` | Pipeline: dataset adına göre, çoklu pipeline | RabbitMQ: ayrı worker, DG exchange, domain = exchange adından | Mail: MngNotifier (sadece gönderim), template = Workflow (`@wf_mail_templates`) | DG: her dataset ayrı HTTP validation, Gateway base URL, JWT forward, timeout per-validation (varsayılan 30s)  
**İlgili:** [Task Manager Planlama](../task_manager/TASK_MANAGER_PLANNING.md), MngDataGateway HTTP Validation, RabbitMQ Events

---

## 1. Özet ve Hedef

MonitraNG ekosisteminde **dataset odaklı otomasyon** için bir workflow sistemi tasarlanacak. Temel kullanım senaryoları:

| Senaryo | Açıklama | Örnek |
|---------|----------|-------|
| **Validasyon flow'ları** | Kayıt eklenmeden önce kriterlere veya bir flow sonucuna göre uygunluk kontrolü | `tm_issues` için projectKey validation; belirli alan kombinasyonlarına göre red/onay |
| **Post-insert aksiyonlar** | Kayıt eklendikten sonra belirli kriterlere göre iş tetikleme | Kritik öncelikli issue → mail at; belirli dataset güncellemesi yap |
| **Çapraz dataset senkronizasyonu** | Bir dataset'teki değişiklik başka dataset'i güncelle | `tm_issues` status değişimi → `tm_sprints` burndown güncelle |

---

## 2. Mevcut Altyapı (Önemli Noktalar)

### 2.1 MngDataGateway

| Özellik | Durum | Workflow ile ilişki |
|---------|-------|---------------------|
| **HTTP Validation** | ✅ Var | Dataset schema'da `validations` → `type: "http"`, `url` tanımlanır. DG, create/update öncesi bu URL'e POST atar. Response: `{ isValid, errorMessage }` |
| **Expression validation** | ✅ Var | Schema içi `validations` → `type: "expression"`, basit ifadeler (örn. `price > 0 && pageCount >= 0`) |
| **RabbitMQ events** | ✅ Var | Create/update/delete sonrası `dataset.{datasetName}.{operation}` routing key ile event yayınlanır. `publishMode: basic|full|none` |
| **Domain izolasyonu** | ✅ Var | Exchange: `monitra.data.events.{domainName}` |

### 2.2 Node-RED (mng_common)

- `flows.json` ile mevcut flow'lar (Configs, Mail Sender, SignalR, vb.)
- RabbitMQ consumer, HTTP endpoint, WebSocket bağlantıları mevcut
- **HTTP Validation** dokümantasyonunda Node-RED ile entegrasyon örneği var

### 2.3 Task Manager Planlama

- Task Manager için **ayrı backend servisi yok**. DG + **MngWorkflow** kullanılacak.
- `projectKey` validation: MngWorkflow validation pipeline ile yapılacak.

---

## 3. Yaklaşım Karşılaştırması

### 3.1 Yöntem 1: MngRules (IFTTT tarzı)

**MngRules:** Tetikleyici → koşul → aksiyon modeli. Basit kural tabanlı otomasyon. Form/UI ile yapılandırılır. *(Karar: Bu yaklaşım ile devam)*

```
[TRIGGER] Dataset X'te kayıt eklendi
    ↓
[CONDITION] statusId == "critical" && priorityId == "high"
    ↓
[ACTION] Mail gönder / Başka dataset güncelle
```

**Artıları:**
- Basit, anlaşılır
- Kod yazmadan UI ile kurulabilir
- Çoğu senaryo için yeterli
- Bakım kolay

**Eksileri:**
- Karmaşık dallanma (A ise X, B ise Y, C ise Z) zor
- Çoklu adım (A → B → C) sınırlı
- Özel mantık (örn. harici API çağrısı, hesaplama) için "custom action" gerekir

**Uygulama fikri:**
- DG dataset'lerde saklanan kurallar: `@wf_rules` (trigger, condition, action) — workflow dataset'leri `@` prefix ile
- Condition: expression (DG'deki gibi) veya basit JSON path
- Action: mail, dataset update, HTTP call, vb.

---

### 3.2 Yöntem 2: Mini Node-RED Tarzı (Görsel Flow)

**Mantık:** Node'lar ve bağlantılarla akış çizilir. Her node bir işlem (trigger, filter, action).

```
[RabbitMQ In] → [Switch/Condition] → [Mail] 
                    ↓
               [Dataset Update]
```

**Artıları:**
- Esnek, karmaşık akışlar mümkün
- Görsel debug kolay
- Zaten Node-RED var; bu yaklaşım doğal

**Eksileri:**
- UI geliştirme maliyeti yüksek (kendi editor'ümüzü yazarsak)
- Node-RED'i doğrudan kullanırsak: domain izolasyonu, multi-tenant yönetimi, UI entegrasyonu zor
- Bakım ve versiyonlama (flow'ların export/import, rollback) daha karmaşık

**Uygulama fikri:**
- **Seçenek A:** Mevcut Node-RED'i kullan, sadece flow şablonları + RabbitMQ/HTTP bağlantılarını standartlaştır
- **Seçenek B:** Vue Flow / React Flow benzeri hafif bir flow editor + backend executor (MngWorkflow)

---

### 3.3 Yöntem 3: Hibrit — MngRules + Pipeline (Önerilen)

**Mantık:** Basit senaryolar için **MngRules** kuralları; karmaşık senaryolar için "pipeline" (adım adım işlem zinciri). İleride Node-RED benzeri görsel flow genişletmesi yapılabilir. Pipeline, JSON/DSL ile tanımlanır.

```
Kural örneği:
  trigger: dataset.tm_issues.created
  condition: payload.priorityId == "critical"
  actions:
    - type: mail
      template: "critical_issue"
    - type: dataset_update
      dataset: tm_sprints
      ...

Pipeline örneği (validasyon):
  steps:
    - fetch: tm_projects by projectId
    - assert: project.key == payload.projectKey
    - return: { isValid: true }
```

**Artıları:**
- Basit senaryolar için hızlı (kural UI)
- Karmaşık senaryolar için pipeline DSL
- DG HTTP Validation + RabbitMQ ile doğal entegrasyon
- Görsel editor ileride eklenebilir (pipeline → visual export)

**Eksileri:**
- İki modeli (kural + pipeline) yönetmek
- DSL tasarımı ve dokümantasyon ihtiyacı

---

## 4. Senaryo → Yaklaşım Eşlemesi

| Senaryo | MngRules | Node-RED | Hibrit |
|---------|----------|----------|--------|
| Validasyon (kayıt öncesi) | ✅ HTTP validation URL'e kural servisi bağlanır | ✅ Node-RED flow HTTP endpoint | ✅ Pipeline → HTTP endpoint |
| Post-insert (mail, dataset update) | ✅ Kural: trigger + condition + action | ✅ RabbitMQ consumer flow | ✅ MngRules veya pipeline |
| Çok adımlı (A→B→C) | ⚠️ Sınırlı | ✅ Tam destek | ✅ Pipeline |
| Görsel debug | ❌ | ✅ | ⚠️ Pipeline log ile |
| Bakım kolaylığı | ✅ | ⚠️ | ✅ |

---

## 5. Mimari Önerisi (Hibrit Yaklaşım)

### 5.1 Bileşenler

```
┌─────────────────────────────────────────────────────────────────┐
│                        MngDataGateway                             │
│  • HTTP Validation (create/update öncesi) → MngWorkflow API      │
│  • RabbitMQ Publish (create/update/delete sonrası)               │
└─────────────────────────────────────────────────────────────────┘
         │                                    │
         ▼                                    ▼
┌─────────────────────┐            ┌─────────────────────────────┐
│   MngWorkflow       │            │   RabbitMQ                   │
│   (ayrı servis)     │            │   monitra.data.events.{dom}  │
│                     │            └─────────────────────────────┘
│ • Validation API    │                        │
│   POST /validate    │                        ▼
│ • Rule Engine       │            ┌─────────────────────────────┐
│ • Pipeline Executor │            │  MngWorkflow Event Consumer  │
│ • Kural CRUD        │            │  veya Node-RED               │
└─────────────────────┘            └─────────────────────────────┘
```

### 5.2 Validasyon Akışı

1. Client → DG: `POST /data/api/v1/data/tm_issues` (create)
2. DG → ValidationService → **HTTP validation** (schema'da tanımlı)
3. HTTP validation URL: `POST /validate/{dataset}` (örn. `/validate/tm_issues`)
4. MngWorkflow: İlgili validation pipeline'ları çalıştırır (dataset adına göre eşleşme)
5. Response: `{ isValid: true }` veya `{ isValid: false, errorMessage: "..." }`
6. DG: Geçerse insert, geçmezse 400 + validation errors

**Validation API kararları:**

| Konu | Karar |
|------|-------|
| **URL formatı** | `POST /validate/{dataset}` |
| **Domain izolasyonu** | DG tarafında JWT kullanılır; istek MngWorkflow'a JWT ile iletilir |
| **Pipeline eşlemesi** | Dataset adına göre; bir dataset için **birden fazla pipeline** tanımlanabilir (hepsi sırayla çalışır) |

**DG Entegrasyonu kararları:**

| Konu | Karar |
|------|-------|
| **HTTP validation** | Her dataset için schema'da ayrı HTTP validation tanımı |
| **Base URL** | Gateway üzerinden (MngWorkflow API Gateway'e route eklenecek) |
| **Token iletimi** | JWT forward — DG, Authorization header'ı MngWorkflow'a iletecek |
| **Timeout** | Validation tanımında `timeoutSeconds` (opsiyonel). Varsayılan: 30s |

### 5.3 Post-Insert Akışı

1. DG → Insert tamamlandı → RabbitMQ event
2. RabbitMQ consumer (ayrı worker veya MngWorkflow): DG exchange'inden event alır
3. Kural/pipeline: condition kontrolü → action'ları çalıştırır (mail, dataset update, vb.)

**RabbitMQ Consumer kararları:**

| Konu | Karar |
|------|-------|
| **Çalışma yeri** | Ayrı worker olabilir (MngWorkflow process'ten bağımsız) |
| **Exchange** | DG'nin kullandığı `monitra.data.events.{domainName}` |
| **Queue / binding** | DG exchange yapısı üzerinden (tek queue, çoklu binding veya domain başına queue) |
| **Domain bilgisi** | **Exchange adından** alınır: `monitra.data.events.{domainName}` → domainName |

**Mail Action kararları:**

| Konu | Karar |
|------|-------|
| **Mail gönderimi** | **MngNotifier** servisi kullanılır — sadece mail göndermekle sorumlu |
| **Template oluşturma** | **Workflow** sorumluluğunda — `@wf_mail_templates` dataset'inde saklanır |
| **Akış** | Workflow template'i okur, değişkenleri doldurur (payload, MngKeeper expand vb.), MngNotifier'a `to`, `subject`, `body` gönderir |

### 5.4 Veri Modeli (Önerilen)

**@wf_rules** (DG dataset):
```json
{
  "name": "critical_issue_mail",
  "flowType": "user",
  "categoryId": "...",
  "phase": "after",
  "trigger": { "type": "dataset_event", "dataset": "tm_issues", "operations": ["created"] },
  "condition": { "type": "expression", "expr": "payload.priorityId == 'critical-id'" },
  "actions": [
    { "type": "mail", "templateId": "critical_issue", "to": "payload.assignee" },
    { "type": "dataset_update", "dataset": "tm_sprints", "..." }
  ],
  "enabled": true
}
```

**@wf_validation_pipelines** (DG dataset, validasyon için, before phase):
```json
{
  "name": "tm_issues_project_key",
  "flowType": "system",
  "categoryId": "...",
  "dataset": "tm_issues",
  "steps": [
    { "type": "fetch", "dataset": "tm_projects", "by": "payload.projectId" },
    { "type": "assert", "expr": "result.key == payload.projectKey" },
    { "type": "return", "isValid": "true" }
  ]
}
```

### 5.5 Dataset CRUD Hooks (Before / After)

Flow'lar, dataset CRUD işlemlerine göre **önce** (before) veya **sonra** (after) çalışacak şekilde tanımlanabilir.

```
                    CRUD İşlemi
                         │
    ┌────────────────────┼────────────────────┐
    │                    │                    │
    ▼                    │                    ▼
[BEFORE]              [DG]               [AFTER]
Önce kurallar      Persist / DB        Sonra kurallar
```

#### BEFORE (Önce) — Pre-operation

| Özellik | Açıklama |
|---------|----------|
| **Ne zaman** | Kayıt DB'ye yazılmadan hemen önce |
| **Amaç** | Validasyon, engelleme |
| **Bloklama** | Evet — kural geçmezse DG insert/update yapmaz |
| **Senkron** | DG isteği bekler, timeout sınırı var |
| **DG entegrasyonu** | Mevcut HTTP validation ile |

**Örnek:** `tm_issues` create öncesi projectKey ↔ projectId uyumu kontrolü.

**Veri modeli:**
```json
{
  "phase": "before",
  "trigger": {
    "type": "dataset_event",
    "dataset": "tm_issues",
    "operations": ["created", "updated"]
  },
  "condition": "...",
  "onFailure": "block"
}
```

#### AFTER (Sonra) — Post-operation

| Özellik | Açıklama |
|---------|----------|
| **Ne zaman** | Kayıt DB'ye yazıldıktan sonra |
| **Amaç** | Mail, bildirim, başka dataset güncelleme |
| **Fire & forget** | DG işlemi bloklamaz |
| **Async** | RabbitMQ event → consumer |
| **DG entegrasyonu** | Mevcut RabbitMQ event'leri ile |

**Örnek:** `tm_issues` create sonrası `priorityId == critical` → mail at.

**Veri modeli:**
```json
{
  "phase": "after",
  "trigger": {
    "type": "dataset_event",
    "dataset": "tm_issues",
    "operations": ["created", "updated"]
  },
  "condition": "payload.statusId == 'done-id'",
  "actions": [{ "type": "mail", "..." }]
}
```

#### Operation türleri

| Operation | Before | After |
|-----------|--------|-------|
| **created** | ✅ Validasyon | ✅ Post-insert aksiyonlar |
| **updated** | ✅ Validasyon | ✅ Post-update aksiyonlar |
| **deleted** | ⚠️ Silme öncesi kontrol (örn. referans) | ✅ Post-delete (log, temizlik) |
| **restored** | ❌ Genelde gerekmez | ✅ Post-restore |

#### Özet

| Phase | Tetikleme | DG entegrasyonu | Bloklama |
|-------|-----------|-----------------|----------|
| **before** | created, updated, (deleted) | HTTP validation URL | Evet |
| **after** | created, updated, deleted, restored | RabbitMQ consumer | Hayır |

---

## 6. Kategoriler ve Flow Tipleri

### 6.0 Veri Saklama Konvansiyonu

| Karar | Açıklama |
|-------|----------|
| **Saklama** | Workflow verileri **DG dataset**'lerinde tutulur |
| **Prefix** | Workflow dataset'leri **`@`** prefix ile başlar |
| **Örnekler** | `@wf_rules`, `@wf_categories`, `@wf_validation_pipelines`, `@wf_mail_templates` |

### 6.1 Kategoriler

Flow'lar **kategori** altında gruplanabilir. Kategori, organizasyon ve filtreleme için kullanılır.

| Özellik | Açıklama |
|---------|----------|
| **@wf_categories** | Kategori dataset'i (DG) |
| **Hiyerarşi** | Düz liste veya parent-child (opsiyonel) |
| **Örnekler** | "Task Manager", "Bildirimler", "Validasyonlar", "Monitoring" |

**Veri modeli taslağı:**
```json
{
  "name": "@wf_categories",
  "fields": [
    { "name": "name", "title": "Kategori adı" },
    { "name": "description", "title": "Açıklama" },
    { "name": "order", "title": "Sıra" }
  ]
}
```

Flow (kural) kaydında `categoryId` (relation → `@wf_categories`) ile kategoriye bağlanır.

### 6.2 Flow Tipleri: System vs User

İki flow türü tanımlanır:

| Tip | Açıklama | CRUD yetkisi |
|-----|----------|--------------|
| **System Flow** | Sistem tarafından kullanılan, kritik flow'lar (örn. projectKey validation) | **Sadece admin** kullanıcıları oluşturabilir, güncelleyebilir, silebilir |
| **User Flow** | Kullanıcıların kendi ihtiyaçları için oluşturduğu flow'lar | Yetkili kullanıcılar (domain içi, rol bazlı) |

**Veri modeli:**
```json
{
  "flowType": "system",
  "name": "tm_issues_project_key_validation",
  "categoryId": "...",
  ...
}
```

```json
{
  "flowType": "user",
  "name": "kritik_issue_mail",
  "categoryId": "...",
  ...
}
```

### 6.3 Yetkilendirme

| İşlem | System Flow | User Flow |
|-------|-------------|-----------|
| **Create** | Admin only | Yetkili kullanıcılar |
| **Read** | Tüm yetkili kullanıcılar | Sahibi + yetkili |
| **Update** | Admin only | Sahibi veya yetkili |
| **Delete** | Admin only | Sahibi veya yetkili |
| **Disable/Enable** | Admin only (system) | Sahibi (user) |

**Admin tanımı:** JWT içindeki `is_admin` değeri ile belirlenir. API ve UI'da bu kontrol yapılır.

---

## 7. Hibrit Yöntem Özeti (MngRules + Pipeline)

| Özellik | Açıklama |
|---------|----------|
| **MngRules** | Tetikleyici + koşul + aksiyon. UI ile yönetilebilir. İlk fazda odak. |
| **Validasyon** | Pipeline/DSL ile adım adım. DG HTTP validation bu servise yönlendirilir. |
| **Event-driven** | RabbitMQ consumer ile post-insert aksiyonlar. |
| **Genişletilebilirlik** | Yeni action tipleri (mail, dataset_update, http_call, vb.) plugin gibi eklenebilir. |
| **Görsel editor** | Faz 2+ için pipeline → visual export veya basit flow editor. |

---

## 8. Karar Matrisi

| Kriter | MngRules | Node-RED | Hibrit |
|--------|----------|----------|--------|
| Geliştirme süresi | Kısa | Orta-Uzun | Orta |
| Esneklik | Orta | Yüksek | Yüksek |
| Mevcut altyapı kullanımı | DG HTTP + RabbitMQ | Node-RED + RabbitMQ | DG HTTP + RabbitMQ + (opsiyonel Node-RED) |
| Task Manager ihtiyacı | ✅ projectKey validation | ✅ | ✅ |
| Bakım | Kolay | Orta | Orta |
| Öğrenme eğrisi | Düşük | Orta | Orta |

---

## 9. Önerilen Yol Haritası

### Faz 1 — Temel (MngRules)
- [ ] **MngWorkflow** mikroservisi (ayrı servis)
- [ ] **MngGateway** — MngWorkflow route eklenmesi
- [ ] Validation API: `POST /validate/{dataset}` — dataset adına göre pipeline'lar çalıştırır, `{ isValid, errorMessage }` döner
- [ ] DG dataset'lere HTTP validation ekleme (her dataset için ayrı, Gateway URL, `timeoutSeconds` opsiyonel)
- [ ] Task Manager: projectKey validation pipeline'ı
- [ ] **RabbitMQ consumer** (ayrı worker): DG exchange, domain = exchange adından
- [ ] **@wf_mail_templates** dataset (template oluşturma Workflow sorumluluğunda)
- [ ] Basit kural: condition + mail action (post-insert) → MngNotifier
- [ ] **Kategoriler** (`@wf_categories`) ve **flow tipleri** (system/user) — system flow sadece admin CRUD

### Faz 2 — Genişletme
- [ ] Template CRUD UI
- [ ] Kural CRUD API + UI (kategori, flowType, phase ile)
- [ ] Action tipleri: dataset_update, http_call
- [ ] Pipeline DSL dokümantasyonu ve örnekler

### Faz 3 — İleri
- [ ] Görsel pipeline/flow editor (opsiyonel)
- [ ] Node-RED ile koordinasyon (bazı flow'lar Node-RED'de kalabilir)

---

## 10. Açık Sorular

| Konu | Seçenekler | Not |
|------|------------|-----|
| MngWorkflow ayrı servis mi? | ✅ **Evet** — Karar alındı |
| Node-RED rolü | Tamamen kaldır / Bazı flow'lar için kullan / Hibrit'te opsiyonel consumer | Mevcut flows.json kullanımı devam edebilir |
| Pipeline vs Kural | İkisi birden / Önce sadece kural / Önce sadece pipeline | Hibrit'te ikisi de |
| Validasyon timeout | DG'de per-validation `timeoutSeconds` (varsayılan 30s) | ✅ |

---

## 11. Sonraki Adımlar

1. **Yaklaşım kararı:** ✅ **MngRules** (IFTTT tarzı) ile başlanacak; ileride Node-RED benzeri görsel flow genişletmesi yapılabilir
2. **Task Manager backend:** ✅ DG + MngWorkflow kullanılacak (MngTaskManager yok)
3. **Validation API:** ✅ URL `POST /validate/{dataset}` | Domain: JWT | Pipeline eşlemesi: dataset adına göre, çoklu pipeline
4. **RabbitMQ Consumer:** ✅ Ayrı worker | DG exchange | Domain: exchange adından
5. **Mail Action:** ✅ MngNotifier (sadece gönderim) | Template: Workflow `@wf_mail_templates`
6. **DG Entegrasyonu:** ✅ Her dataset ayrı HTTP validation | Gateway base URL | JWT forward | Timeout: per-validation `timeoutSeconds` (varsayılan 30s)
7. **MngGateway:** MngWorkflow route eklenecek
8. **Yetkilendirme:** Admin = JWT `is_admin` değeri
9. **İlk pilot:** Workflow tamamlandıktan sonra Task Manager ile entegrasyon

---

## 12. İlgili Dokümanlar

- [Faz 1 Implementasyon Planı](FAZ1_IMPLEMENTATION_PLAN.md)
- [Task Manager Planlama](../task_manager/TASK_MANAGER_PLANNING.md)
- [MngDataGateway HTTP Validation](../MngDataGateway/support/guides/HTTP_VALIDATION.md)
- [MngDataGateway Technical Specs](../MngDataGateway/main/TECHNICAL_SPECS.md)
- [Monitoring Workflow](../monitoring_plans/MONITORING_WORKFLOW.md)
