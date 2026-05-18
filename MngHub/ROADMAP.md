# MngHub API - Development Roadmap

**Microservice:** Real-time Event Hub & SignalR Gateway  
**Version:** 1.0.1  
**Last Updated:** 11 Ocak 2026

---

## 📊 Genel Durum

| Component | Status | Completion |
|-----------|--------|------------|
| SignalR Hub Infrastructure | ✅ Complete | 100% |
| RabbitMQ Integration | ✅ Complete | 100% |
| Domain-based Event Routing | ✅ Complete | 100% |
| System Event Listener | ✅ Complete | 100% |
| Group/User Event Listener | ✅ Complete | 100% |
| JWT Authentication | ✅ Complete | 100% |
| Code Optimization | ✅ Complete | 100% |
| Docker Support | ✅ Complete | 100% |
| Monitoring & Metrics | 📋 Planned | 0% |
| Error Handling Improvements | 📋 Planned | 0% |
| Performance Optimizations | 📋 Planned | 0% |
| HTTPS/WSS Support | 📋 Planned | 0% |
| API Versioning | 📋 Planned | 0% |

**Overall Progress:** **80%** of Core Features  
**Chat Room (F2):** planlama tamam; kod + Docker doğrulama sürüyor — bkz. `docs/content/chat_room/BACKEND_DOCKER_STEPS.md`

---

## 💬 Chat Room (F2) — backend planı

| Adım | Açıklama |
|------|-----------|
| DG → RMQ | `cht_*` için `publish_mode != none` iken `mngdatagateway.events`, routing **`{domainSegment}.{eventType}`** (örn. `meral.datacreatedevent`). `domainSegment` pratikte DG’nin kullandığı **domain adı** ile uyumlu; bkz. `docs/content/chat_room/CHAT_ROOM_ROADMAP.md` **§3.2b**. |
| Hub (3A) | `RoutingKeyHelper`: `{domainId}.*` + **`{domainName}.*`** + `domain.{domainName}.#`; `MessageRouter` → domain SignalR grubu **`ReceiveMessage` (`MessageDto`)**. Chat ayırt etmek istemcide **`datasetName === 'cht_messages'`** filtresi. |
| Sonraki (3B) | İsteğe bağlı: oda bazlı SignalR grup (`JoinChatRoom`), mention için hedefli client mesajı; payload / routing daraltma. |

**Docker / yayın sırası:** [BACKEND_DOCKER_STEPS.md](../docs/content/chat_room/BACKEND_DOCKER_STEPS.md)  
**Ürün / şema:** [CHAT_ROOM_ROADMAP.md](../docs/content/chat_room/CHAT_ROOM_ROADMAP.md)

---

## ✅ TAMAMLANAN ÖZELLİKLER

### 1. SignalR Hub Infrastructure - ✅ TAMAMLANDI

**Core Components:**
- ✅ `NotificationHub` - SignalR Hub implementasyonu
- ✅ `MessageRouter` - Message routing servisi (25 Aralık 2025)
- ✅ `ConnectionManager` - Connection lifecycle yönetimi
- ✅ Domain-based room yönetimi (`domain.{domainName}`)
- ✅ Global room yönetimi (`global`)
- ✅ JWT token validation (query string + Authorization header)
- ✅ Automatic reconnection support

