# Merkezi Sertifika Yönetimi Planı

**Versiyon:** 2.1.0  
**Tarih:** 30 Aralık 2025  
**Durum:** 📋 Planlama Aşaması  
**Güncellemeler:**
- Internal CA yaklaşımı eklendi - Servisler arası sertifika validasyon sorunları çözüldü
- Sertifika bilgileri güncellendi: Organization "Serkan MERAL" olarak değiştirildi
- Sertifika bilgileri config'den okunacak şekilde planlandı

---

## 📋 Genel Bakış

MonitraNG ekosisteminde tüm servislerin (MngKeeper, MngHub, Mng.Ui, vb.) HTTPS/WSS desteği için merkezi bir sertifika yönetim sistemi kurulması planlanmaktadır.

### Amaç

- ✅ Tüm sertifikaların tek bir merkezden yönetilmesi
- ✅ MinIO üzerinde merkezi sertifika deposu
- ✅ Servislerin başlangıçta sertifikaları MinIO'dan okuması
- ✅ Development ve Production ortamları için farklı stratejiler
- ✅ Sertifika rotation ve yenileme mekanizması
- ✅ UI'dan sertifika yönetimi (opsiyonel)
- ✅ **Internal CA (Certificate Authority) ile servisler arası güvenli iletişim**
- ✅ **Tüm servislerin birbirleriyle sertifika validasyon sorunları olmadan çalışması**

---

## 🏗️ Mimari Tasarım

### 1. Sertifika Depolama Yapısı

#### MinIO Bucket Yapısı

```
mng-system-bucket (global, domain-agnostic)
├── certificates/
│   ├── ca/                          # Internal Certificate Authority
│   │   ├── development/
│   │   │   ├── root-ca.crt          # Root CA Certificate (public)
│   │   │   ├── root-ca.key          # Root CA Private Key (encrypted)
│   │   │   └── metadata.json
│   │   ├── production/
│   │   │   ├── root-ca.crt
│   │   │   ├── root-ca.key
│   │   │   └── metadata.json
│   │   └── staging/
│   │       └── ...
│   ├── development/
│   │   ├── mngkeeper/
│   │   │   ├── cert.pem            # CA-signed certificate
│   │   │   ├── key.pem            # Private key
│   │   │   ├── chain.pem           # Certificate chain (cert + CA)
│   │   │   └── metadata.json
│   │   ├── mnghub/
│   │   │   ├── cert.pem
│   │   │   ├── key.pem
│   │   │   ├── chain.pem
│   │   │   └── metadata.json
│   │   ├── mngdatagateway/
│   │   │   ├── cert.pem
│   │   │   ├── key.pem
│   │   │   ├── chain.pem
│   │   │   └── metadata.json
│   │   ├── mngui/
│   │   │   ├── cert.pem
│   │   │   ├── key.pem
│   │   │   ├── chain.pem
│   │   │   └── metadata.json
│   │   ├── mngscheduler/            # Gelecek servisler için
│   │   │   └── ...
│   │   └── wildcard/                # Wildcard certificate (opsiyonel)
│   │       ├── cert.pem
│   │       ├── key.pem
│   │       └── metadata.json
│   ├── production/
│   │   ├── ca/
│   │   │   └── ...
│   │   ├── mngkeeper/
│   │   ├── mnghub/
│   │   ├── mngdatagateway/
│   │   ├── mngui/
│   │   └── ...
│   └── staging/
│       └── ...
```

**Not:** `mng-system-bucket` global bir bucket olacak ve domain oluşturma pipeline'ından bağımsız olarak sistem başlangıcında oluşturulacak.

#### Metadata Yapısı

**Service Certificate Metadata:**
```json
{
  "serviceName": "mngkeeper",
  "environment": "development",
  "dnsNames": ["localhost", "localhost:5001", "mngkeeper.internal"],
  "validFrom": "2025-12-30T00:00:00Z",
  "validTo": "2026-12-30T23:59:59Z",
  "issuer": "CN=MonitraNG Root CA, O=Serkan MERAL, C=TR",
  "subject": "CN=mngkeeper.internal, O=Serkan MERAL, L=UMRANIYE, ST=ISTANBUL, C=TR",
  "thumbprint": "ABC123...",
  "caThumbprint": "XYZ789...",
  "certificateType": "CA-Signed",
  "createdAt": "2025-12-30T00:00:00Z",
  "createdBy": "system",
  "lastUpdatedAt": "2025-12-30T00:00:00Z",
  "lastUpdatedBy": "system",
  "isActive": true,
  "notes": "CA-signed certificate for development"
}
```

