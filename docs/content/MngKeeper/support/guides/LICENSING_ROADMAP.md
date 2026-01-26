# Lisanslama Modeli - Roadmap

## 📋 Genel Bakış

MonitraNG platformu için domain bazlı lisanslama sistemi. Her domain için ayrı lisans yönetimi, trial ve real lisans desteği, kullanıcı sayısı sınırlaması ve lisans bitince davranış kontrolü.

**Durum:** Planlama tamamlandı, implementasyon bekliyor  
**Öncelik:** Orta-Yüksek  
**Tahmini Süre:** 4-6 hafta

---

## 🎯 Temel Gereksinimler

### 1. Lisans Tipleri

- **Trial Lisans**: Domain oluşturulduğunda otomatik 15 günlük trial lisans
- **Real Lisans**: Manuel yüklenen, müşteriye özel lisans dosyası

### 2. Lisans Önceliği

- Real lisans varsa ve geçerliyse → Real lisans kullanılır
- Real lisans yoksa veya geçersizse → Trial lisans kullanılır
- Hiç lisans yoksa → Hata

### 3. Lisans Dosyası Konumu

- **Trial**: `{bucketName}/system/license-trial.enc`
- **Real**: `{bucketName}/system/license-real.enc`
- Dosyalar şifrelenmiş (AES-256-GCM) ve imzalanmış

### 4. Lisans Bitişi Davranışları

Lisans dosyası içinde `expirationBehavior` ile kontrol edilir:

- `blockTokenGeneration`: Token almayı blokla
- `blockCrudOperations`: CRUD işlemlerini blokla (Create, Update, Delete)
- `blockGetOperations`: GET işlemlerini blokla
- `allowReadOnly`: Sadece okuma moduna izin ver
- `customMessage`: Özel hata mesajı

---

## 📊 Lisans Dosyası Formatı

### Trial Lisans

```json
{
  "domainName": "example.com",
  "licenseType": "Trial",
  "issuedAt": "2025-01-15T10:00:00Z",
  "expiresAt": "2025-01-30T10:00:00Z",
  "issuedBy": "system",
  "licenseKey": "trial-unique-hash-key",
  "signature": "encrypted-signature",
  "expirationBehavior": {
    "blockTokenGeneration": true,
    "blockCrudOperations": true,
    "blockGetOperations": false,
    "allowReadOnly": true,
    "customMessage": "Trial lisans süreniz dolmuştur."
  }
}
```

### Real Lisans

```json
{
  "domainName": "example.com",
  "licenseType": "Real",
  "issuedAt": "2025-01-15T10:00:00Z",
  "expiresAt": "2026-01-15T10:00:00Z",
  "issuedBy": "isimplatform",
  "licenseKey": "real-unique-hash-key",
  "signature": "encrypted-signature",
  "customerInfo": {
    "customerName": "ABC Şirketi",
    "customerId": "CUST-12345",
    "contactEmail": "info@abc.com",
    "contactPhone": "+90 555 123 4567"
  },
  "licenseFeatures": {
    "maxUsers": 100,
    "maxDomains": 1,
    "maxStorageGB": 100,
    "enableAdvancedFeatures": true,
    "supportLevel": "premium",
    "countActiveUsersOnly": true,
    "activeUserDefinition": {
      "isActive": true,
      "lastLoginDays": 90
    }
  },
  "expirationBehavior": {
    "blockTokenGeneration": true,
    "blockCrudOperations": true,
    "blockGetOperations": true,
    "allowReadOnly": false,
    "customMessage": "Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin."
  },
  "metadata": {
    "purchaseDate": "2025-01-15T10:00:00Z",
    "invoiceNumber": "INV-2025-001",
    "salesRep": "John Doe"
  }
}
```

---

## 🏗️ Mimari Tasarım

### 1. Lisans Service Interface