**Features:**
- ✅ Real-time message broadcasting
- ✅ Domain isolation (her domain sadece kendi event'lerini görür)
- ✅ Connection tracking ve yönetimi
- ✅ Graceful connection disposal handling

**Tarih:** 23 Aralık 2025

---

### 2. RabbitMQ Integration - ✅ TAMAMLANDI

**Components:**
- ✅ `RabbitMqConsumerService` - RabbitMQ consumer yönetimi
- ✅ `SystemEventListenerService` - System event listener (background service)
- ✅ `GroupEventListenerService` - Group/User event listener (background service)

**Features:**
- ✅ Dynamic queue creation per SignalR connection
- ✅ Routing key pattern matching (`global.*`, `system.#`, `domain.{name}.#`, `{domainId}.*`)
- ✅ Exchange binding (`mng.topics`, `mngkeeper.events`)
- ✅ Automatic reconnection on connection loss
- ✅ Message acknowledgment handling

**Routing Patterns:**
- ✅ `global.*` - Global events (tüm kullanıcılara)
- ✅ `system.#` - System events (multi-segment routing keys)
- ✅ `domain.{domainName}.#` - Domain-specific events (by name)
- ✅ `{domainId}.*` - Domain-specific events (by ID, MngKeeper format)

**Tarih:** 23 Aralık 2025

---

### 3. API Gateway Integration (v1.0.1) - ✅ TAMAMLANDI (11 Ocak 2026)

**Yapılan Değişiklikler:**
- ✅ CORS yapılandırması kaldırıldı (Gateway'de merkezi yönetim)
- ✅ Internal network'te çalışıyor (external exposure yok)
- ✅ SignalR WebSocket bağlantıları Gateway üzerinden yönetiliyor

**Faydalar:**
- ✅ Merkezi CORS yönetimi (Gateway'de)
- ✅ Servis basitleştirildi (CORS kaldırıldı)
- ✅ API Gateway pattern'ine uygun mimari
- ✅ SignalR bağlantıları Gateway üzerinden güvenli

**Gateway URL:**
- Production: `https://api.monitra.local/hub/ws/*`
- Development: `https://localhost:5040/hub/ws/*`

**Internal URL (Docker network):**
- `http://mnghub:5020/ws/*`

**Health Endpoint:**
- `/health` (mevcut, değişmedi)

---

### 4. Domain-based Event Routing - ✅ TAMAMLANDI

**Features:**
- ✅ Domain isolation garantisi
- ✅ JWT token'dan domain bilgisi çıkarma
- ✅ Domain room mapping
- ✅ Routing key validation ve filtering
- ✅ Secure message routing (sadece ilgili domain'e gönderim)

**Security:**
- ✅ JWT token validation (MngKeeper API ile)
- ✅ Domain claim extraction (`domain_name`, `domain_id`)
- ✅ Fallback mechanism (iss claim'den realm name çıkarma)
- ✅ Connection rejection for invalid tokens

**Tarih:** 23 Aralık 2025

---

### 4. System Event Listener - ✅ TAMAMLANDI

**Background Service:**
- ✅ `SystemEventListenerService` - IHostedService implementasyonu
- ✅ Durable queue: `mnghub.system.listener`
- ✅ Pattern: `system.#`
- ✅ Exchange: `mng.topics`
- ✅ Domain created event handling

**Features:**
- ✅ Automatic startup on MngHub initialization
- ✅ Independent from SignalR client connections
- ✅ Console logging for system events
- ✅ Automatic reconnection on RabbitMQ connection loss

**Tarih:** 23 Aralık 2025

---

### 5. Group/User Event Listener - ✅ TAMAMLANDI

**Background Service:**
- ✅ `GroupEventListenerService` - IHostedService implementasyonu
- ✅ Durable queue: `mnghub.group.listener`
- ✅ Pattern: `#` (wildcard - tüm routing keys)
- ✅ Exchange: `mngkeeper.events`
- ✅ Console logging for group/user events

**Features:**
- ✅ Automatic startup on MngHub initialization
- ✅ Independent from SignalR client connections
- ✅ Console logging for monitoring
- ✅ Automatic reconnection on RabbitMQ connection loss

**Note:** SignalR broadcasting is handled by `NotificationHub` to avoid duplicate messages.

**Tarih:** 25 Aralık 2025

---

### 6. JWT Authentication - ✅ TAMAMLANDI

**Components:**
- ✅ `JwtValidatorService` - JWT token validation
- ✅ Token extraction from query string and Authorization header
- ✅ `HttpContextExtensions.ExtractJwtToken()` - Extension method (25 Aralık 2025)

**Features:**
- ✅ Token validation via MngKeeper API
- ✅ Domain claim extraction
- ✅ Fallback mechanism for missing claims
- ✅ Connection rejection for invalid tokens

**Tarih:** 23 Aralık 2025

---

### 7. Code Optimization - ✅ TAMAMLANDI

**Refactoring:**
- ✅ `MessageDto.Create()` - Factory method (25 Aralık 2025)
- ✅ `HttpContextExtensions` - Token extraction extension (25 Aralık 2025)
- ✅ `MessageRouter` - Centralized routing logic (25 Aralık 2025)
- ✅ `RabbitMqConsumerService` - Helper methods for exchange declaration and queue binding (25 Aralık 2025)
- ✅ Reduced code duplication
- ✅ Improved maintainability

**Tarih:** 25 Aralık 2025

---

### 8. Docker Support - ✅ TAMAMLANDI

**Components:**
- ✅ `Dockerfile` - Multi-stage build
- ✅ `docker-compose.yml` integration
- ✅ Health check endpoint (`/health`)
- ✅ Environment variable configuration
- ✅ Network integration (`mng_common_mng_network`)

**Features:**
- ✅ Port mapping: `5020:5020`
- ✅ Automatic health checks
- ✅ RabbitMQ connection via Docker network
- ✅ CORS configuration via environment variables

**Tarih:** 25 Aralık 2025

---

## 🔄 DEVAM EDEN İŞLER

### JWT Token Claim Sorunu - Geçici Çözüm Uygulandı

**Durum:** ⚠️ Geçici çözüm aktif, kalıcı çözüm bekleniyor

**Geçici Çözüm:**
- ✅ `iss` claim'den realm name çıkarma
- ✅ `preferred_username`'den domain name çıkarma
- ✅ Fallback mechanism in `JwtValidatorService`

**Kalıcı Çözüm (MngKeeper'da yapılacak):**
- [ ] Mapper yapılandırması kontrolü
- [ ] `domain_name` ve `domain_id` claim'lerinin token'a eklenmesi
- [ ] Mapper endpoint'inin doğru çalıştığının doğrulanması
- [ ] Fallback mekanizmasının kaldırılması (kalıcı çözüm sonrası)

**Öncelik:** Orta

---

## 🎯 GELECEK PLANLAR

### Phase 1: Monitoring & Metrics - YÜKSEK ÖNCELİK

**Amaç:** MngHub'ın sağlık durumunu ve performansını izlemek

**Gereksinimler:**
- [ ] Connection count metrics (active connections per domain)
- [ ] Message throughput metrics (messages per second)
- [ ] RabbitMQ queue depth monitoring
- [ ] SignalR connection health tracking
- [ ] Event type distribution metrics
- [ ] Error rate tracking

**API Endpoints:**
- [ ] `GET /api/metrics/connections` - Active connection count
- [ ] `GET /api/metrics/messages` - Message statistics
- [ ] `GET /api/metrics/health` - Detailed health check
- [ ] `GET /api/metrics/rabbitmq` - RabbitMQ connection status

**Öncelik:** Yüksek

---

### Phase 2: Error Handling Improvements - ORTA ÖNCELİK

**Amaç:** Hata durumlarını daha iyi yönetmek ve sistemin dayanıklılığını artırmak

**Gereksinimler:**
- [ ] Dead Letter Queue (DLQ) implementasyonu
- [ ] Retry mechanism iyileştirmeleri
- [ ] Circuit breaker pattern
- [ ] Error notification system
- [ ] Graceful degradation

**Features:**
- [ ] Failed message retry with exponential backoff
- [ ] DLQ for permanently failed messages
- [ ] Error notification via SignalR (admin users)
- [ ] Automatic recovery mechanisms

**Öncelik:** Orta

---

### Phase 3: Performance Optimizations - ORTA ÖNCELİK

**Amaç:** Sistem performansını artırmak ve kaynak kullanımını optimize etmek

**Gereksinimler:**
- [ ] Message batching
- [ ] Connection pooling optimizasyonları
- [ ] Memory usage optimization
- [ ] CPU usage optimization
- [ ] Network bandwidth optimization

**Features:**
- [ ] Batch message processing
- [ ] Connection pool management
- [ ] Memory-efficient message serialization
- [ ] Async/await optimizations

**Öncelik:** Orta

---

### Phase 4: Advanced Features - DÜŞÜK ÖNCELİK

**Amaç:** Gelişmiş özellikler eklemek

**Gereksinimler:**
- [ ] Message filtering (client-side subscription)
- [ ] Event history (temporary message storage)
- [ ] Message replay functionality
- [ ] Custom routing rules
- [ ] Event transformation

**Features:**
- [ ] Client-side message filtering
- [ ] In-memory message cache (last N messages)
- [ ] Message replay API
- [ ] Custom routing rule engine
- [ ] Message transformation pipeline

**Öncelik:** Düşük

---

### Phase 5: Security Enhancements - YÜKSEK ÖNCELİK

**Amaç:** Güvenlik iyileştirmeleri

**Gereksinimler:**
- [ ] Rate limiting per connection
- [ ] Message size limits
- [ ] Connection throttling
- [ ] IP-based access control
- [ ] Audit logging

**Features:**
- [ ] Per-connection rate limiting
- [ ] Message size validation
- [ ] Connection count limits per domain
- [ ] IP whitelist/blacklist
- [ ] Security event logging

**Öncelik:** Yüksek

---

### Phase 6: HTTPS/WSS Support - YÜKSEK ÖNCELİK

**Amaç:** Production ortamı için güvenli iletişim sağlamak

**Gereksinimler:**
- [ ] HTTPS endpoint configuration
- [ ] WSS (WebSocket Secure) support for SignalR
- [ ] SSL/TLS certificate management
- [ ] Certificate auto-generation (development)
- [ ] Certificate loading from file (production)
- [ ] HTTP to HTTPS redirection
- [ ] Mixed content handling

**Features:**
- [ ] Kestrel HTTPS configuration
- [ ] SignalR WSS transport support
- [ ] Self-signed certificate generation (development)
- [ ] Certificate loading from `appsettings.json` or environment variables
- [ ] Automatic HTTP to HTTPS redirection
- [ ] Certificate validation and renewal

**Configuration:**
- [ ] `MngHubSettings.CertificateSettings` enhancement
- [ ] Environment variable support for certificates
- [ ] Docker volume mounting for certificates

**API Endpoints:**
- [ ] HTTPS health check endpoint
- [ ] Certificate status endpoint

**Öncelik:** Yüksek (Production için kritik)

---

### Phase 7: API Versioning - ✅ TAMAMLANDI

**Amaç:** API değişikliklerini yönetmek ve geriye dönük uyumluluğu sağlamak

**Gereksinimler:**
- ✅ API versioning strategy (URL-based, Header-based, Query-based)
- ✅ Version negotiation
- [ ] Deprecated endpoint handling (gelecekte eklenecek)
- [ ] Version documentation (gelecekte eklenecek)
- [ ] Migration guide (gelecekte eklenecek)

**Features:**
- ✅ URL-based versioning (`/api/v1/`, `/api/v2/`)
- ✅ Header-based versioning (`Api-Version: 1.0`)
- ✅ Query parameter versioning (`?version=1.0`)
- ✅ Version negotiation middleware
- ✅ Default version (v1.0) when unspecified
- ✅ API version reporting in responses
- [ ] Deprecated endpoint warnings (gelecekte eklenecek)
- [ ] Version-specific documentation (gelecekte eklenecek)

**API Structure:**
- ✅ `GET /api/v1/test/connections`
- ✅ `GET /api/v1/test/status`
- ✅ SignalR Hub: `/ws/v1` (legacy `/ws` still supported)
- [ ] `GET /api/v1/metrics/connections` (Phase 1'de eklenecek)
- [ ] `GET /api/v2/...` (gelecekte eklenecek)

**Implementation:**
- ✅ `Asp.Versioning.Mvc` NuGet package (v9.0.0)
- ✅ `Asp.Versioning.Mvc.ApiExplorer` for OpenAPI integration
- ✅ `TestController` v1.0 olarak işaretlendi
- ✅ SignalR Hub endpoint versiyonlandı (`/ws/v1`)

**Documentation:**
- [ ] API version changelog (gelecekte eklenecek)
- [ ] Migration guide between versions (gelecekte eklenecek)
- [ ] Deprecation notices (gelecekte eklenecek)

**Tarih:** 25 Aralık 2025

**Öncelik:** Orta (API stabilizasyonu sonrası)

---

## 📋 TEKNİK DETAYLAR

### Architecture

**Layers:**
- **Presentation:** `MngHub.Api` - API endpoints, SignalR Hub
- **Application:** `MngHub.Application` - Services, DTOs, Configuration
- **Domain:** `MngHub.Domain` - Entities, Constants, Exceptions
- **Infrastructure:** `MngHub.Infrastructure` - RabbitMQ, SignalR, JWT
- **Persistence:** `MngHub.Persistence` - (Future: Database persistence if needed)

**Key Services:**
- `NotificationHub` - SignalR Hub
- `MessageRouter` - Message routing service
- `RabbitMqConsumerService` - RabbitMQ consumer management
- `SystemEventListenerService` - System event listener
- `GroupEventListenerService` - Group/User event listener
- `ConnectionManager` - Connection lifecycle management
- `JwtValidatorService` - JWT token validation

---

### RabbitMQ Structure

**Exchanges:**
- `mng.topics` - System and domain events
- `mngkeeper.events` - User and group CRUD events

**Routing Key Patterns:**
- `global.*` - Global events
- `system.#` - System events (multi-segment)
- `domain.{domainName}.#` - Domain events (by name)
- `{domainId}.*` - Domain events (by ID)

**Queues:**
- `mnghub.{connectionId}` - Per-connection queue (exclusive, auto-delete)
- `mnghub.system.listener` - System event listener queue (durable)
- `mnghub.group.listener` - Group/User event listener queue (durable)

---

### SignalR Structure

**Endpoints:**
- `/ws` - SignalR Hub endpoint
- `/ws/negotiate` - SignalR negotiation test endpoint

**Rooms:**
- `global` - Global room (all users)
- `domain.{domainName}` - Domain-specific room

**Message Format:**
```json
{
  "routingKey": "string",
  "message": "object",
  "timestamp": "DateTime"
}
```

---

## 🔗 İlgili Servisler

- **MngKeeper:** Domain creation, User/Group CRUD, Event publishing
- **RabbitMQ:** Message broker (`mng.topics`, `mngkeeper.events`)
- **UI (Mng.Ui):** SignalR client, Event display

---

## 📚 İlgili Dosyalar

### Core Services
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/SignalR/NotificationHub.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/SignalR/MessageRouter.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/RabbitMq/RabbitMqConsumerService.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/SystemEventListener/SystemEventListenerService.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/GroupEventListener/GroupEventListenerService.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/Connection/ConnectionManager.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/Jwt/JwtValidatorService.cs`

### Configuration
- `MngHub/Core/MngHub.Application/Configuration/MngHubSettings.cs`
- `MngHub/Core/MngHub.Domain/Constants/RoomNames.cs`
- `MngHub/Presentation/MngHub.Api/appsettings.json`

### Extensions
- `MngHub/Infrastructure/MngHub.Infrastructure/Extensions/HttpContextExtensions.cs`

### DTOs
- `MngHub/Core/MngHub.Application/DTOs/Common/MessageDto.cs`
- `MngHub/Core/MngHub.Application/DTOs/Common/ConnectionInfoDto.cs`

### API
- `MngHub/Presentation/MngHub.Api/Controllers/TestController.cs`
- `MngHub/Presentation/MngHub.Api/Program.cs`

### Docker
- `MngHub/Presentation/MngHub.Api/Dockerfile`
- `ApplicationResources/mng_apps/docker-compose.yml`

### Documentation
- `MngHub/README.md`
- `MngHub/docs/ARCHITECTURE_PLAN.md`
- `MngHub/docs/CURRENT_STATUS.md`
- `MngHub/docs/ROADMAP.md` (bu dosya)

---

**Son Güncelleme:** 11 Ocak 2026  
**Durum:** ✅ Core Features Complete, API Gateway Integration Complete, Monitoring & Security Enhancements Planned

