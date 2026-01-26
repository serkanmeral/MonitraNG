Authorization Service + Keycloak Integration PRD
📋 Proje Özeti
Amaç:
MngReactor uygulaması için ayrı bir Authorization Service geliştirerek, Keycloak tabanlı multitenant kimlik doğrulama ve yetkilendirme sistemi kurmak.
Hedefler:
IdentityServer4'ten Keycloak'a geçiş
Domain-based multitenancy
Her domain için ayrı Keycloak realm ve MongoDB veritabanı
Merkezi kullanıcı ve grup yönetimi
Token tabanlı domain izolasyonu
🏗️ Mimari Tasarım
1. Sistem Mimarisi
Apply to Program.cs
2. Proje Yapısı
Apply to Program.cs
�� Fonksiyonel Gereksinimler
1. Domain Yönetimi
1.1 Create Domain
Endpoint: POST /api/v1/domain
Açıklama: Yeni bir domain oluşturur
İşlemler:
Keycloak'ta yeni realm oluşturma
MongoDB'de yeni veritabanı oluşturma
Varsayılan admin kullanıcısı oluşturma
Varsayılan gruplar oluşturma (admin, user)
Domain bilgilerini kaydetme
1.2 Get Domains
Endpoint: GET /api/v1/domain
Açıklama: Tüm domainleri listeler
1.3 Delete Domain
Endpoint: DELETE /api/v1/domain/{domainName}
Açıklama: Domain'i ve ilgili kaynakları siler
2. Kullanıcı Yönetimi
2.1 Create User
Endpoint: POST /api/v1/domain/{domainName}/users
Açıklama: Belirtilen domain'de yeni kullanıcı oluşturur
2.2 Update User
Endpoint: PUT /api/v1/domain/{domainName}/users/{userId}
Açıklama: Kullanıcı bilgilerini günceller
2.3 Delete User
Endpoint: DELETE /api/v1/domain/{domainName}/users/{userId}
Açıklama: Kullanıcıyı siler
3. Grup Yönetimi
3.1 Create Group
Endpoint: POST /api/v1/domain/{domainName}/groups
Açıklama: Yeni grup oluşturur
3.2 Update Group
Endpoint: PUT /api/v1/domain/{domainName}/groups/{groupId}
Açıklama: Grup bilgilerini günceller
3.3 Delete Group
Endpoint: DELETE /api/v1/domain/{domainName}/groups/{groupId}
Açıklama: Grubu siler
4. Kimlik Doğrulama
4.1 Login
Endpoint: POST /api/v1/auth/login
Açıklama: Kullanıcı girişi yapar ve token döner
Request:
Apply to Program.cs
Response:
Apply to Program.cs
4.2 Validate Token
Endpoint: POST /api/v1/auth/validate
Açıklama: Token'ın geçerliliğini kontrol eder
🔧 Teknik Gereksinimler
1. Teknoloji Stack
Backend:
Framework: .NET 8.0
Architecture: Clean Architecture
Pattern: CQRS + MediatR
Database: MongoDB
Authentication: Keycloak
External Services:
Keycloak: 23.0.3 (Latest)
MongoDB: 6.0+
Docker: Containerization
2. Paket Bağımlılıkları
Apply to Program.cs
3. Konfigürasyon
appsettings.json:
Apply to Program.cs
📊 Veri Modelleri
1. Domain Entity
Apply to Program.cs
2. User Entity
Apply to Program.cs
3. Group Entity
Apply to Program.cs
🔐 Güvenlik Gereksinimleri
1. Kimlik Doğrulama
JWT token tabanlı kimlik doğrulama
Token'da domain bilgisi zorunlu
Token süresi: 1 saat (yapılandırılabilir)
Refresh token desteği
2. Yetkilendirme
Role-based access control (RBAC)
Domain-based izolasyon
SuperAdmin rolü (tüm domainlere erişim)
DomainAdmin rolü (kendi domainine erişim)
3. Güvenlik Önlemleri
HTTPS zorunluluğu
Rate limiting
Input validation
SQL injection koruması
XSS koruması
🚀 Uygulama Planı
Faz 1: Temel Altyapı (3-4 gün)
Gün 1-2: Proje Kurulumu
[ ] Solution ve proje yapısı oluşturma
[ ] Clean Architecture implementasyonu
[ ] Temel paketlerin eklenmesi
[ ] Docker compose dosyası hazırlama
Gün 3-4: Keycloak Entegrasyonu
[ ] Keycloak service interface tanımlama
[ ] Keycloak.Net paketi entegrasyonu
[ ] Realm CRUD operasyonları
[ ] User CRUD operasyonları
[ ] Group CRUD operasyonları
Faz 2: Domain Yönetimi (3-4 gün)
Gün 5-6: Domain CRUD
[ ] Domain entity ve repository
[ ] CreateDomain command/handler
[ ] Domain controller
[ ] MongoDB database creation
[ ] Varsayılan admin kullanıcısı oluşturma
Gün 7-8: User/Group Management
[ ] User CRUD operasyonları
[ ] Group CRUD operasyonları
[ ] Role assignment
[ ] Permission management
Faz 3: Authentication & Authorization (2-3 gün)
Gün 9-10: Auth Endpoints
[ ] Login endpoint
[ ] Token validation
[ ] JWT token generation
[ ] Domain extraction from token
Gün 11: Integration
[ ] MngReactor integration
[ ] Token middleware
[ ] Domain-based routing
Faz 4: Testing & Deployment (2-3 gün)
Gün 12-13: Testing
[ ] Unit tests
[ ] Integration tests
[ ] API documentation
[ ] Performance testing
Gün 14: Deployment
[ ] Docker image build
[ ] Environment configuration
[ ] Production deployment
[ ] Monitoring setup
📈 Başarı Kriterleri
Fonksiyonel Kriterler:
[ ] Domain oluşturma işlemi başarılı
[ ] Keycloak realm otomatik oluşturuluyor
[ ] MongoDB veritabanı otomatik oluşturuluyor
[ ] Varsayılan admin kullanıcısı oluşturuluyor
[ ] Token'da domain bilgisi mevcut
[ ] MngReactor domain-based çalışıyor
Performans Kriterleri:
[ ] Domain oluşturma < 30 saniye
[ ] Token validation < 100ms
[ ] API response time < 200ms
[ ] 1000+ concurrent user desteği
Güvenlik Kriterleri:
[ ] Domain izolasyonu tam
[ ] Token güvenliği sağlanmış
[ ] Input validation tam
[ ] Rate limiting aktif
�� Risk Analizi
Yüksek Risk:
Keycloak entegrasyonu: Karmaşık API
Domain izolasyonu: Veri sızıntısı riski
Orta Risk:
Performance: Çok sayıda domain
Backup/Recovery: Veri kaybı
Düşük Risk:
UI/UX: Kullanıcı deneyimi
Documentation: Teknik dokümantasyon
�� Deliverables
Kod:
[ ] AuthorizationService solution
[ ] Keycloak integration
[ ] Domain management API
[ ] User/Group management
[ ] Authentication endpoints
Dokümantasyon:
[ ] API documentation (Swagger)
[ ] Deployment guide
[ ] User manual
[ ] Architecture documentation
Deployment:
[ ] Docker images
[ ] Docker compose files
[ ] Environment configurations
[ ] Monitoring setup