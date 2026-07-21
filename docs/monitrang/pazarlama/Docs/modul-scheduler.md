# Zamanlama (Scheduler) — Platform çapraz tetikleyici

**Kod:** `scheduler` · **Durum:** Canlı · **Backend:** MngScheduler · **UI:** OC admin `scheduled-jobs` · system job admin

**Referanslar:** [MngScheduler API (iç)](../../content/MngScheduler/main/TECHNICAL_SPECS.md) · [Workflow DEVAM §Scheduler](../../odak/workflow/DEVAM.md)

> **Bu dosyanın amacı:** **Scheduler** ayrı broşür modülü değil; platform **omurgasında** periyodik tetikleyici. Tüm modül dokümanları «zamanlama» satırında buraya referans verir.

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Zamanlama (Scheduler)**, MonitraNG’de **belirli saatte veya periyotta** otomatik iş çalıştıran platform servisidir — tek cron motoru, **çok modül hedefi**.

### 1.2 Müşteri perspektifi

«Her Pazartesi 09:00 bakım kontrolü açılsın», «Her gece yedek alınsın», «Ay sonu rapor tetiklensin» — kullanıcı veya admin zamanı tanımlar; Scheduler ilgili servise **HTTP hook** veya entegrasyon API’si ile haber verir. Modül başına ayrı cron daemon kurulmaz.

**Broşür cümlesi:** *«Bir zamanlayıcı — DI belge, operasyon görevi, workflow adımı veya platform bakımı.»*

### 1.3 Scheduler vs Workflow zamanlama

| İhtiyaç | Çözüm |
|---------|--------|
| Cron → **tek** OC WorkItem | **Scheduler** → `from-origin` |
| Cron → **çok adımlı** onay/dağıtım | **Workflow** schedule trigger |
| Uzun **onay beklemesi** (günler) | Workflow delay + Scheduler one-shot |
| Tek DI belge üretimi (aylık rapor) | Scheduler → DI *(plan)* veya Workflow |

**Kural:** Aynı iş için Scheduler ve Workflow **ikileme yapmaz** — basit tetik Scheduler, playbook Workflow.

---

## 2. Temel kavramlar

| Kavram | Tanım |
|--------|--------|
| **System job** | Platform geneli (yedek, dizin sync, workflow schedule sync…) — admin |
| **User job** | Tenant / domain scoped — OC, Workflow, müşteri tanımı |
| **Cron ifadesi** | Quartz tabanlı periyot |
| **Hook URL** | Job tetiklenince çağrılan hedef servis |
| **Execution kaydı** | `@job_executions` — izlenebilirlik |

---

## 3. Fonksiyon envanteri

### 3.1 Scheduler servisi

| Yetenek | Durum | Not |
|---------|-------|-----|
| MngScheduler API (`/scheduler/api/v1/`) | ✅ | Gateway üzerinden |
| System jobs (admin) | ✅ | `@scheduled_jobs` |
| User jobs (domain) | ✅ | |
| Cron tetikleme (Quartz) | ✅ | |
| HttpJob — hedef URL çağrısı | ✅ | Keeper service token (OC deseni) |
| expireDate / maxExecutionCount | ✅ | |
| Health / version | ✅ | |

### 3.2 Modül hedefleri (örnekler)

| Hedef | Tetik örneği | Durum |
|-------|--------------|-------|
| **OC WorkItem** | Haftalık checklist | ✅ `from-origin` |
| **Workflow run** | Schedule trigger sync | ✅ `wf-schedule-{id}` |
| **MngAdmin backup** | Gece DB yedek | ✅ system job |
| **Keeper LDAP sync** | Periyodik dizin | 🔶 system job |
| **DI belge üretimi** | Ay sonu rapor | 🔲 D-S |
| **FTP poll (DI dosya)** | Saatlik içe alım | 🔲 F-FILE-INGEST |
| **Monitoring tarama** | Periyodik kontrol | 📋 teklif |
| **Rapor e-posta** | Zamanlanmış export | 🔲 |

### 3.3 UI yüzeyleri

| Yüzey | Rota | Not |
|-------|------|-----|
| OC — Zamanlanmış işler | `/apps/operation-core/admin/scheduled-jobs` | User job yönetimi |
| Workflow publish | *(otomatik)* | Schedule trigger → Scheduler sync |
| System job admin | API / ops | Platform ekibi |

---

## 4. Platform bağlantı haritası ile uyum

SVG ve [platform-tanitimi.md](./platform-tanitimi.md) diyagramında Scheduler:

- **Omurga rozeti** — Keeper · DG · Notifier ile aynı merkez
- **Kehribar kesik oklar** → modül aksiyonu (DI belge, OC WI, Monitoring adımı…)

```text
Scheduler (cron)
    ├─► OC: WorkItem oluştur
    ├─► Workflow: run başlat
    ├─► DI: generate *(plan)*
    ├─► MngAdmin: backup
    └─► Keeper: directory sync
```

---

## 5. Gerçek hayat örnekleri

| # | Senaryo |
|---|---------|
| 1 | Her Pazartesi 08:00 vardiya devri checklist WI |
| 2 | Her gece 02:00 Mongo yedek |
| 3 | Yayınlanan workflow’un cron ile otomatik çalışması |
| 4 | Ayın 1’i 06:00 aylık DI rapor şablonu *(plan)* |
| 5 | SLA tarama job’ı → OC escalation *(plan)* |

---

## 6. Müşteriye net sınırlar

| Beklenti | Gerçek |
|----------|--------|
| «Scheduler = Workflow» | Scheduler **tetikler**; çok adım **Workflow** |
| «Her modül kendi cron’u» | **Merkezi** Scheduler |
| «Zamanlama broşür modülü» | Omurga bileşeni — modüllerle birlikte anlatılır |

---

## 7. Teknik referans (iç)

| Alan | Konum |
|------|--------|
| Servis | `MngScheduler/` |
| Dataset | `@scheduled_jobs`, `@job_executions` |
| OC entegrasyon | [SCHEDULED_WORK_ITEMS.md](../../odak/operationcore/mngoperations/SCHEDULED_WORK_ITEMS.md) |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · v0.1*
