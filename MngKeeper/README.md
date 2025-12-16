# MngKeeper API

**Identity & Access Management (IAM) Microservice**

MngKeeper, MonitraNG ekosisteminin merkezi kimlik ve yetkilendirme servisidir. Multi-tenant domain yönetimi, kullanıcı/grup yönetimi ve JWT token tabanlı authentication sağlar.

---

## 🚀 Hızlı Başlangıç

### Infrastructure Başlatma

```bash
cd ApplicationResources/mng_common
docker-compose up -d
```

### MngKeeper Başlatma

```bash
cd MngKeeper/Presentation/MngKeeper.Api
dotnet run
```

**API:** `https://localhost:5001`

---

## 📚 Dokümantasyon

Tüm detaylı dokümantasyon için `docs` klasörüne bakın:

- **[README.md](docs/README.md)** - Tam API dokümantasyonu ve kullanım örnekleri
- **[ROADMAP.md](docs/ROADMAP.md)** - Geliştirme planı ve tamamlanan özellikler
- **[ENVIRONMENT_VARIABLES.md](docs/ENVIRONMENT_VARIABLES.md)** - Environment değişkenleri referansı
- **[CHANGELOG.md](docs/CHANGELOG.md)** - Değişiklik geçmişi
- **[VERSION.md](docs/VERSION.md)** - Versiyon bilgileri
- **[CLEANUP_INSTRUCTIONS.md](docs/CLEANUP_INSTRUCTIONS.md)** - Test verilerini temizleme talimatları
- **[tests-README.md](docs/tests-README.md)** - Test script'leri dokümantasyonu

---

## 🎯 Özellikler

- ✅ **Multi-Tenant Domain Management** - Her domain kendi database, realm ve storage ile izole
- ✅ **Keycloak Integration** - Enterprise-grade authentication
- ✅ **JWT Token Authentication** - Custom claims (user_groups, isAdmin)
- ✅ **Pipeline Architecture** - 11 adımlı domain creation workflow
- ✅ **Redis Cache** - Yüksek performans için user/group cache
- ✅ **RabbitMQ Events** - Real-time event publishing
- ✅ **MinIO Storage** - S3-compatible object storage
- ✅ **Clean Architecture** - Domain, Application, Infrastructure, Presentation katmanları

---

## 🔧 Test Verilerini Temizleme

Tüm test domain'lerini temizlemek için:

```powershell
cd MngKeeper/tests
.\clean_domains.ps1
```

Bu script şunları temizler:
- Keycloak realm'leri (master hariç)
- MongoDB database'leri (`mng_*`)
- MinIO bucket'ları

Detaylı bilgi için: [docs/CLEANUP_INSTRUCTIONS.md](docs/CLEANUP_INSTRUCTIONS.md)

---

## 📊 API Endpoints

**Toplam: 18 Production-Ready Endpoints**

- 🏢 Domain Management (2)
- 🔐 Authentication (3)
- 🔧 Admin Operations (1)
- 👥 User Management (5)
- 👪 Group Management (5)
- 🔗 User-Group Assignment (2)

Detaylı API dokümantasyonu: [docs/README.md](docs/README.md)

---

## 🔗 Hızlı Linkler

- **Swagger UI:** https://localhost:5001/swagger
- **GraphQL Playground:** https://localhost:5001/graphql
- **Keycloak Admin:** http://localhost:8080/admin
- **Mongo Express:** http://localhost:8081
- **RabbitMQ Management:** http://localhost:15672
- **MinIO Console:** http://localhost:9091
- **Redis Commander:** http://localhost:8001
- **Seq Logs:** http://localhost:5341

---

## 📄 License

MIT License - MonitraNG Project

---

**Son Güncelleme:** 2025-12-16  
**Version:** 1.0.0  
**Status:** ✅ Production Ready

