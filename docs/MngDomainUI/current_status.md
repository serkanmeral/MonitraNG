# MngDomainUI - Mevcut Durum

**Son Güncelleme:** 7 Ocak 2026

---

## Son Çalışılan Konu

**Docker Containerization ve Container-to-Container API Erişimi - TAMAMLANDI ✅**

MngDomainUI için Docker yapılandırması tamamlandı ve container-to-container API erişimi sorunu çözüldü. Tüm API çağrıları başarıyla çalışıyor. Proje şu anda stabil durumda.

---

## Tamamlanan İşler

### ✅ Domain Management UI Özellikleri
- Domain listesi görüntüleme
- Domain detay sayfası
- Domain oluşturma
- Domain güncelleme
- Domain silme (UI hazır, backend implementasyonu bekliyor)

### ✅ Test Dataset ve Veri İşlemleri
- Test dataset'leri oluşturma (tst_publishers, tst_genres, tst_books)
- Test verileri yükleme
- Test kullanıcı grupları oluşturma
- Test kullanıcıları oluşturma (dinamik sayı, varsayılan şifre)
- "Serkan Meral" kullanıcısı otomatik ekleme

### ✅ Authentication Sistemi
- Login sayfası (Keycloak admin kullanıcısı ile)
- Auth store (Pinia) - token ve user bilgilerini saklama
- Authentication middleware - korumalı route'lar için
- Guest middleware - login sayfası için
- Logout işlemi
- Token görüntüleme modal'ı (token ve decode edilmiş içerik)