```csharp
public interface ILicenseService
{
    Task<LicenseInfo> CreateTrialLicenseAsync(string domainName, int days = 15);
    Task<LicenseValidationResult> ValidateLicenseAsync(string domainName);
    Task<LicenseInfo?> GetLicenseAsync(string domainName, LicenseType type);
    Task<LicenseInfo?> GetActiveLicenseAsync(string domainName); // Real > Trial
    Task<bool> UploadRealLicenseAsync(string domainName, Stream licenseFile);
    Task<bool> RenewLicenseAsync(string domainName, DateTime newExpiryDate);
    Task<bool> IsOperationAllowedAsync(string domainName, LicenseOperation operation);
    Task<int> GetActiveUserCountAsync(string domainName);
    Task<bool> CanCreateUserAsync(string domainName);
}

public enum LicenseType
{
    Trial,
    Real
}

public enum LicenseOperation
{
    TokenGeneration,
    CrudOperation,
    GetOperation
}
```

### 2. Lisans Kontrol Noktaları

**A) MngKeeper - Token Generation**
- `POST /api/auth/token` → `GetToken()` metodu
- `POST /api/auth/refresh` → `RefreshToken()` metodu
- Kontrol: `blockTokenGeneration == true` ise → 403 Forbidden

**B) MngDataGateway - CRUD Operations**
- `POST /api/v1/data/{datasetName}` → `Create()`
- `PUT /api/v1/data/{datasetName}/{dataId}` → `Update()`
- `DELETE /api/v1/data/{datasetName}/{dataId}` → `Delete()`
- `POST /api/v1/data/{datasetName}/bulk` → `BulkCreate()`
- Kontrol: `blockCrudOperations == true` ise → 403 Forbidden

**C) MngDataGateway - GET Operations**
- `GET /api/v1/data/{datasetName}` → `List()`
- `GET /api/v1/data/{datasetName}/{dataId}` → `GetById()`
- `POST /api/v1/data/{datasetName}/query` → `Query()`
- `POST /api/v1/data/{datasetName}/aggregate` → `Aggregate()`
- Kontrol: `blockGetOperations == true` ise → 403 Forbidden

### 3. Domain Entity Güncellemesi

```csharp
// Domain entity'ye eklenecek
[BsonElement("licenseInfo")]
public LicenseInfo LicenseInfo { get; set; } = new();

public class LicenseInfo
{
    [BsonElement("hasRealLicense")]
    public bool HasRealLicense { get; set; } = false;
    
    [BsonElement("realLicenseExpiresAt")]
    public DateTime? RealLicenseExpiresAt { get; set; }
    
    [BsonElement("trialLicenseExpiresAt")]
    public DateTime? TrialLicenseExpiresAt { get; set; }
    
    [BsonElement("activeLicenseType")]
    public LicenseType ActiveLicenseType { get; set; } = LicenseType.Trial;
    
    [BsonElement("lastLicenseCheck")]
    public DateTime? LastLicenseCheck { get; set; }
    
    [BsonElement("currentUserCount")]
    public int CurrentUserCount { get; set; } = 0;
    
    [BsonElement("lastUserCountUpdate")]
    public DateTime? LastUserCountUpdate { get; set; }
}
```

---

## 📝 Implementasyon Fazları

### Faz 1: Temel Lisanslama (Trial) - 2 hafta

**Hedef:** Domain oluşturulduğunda otomatik trial lisans oluşturma

#### 1.1 License Entity ve DTO'ları
- [ ] `LicenseInfo` entity
- [ ] `LicenseValidationResult` DTO
- [ ] `ExpirationBehavior` DTO
- [ ] `LicenseFeatures` DTO (Real lisans için)

#### 1.2 License Service
- [ ] `ILicenseService` interface
- [ ] `LicenseService` implementasyonu
- [ ] `CreateTrialLicenseAsync()` metodu
- [ ] `ValidateLicenseAsync()` metodu
- [ ] `GetLicenseAsync()` metodu

