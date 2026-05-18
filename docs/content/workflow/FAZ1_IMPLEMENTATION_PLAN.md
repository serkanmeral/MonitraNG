# MngWorkflow Faz 1 — Implementasyon Planı

**Tarih:** 25 Şubat 2026  
**Referans:** [Workflow Planlama](WORKFLOW_PLANNING.md)

---

## 1. Özet

Faz 1, MngRules (IFTTT tarzı) temel altyapısını kurar: MngWorkflow servisi, Validation API, RabbitMQ consumer, workflow dataset'leri ve mail action.

---

## 2. Faz 1 Kapsamı

| Bileşen | Açıklama |
|---------|----------|
| MngWorkflow | Ayrı .NET mikroservisi |
| Validation API | `POST /validate/{dataset}` |
| RabbitMQ consumer | Ayrı worker (veya MngWorkflow içinde) |
| @wf_* dataset'leri | @wf_categories, @wf_rules, @wf_validation_pipelines, @wf_mail_templates |
| MngGateway | MngWorkflow route |
| Mail action | MngNotifier entegrasyonu |

---

## 3. Implementasyon Adımları

### Faz 1.1 — Proje Yapısı ve Altyapı

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.1.1 | MngWorkflow solution oluştur | Clean Architecture: Domain, Application, Infrastructure, Persistence, Api. MngNotifier/MngReactor yapısı referans alınır | — |
| 1.1.2 | Temel config | appsettings.json, RabbitMQ, DG BaseUrl, MngNotifier BaseUrl | 1.1.1 |
| 1.1.3 | DG HTTP client | MngDataGateway API'ye erişim (dataset okuma). JWT forward ile | 1.1.1 |
| 1.1.4 | MngNotifier HTTP client | Mail gönderme API çağrısı | 1.1.1 |

---

### Faz 1.2 — Workflow Dataset'leri (DG)

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.2.1 | setup-workflow-datasets.ps1 | `scripts/tests/MngDataGateway/workflow/` altında. setup-monitoring-datasets.ps1 referans | — |
| 1.2.2 | @wf_categories şeması | name, description, order. DG'de oluştur | 1.2.1 |
| 1.2.3 | @wf_validation_pipelines şeması | name, flowType, categoryId, dataset, steps (array) | 1.2.1 |
| 1.2.4 | @wf_rules şeması | name, flowType, categoryId, phase, trigger, condition, actions, enabled | 1.2.1 |
| 1.2.5 | @wf_mail_templates şeması | templateId, name, subject, body, variables (array) | 1.2.1 |
| 1.2.6 | Seed kategoriler | "Validasyonlar", "Bildirimler" vb. | 1.2.2 |
| 1.2.7 | Dataset kategorisi | "Workflow" kategori oluştur, @wf_* dataset'leri ata | 1.2.1 |

---

### Faz 1.3 — Validation API

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.3.1 | ValidateController | `POST /api/v1/validate/{dataset}` endpoint | 1.1.1 |
| 1.3.2 | JWT + domain | Request'ten JWT parse, domain_id / domain_name çıkar | 1.3.1 |
| 1.3.3 | Pipeline loader | DG'den @wf_validation_pipelines okuma (dataset + domain filtresi) | 1.1.3, 1.2.3 |
| 1.3.4 | Pipeline executor | fetch, assert, return step'leri. DG API ile fetch | 1.3.3 |
| 1.3.5 | Response format | `{ isValid: bool, errorMessage?: string }` | 1.3.4 |
| 1.3.6 | Hata yönetimi | Timeout, pipeline hata, DG erişim hataları | 1.3.4 |

---

### Faz 1.4 — MngGateway Entegrasyonu

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.4.1 | ocelot.json route | `/workflow/api/{everything}` → mngworkflow:port | 1.3.1 |
| 1.4.2 | JWT forward | Gateway'den MngWorkflow'a Authorization header iletimi (mevcut yapı) | 1.4.1 |
| 1.4.3 | docker-compose | MngWorkflow container tanımı (varsa) | 1.1.1 |

---

### Faz 1.5 — RabbitMQ Consumer

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.5.1 | Domain discovery | Hangi domain exchange'lerine bind edilecek? (config veya MngKeeper API) | 1.1.1 |
| 1.5.2 | Queue + binding | `wf_events` queue, `monitra.data.events.{domain}` exchange'lerine `dataset.#` binding | 1.5.1 |
| 1.5.3 | Consumer service | Mesaj al, exchange adından domain çıkar | 1.5.2 |
| 1.5.4 | @wf_rules loader | phase=after, trigger.dataset, trigger.operations eşleşen kurallar | 1.1.3, 1.2.4 |
| 1.5.5 | Condition evaluator | Expression (payload alanlarına göre) | 1.5.4 |
| 1.5.6 | Action executor — mail | templateId, to (sabit veya payload path), MngNotifier API | 1.1.4, 1.2.5 |
| 1.5.7 | Worker process | Ayrı worker veya MngWorkflow host içinde background service | 1.5.3 |

---

### Faz 1.6 — Mail Action Detayı

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.6.1 | Template loader | @wf_mail_templates'dan templateId ile okuma | 1.1.3, 1.2.5 |
| 1.6.2 | Variable replacement | {{variableName}} → payload / context değerleri | 1.6.1 |
| 1.6.3 | To alanı | Faz 1: sabit email veya action spec'te `to: "email@x.com"`. persons→email Faz 2 | 1.5.6 |
| 1.6.4 | MngNotifier API | POST /api/v1/notifications/mail (direct mail) | 1.1.4 |