### ✅ Clear All Domains Özelliği
- Keycloak realm'lerini temizleme (master hariç)
- MinIO bucket'larını temizleme
- Onay modal'ı
- MongoDB temizliği manuel (UI'da yok)

### ✅ Docker Yapılandırması
- Dockerfile oluşturuldu (multi-stage build: Node.js 20 + Nitro server)
- .dockerignore oluşturuldu
- docker-compose.yml güncellendi (mngdomainui servisi eklendi)
- Health check endpoint eklendi (`/api/health`)
- Port mapping: `3001:3000` (MngUI ile çakışmaması için)

### ✅ Runtime Config Yapılandırması
- Server-side ve client-side URL'ler ayrıldı
- Container-to-container communication için `SERVER_*` environment variable'ları eklendi
- Keycloak ve MinIO config'leri server-side only

### ✅ UI İyileştirmeleri
- Dark mode desteği (text renkleri)
- Login sayfasında development-only pre-fill
- Renklendirme sorunları düzeltildi (dark card'larda okunabilirlik)

---

## Devam Eden İşler / Sorunlar

### ✅ Container-to-Container API Erişimi - ÇÖZÜLDÜ
**Sorun:** Docker container içinden MngKeeper API'sine erişim başarısız oluyordu.

**Çözüm:**
- Nuxt runtime config build zamanında environment variable'ları okuduğu için, container runtime'da set edilen environment variable'lar okunmuyordu
- Server-side route'larda `process.env` direkt olarak kullanılarak sorun çözüldü
- Artık `process.env.SERVER_KEEPER_URL` runtime'da doğru okunuyor

**Durum:**
- ✅ Environment variable'lar doğru set edildi (`SERVER_KEEPER_URL=https://mngkeeper:5001`)
- ✅ Server-side route'lar `process.env` direkt kullanıyor (runtime'da okunuyor)
- ✅ SSL bypass plugin Docker'da çalışıyor (`ENABLE_SSL_BYPASS=true`)
- ✅ Container içinden `https://mngkeeper:5001` adresine erişim başarılı
- ✅ API çağrıları başarılı (domain listesi çalışıyor)

**Yapılan Değişiklikler:**
- Tüm server-side route'larda `config.serverKeeperUrl` yerine `process.env.SERVER_KEEPER_URL` öncelikli kullanılıyor
- Fallback mantığı: `process.env.SERVER_KEEPER_URL` → `process.env.KEEPER_URL` → `config.serverKeeperUrl` → `config.public.keeperUrl` → default

---

## Sonraki Adımlar

### 1. Container-to-Container API Erişimi Sorununu Çözme ✅
- [x] Container log'larını detaylı inceleme
- [x] Container içinden direkt API testleri
- [x] Network bağlantısını doğrulama
- [x] SSL/TLS yapılandırmasını kontrol etme
- [x] Runtime environment variable okuma sorunu çözüldü

### 2. Backend İşlemleri (Roadmap'te)
- [ ] Domain silme pipeline'ı (full deletion - MongoDB, Keycloak, MinIO)
- [ ] Domain suspend/activate endpoint'leri
- [ ] Token'a kullanıcı gruplarını ekleme

### 3. UI İyileştirmeleri
- [ ] Toast notification sistemi
- [ ] Loading state'leri iyileştirme
- [ ] Error handling iyileştirme
- [ ] Domain silme confirmation dialog

---

## Önemli Notlar

### Docker Yapılandırması
- **Image:** `localhost:5000/mngdomainui:1.0.0`
- **Container:** `mngdomainui`
- **Port:** `3001:3000`
- **Network:** `mng_common_mng_network` (external)
- **Depends on:** mngkeeper, mngdatagateway, mnghub

### Environment Variables
**Server-side (Container-to-container):**
- `SERVER_KEEPER_URL=https://mngkeeper:5001`
- `SERVER_DATAGATEWAY_URL=https://mngdatagateway:5010`
- `SERVER_HUB_URL=http://mnghub:5020`
- `KEYCLOAK_BASE_URL=http://keycloak:8080`
- `MINIO_ENDPOINT=minio:9000`
- `ENABLE_SSL_BYPASS=true`

**Client-side (Browser accessible):**
- `KEEPER_URL=https://localhost:5001`
- `DATAGATEWAY_URL=https://localhost:5010`
- `HUB_URL=http://localhost:5020`

### API Route'ları
- **Keeper Proxy:** `/api/keeper/*` → `https://mngkeeper:5001/api/*`
- **DataGateway Proxy:** `/api/datagateway/*` → `https://mngdatagateway:5010/api/*`
- **Auth:** `/api/auth/login` (Keycloak master realm)
- **Clear All:** `/api/clear-all-domains` (Keycloak + MinIO)

### Test Kullanıcısı
- **Username:** `serkan.meral`
- **Email:** `serkan.meral@outlook.com`
- **Password:** `Serkan123!`
- Test kullanıcıları oluşturulurken otomatik olarak ilk sırada ekleniyor

### Login Bilgileri
- **Development:** Username ve password pre-fill (`admin` / `admin123`)
- **Production:** Boş form
- **Keycloak Admin:** `admin` / `admin123` (master realm)

---

## Bilinen Sorunlar

- Şu anda bilinen kritik sorun yok ✅

---

## Teknik Detaylar

### Node.js Versiyonu
- **Dockerfile:** `node:20-alpine` (Nuxt 3.20.2 gereksinimi)

### Build Komutu
- `npm run build` (SSR mode - Nitro server)

### Runtime
- Nitro server (`node .output/server/index.mjs`)
- Port: 3000 (container içinde)

### Dependencies
- **Production:** minio, pinia, zod, @nuxt/ui, @pinia/nuxt
- **Dev:** @nuxt/devtools, @types/node, nuxt, typescript, vue-tsc

---

**Sonraki Oturum:** 
- Toast notification sistemi eklenebilir
- Domain silme onay dialogu iyileştirilebilir
- Backend işlemleri (Domain silme pipeline, suspend/activate endpoint'leri) planlanabilir
- UI/UX iyileştirmeleri yapılabilir

**Not:** Proje şu anda stabil durumda. Tüm temel özellikler çalışıyor ve Docker container'da başarıyla çalışıyor.