**CA Certificate Metadata:**
```json
{
  "caType": "Root",
  "environment": "development",
  "commonName": "MonitraNG Root CA",
  "organization": "Serkan MERAL",
  "validFrom": "2025-12-30T00:00:00Z",
  "validTo": "2035-12-30T23:59:59Z",
  "thumbprint": "XYZ789...",
  "createdAt": "2025-12-30T00:00:00Z",
  "createdBy": "system",
  "isActive": true,
  "notes": "Root Certificate Authority for MonitraNG ecosystem"
}
```

---

## 🔧 Teknik Detaylar

### 2. Internal CA (Certificate Authority) Yaklaşımı

#### 2.1 Neden Internal CA?

**Problem:** Her servis için ayrı self-signed sertifika kullanıldığında:
- ❌ Servisler birbirleriyle iletişim kurarken sertifika validasyon hataları
- ❌ Her serviste diğer servislerin sertifikalarını manuel trust etme gereksinimi
- ❌ Browser/HTTP client'lar sertifika uyarıları verir

**Çözüm: Internal CA**
- ✅ Tek bir Root CA oluşturulur
- ✅ Tüm servis sertifikaları bu CA'dan imzalanır
- ✅ CA sertifikası tüm servislere dağıtılır
- ✅ Servisler CA'yı trust store'a ekler
- ✅ Servisler arası iletişimde validasyon sorunları olmaz
- ✅ Air-gapped sistemlerde çalışır (external CA'ya ihtiyaç yok)

#### 2.2 CA Yapısı

**Root CA:**
- 10 yıl geçerlilik (uzun ömürlü)
- Sadece servis sertifikalarını imzalar
- Private key çok güvenli saklanır (MinIO encryption at rest)

**Service Certificates:**
- 1 yıl geçerlilik (rotation için)
- Root CA tarafından imzalanır
- Her servis için ayrı DNS name (mngkeeper.internal, mnghub.internal, vb.)

#### 2.3 Trust Store Yönetimi

**Her servis startup'ta:**
1. CA sertifikasını MinIO'dan okur
2. CA'yı trust store'a ekler (memory veya system store)
3. Kendi sertifikasını okur
4. HTTPS/WSS bağlantılarını kurar

**Platform-specific:**
- **.NET (MngKeeper, MngHub, MngDataGateway):** `X509Store` veya memory trust
- **Node.js (Mng.Ui):** `https.globalAgent.options.ca` veya custom CA bundle
- **Docker Containers:** CA'yı container'a mount eder veya environment variable

---

### 3. Sertifika Yönetim Servisi

#### 3.1 MngKeeper'da ICertificateService

**Lokasyon:** `MngKeeper/Core/MngKeeper.Application/Interfaces/ICertificateService.cs`

```csharp
public interface ICertificateService
{
    // CA Yönetimi
    Task<X509Certificate2> GetRootCaCertificateAsync(
        string environment,
        CancellationToken cancellationToken = default);
    
    Task<CertificateInfo> CreateRootCaAsync(
        string environment,
        int validityYears = 10,
        CancellationToken cancellationToken = default);
    
    // Sertifika okuma
    Task<X509Certificate2> GetCertificateAsync(
        string serviceName, 
        string environment, 
        CancellationToken cancellationToken = default);
    
    // CA-signed sertifika oluşturma
    Task<CertificateInfo> CreateCaSignedCertificateAsync(
        string serviceName,
        string environment,
        string[] dnsNames,
        int validityDays = 365,
        CancellationToken cancellationToken = default);
    
    // Self-signed sertifika oluşturma (fallback)
    Task<CertificateInfo> CreateSelfSignedCertificateAsync(
        string serviceName,
        string environment,
        string[] dnsNames,
        int validityDays = 365,
        CancellationToken cancellationToken = default);
    
    // Sertifika yükleme (external)
    Task<CertificateInfo> UploadCertificateAsync(
        string serviceName,
        string environment,
        byte[] certBytes,
        byte[] keyBytes,
        CancellationToken cancellationToken = default);
    
    // Sertifika metadata
    Task<CertificateMetadata> GetCertificateMetadataAsync(
        string serviceName,
        string environment,
        CancellationToken cancellationToken = default);
    
    // Sertifika yenileme kontrolü
    Task<bool> IsCertificateExpiringSoonAsync(
        string serviceName,
        string environment,
        int daysThreshold = 30,
        CancellationToken cancellationToken = default);
    
    // Sertifika listesi
    Task<List<CertificateInfo>> ListCertificatesAsync(
        string? serviceName = null,
        string? environment = null,
        CancellationToken cancellationToken = default);
    
    // Trust store yönetimi
    Task AddCaToTrustStoreAsync(
        string environment,
        CancellationToken cancellationToken = default);
}
```

#### 3.2 CertificateService Implementasyonu

**Lokasyon:** `MngKeeper/Infrastructure/MngKeeper.Infrastructure/Services/Certificate/CertificateService.cs`

**Bağımlılıklar:**
- `IMinioService` - Sertifika dosyalarını MinIO'dan okuma/yazma
- `ILogger<CertificateService>` - Logging
- `MngKeeperSettings` - Configuration

**Özellikler:**
- **Root CA oluşturma ve yönetimi**
- **CA-signed sertifika oluşturma** (önerilen)
- MinIO'dan sertifika okuma/yazma
- Self-signed sertifika oluşturma (fallback)
- PEM format desteği (cert.pem, key.pem, chain.pem)
- Certificate chain oluşturma
- Metadata yönetimi
- Trust store yönetimi
- **Sertifika bilgileri config'den okunur** (Country, State, Locality, Organization)
- Cache mekanizması (Redis - opsiyonel)

#### 3.3 Program.cs Entegrasyonu

**Değişiklikler:**
- `CertificateHandler` yerine `ICertificateService` kullanımı
- Startup'ta CA oluşturma/yükleme
- CA'yı trust store'a ekleme
- Sertifika yoksa otomatik oluşturma (CA-signed)
- MinIO'dan sertifika okuma

```csharp
// Program.cs
var certificateService = serviceProvider.GetRequiredService<ICertificateService>();
var environment = builder.Environment.EnvironmentName.ToLower();

// 1. CA'yı trust store'a ekle (servisler arası iletişim için)
await certificateService.AddCaToTrustStoreAsync(environment, cancellationToken);

// 2. Kendi sertifikasını al (yoksa CA-signed oluştur)
var certificate = await certificateService.GetCertificateAsync(
    "mngkeeper", 
    environment,
    cancellationToken);

// 3. Kestrel HTTPS yapılandırması
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5001, listenOptions =>
    {
        listenOptions.UseHttps(certificate);
    });
});

// 4. HttpClient için CA trust (diğer servislere çağrı yaparken)
var httpClientHandler = new HttpClientHandler();
var caCert = await certificateService.GetRootCaCertificateAsync(environment, cancellationToken);
// CA'yı HttpClient'a ekle (platform-specific)
```

---

### 4. MngHub Entegrasyonu

#### 3.1 Sertifika Okuma

**Yaklaşım 1: MngKeeper API'den Okuma**
- MngHub, MngKeeper'ın `/api/certificate/{serviceName}` endpoint'ini çağırır
- JWT token ile authentication
- Sertifika PEM formatında döner

**Yaklaşım 2: MinIO'dan Direkt Okuma**
- MngHub, MinIO'ya direkt bağlanır (aynı credentials)
- `mng-system-bucket/certificates/{environment}/mnghub/` klasöründen okur
- Daha bağımsız, ancak MinIO credentials gerektirir

**Öneri:** Yaklaşım 1 (MngKeeper API) - Daha güvenli ve merkezi kontrol

#### 3.2 SignalR WSS Yapılandırması

```csharp
// Program.cs
var certificate = await GetCertificateFromKeeperAsync("mnghub", environment);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5020, listenOptions =>
    {
        listenOptions.UseHttps(certificate);
    });
});
```

---

### 5. MngDataGateway Entegrasyonu

#### 5.1 Sertifika Okuma

**Yaklaşım:** MngKeeper API'den Okuma (önerilen)
- MngDataGateway, MngKeeper'ın `/api/certificate/{serviceName}` endpoint'ini çağırır
- JWT token ile authentication
- Sertifika PEM formatında döner
- CA sertifikası da döner (trust için)

#### 5.2 Program.cs Güncellemesi

```csharp
// Program.cs
var certificateService = await GetCertificateServiceFromKeeperAsync();
var environment = builder.Environment.EnvironmentName.ToLower();

// CA'yı trust store'a ekle
await certificateService.AddCaToTrustStoreAsync(environment, cancellationToken);

// Kendi sertifikasını al
var certificate = await certificateService.GetCertificateAsync("mngdatagateway", environment, cancellationToken);

// Kestrel HTTPS yapılandırması
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5030, listenOptions =>
    {
        listenOptions.UseHttps(certificate);
    });
});
```

---

### 6. Mng.Ui (Nuxt) Entegrasyonu

#### 4.1 Development Ortamı

**Nuxt Dev Server HTTPS:**
- `nuxt.config.ts` içinde HTTPS yapılandırması
- Sertifika MinIO'dan okunur (server middleware)
- Veya local file system'den (development için)

#### 4.2 Production Ortamı

**Nginx Reverse Proxy:**
- Nginx, MinIO'dan sertifikayı okur
- HTTPS termination Nginx'te yapılır
- Nuxt uygulaması HTTP olarak çalışır (internal)

**Alternatif: Node.js HTTPS:**
- Nuxt server direkt HTTPS kullanır
- Sertifika MinIO'dan okunur (startup'ta)

