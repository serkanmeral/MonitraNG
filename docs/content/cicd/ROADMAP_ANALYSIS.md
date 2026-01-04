# Roadmap Analizi ve Eksik Görevler

**Tarih:** 2 Ocak 2026  
**Durum:** Roadmap güncellendi, eksik görevler belirlendi

---

## ✅ Tamamlanan Görevler

### CI/CD ve Production Deployment (1 Ocak 2026)
- ✅ GitLab CI/CD Pipeline kurulumu
- ✅ Production deployment (SSH tabanlı)
- ✅ Zero-downtime deployment (Rolling update)
- ✅ Pre-deployment backup
- ✅ Health check'ler
- ✅ Automated rollback
- ✅ Monitoring script

### Pipeline Performans Optimizasyonu (2 Ocak 2026)
- ✅ .NET NuGet package cache (`.nuget/` klasörü - 8847 dosya)
- ✅ NPM cache (frontend build için)
- ✅ Docker layer cache (BuildKit + cache-from)
- ✅ Global cache yapılandırması
- ⚠️ **Durum:** Cache çalışıyor ancak etkisi beklenenden düşük (8:14 dakika)

---

## ⏳ Bekleyen Görevler (Öncelik Sırasına Göre)

### 🔴 Yüksek Öncelik - Altyapı ve Güvenlik

#### 1. Şifrelerin Elden Geçirilmesi
- **Durum:** 🔐 Güvenlik Kritik
- **Süre:** 1 gün
- **Görevler:**
  - [ ] Tüm servislerin şifrelerini liste çıkarma
  - [ ] Güçlü şifre politikası belirleme
  - [ ] Şifreleri güvenli şekilde değiştirme
  - [ ] Environment variable'ları güncelleme
  - [ ] Docker Compose dosyalarını güncelleme
  - [ ] Application config dosyalarını güncelleme
  - [ ] Şifre değişikliklerini test etme
  - [ ] Backup alınması

#### 2. Port ve Nginx Yapılandırması ✅
- **Durum:** ✅ Tamamlandı (4 Ocak 2026)
- **Süre:** 1-2 gün
- **Görevler:**
  - [x] Nginx containerization (Docker Compose) ✅
  - [x] Nginx yapılandırma dosyaları oluşturuldu ✅
  - [x] Container name'ler kullanımı ✅
  - [x] Application servislerin port mapping'leri kaldırıldı (docker-compose.production.yml) ✅
  - [x] GitLab port mapping'leri kaldırıldı ✅
  - [x] Keycloak port mapping'i kaldırıldı ✅
  - [x] Nginx container başarıyla çalışıyor ✅
  - [x] Port 80 ve 443 sadece Nginx tarafından kullanılıyor ✅
  - [x] Container name erişimi test edildi ✅
- **Kalan Opsiyonel İşler:**
  - [ ] Application servislerin kalan port mapping'lerini kaldır (mngui:3000, mnggateway:5000, keycloak:8080) - Development için bırakılabilir
  - [ ] Internal servislerin port mapping'lerini kaldır (MongoDB:27017, PostgreSQL:5432, Redis:6379, RabbitMQ:5672) - Güvenlik için önerilir
  - [ ] Admin/UI servislerini Nginx üzerinden erişilebilir hale getir (Portainer, MinIO Console, vb.)
  - [ ] Nginx yapılandırma uyarılarını düzelt (http2 directive deprecated)

### 🔴 Yüksek Öncelik - Uygulama Geliştirme