#### 1.3 Şifreleme Servisi
- [ ] `ILicenseEncryptionService` interface
- [ ] AES-256-GCM şifreleme
- [ ] Domain-specific key türetme (PBKDF2)
- [ ] Master key yönetimi

#### 1.4 Pipeline Entegrasyonu
- [ ] `CreateLicenseStep` oluştur
- [ ] Pipeline'a ekle (Step 11: CreateMinIOBucketStep'ten sonra)
- [ ] MinIO'ya lisans dosyası kaydetme
- [ ] Domain entity'yi güncelleme

#### 1.5 Lisans Doğrulama
- [ ] MinIO'dan lisans dosyası okuma
- [ ] Şifre çözme
- [ ] Signature doğrulama
- [ ] Expiry date kontrolü

**Deliverables:**
- Trial lisans otomatik oluşturuluyor
- MinIO'ya kaydediliyor
- Domain entity güncelleniyor

---

### Faz 2: Real Lisans Desteği - 1.5 hafta

**Hedef:** Real lisans yükleme ve öncelik mantığı

#### 2.1 Real Lisans Formatı
- [ ] Real lisans JSON formatı
- [ ] Customer info desteği
- [ ] License features desteği
- [ ] Metadata desteği

#### 2.2 Lisans Öncelik Mantığı
- [ ] `GetActiveLicenseAsync()` metodu
- [ ] Real > Trial öncelik kontrolü
- [ ] Fallback mekanizması

#### 2.3 Lisans Yükleme API
- [ ] `LicenseController` oluştur
- [ ] `POST /api/license/upload` endpoint
- [ ] Dosya doğrulama (format, signature)
- [ ] MinIO'ya kaydetme
- [ ] Domain entity güncelleme
- [ ] Redis cache temizleme

#### 2.4 Lisans Yönetim API
- [ ] `GET /api/license/{domainName}` - Lisans bilgisi
- [ ] `POST /api/license/validate` - Lisans doğrulama
- [ ] `GET /api/license/{domainName}/download` - Lisans indirme

**Deliverables:**
- Real lisans yüklenebiliyor
- Öncelik mantığı çalışıyor
- API endpoint'leri hazır

---

### Faz 3: Lisans Kontrol Mekanizmaları - 1.5 hafta

**Hedef:** API çağrılarında lisans kontrolü

#### 3.1 MngKeeper - Token Generation Kontrolü
- [ ] `AuthController.GetToken()` içinde lisans kontrolü
- [ ] `AuthController.RefreshToken()` içinde lisans kontrolü
- [ ] `blockTokenGeneration` kontrolü
- [ ] Hata mesajları

#### 3.2 MngDataGateway - CRUD/GET Kontrolü
- [ ] `LicenseValidationMiddleware` oluştur
- [ ] CRUD operation kontrolü
- [ ] GET operation kontrolü
- [ ] Middleware pipeline'a ekle
- [ ] Hata mesajları

#### 3.3 Cache Mekanizması
- [ ] Redis cache entegrasyonu
- [ ] Lisans durumu cache'leme
- [ ] Cache invalidation (lisans güncelleme)
- [ ] TTL yönetimi

#### 3.4 Background Job
- [ ] Periyodik lisans kontrolü (günlük)
- [ ] Süresi dolan domain'leri işaretleme
- [ ] Email bildirimleri (lisans bitmeden önce)

**Deliverables:**
- Token generation kontrolü çalışıyor
- CRUD/GET kontrolü çalışıyor
- Cache mekanizması aktif
- Background job çalışıyor

---

### Faz 4: UI Entegrasyonu - 1 hafta

**Hedef:** Lisans yönetimi için UI sayfaları

#### 4.1 MngDomainUI - Lisans Yükleme
- [ ] `pages/domains/[id]/license.vue` sayfası
- [ ] Lisans durumu gösterimi
- [ ] Dosya yükleme (drag & drop)
- [ ] Lisans bilgisi görüntüleme
- [ ] Lisans indirme

