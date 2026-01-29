# MngKeeper Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Domain Creation Pipeline** — 13 adımlı pipeline (validate, DB, Keycloak realm, default groups, admin user, RabbitMQ event, Redis cache, MinIO bucket, aktivasyon). Domain model: RelatedPersonPhone, Logo, LogoUrl.
- **Authentication API** — Token, refresh, revoke, change-password, reset-password, create-reset-token. Custom claims: `user_groups`, `isAdmin`, `isManager`, `domain_id`, `domain_name`. Keycloak PathPrefix desteği (lokal / reverse proxy).
- **User & Group Management** — CRUD, pagination, search, soft delete, Keycloak + DataGateway sync, RabbitMQ events (user/group created, updated, deleted; user added/removed from group).
- **DataGateway Sync** — Direct MongoDB sync (RabbitMQ yok), domain-based DB routing, manuel sync endpoint’leri.
- **License Management** — Trial/Real lisans, yükleme/doğrulama, aktif kullanıcı sayısı, MaxUsers/ActiveUserDefinition, cache invalidation.
- **Template Management** — Template CRUD (metadata MongoDB, içerik MinIO), domain oluştururken initial data kopyalama (`InitializeInitialDataStep`).
- **API Gateway Integration** — SSL termination Gateway’de, CORS merkezi, health `/health`, internal network.
- **Code Optimization** — Redis cache (GetUsers/GetGroups), MongoDB index’ler, cache-aside, DB-level filtering/pagination, constants, exception handling standardizasyonu.
- **RabbitMQ Events** — domain.created, user/group CRUD, user.group.added/removed.
- **Infrastructure** — Keycloak, Redis, RabbitMQ, MinIO, MongoDB, JwtTokenService, DataGatewaySyncService.
- **Clean Architecture** — Domain, Application, Infrastructure, Presentation; CQRS (MediatR), Pipeline pattern, Repository, DI, Serilog.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **User Profile Enhancement** — Title, Department, Gender, PhoneNumber, PhotoUrl; photo upload (MinIO); cinsiyete göre avatar renkleri.
- **Manager Role & Authorization** — `isManager` claim, Admin/Manager hiyerarşisi, ManagerAuthorizationAttribute / AdminOnlyAuthorizationAttribute, endpoint yetkilendirme güncellemeleri.
- **Server-Side Pagination (Frontend)** — v-data-table server-items-length, backend’e search/filter gönderimi, büyük listelerde performans.
- **Forgot Password** — Email ile reset token gönderimi (SMTP); karar/öncelik netleştirilecek.
- **RabbitMQ Event Tamamlama** — domain.updated/deleted; event retry, DLQ, event versioning.
- **Permission Management** — Group-based permission, CRUD, endpoint’lerde permission kontrolü.
- **Audit Logging** — CRUD işlemleri için audit log, retention.
- **Password Management** — Forgot-password akışı ve gerekirse rate limiting / email entegrasyonu.

## Kararlar

- **Keycloak Mapper** — Protocol mapper’lar domain oluşturma sırasında otomatik eklenemiyor; iki aşamalı süreç: (1) domain oluştur, (2) `POST /api/admin/realms/{realmName}/configure-mappers` ile mapper’ları yapılandır.
- **SSL Termination** — TLS sonlandırma API Gateway (Ocelot/Nginx) üzerinde; servisler internal network’te HTTP kullanır.
- **DataGateway Sync** — Kullanıcı/grup senkronu doğrudan MongoDB ile yapılır; event kaybı riski olmaması ve aynı işlem içinde tamamlanması tercih edildi.
- **Avatar** — Offline sistemlerde Gravatar kullanılmaz; MinIO + initials tabanlı avatar kullanılır.

---

Detaylı geliştirme roadmap’i ve teknik notlar için proje kökündeki **MngKeeper/ROADMAP.md** dosyasına bakılabilir.
