# MngEngine Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/engine/api/v1/` (ör. `https://gateway.example.com/engine/api/v1/config`)
- **Kimlik doğrulama:** Tüm endpoint’ler JWT token gerektirir.
- **Content-Type:** `application/json`.

---

## 1. Config — `api/v1/config`

### 1.1 Config getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/config` |
| **Auth** | Evet |

#### Response (200 OK)

Konfigürasyon nesnesi (uygulama tanımlı).

### 1.2 Config uygula

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/config` |
| **Auth** | Evet |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `config` | object | Hayır | Uygulanacak konfigürasyon. |

#### Response (200 OK)

Uygulama sonucu.

---

## 2. Job — `api/v1/job`

### 2.1 Job listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/job` |
| **Auth** | Evet |

#### Response (200 OK)

Job listesi (uygulama tanımlı yapı).

### 2.2 Job oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/job` |
| **Auth** | Evet |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `name` | string | Evet | Job adı. | `"job-name"` |
| `cronExpression` | string | Evet | Cron ifadesi. | `"0 0 * * *"` |
| `type` | string | Evet | Job tipi (örn. LinuxHost, WindowsHost). | `"LinuxHost"` |

#### Response (200 OK / 201)

Oluşturulan job nesnesi.

### 2.3 Job güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/v1/job/{id}` |
| **Auth** | Evet |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | string | Evet | Job ID. |

#### Request body

Güncellenecek alanlar (örn. `cronExpression`).

#### Response (200 OK)

Güncellenmiş job. 404: Job bulunamadı.

### 2.4 Job sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/job/{id}` |
| **Auth** | Evet |

#### Response (200 OK / 204 / 404)

Başarı veya 404.

---

## 3. Data — `api/v1/data`

### 3.1 Veri getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/data` |
| **Auth** | Evet |

#### Response (200 OK)

Veri listesi veya toplu veri (uygulama tanımlı).

---

## Hata yanıtları

- 400: Bad Request.
- 401: Unauthorized.
- 404: Job/Config bulunamadı.
- 500: Internal server error.

---

İlgili doküman: [Architecture Guide](../support/architecture/ARCHITECTURE_GUIDE.md).
