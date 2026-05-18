# Workflow — Checkpoint (25 Şubat 2026)

**Buraya dönülecek.** Yarın buradan devam edilecek.

---

## 1. Tamamlananlar

### Planlama
- [x] Workflow planlama belgesi (WORKFLOW_PLANNING.md)
- [x] MngRules yaklaşımı kararı
- [x] Dataset CRUD hooks (before/after)
- [x] Kategoriler ve flow tipleri (system/user)
- [x] Tüm kararların netleştirilmesi (Validation API, RabbitMQ, Mail, DG entegrasyonu, Yetkilendirme)
- [x] Faz 1 implementasyon planı (FAZ1_IMPLEMENTATION_PLAN.md)

### Implementasyon (Faz 1 başlangıcı)
- [x] **MngWorkflow** solution ve proje yapısı (Domain, Application, Infrastructure, Api)
- [x] **Validation API** — `POST /api/v1/validate/{datasetName}`
- [x] **DataGatewayClient** — DG API'den @wf_validation_pipelines okuma
- [x] **ValidationPipelineService** — fetch, assert, return step'leri
- [x] **ValidateController** — domain (JWT veya X-Domain-Name header)
- [x] **MngGateway** route — `/workflow/api/v1/*` → mngworkflow:5085
- [x] **DG timeoutSeconds** — ValidationDefinition'a per-validation timeout eklendi
- [x] Build başarılı

---

## 2. Sonraki Adımlar (Yarın)

### Öncelik 1 — setup-workflow-datasets.ps1
- [ ] `scripts/tests/MngDataGateway/workflow/setup-workflow-datasets.ps1` oluştur
- [ ] @wf_categories, @wf_validation_pipelines, @wf_rules, @wf_mail_templates şemaları
- [ ] Seed kategoriler ("Validasyonlar", "Bildirimler")
- [ ] "Workflow" dataset kategorisi
- [ ] Referans: `setup-monitoring-datasets.ps1`

### Öncelik 2 — Test ve doğrulama
- [ ] MngWorkflow çalıştır: `cd MngWorkflow\Presentation\MngWorkflow.Api && dotnet run`
- [ ] Swagger: http://localhost:5085/swagger
- [ ] `POST /api/v1/validate/test_dataset` — X-Domain-Name header ile test
- [ ] DG'de bir test dataset'e HTTP validation ekle, MngWorkflow URL ile dene

### Öncelik 3 — RabbitMQ consumer
- [ ] Domain discovery (config veya MngKeeper)
- [ ] Queue + binding (wf_events, monitra.data.events.{domain})
- [ ] @wf_rules loader, condition evaluator
- [ ] Mail action executor

### Öncelik 4 — Mail action
- [ ] @wf_mail_templates loader
- [ ] Variable replacement
- [ ] MngNotifier API entegrasyonu

---

## 3. Alınan Kararlar (Özet)

| Konu | Karar |
|------|-------|
| MngWorkflow | Ayrı servis |
| Task Manager | DG + MngWorkflow (MngTaskManager yok) |
| Veri saklama | DG dataset, `@` prefix |
| Validation API | `POST /validate/{dataset}` |
| Pipeline | Dataset adına göre, çoklu pipeline |
| RabbitMQ | Ayrı worker, DG exchange, domain = exchange adından |
| Mail | MngNotifier, template = @wf_mail_templates |
| DG entegrasyonu | Her dataset ayrı HTTP validation, Gateway URL, JWT forward, timeoutSeconds |
| Yetkilendirme | Admin = JWT `is_admin` |

---

## 4. Teknik Bilgiler

| Bileşen | Değer |
|---------|-------|
| MngWorkflow port | 5085 |
| Gateway route | `/workflow/api/v1/{everything}` |
| Validation endpoint | `POST /api/v1/validate/{datasetName}` |
| Domain header (dev) | `X-Domain-Name` |

---

## 5. İlgili Dosyalar

- [Workflow Planlama](WORKFLOW_PLANNING.md)
- [Faz 1 Implementasyon Planı](FAZ1_IMPLEMENTATION_PLAN.md)
- [Task Manager Planlama](../task_manager/TASK_MANAGER_PLANNING.md)
- MngWorkflow projesi: `MngWorkflow/`
- setup-monitoring-datasets.ps1: `scripts/tests/MngDataGateway/dataset/`
