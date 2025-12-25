# MngHub - Mevcut Durum ve Son Çalışmalar

**Tarih:** 23 Aralık 2025  
**Durum:** ⚠️ JWT Token Claim Sorunu - Geçici Çözüm Uygulandı, Kalıcı Çözüm MngKeeper'da Gerekli

---

## 📋 Son Yapılan İşler

### 1. System Event Listener Service Implementasyonu

**Problem:** Domain oluşturulduğunda MngHub'da log görünmüyordu çünkü RabbitMQ subscription'ları sadece SignalR client bağlantıları olduğunda oluşturuluyordu.

**Çözüm:** `SystemEventListenerService` adında bir `IHostedService` implementasyonu eklendi. Bu servis:
- MngHub başladığında otomatik olarak RabbitMQ'ya bağlanır
- `mnghub.system.listener` adında durable bir queue oluşturur
- `system.#` routing key pattern'ine subscribe olur
- System event'lerini (özellikle `system.mngkeeper.domain.created`) yakalar ve loglar
- SignalR client bağlantılarından bağımsız çalışır

**Dosya:** `MngHub/Infrastructure/MngHub.Infrastructure/Services/SystemEventListener/SystemEventListenerService.cs`

### 2. RabbitMQ Routing Key Pattern Düzeltmesi

**Problem:** `system.*` pattern'i sadece tek segment routing key'leri eşleştiriyordu (örn: `system.test`). `system.mngkeeper.domain.created` gibi çok segmentli routing key'ler eşleşmiyordu.

**Çözüm:** 
- `system.*` → `system.#` olarak değiştirildi
- `#` wildcard'ı sıfır veya daha fazla segment eşleştirir
- Değişiklik yapılan dosyalar:
  - `SystemEventListenerService.cs` - `SystemRoutingKeyPattern = "system.#"`
  - `RoomNames.cs` - `RoutingKeyPatterns.System = "system.#"`
  - `NotificationHub.cs` - System routing pattern güncellendi

### 3. Kod Temizliği ve Optimizasyon

**Yapılan Temizlikler:**

#### SystemEventListenerService
- ✅ Gereksiz debug logları kaldırıldı
- ✅ Periyodik status check (her 5 dakikada bir) kaldırıldı
- ✅ Gereksiz yorumlar temizlendi
- ✅ Log seviyeleri optimize edildi (bazı Information → Debug)

#### RabbitMqConsumerService
- ✅ Her mesaj için Information level log → Debug level'a alındı
- ✅ Gereksiz "Message processed" log'u kaldırıldı

