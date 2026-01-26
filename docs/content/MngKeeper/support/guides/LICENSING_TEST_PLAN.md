# Lisanslama Sistemi - Test Planı

## 📋 Genel Bakış

Bu döküman, MonitraNG lisanslama sisteminin kapsamlı test planını içerir.

**Test Tipi:** Integration Tests (PowerShell Scripts)  
**Test Ortamı:** Docker Compose (Development)  
**Tahmini Süre:** 2-3 saat

---

## 🎯 Test Kategorileri

### 1. Trial Lisans Testleri

#### 1.1 Domain Oluşturma ve Otomatik Trial Lisans
**Test Senaryosu:** Domain oluşturulduğunda otomatik 15 günlük trial lisans oluşturulmalı

**Adımlar:**
1. Yeni bir domain oluştur (`POST /api/domain`)
2. Pipeline'ın tamamlanmasını bekle (10-15 saniye)
3. Lisans bilgisini kontrol et (`GET /api/license/{domainName}`)
4. MinIO'da lisans dosyasının varlığını kontrol et

**Beklenen Sonuçlar:**
- ✅ Domain başarıyla oluşturuldu
- ✅ Lisans tipi: "Trial"
- ✅ Lisans geçerli (isValid: true)
- ✅ Bitiş tarihi: 15 gün sonra
- ✅ MinIO'da `{bucketName}/system/license-trial.enc` dosyası var

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 1-2)

---

#### 1.2 Lisans Doğrulama
**Test Senaryosu:** Lisans doğrulama endpoint'i çalışmalı

**Adımlar:**
1. `POST /api/license/validate` endpoint'ini çağır
2. Response'u kontrol et

**Beklenen Sonuçlar:**
- ✅ `isValid: true` (lisans geçerliyse)
- ✅ `isExpired: false`
- ✅ `licenseType: "Trial"`
- ✅ `expiresAt` değeri doğru
- ✅ `expirationBehavior` objesi mevcut

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 2.2)

---

#### 1.3 Lisans İndirme
**Test Senaryosu:** Trial lisans dosyası indirilebilmeli

**Adımlar:**
1. `GET /api/license/{domainName}/download?type=trial` endpoint'ini çağır
2. İndirilen dosyayı kontrol et

**Beklenen Sonuçlar:**
- ✅ Dosya başarıyla indirildi
- ✅ Dosya adı: `license-trial-{domainName}.enc`
- ✅ Dosya boyutu > 0
- ✅ Dosya şifrelenmiş (binary format)

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 7)

---

### 2. Real Lisans Testleri

#### 2.1 Real Lisans Yükleme
**Test Senaryosu:** Real lisans dosyası yüklenebilmeli

**Ön Hazırlık:**
- Geçerli bir real lisans dosyası hazırla (şifrelenmiş ve imzalı)

**Adımlar:**
1. Real lisans dosyasını hazırla
2. `POST /api/license/upload` endpoint'ini çağır (multipart/form-data)
3. Lisans bilgisini kontrol et

**Beklenen Sonuçlar:**
- ✅ Lisans başarıyla yüklendi
- ✅ Lisans tipi: "Real"
- ✅ `hasRealLicense: true` (domain entity'de)
- ✅ MinIO'da `{bucketName}/system/license-real.enc` dosyası var
- ✅ Real lisans öncelikli (trial yerine kullanılıyor)

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 6)

---

#### 2.2 Lisans Öncelik Mantığı
**Test Senaryosu:** Real lisans varsa trial yerine real kullanılmalı