#### 4.2 MngUI - Manager Lisans Yönetimi
- [ ] `pages/admin/license-management.vue` sayfası
- [ ] Tüm domain'lerin lisans durumu listesi
- [ ] Domain bazlı lisans yükleme
- [ ] Lisans durumu filtreleme
- [ ] Lisans yenileme hatırlatıcıları
- [ ] Sayfa tipi: `pageType: 'manager'`

**Deliverables:**
- MngDomainUI'da lisans yönetimi
- MngUI'da manager lisans yönetimi

---

### Faz 5: Kullanıcı Sayısı Sınırlaması - 1 hafta

**Hedef:** Lisans bazlı kullanıcı sayısı kontrolü

#### 5.1 Aktif Kullanıcı Tanımı
- [ ] `ActiveUserDefinition` entity
- [ ] `IsActive` kontrolü
- [ ] `LastLoginDays` kontrolü (opsiyonel)
- [ ] Hibrit yaklaşım

#### 5.2 Kullanıcı Sayımı
- [ ] `UserRepository.CountActiveUsersAsync()` metodu
- [ ] Aktif kullanıcı filtreleme
- [ ] Redis cache entegrasyonu
- [ ] Background job (periyodik sayım)

#### 5.3 Kullanıcı Oluşturma Kontrolü
- [ ] `CreateUserCommandHandler` içinde kontrol
- [ ] Lisans'tan `maxUsers` okuma
- [ ] Aktif kullanıcı sayısı kontrolü
- [ ] Hata mesajları

#### 5.4 UI Gösterimi
- [ ] Kullanıcı sayısı gösterimi
- [ ] Limit uyarıları
- [ ] Progress bar (kullanılan/toplam)

**Deliverables:**
- Kullanıcı sayısı kontrolü çalışıyor
- UI'da gösterim var

---

## 🔧 Teknik Detaylar

### Şifreleme Stratejisi

