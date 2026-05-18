# MngReactor Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/reactor/api/v1/` (ör. `https://gateway.example.com/reactor/api/v1/asset/tree`)
- **Kimlik doğrulama:** Tüm endpoint’ler `Authorization: Bearer <access_token>` gerektirir (auth/token hariç).
- **Content-Type:** `application/json`.

---

## 1. Asset — `api/v1/asset`

### 1.1 Asset ağacı

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/asset/tree` |
| **Auth** | Evet |

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `assets` | array | Ağaç yapısında asset listesi (id, name, type, children). |

---

## 2. Engine — `api/v1/engine`

### 2.1 Engine asset’leri

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/engine/assets` |
| **Auth** | Evet |

#### Response (200 OK)

`{ "assets": [ { "id", "name", ... } ] }`

### 2.2 Engine bilgisi

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/engine` |
| **Auth** | Evet |

#### Response (200 OK)

`{ "engines": [] }`

---

## 3. Data — `api/v1/data`

### 3.1 Veri getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/data` |
| **Auth** | Evet |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `filter` | string | Hayır | MongoDB filter JSON. |

#### Response (200 OK)

`{ "data": [], "total": number }`

### 3.2 Veri işle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data` |
| **Auth** | Evet |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `operation` | string | Evet | Örn. `"process"`. |
| `data` | object | Hayır | İşlenecek veri. |

#### Response (200 OK)

İşlem sonucu (uygulama tanımlı).

---

## 4. Auth — `api/v1/auth`

### 4.1 Token al

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/auth/token` |
| **Auth** | Yok |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `username` | string | Evet | Kullanıcı adı. |
| `password` | string | Evet | Şifre. |

#### Response (200 OK)

`{ "token": "jwt_token_here" }`

---

## 5. MQTT — `api/v1/mqtt`

### 5.1 Mesaj yayınla

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/mqtt/publish` |
| **Auth** | Evet |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `topic` | string | Evet | MQTT konusu. |
| `message` | string | Evet | Mesaj içeriği. |

#### Response (200 OK)

Yayınlama sonucu (uygulama tanımlı).

---

## 6. Health — `api/v1/health`

### 6.1 Sağlık kontrolü

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health` |
| **Auth** | Hayır |

#### Response (200 OK)

`{ "status": "healthy", "timestamp": "<ISO 8601>", "checks": {} }`

### 6.2 Liveness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/live` |
| **Auth** | Hayır |

#### Response (200 OK)

`{ "status": "alive", "timestamp": "<ISO 8601>" }`

### 6.3 Readiness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/ready` |
| **Auth** | Hayır |

#### Response (200 OK)

`{ "status": "ready", "timestamp": "<ISO 8601>" }`

---

## 7. Version — `api/v1/version`

### 7.1 Sürüm bilgisi

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

`{ "version": "1.0.0", "buildDate": "<ISO 8601>" }`

---

## Hata yanıtları

- 400: `{ "error": "Invalid request", "message": "..." }`
- 401: `{ "error": "Unauthorized", "message": "Invalid or missing token" }`
- 500: `{ "error": "Internal server error", "message": "..." }`

---

İlgili dokümanlar: [Architecture Guide](../support/architecture/ARCHITECTURE_GUIDE.md), [Usage Guide](../support/guides/USAGE_GUIDE.md), [Configuration](../support/guides/CONFIGURATION.md).