**Adımlar:**
1. Domain oluştur (trial lisans otomatik oluşur)
2. Real lisans yükle
3. `GET /api/license/{domainName}` çağır
4. Real lisans geçersiz yap (expiry date'i geçmiş yap)
5. Tekrar kontrol et (trial'a düşmeli)

**Beklenen Sonuçlar:**
- ✅ Real lisans varsa ve geçerliyse → Real kullanılır
- ✅ Real lisans geçersizse → Trial'a düşer
- ✅ Hiç lisans yoksa → Hata

**Test Script:** Manuel test (test-licensing.ps1'e eklenecek)

---

#### 2.3 Lisans Dosyası Doğrulama
**Test Senaryosu:** Geçersiz lisans dosyası reddedilmeli

**Adımlar:**
1. Geçersiz bir lisans dosyası hazırla (yanlış signature, yanlış format)
2. Yüklemeyi dene
3. Hata mesajını kontrol et

**Beklenen Sonuçlar:**
- ✅ Geçersiz signature → Hata: "License signature validation failed"
- ✅ Yanlış format → Hata: "Invalid real license format"
- ✅ Yanlış domain → Hata (domain name mismatch)

**Test Script:** Manuel test

---

### 3. Lisans Kontrol Mekanizmaları

#### 3.1 Token Generation Kontrolü
**Test Senaryosu:** Lisans süresi dolduğunda token alınamamalı

**Adımlar:**
1. Domain oluştur ve token al
2. Lisans süresini geçmiş yap (manuel veya test için)
3. Yeni token almaya çalış
4. Refresh token yapmaya çalış

**Beklenen Sonuçlar:**
- ✅ `blockTokenGeneration: true` ise → 403 Forbidden
- ✅ Hata mesajı: `expirationBehavior.customMessage` veya default mesaj
- ✅ Mevcut token'lar çalışmaya devam eder (sadece yeni token alınamaz)

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 3)

---

#### 3.2 CRUD Operations Kontrolü (MngDataGateway)
**Test Senaryosu:** Lisans süresi dolduğunda CRUD işlemleri bloklanmalı

**Adımlar:**
1. Domain oluştur ve token al
2. MngDataGateway'e CRUD isteği gönder (`POST /api/v1/data/{datasetName}`)
3. Lisans süresini geçmiş yap
4. Tekrar CRUD isteği gönder

**Beklenen Sonuçlar:**
- ✅ `blockCrudOperations: true` ise → 403 Forbidden
- ✅ Hata mesajı: "Lisans süreniz dolmuştur..."
- ✅ GET işlemleri çalışmaya devam eder (eğer `blockGetOperations: false` ise)

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 9)

---

#### 3.3 GET Operations Kontrolü (MngDataGateway)
**Test Senaryosu:** Lisans süresi dolduğunda GET işlemleri bloklanabilmeli

**Adımlar:**
1. Domain oluştur ve token al
2. MngDataGateway'e GET isteği gönder (`GET /api/v1/data/{datasetName}`)
3. Lisans süresini geçmiş yap ve `blockGetOperations: true` yap
4. Tekrar GET isteği gönder

**Beklenen Sonuçlar:**
- ✅ `blockGetOperations: true` ise → 403 Forbidden
- ✅ `blockGetOperations: false` ise → İstek başarılı

**Test Script:** Manuel test

---

#### 3.4 Cache Mekanizması
**Test Senaryosu:** Lisans durumu cache'lenmeli

**Adımlar:**
1. `GET /api/license/{domainName}` çağır (ilk çağrı)
2. Süreyi ölç
3. Hemen tekrar çağır (ikinci çağrı)
4. Süreyi ölç ve karşılaştır

**Beklenen Sonuçlar:**
- ✅ İkinci çağrı daha hızlı (cache'den geliyor)
- ✅ Redis'te `license:{domainName}` key'i var
- ✅ TTL: 1 saat veya expiresAt'a kadar

**Test Script:** `MngKeeper/tests/test-licensing.ps1` (Bölüm 8)

---

### 4. Kullanıcı Sayısı Sınırlaması

#### 4.1 Aktif Kullanıcı Sayımı
**Test Senaryosu:** Aktif kullanıcı sayısı doğru hesaplanmalı

**Adımlar:**
1. Domain oluştur
2. 5 aktif kullanıcı oluştur
3. 3 pasif kullanıcı oluştur
4. `GET /api/license/{domainName}/user-count` çağır

**Beklenen Sonuçlar:**
- ✅ Aktif kullanıcı sayısı: 5 (pasif kullanıcılar sayılmıyor)
- ✅ `countActiveUsersOnly: true` ise sadece aktif kullanıcılar sayılıyor
- ✅ `lastLoginDays` kontrolü çalışıyorsa, son X gün içinde giriş yapanlar sayılıyor

**Test Script:** Manuel test

---

#### 4.2 Kullanıcı Limit Kontrolü
**Test Senaryosu:** Limit aşıldığında yeni kullanıcı oluşturulamaz

**Adımlar:**
1. Domain oluştur (maxUsers: 5)
2. 5 kullanıcı oluştur
3. 6. kullanıcıyı oluşturmaya çalış
4. Hata mesajını kontrol et

**Beklenen Sonuçlar:**
- ✅ 6. kullanıcı oluşturulamaz
- ✅ Hata mesajı: "Kullanıcı limiti aşıldı. Maksimum: 5, Mevcut: 5"
- ✅ `canCreateUser: false` döner

**Test Script:** Manuel test

---

#### 4.3 Kullanıcı Sayısı Cache
**Test Senaryosu:** Kullanıcı sayısı cache'lenmeli

**Adımlar:**
1. `GET /api/license/{domainName}/user-count` çağır
2. Yeni kullanıcı oluştur
3. Hemen tekrar çağır
4. Cache'in güncellenip güncellenmediğini kontrol et

**Beklenen Sonuçlar:**
- ✅ Cache TTL: 5 dakika
- ✅ Cache güncellemesi çalışıyor
- ✅ Redis'te `user_count:{domainId}:active` key'i var

**Test Script:** Manuel test

---

### 5. Background Job Testleri

#### 5.1 Günlük Lisans Kontrolü
**Test Senaryosu:** Background job günlük lisans kontrolü yapmalı

**Adımlar:**
1. Süresi dolmuş bir domain oluştur (expiry date geçmiş)
2. Background job'ın çalışmasını bekle (02:00 AM UTC veya manuel tetikleme)
3. Domain status'unu kontrol et

**Beklenen Sonuçlar:**
- ✅ Background job çalışıyor
- ✅ Süresi dolan domain'ler `Expired` status'una geçiyor
- ✅ Log'larda "Daily license validation completed" mesajı var

**Test Script:** Manuel test (log kontrolü)

---

### 6. Şifreleme ve Güvenlik Testleri

#### 6.1 Lisans Şifreleme
**Test Senaryosu:** Lisans dosyaları şifrelenmiş olmalı

**Adımlar:**
1. Lisans dosyasını MinIO'dan indir
2. Dosya içeriğini kontrol et
3. Şifre çözme işlemini test et

**Beklenen Sonuçlar:**
- ✅ Dosya binary format (şifrelenmiş)
- ✅ AES-256-GCM şifreleme kullanılıyor
- ✅ Domain-specific key ile şifrelenmiş
- ✅ Master key olmadan çözülemez

**Test Script:** Manuel test

---

#### 6.2 Signature Doğrulama
**Test Senaryosu:** Lisans dosyaları imzalanmış olmalı

**Adımlar:**
1. Lisans dosyasını çöz
2. Signature'ı kontrol et
3. Signature'ı değiştir ve tekrar doğrula

**Beklenen Sonuçlar:**
- ✅ HMAC-SHA256 ile imzalanmış
- ✅ Signature değiştirilirse doğrulama başarısız
- ✅ Domain-specific key ile imzalanmış

**Test Script:** Manuel test

---

## 🚀 Test Scriptleri

### Ana Test Script
**Dosya:** `MngKeeper/tests/test-licensing.ps1`

**Kullanım:**
```powershell
cd MngKeeper/tests
.\test-licensing.ps1 -BaseUrl "http://localhost:5001" -TestDomainName "test-license-001"
```

**Parametreler:**
- `-BaseUrl`: MngKeeper API base URL (default: http://localhost:5001)
- `-TestDomainName`: Test domain adı (default: test-license-{timestamp})
- `-AdminUsername`: Admin kullanıcı adı (default: admin)
- `-AdminPassword`: Admin şifresi (default: Admin123!)
- `-SkipCleanup`: Test sonunda domain'i silme (default: false)

---

### Test Senaryoları

#### Senaryo 1: Temel Trial Lisans Testi
```powershell
.\test-licensing.ps1 -TestDomainName "test-trial-001"
```
- Domain oluşturma
- Trial lisans kontrolü
- Lisans doğrulama
- Lisans indirme

---

#### Senaryo 2: Real Lisans Testi
```powershell
# Önce real lisans dosyası hazırla
.\test-licensing.ps1 -TestDomainName "test-real-001"
```
- Real lisans yükleme
- Öncelik kontrolü
- Lisans indirme

---

#### Senaryo 3: Lisans Kontrol Testi
```powershell
.\test-licensing.ps1 -TestDomainName "test-control-001"
```
- Token generation kontrolü
- CRUD operations kontrolü
- GET operations kontrolü

---

#### Senaryo 4: Kullanıcı Limit Testi
```powershell
# Manuel test gerekli
# 1. Domain oluştur (maxUsers: 5)
# 2. 5 kullanıcı oluştur
# 3. 6. kullanıcıyı oluşturmaya çalış
```

---

## 📊 Test Checklist

### Faz 1: Temel Lisanslama (Trial)
- [ ] Domain oluşturulduğunda trial lisans oluşturuluyor mu?
- [ ] Trial lisans MinIO'ya kaydediliyor mu?
- [ ] Lisans doğrulama çalışıyor mu?
- [ ] Lisans indirme çalışıyor mu?
- [ ] Domain entity'de LicenseInfo güncelleniyor mu?

### Faz 2: Real Lisans Desteği
- [ ] Real lisans yüklenebiliyor mu?
- [ ] Real lisans öncelikli mi?
- [ ] Real lisans geçersizse trial'a düşüyor mu?
- [ ] Lisans dosyası doğrulama çalışıyor mu?

### Faz 3: Lisans Kontrol Mekanizmaları
- [ ] Token generation bloklanıyor mu?
- [ ] CRUD operations bloklanıyor mu?
- [ ] GET operations bloklanıyor mu?
- [ ] Cache mekanizması çalışıyor mu?
- [ ] Background job çalışıyor mu?

### Faz 4: UI Entegrasyonu
- [ ] MngDomainUI'da lisans yönetimi sayfası çalışıyor mu?
- [ ] Lisans yükleme (drag & drop) çalışıyor mu?
- [ ] Lisans bilgisi görüntüleniyor mu?
- [ ] MngUI'da manager lisans yönetimi çalışıyor mu?

### Faz 5: Kullanıcı Sayısı Sınırlaması
- [ ] Aktif kullanıcı sayımı doğru mu?
- [ ] Limit aşıldığında kullanıcı oluşturulamıyor mu?
- [ ] Pasif kullanıcılar sayılmıyor mu?
- [ ] UI'da kullanıcı sayısı gösteriliyor mu?

---

## 🔧 Test Ortamı Gereksinimleri

### Docker Containers
- ✅ mngkeeper (port 5001)
- ✅ mngdatagateway (port 5010)
- ✅ mongo (MongoDB)
- ✅ redis (Redis cache)
- ✅ minio (MinIO object storage)
- ✅ keycloak (Authentication)
- ✅ rabbitmq (Message queue)

### Environment Variables
- ✅ `MngKeeperSettings__License__MasterKey` (docker-compose.yml'de ayarlanmalı)

### Test Data
- Test domain'leri otomatik oluşturulacak
- Real lisans dosyası için örnek dosya hazırlanmalı (opsiyonel)

---

## 📝 Test Sonuçları Raporlama

Test script'i çalıştırıldığında:
1. Her test kategorisi için sonuçlar gösterilir
2. Başarılı/Başarısız/Atlanan testler sayılır
3. Detaylı hata mesajları gösterilir
4. Test sonunda özet rapor oluşturulur

**Örnek Çıktı:**
```
========================================
  Test Sonuçları Özeti
========================================

[TrialLicense]
  ✓ DomainCreated : OK
  ✓ LicenseCreated : OK
  ✓ DownloadLicense : OK

[LicenseValidation]
  ✓ ValidateLicense : OK

[LicenseControl]
  ✓ TokenGeneration : OK
  ✓ CheckTokenGeneration : OK
  ✓ CheckCrudOperation : OK

========================================
Toplam: 7 | Başarılı: 7 | Başarısız: 0 | Atlanan: 0
========================================
```

---

## 🎯 Sonraki Adımlar

1. **Test Script'i Çalıştır:**
   ```powershell
   cd MngKeeper/tests
   .\test-licensing.ps1
   ```

2. **Manuel Testler:**
   - Real lisans yükleme
   - Expired lisans senaryoları
   - Kullanıcı limit testleri

3. **UI Testleri:**
   - MngDomainUI lisans yönetimi sayfası
   - MngUI manager lisans yönetimi

4. **Performance Testleri:**
   - Cache performansı
   - Background job performansı
   - Concurrent request handling

---

**Son Güncelleme:** 2025-01-XX  
**Versiyon:** 1.0  
**Durum:** Test Planı Hazır