#### NotificationHub
- ✅ Domain created event için gereksiz parsing kodu kaldırıldı (SystemEventListenerService'de zaten var)
- ✅ Gereksiz yorumlar temizlendi

#### TestController
- ✅ `system-queue-status` endpoint'indeki test mesajı publish etme kısmı kaldırıldı
- ✅ Endpoint sadeleştirildi, sadece queue bilgilerini döndürüyor

### 4. Test Dosyaları Temizliği

**Yapılanlar:**
- ✅ Gereksiz Node-RED dosyaları kaldırıldı:
  - `NODE-RED-KURULUM.md`
  - `WEBSOCKET-NODE-KURULUM.md`
  - `node-red-signalr-flow.json`
  - `node-red-signalr-http-flow.json`
  - `node-red-signalr-simple-flow.json`

### 5. HTML Test Sayfası Geliştirmeleri

**Yapılanlar:**
- ✅ Domain created event'leri için özel görsel gösterim eklendi
- ✅ Routing key'e göre mesaj kategorileri (System, Domain, Global)
- ✅ İstatistikler paneli eklendi (toplam mesaj, event sayıları)
- ✅ Detaylı console logging eklendi (debug için)
- ✅ Domain created event'lerinde otomatik bilgi güncelleme

**Dosya:** `MngHub/tests/test-signalr.html`

**Erişim:**
- Direkt: `http://localhost:5020/tests/test-signalr.html`
- Kısayol: `http://localhost:5020/test` (redirect)

### 6. JWT Validator Fallback Mekanizması

**Problem:** JWT token'larda `domain_name` ve `domain_id` claim'leri eksik. Bu claim'ler MngKeeper'da mapper yapılandırması ile ekleniyor, ancak henüz yapılandırılmamış domain'ler için token geçersiz oluyordu.

**Geçici Çözüm:** JwtValidatorService'e fallback mekanizması eklendi:
- `iss` claim'inden realm name çıkarılıyor (örn: `http://localhost:8080/realms/test-domain-202512234` → `test-domain-202512234`)
- Eğer hala bulunamazsa `preferred_username`'den çıkarılıyor (örn: `test-domain-202512234_admin` → `test-domain-202512234`)

**Dosya:** `MngHub/Infrastructure/MngHub.Infrastructure/Services/Jwt/JwtValidatorService.cs`

**Not:** Bu geçici bir çözümdür. Kalıcı çözüm için MngKeeper'da mapper'ları yapılandırmak gerekiyor:
```http
POST https://localhost:5001/api/admin/realms/{domainName}/configure-mappers
```

### 7. Log Seviyeleri İyileştirmeleri

**Yapılanlar:**
- ✅ NotificationHub'da mesaj routing logları: Debug → Information
- ✅ RabbitMqConsumerService'de mesaj alma logları: Debug → Information
- ✅ HTML sayfasında detaylı console logging eklendi

---

## 🏗️ Mevcut Mimari

### Ana Bileşenler

1. **NotificationHub** (`Services/SignalR/NotificationHub.cs`)
   - SignalR Hub implementasyonu
   - JWT token validation
   - Domain-based room yönetimi
   - RabbitMQ mesajlarını SignalR client'lara yönlendirme

2. **RabbitMqConsumerService** (`Services/RabbitMq/RabbitMqConsumerService.cs`)
   - SignalR client bağlantıları için RabbitMQ subscription yönetimi
   - Her client için ayrı queue oluşturur
   - Routing key pattern'lere göre mesajları filtreler

3. **SystemEventListenerService** (`Services/SystemEventListener/SystemEventListenerService.cs`)
   - Background service (IHostedService)
   - System event'lerini yakalamak için sürekli çalışır
   - `system.#` pattern'ine subscribe
   - Domain created event'lerini özel olarak işler

4. **ConnectionManager** (`Services/Connection/ConnectionManager.cs`)
   - SignalR connection lifecycle yönetimi
   - Domain-based room mapping

5. **JwtValidatorService** (`Services/Jwt/JwtValidatorService.cs`)
   - JWT token validation (MngKeeper API ile)

### RabbitMQ Yapısı

**Exchange:** `mng.topics` (Topic Exchange)

**Routing Key Patterns:**
- `global.*` - Global event'ler (tüm kullanıcılara)
- `system.#` - System event'ler (çok segmentli routing key'ler dahil)
- `domain.{domainName}.#` - Domain-specific event'ler
- `{domainId}.*` - DomainId-based event'ler (MngKeeper EventPublisher formatı)

**Queues:**
- `mnghub.system.listener` - System event'ler için durable queue (SystemEventListenerService)
- `mnghub.{connectionId}` - Her SignalR client için geçici queue (RabbitMqConsumerService)

### SignalR Room Yapısı

- **Global Room:** `global` - Tüm kullanıcılar
- **Domain Room:** `domain.{domainName}` - Domain-specific kullanıcılar

---

## 🔍 Önemli Notlar

### System Event Listener

- Servis MngHub başladığında otomatik olarak başlar
- Queue durable olduğu için servis restart olsa bile mesajlar kaybolmaz
- `system.#` pattern'i şu routing key'leri eşleştirir:
  - `system.test`
  - `system.mngkeeper.domain.created`
  - `system.mngkeeper.domain.deleted`
  - vb.

### Domain Created Event Flow

1. MngKeeper domain oluşturur
2. `PublishDomainCreatedEventStep` RabbitMQ'ya `system.mngkeeper.domain.created` routing key'i ile mesaj publish eder
3. SystemEventListenerService mesajı yakalar ve loglar
4. NotificationHub'a bağlı client'lar varsa, onlara da mesaj gönderilir

### Log Seviyeleri

- **Information:** System event'lerin alındığı, domain created event'lerin detayları
- **Debug:** Mesaj routing, queue binding, connection durumu
- **Warning:** Connection kaybı, consumer shutdown
- **Error:** Mesaj işleme hataları, connection hataları

---

## 🧪 Test Endpoint'leri

### TestController Endpoints

- `GET /api/test/status` - Servis durumu
- `GET /api/test/connections` - Aktif bağlantılar
- `GET /api/test/connections/{connectionId}` - Belirli bir bağlantı
- `GET /api/test/connections/domain/{domainName}` - Domain'e göre bağlantılar
- `GET /api/test/system-queue-status` - System event listener queue durumu
- `POST /api/test/publish-test-domain-event` - Test domain created event publish etme