---

## 📦 Implementation Planı

### Phase 1: Infrastructure Setup (2-3 gün)

#### 1.1 MinIO System Bucket Oluşturma

**Lokasyon:** `MngKeeper/Infrastructure/MngKeeper.Infrastructure/Services/SystemInitializationService.cs`

**Görevler:**
- [ ] `mng-system-bucket` bucket'ını oluştur
- [ ] `certificates/ca/` klasör yapısını oluştur (development, production, staging)
- [ ] `certificates/development/`, `certificates/production/`, `certificates/staging/` klasörlerini oluştur
- [ ] Her servis için alt klasörler oluştur (mngkeeper, mnghub, mngdatagateway, mngui, mngscheduler, vb.)
- [ ] System startup'ta otomatik çalıştır

#### 1.2 ICertificateService Interface

- [ ] `ICertificateService` interface'ini oluştur
- [ ] `CertificateInfo` ve `CertificateMetadata` DTO'larını oluştur

#### 1.3 CertificateService Implementation

- [ ] `CertificateService` implementasyonu
- [ ] **Root CA oluşturma** (RSA 4096-bit, 10 yıl)
- [ ] **CA-signed sertifika oluşturma** (CSR + imzalama)
- [ ] MinIO entegrasyonu (IMinioService)
- [ ] Self-signed sertifika oluşturma (fallback)
- [ ] PEM format okuma/yazma (cert.pem, key.pem, chain.pem)
- [ ] Certificate chain oluşturma
- [ ] Metadata yönetimi
- [ ] Trust store yönetimi (.NET X509Store)