**AES-256-GCM:**
- Domain-specific key: Domain adından türetilen key (PBKDF2)
- Master key: Sistem genelinde bir master key (appsettings'te)
- İki katmanlı: Master key + domain key kombinasyonu

### Lisans Doğrulama

**Signature Doğrulama:**
- RSA veya HMAC-SHA256 ile imza kontrolü
- Master key ile doğrulama
- Domain-specific key ile doğrulama

### Performans Optimizasyonu

**Cache Stratejisi:**
```
Redis Key: license:{domainName}
Value: { isValid: bool, expiresAt: DateTime, behavior: ExpirationBehavior }
TTL: 1 hour (veya expiresAt'a kadar)
```

**Kullanıcı Sayısı Cache:**
```
Redis Key: user_count:{domainId}:active
Value: int (kullanıcı sayısı)
TTL: 5 dakika
```

### Background Jobs

**Günlük Lisans Kontrolü:**
- Tüm domain'lerin lisanslarını kontrol eder
- Süresi dolan domain'leri `Expired` yapar
- `ExpirationBehavior`'ı MinIO'dan okuyup MongoDB'ye kaydeder

**Saatlik Kullanıcı Sayısı Güncelleme:**
- Tüm domain'lerin aktif kullanıcı sayısını günceller
- Redis cache'e kaydeder

---

## 📊 Lisans Senaryoları

### Senaryo 1: Trial Bitince Tam Bloklama
```json
{
  "expirationBehavior": {
    "blockTokenGeneration": true,
    "blockCrudOperations": true,
    "blockGetOperations": true,
    "allowReadOnly": false
  }
}
```
→ Hiçbir işlem yapılamaz

### Senaryo 2: Trial Bitince Sadece Yazma Bloklama
```json
{
  "expirationBehavior": {
    "blockTokenGeneration": false,
    "blockCrudOperations": true,
    "blockGetOperations": false,
    "allowReadOnly": true
  }
}
```
→ Token alınabilir, GET yapılabilir, CRUD yapılamaz

### Senaryo 3: Trial Bitince Sadece Token Bloklama
```json
{
  "expirationBehavior": {
    "blockTokenGeneration": true,
    "blockCrudOperations": false,
    "blockGetOperations": false,
    "allowReadOnly": false
  }
}
```
→ Yeni token alınamaz, mevcut token'lar çalışır

---

## 🎯 Kullanıcı Sayısı Sınırlaması

### Aktif Kullanıcı Tanımı

**Önerilen Yaklaşım: Hibrit**

```csharp
public class ActiveUserDefinition
{
    // Kriter 1: IsActive == true
    public bool IsActive { get; set; } = true;
    
    // Kriter 2: Son X gün içinde giriş yapmış (opsiyonel)
    public int? LastLoginDays { get; set; } = 90; // null = kontrol etme
}
```

**Lisans Dosyasında:**
```json
{
  "licenseFeatures": {
    "maxUsers": 100,
    "countActiveUsersOnly": true,
    "activeUserDefinition": {
      "isActive": true,
      "lastLoginDays": 90
    }
  }
}
```

### Kontrol Noktası

```csharp
// CreateUserCommandHandler içinde
var license = await _licenseService.GetActiveLicenseAsync(domainName);
var currentUserCount = await _userRepository.CountActiveUsersAsync(
    domainId,
    license.LicenseFeatures.ActiveUserDefinition);

if (currentUserCount >= license.LicenseFeatures.MaxUsers)
{
    return new CreateUserResponse 
    { 
        IsSuccess = false, 
        ErrorMessage = $"Kullanıcı limiti aşıldı. Maksimum: {license.LicenseFeatures.MaxUsers}, Mevcut: {currentUserCount}" 
    };
}
```

---

## 📋 Test Senaryoları

### 1. Trial Lisans Testleri
- [ ] Domain oluşturulduğunda trial lisans oluşturuluyor mu?
- [ ] Trial lisans MinIO'ya kaydediliyor mu?
- [ ] 15 gün sonra lisans geçersiz oluyor mu?
- [ ] Expiration behavior çalışıyor mu?

### 2. Real Lisans Testleri
- [ ] Real lisans yüklenebiliyor mu?
- [ ] Real lisans öncelikli mi?
- [ ] Real lisans geçersizse trial'a düşüyor mu?
- [ ] Lisans dosyası doğrulama çalışıyor mu?

### 3. Lisans Kontrol Testleri
- [ ] Token generation bloklanıyor mu?
- [ ] CRUD operations bloklanıyor mu?
- [ ] GET operations bloklanıyor mu?
- [ ] Cache mekanizması çalışıyor mu?

### 4. Kullanıcı Sayısı Testleri
- [ ] Aktif kullanıcı sayımı doğru mu?
- [ ] Limit aşıldığında kullanıcı oluşturulamıyor mu?
- [ ] Pasif kullanıcılar sayılmıyor mu?
- [ ] Cache güncellemesi çalışıyor mu?

---

## 🔗 İlgili Dokümantasyon

- [Domain Creation Pipeline](../ROADMAP.md#domain-creation-pipeline)
- [User Management](../ROADMAP.md#user-management)
- [MinIO Storage](../ROADMAP.md#minio-storage)

---

## 📝 Notlar

- Lisans dosyaları şifrelenmiş ve imzalanmış olmalı
- Master key güvenli bir şekilde saklanmalı (appsettings veya environment variable)
- Lisans doğrulama performansı için cache kullanılmalı
- Background job'lar production'da çalışmalı
- UI sayfaları responsive olmalı

---

**Son Güncelleme:** 2025-01-XX  
**Versiyon:** 1.0  
**Durum:** Planlama Tamamlandı
