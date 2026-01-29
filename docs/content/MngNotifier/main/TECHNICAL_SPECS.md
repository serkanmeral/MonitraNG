# MngNotifier Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/notifier/api/v1/` (ör. `https://gateway.example.com/notifier/api/v1/notifications/mail`)
- **Kimlik doğrulama:** Mail gönderme endpoint’i şu an AllowAnonymous; Health/Version auth uygulama ayarına bağlı olabilir.
- **Content-Type:** `application/json`.

---

## 1. Health — `api/v1/health`

Sağlık ve hazırlık kontrolleri.

### 1.1 Health check

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health` |
| **Auth** | Yok (AllowAnonymous) |

#### Response (200 OK / 503)

Durum (healthy/degraded/unhealthy), bileşen kontrolleri (örn. RabbitMQ, disk). 503: unhealthy.

### 1.2 Liveness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/live` |
| **Auth** | Yok |

#### Response (200 OK)

`{ "status": "alive", "timestamp": "..." }`

### 1.3 Readiness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/ready` |
| **Auth** | Yok |

#### Response (200 OK / 503)

Hazır/hazır değil; bağımlılık durumları. 503: not ready.

---

## 2. Version — `api/v1/version`

Sürüm bilgisi.

### 2.1 Detaylı sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Product, Version, AssemblyVersion, BuildDate, Company, Copyright, Environment, Runtime, Dependencies.

### 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version/short` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

`{ "version": "..." }` veya sadece string.

---

## 3. Notifications — `api/v1/notifications`

Bildirim gönderimi.

### 3.1 Mail gönder

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/notifications/mail` |
| **Auth** | Yok (AllowAnonymous) |
| **Amaç** | Doğrudan SMTP ile mail gönderir; ileride RabbitMQ kuyruğu planlanıyor. |

#### Request body (SendMailRequest)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `to` | string[] | Evet | Alıcı e-posta adresleri (en az bir). | `["user@example.com"]` |
| `cc` | string[] | Hayır | CC alıcıları. | — |
| `from` | object | Hayır | Gönderen; yoksa appsettings kullanılır. | Aşağıda |
| `subject` | string | Evet | Konu. | `"Konu"` |
| `body` | string | Evet | Metin veya HTML gövde. | — |
| `isHtml` | boolean | Hayır | Gövde HTML ise true. | `true` |

**from** alt alanları: `email` (string), `name` (string, opsiyonel).

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `notificationId` | string | Üretilen bildirim ID’si (örn. GUID). |
| `status` | string | `"sent"` (doğrudan gönderimde). |
| `queuedAt` | string (ISO 8601) | İşlem zamanı. |

#### Hata (400 / 500)

- 400: Request body yok, `to` boş, `subject` veya `body` eksik.
- 500: SMTP/gönderim hatası.

---

Ortak hata yanıtları: 400, 500. Body tipi: `{ "error": "message" }` veya proje standartına uygun ErrorResponseDto.
