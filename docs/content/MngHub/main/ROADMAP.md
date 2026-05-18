# MngHub Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **SignalR Hub** — NotificationHub, MessageRouter, ConnectionManager; domain room (`domain.{name}`), global room; JWT (query + header); otomatik yeniden bağlanma.
- **RabbitMQ Integration** — Dynamic queue per connection, routing (`global.*`, `system.#`, `domain.{name}.#`, `{domainId}.*`), mng.topics + mngkeeper.events, reconnection.
- **Domain-based Event Routing** — Domain izolasyonu, JWT’den domain claim, room mapping, güvenli yönlendirme.
- **System Event Listener** — SystemEventListenerService, `mnghub.system.listener`, `system.#`, domain created vb.
- **Group/User Event Listener** — GroupEventListenerService, `mnghub.group.listener`, mngkeeper.events; MngKeeper user/group event’leri.
- **JWT Authentication** — JwtValidatorService, MngKeeper API ile doğrulama, domain claim, iss/preferred_username fallback.
- **API Gateway Integration** — CORS Gateway’de, internal network, SignalR Gateway üzerinden; `/hub/ws/*`.
- **Code Optimization** — MessageDto.Create, HttpContextExtensions, MessageRouter, RabbitMqConsumerService helper’lar.
- **Docker** — Dockerfile, health check, mng_common_mng_network, port 5020.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **Chat Room (F2)** — `cht_*` DG unified event’lerinin domain odağına taşınması (mevcut `{domainId}.*` + `MessageRouter`); isteğe bağlı Hub method’ları (`JoinChatRoom` vb.) ve mention anlık mesajı. **Docker deneme sırası:** [Chat Room — Backend & Docker](../../chat_room/BACKEND_DOCKER_STEPS.md).
- **Monitoring & Metrics** — Connection count, message throughput, RabbitMQ queue depth, `/api/metrics/*` endpoint’leri.
- **Error Handling** — DLQ, retry (exponential backoff), circuit breaker, hata bildirimi.
- **Performance** — Message batching, connection pool, bellek ağı.
- **Security** — Rate limiting per connection, mesaj boyutu, connection throttle, IP kontrolü, audit log.
- **HTTPS/WSS** — Production’da WSS/HTTPS (şu an Gateway üzerinden TLS).
- **JWT Kalıcı Çözüm** — domain_name/domain_id claim’lerinin MngKeeper’da tutarlı eklenmesi; fallback kaldırılması.

## Kararlar

- **JWT Fallback** — domain_name/domain_id yoksa iss + preferred_username kullanılıyor; kalıcı çözüm MngKeeper mapper’da.
- **SignalR** — Gateway üzerinden tek WSS ucu; backend internal HTTP.
- **Listener vs Hub** — System/Group listener’lar arka planda event dinler; broadcast NotificationHub üzerinden, çift mesaj yok.

---

Detaylı geliştirme roadmap’i için proje kökündeki **MngHub/ROADMAP.md** dosyasına bakılabilir.
