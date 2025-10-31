# MonitraNG - Multi-Tenant IoT Monitoring Platform

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Nuxt](https://img.shields.io/badge/Nuxt-3.13-00DC82?logo=nuxt.js)](https://nuxt.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-47A248?logo=mongodb)](https://www.mongodb.com/)
[![Keycloak](https://img.shields.io/badge/Keycloak-23.0.3-0080FF)](https://www.keycloak.org/)

Modern, multi-tenant IoT monitoring ve yönetim platformu. Mikroservis mimarisi, Clean Architecture ve Keycloak tabanlı authentication ile geliştirilmiştir.

> **Not:** Bu bir **mono-repository**'dir. Her servis kendi versiyonunu yönetir.

## 🏗️ Mono-Repository Yapısı

Bu repository 4 ana servisi içerir:

```
MonitraNG/ (Mono-repo)
├── MngKeeper/              # 🔐 Authorization & Multi-tenant Management
│   ├── Version: 1.0.0
│   ├── Core/ (Application + Domain)
│   ├── Infrastructure/ (Services + Persistence)
│   ├── Presentation/ (API)
│   ├── README.md           # MngKeeper dokümantasyonu
│   ├── VERSION.md          # Versiyonlama stratejisi
│   └── CHANGELOG.md        # Değişiklik geçmişi
│
├── MngReactor/             # ⚙️ Main Business Logic Service
│   ├── Version: -
│   └── (Coming soon)
│
├── MngEngine/              # 📊 Data Collection Engine  
│   ├── Version: -
│   └── (Coming soon)
│
├── Mng.Ui/                 # 🎨 Frontend (Nuxt 3 + Vuetify)
│   ├── Version: 6.0.0
│   └── (In development)
│
└── ApplicationResources/   # 🐳 Shared Docker configs, Test data
```

## 🎯 Servisler

### 🔐 [MngKeeper](MngKeeper/) - Authorization Service
**Version:** 1.0.0 | **Status:** ✅ Production Ready

Multi-tenant kimlik doğrulama ve yetkilendirme servisi.

**Özellikler:**
- Keycloak entegrasyonu
- Domain, User, Group yönetimi
- JWT + Refresh token
- Multi-database support

**Dokümantasyon:** [MngKeeper/README.md](MngKeeper/README.md)

---

### ⚙️ [MngReactor](MngReactor/) - Business Logic Service
**Version:** - | **Status:** 🚧 In Development

Ana iş mantığı ve veri işleme servisi.

**Özellikler:**
- Asset management
- Data processing
- LDAP integration
- Token validation

**Dokümantasyon:** [MngReactor/README.md](MngReactor/README.md)

---

### 📊 [MngEngine](MngEngine/) - Data Collection Engine
**Version:** - | **Status:** 🚧 In Development

Veri toplama ve işleme motoru.

**Özellikler:**
- Scheduled jobs (Quartz)
- Linux/Windows collectors
- REST API integration

**Dokümantasyon:** [MngEngine/README.md](MngEngine/README.md)

---

### 🎨 [Mng.Ui](Mng.Ui/) - Frontend Application
**Version:** 6.0.0 | **Status:** 🚧 In Development

Nuxt 3 + Vuetify tabanlı modern web arayüzü.

**Özellikler:**
- Material Design
- Responsive design
- Pinia state management

**Dokümantasyon:** [Mng.Ui/README.md](Mng.Ui/README.md)

## 🛠️ Teknoloji Stack

### Backend
| Teknoloji | Versiyon | Kullanım |
|-----------|----------|----------|
| .NET | 9.0 | Application framework |
| MongoDB | 7.0 | NoSQL database |
| Keycloak | 23.0.3 | Identity & access management |
| Redis | 7 | Cache & session store |
| RabbitMQ | 3 | Message broker |
| Mosquitto | 2.0 | MQTT broker |
| Seq | Latest | Log aggregation |

### Frontend
| Teknoloji | Versiyon | Kullanım |
|-----------|----------|----------|
| Nuxt | 3.13 | Vue.js framework |
| Vuetify | 3.7 | Material Design UI |
| Pinia | 2.2 | State management |
| TypeScript | Latest | Type safety |

### Patterns & Architecture
- Clean Architecture
- CQRS + MediatR
- Repository Pattern
- Dependency Injection
- Domain-Driven Design (DDD)

## 📋 Gereksinimler

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

## 🚀 Hızlı Başlangıç

### 1. Repository'yi Klonlayın

```bash
git clone https://github.com/serkanmeral/MonitraNG.git
cd MonitraNG
```

### 2. Docker Container'ları Başlatın

```bash
cd ApplicationResources/mng_common
docker-compose up -d
```

**Başlatılan Servisler:**
- 🗄️ MongoDB (27017)
- 🔐 Keycloak (8080)
- 💾 Redis (6379)
- 📨 RabbitMQ (5672, Management: 15672)
- 📡 Mosquitto MQTT (1883)
- 📝 Seq Logs (5341)
- 🌐 Mongo Express (8081)

### 3. Her Servisi Başlatın

Her servisin kendi README'sinde detaylı kurulum talimatları vardır:

- **MngKeeper:** [Kurulum Talimatları](MngKeeper/README.md#kurulum)
- **MngReactor:** [Kurulum Talimatları](MngReactor/README.md)
- **MngEngine:** [Kurulum Talimatları](MngEngine/README.md)
- **Mng.Ui:** [Kurulum Talimatları](Mng.Ui/README.md)

## 📊 Monitoring & Admin Tools

Platform çalışırken erişilebilir admin araçları:

| Tool | URL | Kullanım |
|------|-----|----------|
| **Seq** | http://localhost:5341 | Application logs |
| **Mongo Express** | http://localhost:8081 | Database UI |
| **RabbitMQ** | http://localhost:15672 | Message queue |
| **Keycloak** | http://localhost:8080 | Identity management |
| **Portainer** | http://localhost:9000 | Container management |

**Credentials:** Username: `admin`, Password: `admin123`

## 🧪 Testing

Her servisin kendi test script'leri vardır. Detaylar için ilgili servisin README'sine bakın.

**MngKeeper Test:**
```powershell
cd ApplicationResources/test_data/mng_keeper
# Domain, User, Auth testleri
```

## 📦 Deployment

Her servis için deployment talimatları kendi README'sinde bulunur:
- [MngKeeper Deployment](MngKeeper/README.md#deployment)
- [MngReactor Deployment](MngReactor/README.md#deployment)
- [MngEngine Deployment](MngEngine/README.md#deployment)

## 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'feat: Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request oluşturun

### Commit Message Convention

```
feat: Yeni özellik
fix: Bug düzeltmesi
docs: Dokümantasyon
refactor: Code refactoring
test: Test ekleme/düzeltme
chore: Build, dependencies güncellemesi
```

## 📄 Lisans

Bu proje MIT lisansı altında lisanslanmıştır. Detaylar için [LICENSE](LICENSE) dosyasına bakın.

## 👥 Ekip

**Proje Sahibi:** Serkan MERAL  
**Email:** serkan.meral@isimplatform.io  
**GitHub:** [@serkanmeral](https://github.com/serkanmeral)

## 🔗 Bağlantılar

- [GitHub Repository](https://github.com/serkanmeral/MonitraNG)
- [Product Requirements Document](prd.md)
- [MngKeeper Documentation](MngKeeper/README.md)
- [API Documentation](http://localhost:5001/api-docs)

## 📈 Roadmap

### ✅ Tamamlanan (MngKeeper v1.0.0)
- [x] Clean Architecture implementation
- [x] Multi-tenant authentication (Keycloak)
- [x] Domain, User, Group management
- [x] Refresh token support
- [x] API documentation (Swagger + Scalar)
- [x] Logging infrastructure (Seq)
- [x] Versioning system

### 🚧 Devam Eden
- [ ] **MngKeeper v1.1.0**
  - [ ] Unit & Integration tests
  - [ ] Performance optimization
  - [ ] Advanced RBAC
  
- [ ] **MngReactor**
  - [ ] Token validation middleware
  - [ ] Asset management
  - [ ] Data processing pipeline
  
- [ ] **MngEngine**
  - [ ] Data collectors
  - [ ] Job scheduling
  - [ ] MngReactor integration
  
- [ ] **Mng.Ui**
  - [ ] Authentication flow
  - [ ] Domain management UI
  - [ ] User management UI

### 🔮 Planlanan
- [ ] OPC UA integration
- [ ] Real-time monitoring
- [ ] Alerting system
- [ ] Mobile app
- [ ] API rate limiting
- [ ] Distributed caching

## 🔄 Versiyonlama

Her servis **bağımsız versiyonlanır** (Semantic Versioning):

```
MngKeeper  → v1.0.0  ✅ Production
MngReactor → v0.1.0  🚧 Development  
MngEngine  → v0.1.0  🚧 Development
Mng.Ui     → v6.0.0  🚧 Development
```

**Versiyon komutları her servis klasöründe:**
```powershell
cd MngKeeper
.\version.ps1 current           # Mevcut versiyon
.\version.ps1 bump-patch        # Bug fix (1.0.0 → 1.0.1)
.\version.ps1 bump-minor        # Feature (1.0.0 → 1.1.0)
.\version.ps1 bump-major        # Breaking (1.0.0 → 2.0.0)
.\release.ps1 -BumpType minor   # Otomatik release
```

## 📞 Destek

Sorularınız için:
- GitHub Issues: [Issues](https://github.com/serkanmeral/MonitraNG/issues)
- Email: serkan.meral@isimplatform.io

---

**⭐ Projeyi beğendiyseniz yıldız vermeyi unutmayın!**

