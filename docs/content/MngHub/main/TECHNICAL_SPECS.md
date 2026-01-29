# MngHub Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/hub/` — WebSocket `/hub/ws/` veya `/hub/ws/v1`, REST `/hub/api/v1/` (ör. `https://gateway.example.com/hub/api/v1/test/status`)
- **Kimlik doğrulama:** SignalR bağlantısı JWT (query string veya Authorization header) ile doğrulanır. Test/Version REST endpoint’leri uygulama ayarına göre auth gerektirebilir.
- **Content-Type:** `application/json`.

---

## 1. Health — `/health`

Uygulama canlılık kontrolü. Auth gerekmez.

### 1.1 Health check

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/health` |
| **Auth** | Yok |

#### Response (200 OK)

`{ "status": "healthy", "service": "MngHub", "timestamp": "<ISO 8601>" }`

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

Product, Version, AssemblyVersion, BuildDate, Company, Copyright, Environment, Runtime (Framework, OS, MachineName, ProcessorCount), Dependencies vb.

### 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version/simple` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Kısa sürüm string veya `{ "version": "..." }`.

---

## 3. Test / Debug — `api/v1/test`

Bağlantı ve kuyruk durumu, test event yayını. Genelde geliştirme/test ortamında kullanılır.

### 3.1 Bağlantıları listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/test/connections` |
| **Auth** | Uygulama ayarına bağlı |
| **Amaç** | Tüm aktif SignalR bağlantılarını döndürür. |

#### Response (200 OK)

Bağlantı listesi (connectionId, domain, vb.).

### 3.2 Domain’e göre bağlantılar

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/test/connections/domain/{domainName}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Response (200 OK)

İlgili domain’e ait bağlantı listesi.

### 3.3 Tek bağlantı

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/test/connections/{connectionId}` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK / 404)

Tek bağlantı objesi. 404: Connection not found.

### 3.4 Servis durumu

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/test/status` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

`service`, `status`, `timestamp`, `endpoints` (signalR, signalRLegacy, health, connections).

### 3.5 Test domain event yayını

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/test/publish-test-domain-event` |
| **Auth** | Uygulama ayarına bağlı |
| **Amaç** | RabbitMQ’ya örnek bir domain.created event’i yayınlar (test amaçlı). |

#### Response (200 OK)

Başarı mesajı veya event bilgisi.

### 3.6 System queue durumu

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/test/system-queue-status` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Sistem kuyruğu ile ilgili durum bilgisi.

---

## SignalR Hub

Gerçek zamanlı mesajlaşma WebSocket üzerinden `/ws` veya `/ws/v1` (versiyonlu) endpoint’leri ile sağlanır. Bağlantı sırasında JWT geçirilir; domain odaları (`domain.{domainName}`) ve global oda kullanılır. Protokol ve mesaj formatı için [Mimari](../support/architecture/ARCHITECTURE_PLAN.md) ve [Gateway Integration](../support/guides/GATEWAY_INTEGRATION.md) sayfalarına bakınız.

---

Ortak hata yanıtları: 400 (Bad Request), 401 (Unauthorized), 404 (Not Found), 500 (Internal Server Error).