---

### Phase 2: MngKeeper Entegrasyonu (1 gün)

#### 2.1 CertificateService Registration

- [ ] `ServiceRegistration.cs` içinde `ICertificateService` kaydı
- [ ] Dependency injection yapılandırması

#### 2.2 Program.cs Güncellemesi

- [ ] `CertificateHandler` yerine `ICertificateService` kullanımı
- [ ] Startup'ta CA oluşturma/yükleme
- [ ] CA'yı trust store'a ekleme
- [ ] Startup'ta sertifika okuma
- [ ] Sertifika yoksa otomatik oluşturma (CA-signed)
- [ ] Kestrel HTTPS yapılandırması
- [ ] HttpClient için CA trust yapılandırması

#### 2.3 Certificate API Controller (Opsiyonel)

**Lokasyon:** `MngKeeper/Presentation/MngKeeper.Api/Controllers/CertificateController.cs`

**Endpoint'ler:**
- `GET /api/certificate/{serviceName}` - Sertifika okuma
- `GET /api/certificate/{serviceName}/metadata` - Metadata okuma
- `POST /api/certificate/{serviceName}` - Sertifika yükleme
- `POST /api/certificate/{serviceName}/generate` - Self-signed oluşturma
- `GET /api/certificate` - Tüm sertifikaları listeleme

**Güvenlik:**
- Admin-only endpoint'ler
- JWT token validation
- Domain isolation (opsiyonel)

---

### Phase 3: MngHub Entegrasyonu (0.5 gün)

#### 3.1 Certificate Client

**Lokasyon:** `MngHub/Infrastructure/MngHub.Infrastructure/Services/Certificate/CertificateClient.cs`

