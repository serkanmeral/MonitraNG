# MngAdmin Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/admin/api/v1/` (ör. `https://gateway.example.com/admin/api/v1/backup/system`)
- **Kimlik doğrulama:** Şu an BackupController’da Authorize kapatılmış olabilir; production’da JWT/Admin kullanımı planlanır.
- **Content-Type:** `application/json`.

---

## 1. Health — `api/v1/health`

### 1.1 Health check / Liveness / Readiness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health`, `/api/v1/health/live`, `/api/v1/health/ready` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK / 503)

Durum ve bileşen kontrolleri (MongoDB, disk vb.). Ready: bağımlılıklar sağlıklı değilse 503.

---

## 2. Version — `api/v1/version`

### 2.1 Detaylı / 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version`, `/api/v1/version/short` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Product, Version, BuildDate, Runtime, Dependencies / kısa sürüm string.

---

## 3. Backup — `api/v1/backup`

Yedekleme işlemleri: sistem (MongoDB, PostgreSQL) ve domain (MongoDB) backup’ları.

### 3.1 Sistem backup’ı oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/backup/system` |
| **Auth** | Uygulama ayarına bağlı |

#### Request body (BackupRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `databaseType` | string | Hayır | `mongodb` veya `postgresql`. |
| (diğer alanlar) | — | — | Uygulama DTO’suna göre. |

#### Response (200 OK)

BackupResponseDto: backupId, status, startedAt, databaseName, vb. 400: Hata mesajı.

### 3.2 Sistem MongoDB backup’ı

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/backup/system/mongodb` |
| **Auth** | Uygulama ayarına bağlı |

#### Request body

BackupRequestDto (opsiyonel); databaseType mongodb kabul edilir. Body boş veya `{ "databaseType": "mongodb" }` olabilir.

#### Response (200 OK)

BackupResponseDto. 400: ex.Message.

### 3.3 Sistem PostgreSQL backup’ı

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/backup/system/postgresql` |
| **Auth** | Uygulama ayarına bağlı |

#### Request body

BackupRequestDto (opsiyonel); databaseType postgresql. Body boş veya `{ "databaseType": "postgresql" }`.

#### Response (200 OK)

BackupResponseDto. 400: ex.Message.

### 3.4 Domain backup’ı oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/backup/domain/{domainName}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı (örn. mng_ ile başlayan DB’nin domain karşılığı). |

#### Request body

BackupRequestDto (opsiyonel). Domain MongoDB backup’ı alınır.

#### Response (200 OK / 404)

BackupResponseDto. 404: Domain not found. 400: ex.Message.

### 3.5 Backup durumu getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/backup/{backupId}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `backupId` | string | Evet | Backup ID. |

#### Response (200 OK / 404)

BackupResponseDto. 404: Backup not found.

### 3.6 Sistem backup listesi

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/backup/system` |
| **Auth** | Uygulama ayarına bağlı |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `databaseName` | string | Hayır | Filtre: belirli veritabanı. |

#### Response (200 OK)

BackupListResponseDto: backup listesi (items, totalCount vb.).

### 3.7 Domain backup listesi

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/backup/domain/{domainName}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path / Query

`domainName` (path), `databaseName` (query, opsiyonel).

#### Response (200 OK)

BackupListResponseDto.

### 3.8 Full backup

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/backup/full` |
| **Auth** | Uygulama ayarına bağlı |
| **Amaç** | Sistem (MongoDB + PostgreSQL) + tüm domain’ler için sıralı backup. |

#### Request body

Yok (POST body boş veya `{}`).

#### Response (200 OK)

FullBackupResponseDto: systemBackups, domainBackups, successCount, failedCount, totalDuration vb. 400: ex.Message.

---

## Hata yanıtları

- 400: Bad Request (`error` mesajı).
- 404: Backup veya domain bulunamadı.
- 500: Internal server error.

Ortak hata gövdesi: `{ "error": "message" }`.