#### 3. User CRUD İşlemleri Test
- **Durum:** ✅ Hazır, Test Edilecek
- **Süre:** 2-3 saat
- **Test Senaryoları:**
  - [ ] User oluşturma (domain içinde)
  - [ ] User listesi (pagination, search, filter)
  - [ ] User detay
  - [ ] User güncelleme
  - [ ] User silme
  - [ ] User'ı gruba ekleme
  - [ ] User'ı gruptan çıkarma
  - [ ] Multi-tenant izolasyonu (farklı domain'lerde)
- **Test Script:** `MngKeeper/tests/user-crud-test.ps1`

#### 4. Group CRUD İşlemleri Test
- **Durum:** ✅ Hazır, Test Edilecek
- **Süre:** 1-2 saat
- **Test Senaryoları:**
  - [ ] Group oluşturma
  - [ ] Group listesi (pagination, search, filter)
  - [ ] Group güncelleme (name, description, permissions)
  - [ ] Group silme
  - [ ] Multi-tenant izolasyonu
- **Test Script:** `MngKeeper/tests/group-crud-test.ps1` (oluşturulacak)

---

### 🟡 Orta Öncelik - Altyapı

#### 5. Alan Adı Kurulumu ✅
- **Durum:** ✅ Tamamlandı (2 Ocak 2026)
- **Süre:** 2-3 saat
- **Görevler:**
  - [x] Domain satın alma: `monitrang.com` ✅
  - [x] Nameserver yapılandırması ✅
  - [x] DNS kayıtları yapılandırma (A kayıtları) ✅
  - [x] Subdomain'ler tanımlama (app, api, auth, docs, gitlab) ✅
  - [x] DNS propagation kontrolü ✅
  - [x] Domain doğrulama ✅

#### 6. Mail Sunucusu Kurulumu
- **Durum:** 📧 Bildirimler İçin Gerekli
- **Süre:** 1-2 gün
- **Görevler:**
  - [ ] Mail sunucusu seçimi (Postfix + Dovecot veya Mail-in-a-Box)
  - [ ] Docker container kurulumu
  - [ ] SMTP/IMAP/POP3 port yapılandırması
  - [ ] SSL/TLS sertifikası yapılandırması
  - [ ] SPF, DKIM, DMARC kayıtları
  - [ ] Keycloak SMTP yapılandırması
  - [ ] GitLab SMTP yapılandırması
  - [ ] Test e-postaları gönderimi

### 🟡 Orta Öncelik - Uygulama Geliştirme

#### 7. RabbitMQ Event Publishing
- **Durum:** 🔄 Tasarım + İmplementasyon
- **Süre:** 1 gün
- **Event'ler:**
  - Domain Events: `domain.created`, `domain.updated`, `domain.deleted`
  - User Events: `user.created`, `user.updated`, `user.deleted`, `user.group.added`, `user.group.removed`
  - Group Events: `group.created`, `group.updated`, `group.deleted`
- **İmplementasyon:**
  - [ ] Event model'leri oluşturma
  - [ ] Domain event handler'lar
  - [ ] RabbitMQ publisher entegrasyonu
  - [ ] Event consumer'lar (MngReactor için)
  - [ ] Event logging
  - [ ] Dead letter queue
  - [ ] Retry mechanism

#### 8. MngStorage Servisi
- **Durum:** 📦 Yeni Mikroservis
- **Süre:** 4-6 gün (Project Setup: 1 gün, Core Features: 2-3 gün, gRPC & Tests: 1-2 gün)
- **Özellikler:**
  - Upload/Download (streaming, chunked, multipart)
  - Metadata yönetimi (MongoDB)
  - Business logic (validation, thumbnail, hash, duplicate detection)
  - Domain izolasyonu (bucket-per-domain)
  - Event publishing (RabbitMQ)
- **API:** REST + gRPC
- **Storage:** MinIO (S3 compatible)

#### 9. API Gateway (Ocelot)
- **Durum:** 🚪 Merkezi Giriş Noktası
- **Süre:** 2-3 gün (Setup: 1 gün, Advanced Features: 1-2 gün)
- **Özellikler:**
  - Tek giriş noktası (unified entry point)
  - Merkezi authentication (JWT validation)
  - Rate limiting
  - CORS policy
  - Request/Response logging
  - SSL/TLS termination
- **Routing:**
  - `/keeper/*` → MngKeeper
  - `/storage/*` → MngStorage
  - `/scheduler/*` → MngScheduler
  - `/data/*` → MngDataGateway
  - `/monitor/*` → MngMonitor
  - `/auth/*` → KeyCloak

#### 10. MngScheduler Servisi
- **Durum:** 🕐 Zamanlanmış Görevler
- **Süre:** 3-4 gün (Hangfire Setup: 1 gün, Dynamic Jobs: 1-2 gün, API & Dashboard: 1 gün)
- **Özellikler:**
  - Database-driven jobs (MongoDB)
  - Scheduled HTTP calls (cron-based)
  - Hangfire dashboard
  - Management API (CRUD operations)
  - Automatic retry mechanism
  - Event publishing (RabbitMQ)

#### 11. MngChatBot Servisi
- **Durum:** 🤖 AI Destekli Dokümantasyon Asistanı
- **Süre:** 6-9 gün
  - Docker Infrastructure: 1 gün
  - Core Services & RAG: 2-3 gün
  - Function Calling & Tool Use: 2-3 gün
  - API & Integration: 1-2 gün
- **Özellikler:**
  - RAG (Retrieval Augmented Generation)
  - Function Calling (Tool Use) - MngDataGateway API entegrasyonu
  - Self-hosted AI stack (Qdrant + Ollama)
  - Real-time chat (SignalR)
  - Multi-tenant support
  - Türkçe destekli modeller

---

### 🟢 Düşük Öncelik

#### 12. SonarQube Karar ve Kurulumu
- **Durum:** 🔍 Code Quality İçin Opsiyonel
- **Süre:** 1-2 gün
- **Görevler:**
  - [ ] SonarQube Community Edition vs Enterprise Edition karşılaştırması
  - [ ] Self-hosted vs Cloud (SonarCloud) değerlendirmesi
  - [ ] Air-gapped sistem uyumluluğu kontrolü
  - [ ] Karar alma
  - [ ] SonarQube kurulumu (eğer karar verilirse)
  - [ ] GitLab CI/CD entegrasyonu

#### 13. MinIO Infrastructure Setup
- **Durum:** 📁 Altyapı Kurulumu
- **Süre:** 3-4 saat
- **Özellikler:**
  - MinIO container kurulumu
  - Bucket-per-domain yapısı
  - Access policy yapılandırması
  - Admin web UI

#### 14. MngDataGateway Servisi
- **Durum:** 🗄️ MongoDB CRUD Gateway
- **Süre:** TBD
- **Durum:** ⏳ Planlanıyor - Detaylar belirlenecek

---

## 📊 Öncelik Matrisi

### Hemen Yapılacaklar (Bu Hafta)
1. **Şifrelerin Elden Geçirilmesi** (1 gün) 🔴 - Güvenlik kritik
2. **Port ve Nginx Yapılandırması** (1-2 gün) 🔴 - Mevcut yapılandırmanın düzenlenmesi
3. **User CRUD Test** (2-3 saat) 🔴
4. **Group CRUD Test** (1-2 saat) 🔴

### Kısa Vadeli (Bu Ay)
5. **Alan Adı Kurulumu** (2-3 saat) 🟡
6. **Mail Sunucusu Kurulumu** (1-2 gün) 🟡
7. **RabbitMQ Events** (1 gün) 🟡
8. **MngStorage Servis (Project Setup)** (1 gün) 🟡
9. **API Gateway (Ocelot Setup)** (1 gün) 🟡

### Orta Vadeli (Gelecek Ay)
10. **MngStorage Servis (Core Features)** (2-3 gün) 🟡
11. **MngScheduler Servis (Hangfire Setup)** (1 gün) 🟡
12. **MngChatBot Servis (Docker Infrastructure)** (1 gün) 🟡

### Uzun Vadeli (Gelecek)
13. **MngChatBot Servis (Core Services & RAG)** (2-3 gün) 🟡
14. **MngChatBot Servis (Function Calling)** (2-3 gün) 🟡
15. **SonarQube Karar ve Kurulumu** (1-2 gün) 🟢
16. **MngDataGateway Servis** (TBD) ⏳

---

## 🎯 Önerilen Sıralama

### Faz 1: Altyapı ve Güvenlik (1-2 Hafta)
1. Şifrelerin elden geçirilmesi (güvenlik kritik)
2. Port ve Nginx yapılandırması
3. Alan adı kurulumu
4. Mail sunucusu kurulumu

### Faz 2: Test ve Doğrulama (1 Hafta)
5. User CRUD Test
6. Group CRUD Test
7. RabbitMQ Events (temel implementasyon)

### Faz 3: Altyapı Servisleri (2-3 Hafta)
8. MinIO Infrastructure Setup
9. MngStorage Servis (Project Setup + Core Features)
10. API Gateway (Ocelot Setup)

### Faz 4: İş Mantığı Servisleri (3-4 Hafta)
11. MngScheduler Servis (Hangfire Setup + Dynamic Jobs)
12. MngChatBot Servis (Docker Infrastructure + Core Services)

### Faz 5: Gelişmiş Özellikler (2-3 Hafta)
13. MngChatBot Servis (Function Calling & Tool Use)
14. SonarQube Karar ve Kurulumu (opsiyonel)
15. MngDataGateway Servis (planlama ve implementasyon)

---

## 📝 Notlar

- **Güvenlik Önceliği:** Şifrelerin elden geçirilmesi ve port/nginx yapılandırması güvenlik açısından kritik, öncelikli olarak yapılmalı.
- **Pipeline Performans:** Cache çalışıyor ancak etkisi beklenenden düşük. Mevcut süre (8:14 dakika) idare edilebilir seviyede.
- **Alan Adı ve Mail:** Production için gerekli, ancak development ortamında opsiyonel.
- **SonarQube:** Code quality için faydalı ancak opsiyonel. Air-gapped sistem uyumluluğu kontrol edilmeli.
- **Test Önceliği:** User ve Group CRUD testleri yüksek öncelikli çünkü temel işlevsellik.
- **Mikroservis Geliştirme:** MngStorage, MngScheduler, MngChatBot servisleri sırayla geliştirilebilir.
- **API Gateway:** Tüm servisler hazır olduğunda API Gateway kurulabilir.
- **MngDataGateway:** Detaylar belirlenecek, planlama aşamasında.

---

**Son Güncelleme:** 2 Ocak 2026

