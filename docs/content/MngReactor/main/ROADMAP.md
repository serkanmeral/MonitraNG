# MngReactor Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Proje yapısı** — Clean Architecture; Asset, Data, Engine, Auth, LDAP, Token, Crypt, Domain processing bileşenleri; MQTT servisi; Version controller.
- **API** — Asset tree, Engine assets, Data (filter), Login/Token, Health; JWT authentication; Gateway üzerinden `/reactor/api/v1/*`.
- **Entegrasyon testleri** — WebApplicationFactory ile 48 test; Health, Ingest, Engine, MonAgents, MonAssets, MonAssetsEncryption.
- **Docker testleri** — Docker container üzerinde HTTP tabanlı smoke testler.
- **Docker deployment** — mng_apps compose'a MngReactor servisi; Dockerfile (port 5003).
- **MQTT yapılandırması** — Mosquitto (monitrang) kimlik doğrulaması; Configuration rehberi.
- **Monitoring API** — MonAgents, MonAssets, MonEngines controller'ları.

Proje kökünde ayrı bir **ROADMAP.md** dosyası yoktur. Detaylı endpoint listesi için [Technical Specs](TECHNICAL_SPECS.md), sürüm geçmişi için [Changelog](CHANGELOG.md), yapılandırma için [Configuration](../support/guides/CONFIGURATION.md) dosyasına bakınız.

## Yapılacaklar

Implementasyon planına göre ([MONITORING_IMPLEMENTATION_PLAN](../../monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md) Faz 1):

- **Tenant varsayılanları** — RabbitMQ `system.mngkeeper.domain.created` dinle; mon_schedules, mon_collection_periods "Sürekli", "1 dakika" seed.
- **MQTT event sync** — Topic: monitoring/{domain}/engine/{engineId}/sync, command.
- **Rate limiting (Ingest)** — Token–engineId doğrulama sonrası; 30–60 req/dk (Faz 5).
- **Observability** — OpenTelemetry trace, metric export (Faz 5).
- **MonitraNG UI** — Asset/Item, Engine/Agent CRUD ve Dashboard entegrasyonu (Faz 5).

## Kararlar

- MngReactor veriyi DG yerine **doğrudan MongoDB** Time Series (`mon_metrics`) ile yazar; TTL `MONITORING_METRICS_TTL_DAYS` ile yapılandırılır.
- Ingest için **REST** kullanılır; yük artarsa kuyruk (RabbitMQ) tabanlı mimari değerlendirilebilir.
- Engine–Reactor iletişiminde Keeper'dan alınan **JWT Bearer** token kullanılır.
