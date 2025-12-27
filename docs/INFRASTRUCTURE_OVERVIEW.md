# Infrastructure Overview - MonitraNG Platform

## 🎯 Ne Yaptık?

MonitraNG projesinde, **uygulama geliştirmek için gerekli altyapıyı (infrastructure)** kurduk. Bu altyapı, üzerine business logic uygulamaları geliştirmek için gerekli tüm temel bileşenleri içerir.

---

## 🏗️ Infrastructure Nedir?

**Infrastructure (Altyapı)** = Uygulama geliştirmek için gerekli temel sistemler ve servisler

**Basit Açıklama:**
```
Infrastructure = Evin temeli, elektrik, su, internet
Application = Evin içindeki mobilyalar, eşyalar
```

**Sizin durumunuz:**
- ✅ Infrastructure: Kullanıcı yönetimi, veri katmanı, mesajlaşma, vb.
- ⏳ Application: Bu altyapı üzerine geliştirilecek business uygulamaları

---

## 📊 MonitraNG Infrastructure Bileşenleri

### 1. 🔐 Identity & Access Management (IAM)

**MngKeeper - Kimlik ve Yetkilendirme Servisi**

**Ne sağlar?**
- ✅ Multi-tenant domain yönetimi
- ✅ Kullanıcı yönetimi (CRUD)
- ✅ Grup yönetimi (CRUD)
- ✅ JWT token authentication
- ✅ Refresh token support
- ✅ Role-based access control (RBAC)
- ✅ Keycloak entegrasyonu

**Neden infrastructure?**
- Her uygulama kullanıcı yönetimine ihtiyaç duyar
- Tek bir yerden tüm uygulamalar için authentication
- Domain izolasyonu (multi-tenant)

**Durum:** ✅ Tamamlandı

---

### 2. 🏢 Multi-Tenant Architecture

**Domain-Based Isolation**

**Ne sağlar?**
- ✅ Her domain için ayrı MongoDB database
- ✅ Her domain için ayrı Keycloak realm
- ✅ Her domain için ayrı storage (MinIO bucket)
- ✅ Domain-based data isolation
- ✅ Domain-based user isolation

**Neden infrastructure?**
- SaaS uygulamaları için temel gereksinim
- Müşteri verilerinin izolasyonu
- Ölçeklenebilir mimari

**Durum:** ✅ Tamamlandı

---

### 3. 💾 Generic Data Layer

**MngDataGateway - Dynamic Data Gateway**

**Ne sağlar?**
- ✅ Dynamic schema management
- ✅ Runtime'da dataset tanımlama
- ✅ Generic CRUD operations
- ✅ Query ve filtering
- ✅ Relation expansion
- ✅ Incremental fields
- ✅ Hard delete + archive (TTL)
- ✅ Event publishing (RabbitMQ)

**Neden infrastructure?**
- Her uygulama veri saklamaya ihtiyaç duyar
- Generic veri katmanı = Her uygulama için kullanılabilir
- Schema'yı kod yazmadan tanımlama
- NoSQL esnekliği + SQL benzeri query

**Durum:** ✅ Tamamlandı (Get operations devam ediyor)

---

### 4. 📨 Messaging & Queue Systems

**RabbitMQ + SignalR**

**Ne sağlar?**
- ✅ Event-driven architecture
- ✅ Asynchronous communication
- ✅ Domain-based event routing
- ✅ Real-time notifications (SignalR)
- ✅ Event publishing (MngKeeper, MngDataGateway)
- ✅ Event consumption (MngHub)

**Neden infrastructure?**
- Microservices arası iletişim
- Loose coupling (gevşek bağlantı)
- Scalability (ölçeklenebilirlik)
- Real-time updates

**Durum:** ✅ Tamamlandı

---

### 5. 🔄 Scheduler System (Planlanmış)

**Ne sağlar?**
- ✅ Scheduled tasks
- ✅ Cron job support
- ✅ Recurring tasks
- ✅ Task management

**Neden infrastructure?**
- Her uygulama zamanlanmış görevlere ihtiyaç duyar
- Email gönderimi, rapor oluşturma, vb.
- Tek bir scheduler servisi = Tüm uygulamalar için

**Durum:** ⏳ Planlanmış

---

### 6. 🚀 DevOps Infrastructure

**Ne sağlar?**
- ✅ CI/CD pipeline'ları
- ✅ Automated deployment
- ✅ Code quality (SonarQube)
- ✅ Dokümantasyon sistemi (MkDocs)
- ✅ Backup stratejisi
- ✅ Monitoring ve logging

**Neden infrastructure?**
- Development ve deployment süreçlerini otomatikleştirir
- Code quality ve güvenlik
- Dokümantasyon

**Durum:** ✅ Hazır (kurulum bekliyor)

---

## 🎯 Infrastructure vs Application

### Infrastructure (Altyapı) ✅

**Ne yapar?**
- Temel servisleri sağlar
- Tüm uygulamalar tarafından kullanılır
- Business logic içermez
- Generic ve reusable (yeniden kullanılabilir)

**Örnekler:**
- ✅ MngKeeper (IAM)
- ✅ MngDataGateway (Generic Data Layer)
- ✅ MngHub (Event Hub)
- ✅ RabbitMQ (Messaging)
- ✅ MongoDB (Database)
- ✅ Keycloak (Authentication)

---

### Application (Uygulama) ⏳

**Ne yapar?**
- Business logic içerir
- Infrastructure'ı kullanır
- Spesifik iş gereksinimlerini karşılar