**Özellikler:**
- MngKeeper API'den sertifika okuma
- CA sertifikasını alma ve trust store'a ekleme
- HTTP client (JWT token ile)
- Certificate caching (memory)
- Retry logic

#### 3.2 Program.cs Güncellemesi

- [ ] Startup'ta CA'yı trust store'a ekleme
- [ ] Startup'ta sertifika okuma
- [ ] Kestrel HTTPS yapılandırması
- [ ] SignalR WSS desteği
- [ ] HttpClient için CA trust

---

### Phase 3.5: MngDataGateway Entegrasyonu (0.5 gün)

#### 3.5.1 Certificate Client

- [ ] MngKeeper API'den sertifika okuma
- [ ] CA sertifikasını trust store'a ekleme

#### 3.5.2 Program.cs Güncellemesi

- [ ] Startup'ta CA'yı trust store'a ekleme
- [ ] Startup'ta sertifika okuma
- [ ] Kestrel HTTPS yapılandırması
- [ ] HttpClient için CA trust

#### 3.1 Certificate Client

**Lokasyon:** `MngHub/Infrastructure/MngHub.Infrastructure/Services/Certificate/CertificateClient.cs`

**Özellikler:**
- MngKeeper API'den sertifika okuma
- HTTP client (JWT token ile)
- Certificate caching (memory)
- Retry logic

#### 3.2 Program.cs Güncellemesi

- [ ] Startup'ta sertifika okuma
- [ ] Kestrel HTTPS yapılandırması
- [ ] SignalR WSS desteği

---

### Phase 4: Mng.Ui Entegrasyonu (1 gün)

#### 4.1 Development Ortamı

**Nuxt Server Middleware:**
- [ ] `server/middleware/certificate.ts` - MinIO'dan sertifika okuma
- [ ] CA sertifikasını trust store'a ekleme (Node.js)
- [ ] `nuxt.config.ts` - HTTPS yapılandırması
- [ ] Development için CA-signed sertifika oluşturma
- [ ] Axios/HTTP client için CA trust yapılandırması

#### 4.2 Production Ortamı

**Nginx Configuration:**
- [ ] Nginx config template
- [ ] MinIO'dan sertifika okuma script'i
- [ ] SSL certificate auto-renewal

**Alternatif: Node.js HTTPS:**
- [ ] Nuxt server HTTPS yapılandırması
- [ ] Sertifika MinIO'dan okuma

---

### Phase 5: Sertifika Rotation & Monitoring (1 gün)

#### 5.1 Background Service

**Lokasyon:** `MngKeeper/Infrastructure/MngKeeper.Infrastructure/Services/Certificate/CertificateRotationService.cs`

**Özellikler:**
- [ ] Periyodik sertifika expiry kontrolü (günlük)
- [ ] 30 gün kala uyarı
- [ ] Otomatik yenileme (self-signed için)
- [ ] RabbitMQ event publishing (certificate.expiring, certificate.renewed)

#### 5.2 Monitoring & Alerts

- [ ] Seq log'larında sertifika expiry uyarıları
- [ ] Admin UI'da sertifika durumu gösterimi (opsiyonel)
- [ ] Email/SMS alerts (opsiyonel)

---

## 🔒 Güvenlik Önerileri

### 1. Sertifika Erişim Kontrolü

- **MinIO Bucket Policy:** `mng-system-bucket` sadece sistem servisleri tarafından okunabilir
- **API Authentication:** Certificate API endpoint'leri admin-only
- **Key Storage:** Private key'ler asla log'lanmamalı

### 2. Development vs Production

