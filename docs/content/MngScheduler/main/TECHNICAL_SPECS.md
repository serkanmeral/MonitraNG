# MngScheduler Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/scheduler/api/v1/` (ör. `https://gateway.example.com/scheduler/api/v1/system/jobs`)
- **Kimlik doğrulama:** System job endpoint’leri Admin yetkisi gerektirir; User job endpoint’leri domain + kullanıcı bazlıdır. Health/Version auth uygulama ayarına bağlı.
- **Content-Type:** `application/json`.

---

## 1. Health — `api/v1/health`

### 1.1 Health check

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health` |
| **Auth** | Yok |

#### Response (200 OK / 503)

Durum, bileşen kontrolleri (MongoDB, RabbitMQ, disk vb.). 503: unhealthy.

### 1.2 Liveness / 1.3 Readiness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/live`, `/api/v1/health/ready` |
| **Auth** | Yok |

Readiness: bağımlılıklar sağlıklı değilse 503.

---

## 2. Version — `api/v1/version`

### 2.1 Detaylı / 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version`, `/api/v1/version/short` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Detaylı: Product, Version, BuildDate, Runtime, Dependencies. Kısa: `{ "version": "..." }`.

---

## 3. System Jobs — `api/v1/system/jobs`

Sistem seviyesi zamanlanmış işler (mng_keeper.@scheduled_jobs). **Sadece Admin** erişebilir.

### 3.1 Tüm system job’ları listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/system/jobs` |
| **Auth** | Evet (Admin) |

#### Response (200 OK)

ScheduledJob dizisi. 403: Forbid (Admin değil).

### 3.2 Aktif system job’ları listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/system/jobs/active` |
| **Auth** | Evet (Admin) |

#### Response (200 OK)

Aktif ScheduledJob dizisi.

### 3.3 System job getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/system/jobs/{jobId}` |
| **Auth** | Evet (Admin) |

#### Response (200 OK / 404)

Tek ScheduledJob. 404: Job not found.

### 3.4 System job oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/system/jobs` |
| **Auth** | Evet (Admin) |

#### Request body (ScheduledJob)

JobId, JobType ("System"), Name, Description, CronExpression, EndpointUrl, HttpMethod (GET/POST), Headers, Payload, IsActive, RetryPolicy, TimeoutSeconds, StartDate, ExpireDate, MaxExecutionCount vb. (domain entity ile uyumlu).

#### Response (201 Created)

Oluşturulan ScheduledJob. 400: Validasyon. 403: Admin değil.

### 3.5 System job güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/v1/system/jobs/{jobId}` |
| **Auth** | Evet (Admin) |

#### Request body

ScheduledJob; URL’deki jobId ile body’deki JobId aynı olmalı.

#### Response (200 OK / 400 / 404)

### 3.6 System job sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/system/jobs/{jobId}` |
| **Auth** | Evet (Admin) |

#### Response (200 OK / 404)

### 3.7 System job çalıştırma geçmişi

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/system/jobs/{jobId}/executions` |
| **Auth** | Evet (Admin) |

#### Response (200 OK)

JobExecution dizisi (ExecutionId, JobId, Status, ExecutedAt, ResponseTimeMs, ResponseCode, ErrorMessage vb.).

---

## 4. User Jobs — `api/v1/user/jobs`

Domain bazlı kullanıcı işleri (domain veritabanında @scheduled_jobs). JWT’deki domain’e göre filtrelenir.

### 4.1–4.7 User job endpoint’leri

Path’ler `api/v1/system/jobs` yerine `api/v1/user/jobs` kullanır; metod ve davranış System Jobs ile paraleldir.

- `GET /api/v1/user/jobs` — Tüm (mevcut domain’e ait) user job’lar.
- `GET /api/v1/user/jobs/active` — Aktif user job’lar.
- `GET /api/v1/user/jobs/{jobId}` — Tekil job.
- `POST /api/v1/user/jobs` — Yeni user job (domain JWT’den).
- `PUT /api/v1/user/jobs/{jobId}` — Güncelle.
- `DELETE /api/v1/user/jobs/{jobId}` — Sil.
- `GET /api/v1/user/jobs/{jobId}/executions` — Çalıştırma geçmişi.

Request/response şemaları System Jobs ile aynı (ScheduledJob, JobExecution). Yetki: ilgili domain kullanıcısı.

---

## Hata yanıtları

- 400: Validasyon / Bad Request (`error` mesajı).
- 401: Unauthorized.
- 403: Forbid (örn. System job için Admin değil).
- 404: Job/Backup not found.
- 500: Internal server error.

Ortak hata gövdesi: `{ "error": "message" }`.