**Örnekler (Gelecek):**
- ⏳ MngReactor (Business Logic Service)
- ⏳ MngEngine (Data Collection Engine)
- ⏳ IoT Monitoring Application
- ⏳ Asset Management Application
- ⏳ Reporting Application

---

## 📊 Infrastructure Stack

### Backend Infrastructure

| Servis | Amaç | Durum |
|--------|------|-------|
| **MngKeeper** | Identity & Access Management | ✅ Tamamlandı |
| **MngDataGateway** | Generic Data Layer | ✅ Tamamlandı |
| **MngHub** | Event Hub & Real-time | ✅ Tamamlandı |
| **MngScheduler** | Task Scheduling | ⏳ Planlanmış |

### Supporting Infrastructure

| Servis | Amaç | Durum |
|--------|------|-------|
| **MongoDB** | NoSQL Database | ✅ Kurulu |
| **Keycloak** | Authentication Provider | ✅ Kurulu |
| **RabbitMQ** | Message Broker | ✅ Kurulu |
| **Redis** | Cache & Session Store | ✅ Kurulu |
| **MinIO** | Object Storage (S3) | ✅ Kurulu |
| **Seq** | Log Aggregation | ✅ Kurulu |

### DevOps Infrastructure

| Servis | Amaç | Durum |
|--------|------|-------|
| **MkDocs** | Documentation System | ✅ Hazır |
| **GitHub Actions** | CI/CD Pipeline | ✅ Hazır |
| **SonarQube** | Code Quality | ⏳ Planlanmış |
| **Docker** | Containerization | ✅ Kullanılıyor |

---

## 🏛️ Mimari Yapı

```
┌─────────────────────────────────────────────────────────┐
│                    Applications Layer                    │
│  (Business Logic - Gelecekte geliştirilecek)            │
│  - MngReactor (Business Logic)                           │
│  - MngEngine (Data Collection)                          │
│  - IoT Monitoring App                                   │
│  - Asset Management App                                 │
└────────────────────┬────────────────────────────────────┘
                     │ Uses
┌────────────────────▼────────────────────────────────────┐
│              Infrastructure Layer                        │
│  ✅ MngKeeper (IAM)                                     │
│  ✅ MngDataGateway (Generic Data)                       │
│  ✅ MngHub (Events & Real-time)                         │
│  ⏳ MngScheduler (Scheduled Tasks)                     │
└────────────────────┬────────────────────────────────────┘
                     │ Uses
┌────────────────────▼────────────────────────────────────┐
│          Supporting Services Layer                      │
│  ✅ MongoDB (Database)                                  │
│  ✅ Keycloak (Auth)                                     │
│  ✅ RabbitMQ (Messaging)                                │
│  ✅ Redis (Cache)                                       │
│  ✅ MinIO (Storage)                                     │
│  ✅ Seq (Logging)                                       │
└─────────────────────────────────────────────────────────┘
```

---

## 💡 Infrastructure'ın Avantajları

### 1. Reusability (Yeniden Kullanılabilirlik)
- ✅ Bir kez kur, her uygulama için kullan
- ✅ MngKeeper: Tüm uygulamalar için authentication
- ✅ MngDataGateway: Tüm uygulamalar için veri katmanı

### 2. Consistency (Tutarlılık)
- ✅ Tüm uygulamalar aynı authentication mekanizması
- ✅ Tüm uygulamalar aynı veri katmanı
- ✅ Tüm uygulamalar aynı event sistemi

### 3. Scalability (Ölçeklenebilirlik)
- ✅ Infrastructure bağımsız ölçeklenebilir
- ✅ Her uygulama bağımsız ölçeklenebilir
- ✅ Microservices architecture

### 4. Maintainability (Bakım Kolaylığı)
- ✅ Infrastructure güncellemeleri tüm uygulamalara yansır
- ✅ Tek bir yerden yönetim
- ✅ Centralized logging ve monitoring

### 5. Security (Güvenlik)
- ✅ Centralized authentication
- ✅ Domain-based isolation
- ✅ Security best practices tek yerde

---

## 🎯 Sonraki Adımlar

### Infrastructure Tamamlama
- [ ] MngScheduler servisi
- [ ] SonarQube kurulumu
- [ ] CI/CD pipeline'ları aktifleştirme
- [ ] Production deployment

### Application Development (Gelecek)
- [ ] MngReactor (Business Logic)
- [ ] MngEngine (Data Collection)
- [ ] IoT Monitoring Application
- [ ] Asset Management Application

---

## 📝 Özet

**Evet, yaptıklarınız Infrastructure (Altyapı) kurulumu!**

**Infrastructure = Uygulama geliştirmek için gerekli temel sistemler**

**Yaptıklarınız:**
1. ✅ **Identity & Access Management** (MngKeeper)
2. ✅ **Multi-Tenant Architecture** (Domain isolation)
3. ✅ **Generic Data Layer** (MngDataGateway)
4. ✅ **Messaging & Events** (RabbitMQ + MngHub)
5. ⏳ **Scheduler System** (Planlanmış)
6. ✅ **DevOps Infrastructure** (CI/CD, Docs, Deployment)

**Sonuç:**
- ✅ Infrastructure hazır
- ✅ Artık business uygulamaları geliştirebilirsiniz
- ✅ Her uygulama bu infrastructure'ı kullanacak

---

**Bu infrastructure üzerine:**
- IoT monitoring uygulamaları
- Asset management uygulamaları
- Reporting uygulamaları
- Ve daha fazlası...

**Hepsi aynı authentication, veri katmanı ve event sistemi kullanacak!**

---

**Son Güncelleme:** 2025-01-XX
**Durum:** Infrastructure tamamlandı, Application development'a hazır