---

### Faz 1.7 — Yetkilendirme (API)

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.7.1 | JWT middleware | Gateway'den gelen JWT doğrulama (veya Gateway zaten doğruluyorsa passthrough) | 1.1.1 |
| 1.7.2 | is_admin kontrolü | System flow CRUD için JWT `is_admin` kontrolü. Faz 1'de CRUD API sınırlı olabilir | 1.7.1 |

---

### Faz 1.8 — Test ve Doğrulama

| # | Adım | Açıklama | Bağımlılık |
|---|------|----------|------------|
| 1.8.1 | Validation API test | Mock dataset ile POST /validate/{dataset}, pipeline geçti/geçmedi | 1.3, 1.4 |
| 1.8.2 | DG HTTP validation bağlantısı | Bir test dataset'e HTTP validation ekle, MngWorkflow URL ile. Create denemesi | 1.3, 1.4 |
| 1.8.3 | RabbitMQ consumer test | DG'de publishMode: basic dataset'e kayıt ekle, event'in consumer'a ulaştığını doğrula | 1.5 |
| 1.8.4 | Mail action test | After-rule + mail action, MngNotifier'a mail gittiğini doğrula | 1.5, 1.6 |

---

## 4. Sıra ve Paralellik

```
1.1 (Proje) ──────────────────────────────────────────────────────────┐
     │                                                                  │
1.2 (Dataset'ler) ────────────────────────────────────────────────────┤
     │                                                                  │
     ├──► 1.3 (Validation API) ──► 1.4 (Gateway) ──► 1.8.1, 1.8.2     │
     │                                                                  │
     ├──► 1.5 (RabbitMQ) ──► 1.6 (Mail) ──► 1.8.3, 1.8.4             │
     │                                                                  │
     └──► 1.7 (Yetkilendirme) — 1.3, 1.5 ile paralel                  │
```

**Önerilen başlangıç sırası:**
1. 1.1 + 1.2 (paralel başlanabilir — script ayrı)
2. 1.3 Validation API
3. 1.4 Gateway
4. 1.5 RabbitMQ consumer
5. 1.6 Mail action (1.5 ile entegre)
6. 1.7 Yetkilendirme
7. 1.8 Testler

---

## 5. Teknik Notlar

### 5.1 MngWorkflow Proje Yapısı (Önerilen)

```
MngWorkflow/
├── Core/
│   ├── MngWorkflow.Domain/        # Entities, value objects
│   └── MngWorkflow.Application/   # Interfaces, DTOs, services
├── Infrastructure/
│   ├── MngWorkflow.Infrastructure/  # DG client, MngNotifier client, RabbitMQ
│   └── MngWorkflow.Persistence/    # (DG kullandığımız için minimal — belki sadece config)
└── Presentation/
    └── MngWorkflow.Api/            # Controllers, middleware
```

### 5.2 Pipeline Step Tipleri (Faz 1)

| Step type | Açıklama |
|-----------|----------|
| `fetch` | DG'den dataset kaydı getir. `dataset`, `by` (field), `value` (payload path) |
| `assert` | Expression değerlendir. Geçmezse validation fail |
| `return` | `{ isValid: true }` veya `{ isValid: false, errorMessage: "..." }` |

### 5.3 DG API Erişimi

- MngWorkflow, DG'ye **Gateway üzerinden** erişir (JWT ile)
- Validation isteği DG'den geldiğinde JWT forward edilir — aynı token ile DG'ye geri istek yapılabilir (dataset okuma)
- Domain: JWT'deki `domain_id` veya `domain_name` — DG database adı: `mng_{domain}`

### 5.4 RabbitMQ Queue Stratejisi (Implementasyon Sırasında Netleşecek)

- **Öneri:** Tek queue `wf_events`, bilinen domain exchange'lerine binding
- Domain listesi: config'ten (örn. `Workflow:Domains: ["domain1","domain2"]`) veya dinamik

---

## 6. Checklist — Faz 1 Tamamlanma Kriterleri

- [ ] MngWorkflow servisi ayağa kalkıyor
- [ ] `POST /validate/{dataset}` çalışıyor, pipeline sonucu dönüyor
- [ ] MngGateway'den `/workflow/api/*` erişilebiliyor
- [ ] @wf_categories, @wf_rules, @wf_validation_pipelines, @wf_mail_templates oluşturuldu
- [ ] RabbitMQ consumer event alıyor, domain çıkarıyor
- [ ] After-rule tetikleniyor, condition değerlendiriliyor
- [ ] Mail action MngNotifier'a istek atıyor
- [ ] setup-workflow-datasets.ps1 çalışıyor

---

## 7. İlgili Dokümanlar

- [Workflow Planlama](WORKFLOW_PLANNING.md)
- [MngDataGateway HTTP Validation](../MngDataGateway/support/guides/HTTP_VALIDATION.md)
- [setup-monitoring-datasets.ps1](../../../scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1) — Script referansı
- [MngNotifier Mail API](../MngNotifier/support/guides/MAIL_NOTIFICATION_DESIGN.md)
