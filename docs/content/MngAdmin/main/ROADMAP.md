# MngAdmin Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Proje yapısı** — Clean Architecture (Domain, Application, Infrastructure, Persistence, Presentation); Version, Health, Swagger/Scalar, Serilog; port 5080.
- **System Backup** — MongoDB (mngkeeper, mngtemplates), PostgreSQL (keycloak); MinIO’ya yükleme; BackupStatus; retention (MaxBackupCount).
- **Domain Backup** — mng_* veritabanları, domain bucket (mng-{domain}), retention per database.
- **Full Backup** — System + tüm domain’ler; sıralı çalışma, raporlama.
- **Retention Policy** — Database bazında, completed backup’lar, MaxBackupCount sonrası silme (MinIO + BackupStatus).
- **MinIO** — System bucket, domain bucket’lar; yükleme/silme/bilgi.
- **PostgreSQL backup düzeltmesi** — pg_dump -F p (plain) + çıktı okuma; dosya boyutu doğru.
- **Docker** — Dockerfile, mongodump/pg_dump araçları, docker-compose, health check.
- **API Gateway** — `/admin/api/v1/*` → mngadmin:5080; rate limiting.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **Backup restore** — System (MongoDB/PostgreSQL) ve domain restore; restore validation ve izleme.
- **Backup scheduling** — Cron tabanlı planlama (MngScheduler ile entegrasyon düşünülebilir).
- **Backup verification** — Bütünlük kontrolü, doğrulama raporu.
- **Backup encryption** — AES, anahtar yönetimi (düşük öncelik).
- **Compression seçenekleri** — Seviye ve format (zip, gzip, bzip2) (düşük öncelik).
- **Monitoring & alerts** — Hata bildirimi, boyut/süre takibi.
- **DB health monitoring** — MongoDB/PostgreSQL bağlantı ve pool izleme.
- **Authentication** — Backup endpoint’leri için JWT (şu an Authorize yok).

## Kararlar

- **MongoDB backup** — ZIP (mongodump çıktısı).
- **PostgreSQL backup** — Plain SQL + GZIP (pg_dump -F p); custom format ve çıktı okuma sorunu nedeniyle plain tercih edildi.
- **Bucket stratejisi** — System: `system`; domain: `mng-{domain}`.

---

Detaylı geliştirme roadmap’i için proje kökündeki **MngAdmin/ROADMAP.md** dosyasına bakılabilir.