**Development:**
- Internal CA kullanılır
- CA-signed sertifikalar otomatik oluşturulur
- Browser uyarıları: CA'yı browser'a import etmek gerekir (ilk kurulum)
- Servisler arası iletişimde uyarı yok (CA trust store'da)

**Production:**
- Internal CA kullanılır (önerilen)
- CA-signed sertifikalar otomatik oluşturulur
- Browser uyarıları: CA'yı browser'a import etmek gerekir (ilk kurulum)
- Alternatif: External CA-signed sertifikalar (Let's Encrypt, ancak air-gapped sistemlerde mümkün değil)
- Internal CA air-gapped sistemlerde ideal çözüm

### 3. Key Management

- Private key'ler MinIO'da şifrelenmiş olarak saklanmalı (MinIO encryption at rest)
- Key rotation stratejisi
- Backup stratejisi

---

## 📊 Sertifika Formatları

### Desteklenen Formatlar

1. **PEM Format (Önerilen)**
   - `cert.pem` - Certificate
   - `key.pem` - Private Key
   - Base64 encoded, text format

2. **PFX/PKCS12 Format**
   - `cert.pfx` - Certificate + Private Key (password protected)
   - Windows/.NET için uygun

3. **DER Format**
   - Binary format
   - Daha az kullanılır

**Öneri:** PEM format (en yaygın, cross-platform)

---

## 🧪 Test Senaryoları

### 1. CA ve Sertifika Oluşturma

- [ ] Root CA oluşturma (development, production)
- [ ] CA-signed sertifika oluşturma (mngkeeper, mnghub, mngdatagateway, mngui)
- [ ] Self-signed sertifika oluşturma (fallback)
- [ ] External sertifika yükleme (production)
- [ ] Metadata doğrulama
- [ ] Certificate chain doğrulama

### 2. Sertifika Okuma ve Trust Store

- [ ] MngKeeper startup'ta CA ve sertifika okuma
- [ ] MngKeeper startup'ta CA'yı trust store'a ekleme
- [ ] MngHub startup'ta CA ve sertifika okuma (MngKeeper API'den)
- [ ] MngHub startup'ta CA'yı trust store'a ekleme
- [ ] MngDataGateway startup'ta CA ve sertifika okuma
- [ ] MngDataGateway startup'ta CA'yı trust store'a ekleme
- [ ] Mng.Ui startup'ta CA ve sertifika okuma
- [ ] Mng.Ui startup'ta CA'yı trust store'a ekleme (Node.js)

### 3. HTTPS/WSS Bağlantıları

- [ ] MngKeeper HTTPS (https://localhost:5001)
- [ ] MngHub WSS (wss://localhost:5020/notificationHub)
- [ ] MngDataGateway HTTPS (https://localhost:5030)
- [ ] Mng.Ui HTTPS (https://localhost:3000)

### 4. Servisler Arası İletişim (Sertifika Validasyonu)

- [ ] MngKeeper -> MngHub API çağrısı (HTTPS, validasyon başarılı)
- [ ] MngKeeper -> MngDataGateway API çağrısı (HTTPS, validasyon başarılı)
- [ ] MngHub -> MngKeeper API çağrısı (HTTPS, validasyon başarılı)
- [ ] Mng.Ui -> MngKeeper API çağrısı (HTTPS, validasyon başarılı)
- [ ] Mng.Ui -> MngHub SignalR bağlantısı (WSS, validasyon başarılı)
- [ ] Tüm servisler arası iletişimde sertifika uyarısı olmamalı

### 5. Sertifika Rotation

- [ ] Expiry kontrolü
- [ ] Otomatik yenileme (CA-signed)
- [ ] Servis restart gereksinimi
- [ ] CA rotation (10 yılda bir, manuel)

---

## 📝 Configuration Örnekleri

### appsettings.json (MngKeeper)

```json
{
  "MngKeeperSettings": {
    "CertificateManagement": {
      "SystemBucketName": "mng-system-bucket",
      "CertificatePath": "certificates",
      "DefaultEnvironment": "development",
      "AutoGenerateIfMissing": true,
      "ValidityDays": 365,
      "RenewalThresholdDays": 30,
      "CertificateInfo": {
        "Country": "TR",
        "State": "ISTANBUL",
        "Locality": "UMRANIYE",
        "Organization": "Serkan MERAL",
        "RootCaCommonName": "MonitraNG Root CA"
      }
    },
    "MinIO": {
      "Endpoint": "localhost:9090",
      "AccessKey": "admin",
      "SecretKey": "admin123",
      "UseSSL": false
    }
  }
}
```

### appsettings.json (MngHub)

```json
{
  "MngHubSettings": {
    "CertificateManagement": {
      "Source": "MngKeeper", // "MngKeeper" veya "MinIO"
      "MngKeeperApiUrl": "https://localhost:5001",
      "ServiceName": "mnghub",
      "Environment": "development",
      "CacheDurationMinutes": 60
    }
  }
}
```

### nuxt.config.ts (Mng.Ui)

```typescript
export default defineNuxtConfig({
  devServer: {
    https: {
      key: './certificates/development/mngui/key.pem',
      cert: './certificates/development/mngui/cert.pem'
    }
  },
  runtimeConfig: {
    certificateManagement: {
      source: 'MinIO', // veya 'MngKeeper'
      minioEndpoint: 'localhost:9090',
      bucketName: 'mng-system-bucket',
      path: 'certificates/development/mngui'
    }
  }
});
```

---

## 🚀 Deployment Senaryoları

### Senaryo 1: Development

1. Sistem başlangıcında `mng-system-bucket` oluşturulur
2. Root CA otomatik oluşturulur (yoksa)
3. Her servis için CA-signed sertifika otomatik oluşturulur
4. CA sertifikası tüm servislere dağıtılır ve trust store'a eklenir
5. Servisler sertifikaları MinIO'dan okur
6. HTTPS/WSS bağlantıları çalışır
7. Servisler arası iletişimde sertifika validasyon sorunları olmaz

### Senaryo 2: Production

1. Root CA oluşturulur (ilk kurulum, 10 yıl geçerli)
2. Her servis için CA-signed sertifika otomatik oluşturulur
3. CA sertifikası tüm servislere dağıtılır ve trust store'a eklenir
4. Servisler sertifikaları MinIO'dan okur
5. HTTPS/WSS bağlantıları çalışır
6. Servisler arası iletişimde sertifika validasyon sorunları olmaz
7. Sertifika rotation otomatik (1 yılda bir)
8. CA rotation manuel (10 yılda bir)

### Senaryo 3: Air-Gapped System

1. Root CA offline ortamda oluşturulur
2. Her servis için CA-signed sertifika offline ortamda oluşturulur
3. CA ve sertifikalar MinIO'ya yüklenir
4. CA sertifikası tüm servislere dağıtılır ve trust store'a eklenir
5. Servisler sertifikaları MinIO'dan okur
6. HTTPS/WSS bağlantıları çalışır
7. Servisler arası iletişimde sertifika validasyon sorunları olmaz
8. **Internal CA yaklaşımı air-gapped sistemler için ideal çözümdür**

---

## 📚 İlgili Dokümantasyon

- [MngKeeper Roadmap](./MngKeeper/docs/ROADMAP.md) - HTTPS Support
- [MngHub Roadmap](./MngHub/docs/ROADMAP.md) - HTTPS/WSS Support
- [MinIO Documentation](https://min.io/docs/)
- [ASP.NET Core HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/https)

---

## ✅ Checklist

### Phase 1: Infrastructure
- [ ] MinIO system bucket oluşturma
- [ ] ICertificateService interface
- [ ] CertificateService implementation
- [ ] Root CA oluşturma ve yönetimi
- [ ] CA-signed sertifika oluşturma
- [ ] Trust store yönetimi
- [ ] Metadata yapısı

### Phase 2: MngKeeper
- [ ] CertificateService registration
- [ ] Program.cs güncellemesi (CA trust, sertifika okuma)
- [ ] Certificate API Controller (opsiyonel)
- [ ] Test: HTTPS bağlantısı
- [ ] Test: CA trust store ekleme

### Phase 3: MngHub
- [ ] CertificateClient implementation
- [ ] Program.cs güncellemesi (CA trust, sertifika okuma)
- [ ] Test: WSS bağlantısı
- [ ] Test: CA trust store ekleme
- [ ] Test: MngKeeper'a HTTPS çağrısı (validasyon başarılı)

### Phase 3.5: MngDataGateway
- [ ] CertificateClient implementation
- [ ] Program.cs güncellemesi (CA trust, sertifika okuma)
- [ ] Test: HTTPS bağlantısı
- [ ] Test: CA trust store ekleme
- [ ] Test: MngKeeper'a HTTPS çağrısı (validasyon başarılı)

### Phase 4: Mng.Ui
- [ ] Development HTTPS
- [ ] CA trust store ekleme (Node.js)
- [ ] Production HTTPS (Nginx veya Node.js)
- [ ] Test: HTTPS bağlantısı
- [ ] Test: MngKeeper'a HTTPS çağrısı (validasyon başarılı)
- [ ] Test: MngHub'a WSS bağlantısı (validasyon başarılı)

### Phase 5: Rotation & Monitoring
- [ ] CertificateRotationService
- [ ] Monitoring & alerts
- [ ] Test: Sertifika yenileme

---

**Son Güncelleme:** 30 Aralık 2025  
**Durum:** 📋 Planlama Tamamlandı - Implementation Bekleniyor

