# MngDomainUI - Development Roadmap

## 📋 Genel Bakış

**MngDomainUI**, MonitraNG Domain Management için Nuxt 3 + Nuxt UI tabanlı frontend uygulamasıdır. Domain oluşturma, silme, güncelleme, listeleme ve yönetim işlemlerini gerçekleştirmek için geliştirilmiştir.

### Teknoloji Stack

- ✅ **Nuxt 3** - Vue 3 framework
- ✅ **Nuxt UI** - Tailwind CSS tabanlı UI component library
- ✅ **TypeScript** - Type-safe development
- ✅ **Pinia** - State management
- ✅ **Nitro** - Server-side API routes (SSL bypass için)

---

## 🎯 Geliştirme Planı

### Phase 1: Temel Altyapı ve Domain CRUD ✅

**Durum:** Tamamlandı

- ✅ Nuxt 3 projesi oluşturuldu
- ✅ Nuxt UI modülü entegre edildi
- ✅ TypeScript yapılandırması
- ✅ Pinia state management
- ✅ API entegrasyonu (MngKeeper API)
- ✅ SSL sertifika sorunları çözüldü (server-side proxy)
- ✅ Domain listesi sayfası (`/domains`)
- ✅ Domain oluşturma formu
- ✅ Domain listesi component (DomainList.vue)
- ✅ Domain form component (DomainForm.vue) - RelatedPersonPhone, Logo, LogoUrl alanları ile güncellendi (31 Aralık 2025)
- ✅ Temel composables (useApi, useDomain)
- ✅ Domain store (Pinia)
- ✅ Logo file upload özelliği (base64 dönüşümü) - 31 Aralık 2025
- ✅ MonitraNG icon ve branding entegrasyonu - 31 Aralık 2025

**Özellikler:**
- Domain listesi görüntüleme
- Domain oluşturma (modal form)
- Status badge'leri
- Loading states
- Error handling
- Responsive design

---

### Phase 2: Domain Yönetimi İşlemleri ✅

**Durum:** Kısmen Tamamlandı

