# MngDataGateway Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Dataset Schema Management** — CRUD, alan tipleri (text, number, bool, datetime, object, relation, incremental), index tanımları, predefined query, dataset categories.
- **Data CRUD** — Pagination, sorting, filtering, field selection, relation expansion, soft delete/restore, bulk insert, history (opsiyonel).
- **Incremental Field** — Sequence, dinamik/domain prefix (`{domain}`), atomic counter, scope izolasyonu.
- **RabbitMQ Events** — DataCreated/Updated/Deleted/Restored, `mngdatagateway.events`, domain-based routing, `publish_mode`, MngHub entegrasyonu.
- **Search** — Ana + relation (pre-expansion) arama, filter/pagination ile birlikte.
- **CSV Export** — format=csv, nested flatten, array birleştirme, internal alan atlama.
- **Query Parameter Types** — text, number, bool, datetime; validation, required/optional, JsonElement.
- **File Field** — Obje formatı (path, upload_person, upload_time, file_name, file_ext, file_size), update’te file işleme, ASCII-safe MinIO, UI önizleme/indirme.
- **Dataset Authorization** — Group-based permissions (read/create/update/delete), MngKeeper user_groups, JWT entegrasyonu.
- **Index Metadata** — Index tanımları schema içinde saklanıyor (fiziksel oluşturma ayrı serviste).
- **API Gateway Integration** — SSL termination Gateway’de, CORS merkezi, internal network.
- **Chat Room (F1 — `cht_messages`)** — `ValidationService` içinde sunucu tarafı oda doğrulaması: `authorPersonId` = JWT kullanıcı; `direct` / `topic` / `group` için sırasıyla `cht_direct_conversations`, `cht_topic_rooms` + `cht_topic_members` (kök + yan dal kuralı), Keeper kiracı DB’sinde `@groups` (`keycloakGroupId`) + `@users` (`groups` adları). Kod: `ValidationService.ChatRoom.cs`.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **Chat Room (F2)** — İsteğe bağlı ek HTTP validation; `publish_mode` / event payload (yüksek frekans için §3.1a). Dataset şema script: `scripts/tests/MngDataGateway/chat-room/setup-chat-room-datasets.ps1`. **Docker:** [Backend & Docker adımları](../../chat_room/BACKEND_DOCKER_STEPS.md).
- **Dataset Naming Strategy** — environment/datasetType ile collection adı (dev_books, prod_master_books), backward compatibility.
- **Persons & PersonGroups field types** — MngKeeper user/group lookup, validation, expansion, cache.
- **Bulk insert / advanced query / expansion testleri** — Books ve diğer dataset’ler için.
- **Advanced Query** — Query result cache, performans, dokümantasyon (düşük öncelik).
- **Fiziksel index oluşturma** — Index Management Service (metadata DataGateway’de, oluşturma ayrı).

## Kararlar

- **SSL Termination** — TLS Gateway’de; DataGateway internal HTTP.
- **Event publishing** — `publish_mode` (none, basic, full); domain routing key `{domainId}.{eventType}`.
- **File field değeri** — Path yerine obje; legacy `path`/`{ path }` desteklenir.
- **Index** — Tanımlar DataGateway’de metadata; fiziksel index ayrı serviste.

---

Detaylı geliştirme roadmap’i ve teknik notlar için proje kökündeki **MngDataGateway/ROADMAP.md** dosyasına bakılabilir. Ek faz ve planlar için [Roadmap (ek)](../support/guides/ROADMAP_MngDataGateway.md) sayfasına bakılabilir.