### Test Senaryoları

1. **Domain Oluşturma Testi:**
   ```bash
   # MngKeeper'da domain oluştur
   # MngHub loglarında şu mesajı görmelisin:
   # [INF] Domain created event received. EventId: ..., DomainName: ..., DomainId: ...
   ```

2. **System Queue Status:**
   ```bash
   GET http://localhost:5020/api/test/system-queue-status
   # Response: queueName, messageCount, consumerCount, routingKeyPattern
   ```

3. **Test Event Publish:**
   ```bash
   POST http://localhost:5020/api/test/publish-test-domain-event
   # Test domain created event'i RabbitMQ'ya publish eder
   ```

---

## 📝 Bilinen Sorunlar

### 1. JWT Token'da Eksik Claim'ler ⚠️

**Sorun:** JWT token'larda `domain_name` ve `domain_id` claim'leri eksik. Bu claim'ler MngKeeper'da mapper yapılandırması ile ekleniyor.

**Geçici Çözüm:** JwtValidatorService'de fallback mekanizması eklendi. `iss` claim'inden veya `preferred_username`'den domain name çıkarılıyor.

**Kalıcı Çözüm:** MngKeeper'da mapper'ları yapılandırmak gerekiyor:
```http
POST https://localhost:5001/api/admin/realms/{domainName}/configure-mappers
```

**Etkilenen Dosyalar:**
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/Jwt/JwtValidatorService.cs`

**Sonraki Adım:** MngKeeper session'ında mapper yapılandırmasını kontrol etmek ve gerekirse düzeltmek.

---

## 🚀 Sonraki Adımlar

### Öncelikli (MngKeeper Session'ında)

1. **JWT Token Claim Sorunu - Kalıcı Çözüm:**
   - MngKeeper'da mapper yapılandırmasını kontrol et
   - `domain_name` ve `domain_id` claim'lerinin token'a eklendiğinden emin ol
   - Mapper endpoint'inin doğru çalıştığını doğrula
   - Fallback mekanizmasını kaldırmayı düşün (kalıcı çözüm sonrası)

### Opsiyonel

2. **Monitoring ve Metrics:**
   - System event'lerin sayısını takip etme
   - Queue message count monitoring
   - Connection count metrics

3. **Error Handling İyileştirmeleri:**
   - Dead letter queue implementasyonu
   - Retry mechanism iyileştirmeleri

4. **Performance Optimizasyonları:**
   - Message batching
   - Connection pooling optimizasyonları

---

## 📚 İlgili Dosyalar

### Core Services
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/SystemEventListener/SystemEventListenerService.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/RabbitMq/RabbitMqConsumerService.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/SignalR/NotificationHub.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/Connection/ConnectionManager.cs`
- `MngHub/Infrastructure/MngHub.Infrastructure/Services/Jwt/JwtValidatorService.cs`

### Configuration
- `MngHub/Core/MngHub.Application/Configuration/MngHubSettings.cs`
- `MngHub/Core/MngHub.Domain/Constants/RoomNames.cs`

### API
- `MngHub/Presentation/MngHub.Api/Controllers/TestController.cs`
- `MngHub/Presentation/MngHub.Api/Program.cs`

### Documentation
- `MngHub/README.md`
- `MngHub/docs/ARCHITECTURE_PLAN.md`
- `MngHub/docs/CURRENT_STATUS.md` (bu dosya)
- `MngHub/tests/TEST-REHBERI.md`
- `MngHub/tests/DOMAIN-OLUSTURMA.md`

### Test Files
- `MngHub/tests/test-signalr.html` - Ana test sayfası (domain created event gösterimi ile)
- `MngHub/tests/serve-test-page.ps1` - HTML sayfasını serve etmek için (artık gerekli değil, MngHub kendisi serve ediyor)
- `MngHub/tests/test-mnghub.ps1` - Ana test scripti
- `MngHub/tests/quick-test.ps1` - Hızlı test scripti
- `MngHub/tests/test-signalr-events.ps1` - Event test scripti

---

## 🔗 İlgili Servisler

- **MngKeeper:** Domain oluşturma ve `system.mngkeeper.domain.created` event publish etme
- **RabbitMQ:** Message broker, `mng.topics` exchange

---

**Son Güncelleme:** 23 Aralık 2025  
**Durum:** ⚠️ JWT Token Claim Sorunu - Geçici Çözüm Uygulandı

**Not:** MngKeeper session'ında JWT token claim sorununun kalıcı çözümü yapılacak (mapper yapılandırması).