#### 2.1 Domain Silme İşlemi
- [x] Domain silme fonksiyonu (UI hazır)
- [ ] Onay dialogu component (ConfirmDialog.vue) - İleride eklenecek
- [ ] Soft delete işlemi (şu an backend'de sadece status = Deleted yapıyor)
- [ ] Başarı/hata mesajları (toast notifications) - İleride eklenecek
- [ ] **Backend:** DomainDeletionPipeline oluşturulması (gelecek)
  - [ ] Keycloak realm silme
  - [ ] MongoDB database silme
  - [ ] MinIO bucket silme
  - [ ] Redis cache temizleme
  - [ ] RabbitMQ event yayınlama

#### 2.2 Domain Detay/Güncelleme Sayfası ✅
- [x] Domain detay sayfası (`/domains/[id]`)
- [x] Domain bilgilerini görüntüleme
- [x] Domain güncelleme formu
- [x] Domain settings güncelleme
- [x] Domain status değiştirme
- [x] Domain model enhancement alanları (RelatedPersonPhone, Logo, LogoUrl) - 31 Aralık 2025
- [x] Logo file upload özelliği (base64 dönüşümü ile) - 31 Aralık 2025
- [x] Logo önizleme ve remove özelliği - 31 Aralık 2025
- [ ] **Backend:** Domain suspend/activate endpoint'leri (gelecek)
  - [ ] `POST /api/domain/{id}/suspend` - Domain'i pasife al
  - [ ] `POST /api/domain/{id}/activate` - Domain'i aktif et
  - [ ] UpdateDomain endpoint'inde status güncelleme desteği

#### 2.3 Toplu İşlemler ✅
- [x] Tüm domainleri temizleme özelliği (Clear All Domains)
- [x] Onay mekanizması (modal ile)
- [ ] Toplu silme işlemi - İleride eklenecek
- [ ] Toplu status değiştirme - İleride eklenecek

---

### Phase 3: Dataset Yönetimi ✅

**Durum:** Kısmen Tamamlandı

#### 3.1 Varsayılan Dataset Oluşturma ✅
- [x] Test dataset oluşturma butonu (Domain Detail sayfasında)
- [x] Domain seçimi (otomatik - sayfadaki domain)
- [x] Dataset şablonları (tst_publishers, tst_genres, tst_books)
- [x] Otomatik dataset oluşturma (Create Test Datasets butonu)
- [x] MngDataGateway API entegrasyonu (server-side route)
- [ ] Dataset oluşturma sayfası (gelecekte genel dataset yönetimi için)

#### 3.2 Test Verileri Yönetimi ✅
- [x] Test verisi yükleme butonu (Load Test Data)
- [x] Test verisi şablonları (books dataset için örnek veriler)
- [x] Otomatik test verisi ekleme
- [ ] Toplu import (JSON/CSV) - İleride eklenecek
- [ ] Otomatik test verisi üretme (faker benzeri) - İleride eklenecek
- [ ] Veri önizleme - İleride eklenecek

---

### Phase 4: UI/UX İyileştirmeleri 🔄

**Durum:** Devam Ediyor

#### 4.1 Görsel İyileştirmeler ✅
- [x] Dark mode desteği (text renkleri için)
- [x] Renklendirme sorunları düzeltildi
- [x] Layout genişlik sorunları düzeltildi (2 Ocak 2026)
- [x] Domain edit sayfası başlık rengi iyileştirildi (2 Ocak 2026)
- [ ] Tema özelleştirme - İleride eklenecek
- [ ] Animasyonlar ve transitions - İleride eklenecek
- [ ] Loading skeletons - İleride eklenecek
- [x] Empty states iyileştirmeleri (DomainList'te)

#### 4.2 Kullanıcı Deneyimi
- [ ] Toast notifications (başarı/hata mesajları) - İleride eklenecek
- [x] Form validation (zod ile - modals için)
- [x] Application Version gösterimi (2 Ocak 2026)
- [ ] Keyboard shortcuts - İleride eklenecek
- [ ] Breadcrumb navigation - İleride eklenecek
- [ ] Search ve filtreleme özellikleri - İleride eklenecek

#### 4.3 Responsive Design
- [ ] Mobile uyumluluk
- [ ] Tablet uyumluluk
- [ ] Touch gestures
- [ ] Responsive tables

---

### Phase 5: Authentication & Authorization ✅

**Durum:** Temel Özellikler Tamamlandı

#### 5.1 Login Sayfası ✅
- [x] Login formu (Keycloak admin auth)
- [x] Keycloak admin auth entegrasyonu
- [x] Keycloak PathPrefix configurable yapısı (2 Ocak 2026)
- [x] JWT token yönetimi (Auth store - Pinia)
- [x] Session yönetimi (localStorage ile)
- [x] Token görüntüleme modal'ı (token ve decode edilmiş içerik)
- [ ] Token refresh mekanizması - İleride eklenecek

#### 5.2 Route Guards ✅
- [x] Protected routes (auth middleware)
- [x] Guest middleware (login sayfası için)
- [x] Authentication state kontrolü
- [ ] Role-based access control - İleride eklenecek (token'da gruplar eklenince)
- [ ] Permission checking - İleride eklenecek

---

### Phase 6: Docker & Deployment 🐳

**Durum:** Temel Özellikler Tamamlandı

#### 6.1 Docker Yapılandırması ✅
- [x] Dockerfile oluşturuldu (multi-stage build)
- [x] .dockerignore oluşturuldu
- [x] docker-compose.yml entegrasyonu
- [x] Health check endpoint (`/api/health`)
- [x] Environment variable yapılandırması
- [x] SSL bypass plugin (Docker için)
- [x] Container-to-container communication yapılandırması
- [x] Container-to-container API erişimi sorunu çözüldü ✅
  - [x] MngKeeper API erişimi (`https://mngkeeper:5001`) - Çalışıyor
  - [x] SSL/TLS yapılandırması kontrolü - SSL bypass aktif
  - [x] Network bağlantısı doğrulama - Başarılı
  - [x] Runtime environment variable okuma sorunu çözüldü
- [x] Keycloak PathPrefix configurable yapısı (2 Ocak 2026)
  - [x] `KEYCLOAK_PATH_PREFIX` environment variable desteği
  - [x] Lokal ve sunucu ortamları için farklı PathPrefix desteği
  - [x] Docker container içinde Keycloak erişimi test edildi - Başarılı

#### 6.2 Deployment
- [ ] Production build yapılandırması
- [ ] CI/CD pipeline entegrasyonu
- [ ] Environment-specific config
- [ ] Docker image optimization

---

### Phase 7: Gelişmiş Özellikler 🚀

**Durum:** Planlanıyor

#### 6.1 Domain İstatistikleri
- [ ] Domain sayısı
- [ ] Status dağılımı
- [ ] Storage kullanımı
- [ ] User sayıları
- [ ] Dashboard sayfası

#### 6.2 Export/Import
- [ ] Domain listesi export (CSV/JSON)
- [ ] Domain import
- [ ] Bulk operations

#### 6.3 Logging ve Monitoring
- [ ] İşlem logları
- [ ] Error tracking
- [ ] Performance monitoring

---

## 📁 Proje Yapısı

```
MngDomainUI/
├── components/
│   ├── domain/
│   │   ├── DomainList.vue          ✅
│   │   ├── DomainForm.vue          ✅
│   │   └── DomainCard.vue          (gelecek)
│   ├── AppVersion.vue              ✅ (2 Ocak 2026)
│   └── common/
│       ├── ConfirmDialog.vue       (gelecek)
│       └── DataTable.vue           (gelecek)
├── composables/
│   ├── useApi.ts                   ✅
│   ├── useDomain.ts                ✅
│   └── useVersion.ts               ✅ (2 Ocak 2026)
├── pages/
│   ├── index.vue                   ✅
│   └── domains/
│       ├── index.vue                ✅
│       └── [id].vue                 ✅
├── stores/
│   └── domain.ts                   ✅
├── server/
│   ├── api/
│   │   ├── auth/
│   │   │   └── login.post.ts       ✅ (Keycloak PathPrefix desteği - 2 Ocak 2026)
│   │   ├── clear-all-domains.post.ts ✅ (Keycloak PathPrefix desteği - 2 Ocak 2026)
│   │   └── keeper/
│   │       └── [...path].ts        ✅
│   ├── utils/
│   │   └── keycloak.ts             ✅ (2 Ocak 2026)
│   └── plugins/
│       └── ssl-fix.ts              ✅
├── types/
│   └── domain.ts                   ✅
├── public/
│   └── version.json                ✅ (2 Ocak 2026)
└── utils/
    └── (gelecek)
```

---

## 🔗 API Endpoints

### MngKeeper API (Proxy: `/api/keeper/*`)

- `GET /api/keeper/domain` - Tüm domainleri listele
- `GET /api/keeper/domain/{id}` - Domain detayı
- `GET /api/keeper/domain/name/{name}` - İsim ile domain getir
- `POST /api/keeper/domain` - Yeni domain oluştur
- `PUT /api/keeper/domain/{id}` - Domain güncelle
- `DELETE /api/keeper/domain/{id}` - Domain sil

---

## 📝 Notlar

### Backend Geliştirmeleri (Gelecek)

#### Domain Silme Pipeline
Şu anda backend'de domain silme işlemi sadece soft delete yapıyor (status = Deleted). Tam bir cleanup pipeline'ı yok. Gelecekte eklenecek:
- Keycloak realm silme
- MongoDB database silme (`mng_{domainName}`)
- MinIO bucket silme (`mng-{domainName}`)
- Redis cache temizleme
- RabbitMQ event yayınlama (`system.mngkeeper.domain.deleted`)

**Not:** `clean_domains.ps1` script'i tüm kaynakları temizliyor ama bu bir API endpoint değil, manuel çalıştırılan bir script.

#### Domain Suspend/Activate
DomainStatus enum'ında `Suspended` durumu var ama UpdateDomain endpoint'i status'u güncellemiyor. Gelecekte eklenecek:
- `POST /api/domain/{id}/suspend` - Domain'i pasife al
- `POST /api/domain/{id}/activate` - Domain'i aktif et
- UpdateDomain endpoint'inde status güncelleme desteği

### SSL Sertifika Sorunları
- Development ve Docker ortamında SSL sertifika doğrulaması server-side'da bypass ediliyor
- Production'da geçerli SSL sertifikaları kullanılmalı
- SSL bypass `server/plugins/ssl-fix.ts` ile aktif (`ENABLE_SSL_BYPASS=true`)

### Environment Variables
**Client-side (Browser accessible):**
- `KEEPER_URL` - MngKeeper API URL (varsayılan: `https://localhost:5001`)
- `DATAGATEWAY_URL` - MngDataGateway API URL (varsayılan: `https://localhost:5010`)
- `HUB_URL` - MngHub API URL (varsayılan: `http://localhost:5020`)
- `GATEWAY_URL` - API Gateway URL (kullanılırsa diğer URL'ler göz ardı edilir)

**Server-side (Container-to-container):**
- `SERVER_KEEPER_URL` - MngKeeper API URL (Docker: `https://mngkeeper:5001`)
- `SERVER_DATAGATEWAY_URL` - MngDataGateway API URL (Docker: `https://mngdatagateway:5010`)
- `SERVER_HUB_URL` - MngHub API URL (Docker: `http://mnghub:5020`)
- `KEYCLOAK_BASE_URL` - Keycloak URL (Docker: `http://keycloak:8080`)
- `KEYCLOAK_PATH_PREFIX` - Keycloak Path Prefix (Lokal: `""`, Sunucu: `/keycloak`)
- `KEYCLOAK_ADMIN_USER` - Keycloak admin kullanıcı adı (varsayılan: `admin`)
- `KEYCLOAK_ADMIN_PASSWORD` - Keycloak admin şifresi (varsayılan: `admin123`)
- `MINIO_ENDPOINT` - MinIO endpoint (Docker: `minio:9000`)
- `ENABLE_SSL_BYPASS` - SSL bypass aktif et (Docker: `true`)

**Not:** Server-side route'larda `process.env` direkt kullanılıyor (runtime'da okunuyor), çünkü Nuxt runtime config build zamanında environment variable'ları okuyor. Keycloak URL helper fonksiyonları (`server/utils/keycloak.ts`) `process.env` değerlerini öncelikli olarak kontrol eder.

### Domain Creation Pipeline
Domain oluşturma işlemi MngKeeper'da 11 adımlı bir pipeline ile gerçekleştirilir:
1. Domain validation
2. Create domain entity
3. Create database
4. Initialize database collections
5. Initialize DataGateway collections
6. Create indexes
7. Create Keycloak realm
8. Create default groups
9. Create admin user
10. Publish domain created event (RabbitMQ)
11. Initialize domain cache (Redis)
12. Create MinIO bucket
13. Activate domain

---

## 🎯 Öncelikler

1. **Acil/Yüksek Öncelik:**
   - ✅ Container-to-container API erişimi sorunu çözüldü
   - ✅ Keycloak PathPrefix configurable yapısı tamamlandı
   - ✅ Application Version yapısı tamamlandı
   - ✅ UI/UX iyileştirmeleri (layout, renkler) tamamlandı
   - Domain silme onay dialogu (UI hazır, backend pipeline bekliyor)
   - Toast notification sistemi

2. **Orta Öncelik:**
   - Backend: Domain silme pipeline'ı (MongoDB, Keycloak, MinIO)
   - Backend: Domain suspend/activate endpoint'leri
   - Backend: Token'a kullanıcı gruplarını ekleme
   - UI/UX iyileştirmeleri (toast notifications, loading states)

3. **Düşük Öncelik:**
   - Gelişmiş özellikler (istatistikler, dashboard)
   - Export/Import
   - Role-based access control
   - Production deployment yapılandırması

---

## 📚 İlgili Dokümantasyon

- [MngKeeper API Documentation](../MngKeeper/)
- [Domain Creation Pipeline](../MngKeeper/guides/)
- [Nuxt 3 Documentation](https://nuxt.com/)
- [Nuxt UI Documentation](https://ui.nuxt.com/)

---

**Son Güncelleme:** 2 Ocak 2026
**Versiyon:** 1.0.0

---

## ✅ Son Tamamlanan İşler (v1.0.0 - 2 Ocak 2026)

### Keycloak PathPrefix Configurable Yapısı (2 Ocak 2026)
- ✅ Keycloak URL helper fonksiyonları eklendi (`server/utils/keycloak.ts`)
- ✅ `buildKeycloakUrl` fonksiyonu eklendi (MngKeeper ile uyumlu)
- ✅ `getKeycloakConfig` fonksiyonu eklendi (`process.env` öncelikli)
- ✅ Login API route'u güncellendi (configurable PathPrefix)
- ✅ Clear All Domains API route'u güncellendi (configurable PathPrefix)
- ✅ `nuxt.config.ts`'de `keycloakPathPrefix` runtime config eklendi
- ✅ Docker Compose'da `KEYCLOAK_PATH_PREFIX` environment variable eklendi
- ✅ Lokal ve sunucu ortamları için farklı PathPrefix desteği

### Application Version Yapısı (2 Ocak 2026)
- ✅ `public/version.json` dosyası oluşturuldu (version ve buildDate)
- ✅ `composables/useVersion.ts` composable eklendi
- ✅ `components/AppVersion.vue` component eklendi
- ✅ Footer'da versiyon bilgisi gösterimi
- ✅ Runtime config'e `appVersion` eklendi
- ✅ Server-side rendering desteği (SSR-safe)

### UI/UX İyileştirmeleri (2 Ocak 2026)
- ✅ Layout genişlik sorunu düzeltildi (main content tam genişlik)
- ✅ Footer genişlik sorunu düzeltildi (tam genişlik)
- ✅ Domain edit sayfası başlık rengi iyileştirildi (daha koyu, okunabilir)
- ✅ Domain edit sayfası subtitle rengi iyileştirildi (font-medium eklendi)
- ✅ `useVersion` composable'da server-side check eklendi (html.replace hatası çözüldü)

### Docker Build ve Deployment (2 Ocak 2026)
- ✅ Docker build başarıyla tamamlandı (`localhost:5000/mngdomainui:1.0.0`)
- ✅ Container başarıyla çalışıyor (healthy)
- ✅ Keycloak erişimi düzgün çalışıyor (`http://keycloak:8080`)
- ✅ Environment variable'lar doğru ayarlandı (Docker container içinde)

### Domain Model Enhancement ve UI Güncellemeleri (31 Aralık 2025)
- ✅ Domain model'e yeni alanlar eklendi (RelatedPersonPhone, Logo, LogoUrl)
- ✅ DomainForm component'ine yeni alanlar eklendi
- ✅ DomainEditForm component'ine yeni alanlar eklendi
- ✅ Logo file upload özelliği eklendi (otomatik base64 dönüşümü)
- ✅ File upload validasyonu (dosya tipi, boyut kontrolü - max 5MB)
- ✅ Logo önizleme özelliği
- ✅ Type definitions güncellendi (Domain, CreateDomainRequest interfaces)
- ✅ useDomain composable güncellendi

### MonitraNG Icon ve Branding (31 Aralık 2025)
- ✅ IoT Monitoring temasına uygun icon tasarımı oluşturuldu
- ✅ Minimalist icon tasarımı (icon-simple.svg)
- ✅ Favicon entegrasyonu (favicon.svg)
- ✅ Header'da logo gösterimi
- ✅ Nuxt.config.ts'de favicon link'leri yapılandırıldı
- ✅ Icon tasarımı: Merkezi monitoring hub + 4 IoT sensörü + bağlantı hatları

### Docker Container-to-Container API Erişimi (7 Ocak 2026)
- ✅ Container-to-container API erişimi sorunu çözüldü
- ✅ Runtime environment variable okuma sorunu düzeltildi
- ✅ Tüm server-side route'lar `process.env` direkt kullanıyor
- ✅ MngKeeper, MngDataGateway ve MngHub API'lerine erişim başarılı
- ✅ Docker container içinden tüm API çağrıları çalışıyor
