# Backend Services & API Gateway Smoke Tests

**Son Güncelleme:** 7 Ocak 2026  
**Durum:** ✅ Test Script Hazır ve Çalışıyor

---

## 📋 Genel Bakış

Backend servislerin ve API Gateway'in doğru çalıştığını doğrulamak için kapsamlı smoke test script'i. Bu testler:

1. **Direct Health Checks**: Tüm backend servislerin direkt health endpoint'lerini test eder
2. **Gateway Route Tests**: API Gateway üzerinden route'ları test eder
3. **Auth Flow Tests**: Authentication akışını test eder (token alma, authenticated request'ler)
4. **Basic Scenarios**: Temel end-to-end senaryoları test eder

---

## 🚀 Kullanım

### Temel Kullanım

```powershell
cd scripts/tests
.\smoke-test-backend-gateway.ps1
```

### Parametreler

```powershell
.\smoke-test-backend-gateway.ps1 `
    -DirectBaseUrl "https://localhost" `
    -GatewayBaseUrl "https://localhost:5040" `
    -DomainName "meral" `
    -Username "meral_admin" `
    -Password "Admin123!" `
    -TestDirectHealth `
    -TestGatewayRoutes `
    -TestAuthFlow `
    -TestBasicScenarios `
    -SkipCertificateCheck
```

### Parametre Açıklamaları

| Parametre | Varsayılan | Açıklama |
|-----------|------------|----------|
| `-DirectBaseUrl` | `https://localhost` | Direkt servis erişimi için base URL |
| `-GatewayBaseUrl` | `https://localhost:5040` | API Gateway URL'i |
| `-DomainName` | `meral` | Test domain adı |
| `-Username` | `meral_admin` | Test kullanıcı adı |
| `-Password` | `Admin123!` | Test şifresi |
| `-TestDirectHealth` | `$true` | Direct health check testlerini çalıştır |
| `-TestGatewayRoutes` | `$true` | Gateway route testlerini çalıştır |
| `-TestAuthFlow` | `$true` | Authentication flow testlerini çalıştır |
| `-TestBasicScenarios` | `$true` | Temel senaryo testlerini çalıştır |
| `-SkipCertificateCheck` | `$true` | SSL sertifika doğrulamasını atla |
| `-Verbose` | `$false` | Detaylı çıktı göster |

---

## 📊 Test Kapsamı

### 1. Direct Health Check Tests

Backend servislerin direkt health endpoint'lerini test eder:

- ✅ **MngGateway Health** (`http://localhost:5000/health` veya `https://localhost:5443/health`)
- ✅ **MngKeeper Health** (`https://localhost:5001/health`)
- ✅ **MngKeeper Version** (`https://localhost:5001/api/version/short`)
- ✅ **MngKeeper Health Ready** (`https://localhost:5001/health/ready`)
- ✅ **MngDataGateway Health** (`https://localhost:5010/api/v1/health`)
- ✅ **MngDataGateway Health Live** (`https://localhost:5010/api/v1/health/live`)
- ✅ **MngDataGateway Health Ready** (`https://localhost:5010/api/v1/health/ready`)
- ✅ **MngHub Health** (`http://localhost:5020/health`)

### 2. Gateway Route Tests

API Gateway üzerinden route'ları test eder:

- ✅ **Gateway Health** (`https://localhost:5040/health`)
- ✅ **Gateway → MngKeeper** (`https://localhost:5040/keeper/api/version/short`)
- ✅ **Gateway → MngDataGateway** (`https://localhost:5040/data/api/v1/health`)
- ✅ **Gateway → MngHub** (`https://localhost:5040/hub/health`)
- ✅ **Gateway → Keycloak** (`https://localhost:5040/auth/realms/master`)

### 3. Authentication Flow Tests

Authentication akışını test eder:

- ✅ **Get Token (Direct MngKeeper)** - MngKeeper'dan direkt token alma
- ✅ **Get Token (via Gateway)** - Gateway üzerinden token alma
- ✅ **Token Comparison** - Direct vs Gateway token'larını karşılaştırma
- ✅ **Authenticated Request - Get Domains (Direct)** - Direkt authenticated request
- ✅ **Authenticated Request - Get Users (Direct)** - Direkt authenticated request
- ✅ **Authenticated Request - Get Domains (via Gateway)** - Gateway üzerinden authenticated request
- ✅ **Authenticated Request - Get Users (via Gateway)** - Gateway üzerinden authenticated request

### 4. Basic Scenario Tests

Temel end-to-end senaryoları test eder:

- ✅ **Scenario 1: Domain Management Flow**
  - Get Domains
  - Get Domain by ID
- ✅ **Scenario 2: User Management Flow**
  - Get Users
- ✅ **Scenario 3: MngDataGateway Health Check**
  - Get Health Status (MongoDB, RabbitMQ, Disk)

---

## ✅ Beklenen Sonuçlar

### Başarılı Test Sonucu

```
╔════════════════════════════════════════════════════════════════╗
║  Test Summary                                                  ║
╚════════════════════════════════════════════════════════════════╝

  Total Tests:  22
  Passed:       22
  Failed:       0
  Skipped:      0
  Duration:     3.38s

✓ All tests passed! ✓
```

### Başarısız Test Durumları

**1. MngGateway Health Check Başarısız:**
- **Neden:** Gateway HTTP veya HTTPS port'unda çalışmıyor olabilir
- **Çözüm:** Gateway container'ının çalıştığını kontrol edin:
  ```bash
  docker compose -f ApplicationResources/mng_apps/docker-compose.production.yml ps mnggateway
  ```

**2. Authentication Flow Başarısız:**
- **Neden:** Domain veya kullanıcı bulunamıyor
- **Çözüm:** Test domain ve kullanıcının var olduğundan emin olun

**3. Gateway Route Başarısız:**
- **Neden:** Gateway route'ları yanlış yapılandırılmış olabilir
- **Çözüm:** `MngGateway/Presentation/MngGateway.Api/ocelot.json` dosyasını kontrol edin

---

## 🔧 Sorun Giderme

### SSL Sertifika Hataları

Test script'i varsayılan olarak SSL sertifika doğrulamasını atlar (`-SkipCertificateCheck`). Eğer sertifika hataları alıyorsanız:

1. Sertifikaların geçerli olduğundan emin olun
2. Localhost için self-signed sertifika kullanılıyorsa, `-SkipCertificateCheck` parametresini kullanın

### Gateway Port Sorunları

Gateway'in farklı bir port'ta çalışıyorsa:

```powershell
.\smoke-test-backend-gateway.ps1 -GatewayBaseUrl "https://localhost:5040"
```

### Test Domain Bulunamıyor

Test domain'i yoksa, önce domain oluşturun:

```powershell
# MngKeeper üzerinden domain oluştur
# veya mevcut bir domain kullanın
.\smoke-test-backend-gateway.ps1 -DomainName "your-domain" -Username "your-admin" -Password "your-password"
```

---

## 📝 Test Sonuçları Raporlama

Test sonuçları console'a yazdırılır. Ayrıca test detayları `$script:TestResults` içinde saklanır:

- **Total**: Toplam test sayısı
- **Passed**: Başarılı test sayısı
- **Failed**: Başarısız test sayısı
- **Skipped**: Atlanan test sayısı
- **Details**: Her test için detaylı bilgiler

---

## 🎯 CI/CD Entegrasyonu

Bu test script'i CI/CD pipeline'larında kullanılabilir:

```yaml
# .gitlab-ci.yml örneği
smoke_test:
  stage: test
  script:
    - pwsh -ExecutionPolicy Bypass -File scripts/tests/smoke-test-backend-gateway.ps1
  only:
    - main
    - develop
```

---

## 📚 İlgili Dokümantasyon

- [Health Check Durumu](../content/cicd/HEALTH_CHECK_STATUS.md) - Tüm servislerin health check endpoint'leri
- [API Gateway Yapılandırması](../../MngGateway/README.md) - Gateway route yapılandırması
- [Deployment Status](../deployment/current_status.md) - Deployment durumu ve servis erişim bilgileri

---

## 🔄 Güncelleme Geçmişi

- **7 Ocak 2026**: İlk sürüm oluşturuldu
  - Direct health check testleri eklendi
  - Gateway route testleri eklendi
  - Authentication flow testleri eklendi
  - Temel senaryo testleri eklendi

---

**Son Güncelleme:** 7 Ocak 2026

