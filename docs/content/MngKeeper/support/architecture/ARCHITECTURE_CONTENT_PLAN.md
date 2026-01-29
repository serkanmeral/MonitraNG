# MngKeeper Mimari Başlığı — İçerik Planı

**Amaç:** `ARCHITECTURE_GUIDE.md` dosyasını, DOCUMENTATION_STANDARDS’ta tanımlı “mimari kararlar, sistem tasarımı, diyagramlar” kapsamında doldurmak.

**Hedef dosya:** `support/architecture/ARCHITECTURE_GUIDE.md`  
**Mevcut durum:** Genel bakış + “Detaylı mimari dokümantasyonu hazırlanmaktadır” notu; geri kalanı boş.

---

## 1. Standarttaki tanım

DOCUMENTATION_STANDARDS’a göre **architecture/** altında:

- Mimari **kararlar** (neden böyle tasarlandı)
- **Sistem tasarımı** (bileşenler, akışlar, sınırlar)
- **Diyagramlar** (okunabilir, metin/alt metin ile desteklenmeli)

Dil: Açıklamalar Türkçe; kod/teknik terimler İngilizce kalabilir.

---

## 2. Önerilen bölümler (ARCHITECTURE_GUIDE.md)

Aşağıdaki başlıklar, MngKeeper README ve mevcut kod yapısına göre önerilir. Sırayla doldurulabilir.

| # | Bölüm | İçerik (kısa) | Kaynak / not |
|---|--------|----------------|---------------|
| 1 | **Genel bakış** | Servisin rolü (IAM, multi-tenant, domain/kullanıcı/grup/lisans). | Mevcut + README. |
| 2 | **Katmanlı yapı (Clean Architecture)** | Domain, Application, Infrastructure, Presentation katmanları; sorumluluklar ve bağımlılık yönü. | `MngKeeper.Core` / `Presentation` / `Infrastructure` klasörleri. |
| 3 | **Multi-tenant modeli** | Domain kavramı; domain başına MongoDB DB, Keycloak realm, MinIO bucket; izolasyon. | Domain entity, pipeline adımları, repository’ler. |
| 4 | **Domain oluşturma pipeline’ı** | 11 adımlı süreç: Validate → CreateDomainEntity → CreateKeycloakRealm → CreateDatabase → CreateMinIOBucket → CreateAdminUser → CreateDefaultGroups → CreateLicense → InitializeCollections → CreateIndexes → PublishEvent → SendEmail → Activate. Hangi adım ne yapıyor, hata durumunda ne olur. | `Pipelines/DomainCreation/` ve `Steps/`. |
| 5 | **Kimlik ve erişim** | Keycloak entegrasyonu; JWT, custom claims (user_groups, isAdmin, domain_name, domain_id); Manager/Admin/Authenticated yetkilendirme. | AuthController, Attributes, middleware, KeycloakService. |
| 6 | **Veri ve cache** | MongoDB (domain DB’leri, @users, @groups, domains koleksiyonu); Redis cache (hangi veriler, TTL/strateji). | Repository’ler, RedisCacheService, DomainRepository. |
| 7 | **Dış sistemler ve olaylar** | RabbitMQ (domain created vb. event’ler); MinIO (kullanıcı fotoğrafı, sistem locale); MngNotifier (e-posta); DataGateway sync. | EventPublisher, MinioService, NotifierService, DataGatewaySyncService. |
| 8 | **Lisanslama** | Lisans türleri (Trial/Real); lisans dosyası/şifreleme; operasyon kontrolü (TokenGeneration, Crud, Get). | LicenseController, LicenseService, License entity/DTO’lar. |
| 9 | **Diyagramlar** | En az: (a) üst seviye bileşen diyagramı, (b) domain oluşturma akışı, (c) isteğe bağlı: auth/ token akışı. Mermaid veya export edilebilir format; her diyagramın altında kısa metin özeti. | Yukarıdaki bölümlerden türetilir. |

İstersen “Güvenlik özeti”, “Dağıtım / konteyner” veya “Performans ve ölçeklenebilirlik” gibi ek bölümler de eklenebilir; önce bu dokuz bölüm tamamlansın, sonra genişletilir.

---

## 3. Uygulama sırası (öneri)

1. **Genel bakış** — Mevcut metni, README’deki özellik listesi ve IAM tanımıyla genişlet.
2. **Katmanlı yapı** — Klasör/solution yapısından çıkar; kısa tablo (Katman | Sorumluluk | Örnek).
3. **Multi-tenant modeli** — Domain → DB/Realm/Bucket eşlemesi; tek şema/çok tenant.
4. **Domain pipeline** — Adımlar listesi + her adım için 1–2 cümle; gerekiyorsa Mermaid sequence/flow.
5. **Kimlik ve erişim** — Keycloak rolü; token ve claim’ler; attribute’lar (ManagerAuthorization vb.).
6. **Veri ve cache** — MongoDB koleksiyonları; Redis kullanımı (varsa TTL/strateji).
7. **Dış sistemler** — Tablo: Sistem | Amaç | Örnek kullanım.
8. **Lisanslama** — Kısa akış + TECHNICAL_SPECS ile çapraz referans.
9. **Diyagramlar** — Önce bileşen, sonra domain-creation akışı; her diyagram için alt metin.

İstersen 4 ve 5’i pipeline tamamlandıktan hemen sonra yazmak daha mantıklı olur (kod en güncel burada).

---

## 4. Kaynaklar

- **Kod:** `MngKeeper/` — Core (Domain, Application, Pipelines), Infrastructure, Presentation (Controllers, Middleware, Attributes).
- **Mevcut dokümanlar:** `support/guides/GATEWAY_INTEGRATION.md`, `support/guides/API_OVERVIEW.md`, `main/TECHNICAL_SPECS.md`, `main/CHANGELOG.md`.
- **README:** `MngKeeper/README.md` — Özellikler, endpoint sayıları, linkler.
- **Standart:** `docs/content/DOCUMENTATION_STANDARDS.md` — §3.2 (architecture kategorisi), §3.5.2 (support/architecture).

---

## 5. “Tamamlandı” kriteri

Mimari başlığı dolu sayılır cuando:

- ARCHITECTURE_GUIDE.md’de yukarıdaki 1–9 bölümlerinin hepsi (veya ortak kararla belirlenen alt kümesi) metin veya diyagramla doldurulmuş olur.
- Her diyagramın altında en az bir cümlelik açıklama bulunur.
- İlgili rehberlere (GATEWAY_INTEGRATION, TECHNICAL_SPECS) uygun çapraz linkler vardır.
- “Detaylı mimari dokümantasyonu hazırlanmaktadır” ifadesi kaldırılmış veya “Ek bölümler ileride eklenecektir” gibi sınırlı bir notla değiştirilmiş olur.

Bu plan, ileride diğer backend servisleri için de “mimari içerik planı” şablonu olarak kullanılabilir (servise özel başlıklar değiştirilerek).
