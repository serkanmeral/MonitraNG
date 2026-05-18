# MngGateway Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Ocelot entegrasyonu** — Merkezi routing, backend servislere yönlendirme.
- **JWT Authentication** — Keycloak ile token doğrulama.
- **CORS** — Yalnızca Gateway’de merkezi yönetim; backend’lerde kaldırıldı.
- **Rate limiting** — Yapılandırılabilir limitler.
- **SSL/TLS Termination** — Sertifika Gateway’de; backend’ler internal HTTP.
- **Health check** — `/health` endpoint.
- **Serilog** — Yapılandırılmış loglama.
- **Docker** — Dockerfile, docker-compose, production (Nginx + Let’s Encrypt) ile uyum.
- **API Gateway pattern** — Backend’ler internal network’te, dışa sadece Gateway açık.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **Chat Room (F2)** — Gerekirse `ocelot.json` içinde DG/Hub/Notifier upstream veya rate limit satırları; çoğu senaryoda değişiklik gerekmez. **Docker:** [Chat Room backend adımları](../../chat_room/BACKEND_DOCKER_STEPS.md).
- **Request/Response transformation** — Header/body dönüşümleri.
- **API versioning** — Route/header ile sürüm yönetimi.
- **Circuit breaker** — Backend hata/gecikme için devre kesici.
- **Load balancing** — Çoklu instance dağıtımı.
- **Service discovery** — Dinamik backend keşfi (opsiyonel).
- **Monitoring** — Request metrics, error tracking, distributed tracing.
- **Production** — High availability, auto-scaling, disaster recovery (uzun vadeli).

## Kararlar

- **SSL Termination** — Tüm TLS Gateway’de sonlandırılır; backend’ler HTTP.
- **CORS** — Sadece Gateway’de tanımlanır; backend’ler CORS ile uğraşmaz.
- **Sertifika** — Development’ta self-signed; production’da Nginx + Let’s Encrypt veya benzeri.

---

Detaylı geliştirme roadmap’i için proje kökündeki **MngGateway/ROADMAP.md** dosyasına bakılabilir.
