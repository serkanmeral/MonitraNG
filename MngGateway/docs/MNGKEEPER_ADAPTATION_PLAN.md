# MngKeeper - API Gateway Uyarlama Planı

## 📋 Genel Bakış

Bu doküman, MngKeeper servisinin API Gateway kullanımına uyarlanması için yapılacak değişiklikleri ve bunların faydalarını açıklar.

## 🎯 Amaç

MngKeeper'ı API Gateway üzerinden erişilebilir hale getirmek ve gereksiz yükleri kaldırmak.

## 📊 Mevcut Durum Analizi

### Şu Anki Yapı

```
Frontend → MngKeeper:5001 (HTTPS)
         ├─ JWT Validation (her istekte)
         ├─ CORS Policy (her istekte)
         ├─ SSL/TLS Termination
         └─ Direct exposure (port 5001 açık)
```

### Gateway ile Olacak Yapı

```
Frontend → MngGateway:5040 (HTTPS)
           ↓
           ├─ JWT Validation (gateway'de)
           ├─ CORS Policy (gateway'de)
           ├─ SSL/TLS Termination (gateway'de)
           └─ MngKeeper:5001 (HTTP, internal)
              └─ Sadece business logic
```

## 🔄 Yapılacak Değişiklikler

### 1. CORS Yapılandırması (Opsiyonel - İleride)

**Mevcut Durum:**
- MngKeeper'da CORS policy tanımlı
- Her istekte CORS kontrolü yapılıyor

**Gateway ile:**
- Gateway'de CORS kontrolü yapılıyor
- MngKeeper'da CORS gereksiz hale gelir (internal network'te)

**Değişiklik:**
```csharp
// Önceden (Extensions.cs)
builder.Services.AddCors(l =>
{
    l.AddPolicy("CorsPolicy", b =>
    {
        b.WithOrigins("https://app.monitra.local", "http://localhost:3000")
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials();
    });
});

// Gateway ile (opsiyonel - ileride kaldırılabilir)
// CORS artık gateway'de yapılıyor, MngKeeper'da gerek yok
// Ancak şimdilik bırakabiliriz (geriye dönük uyumluluk)
```

**Fayda:**
- ✅ MngKeeper'da CORS kontrolü kaldırılabilir (performans artışı)
- ✅ CORS yönetimi tek yerden (gateway)
- ✅ Frontend origin değişikliklerinde sadece gateway güncellenir

### 2. JWT Authentication (Opsiyonel - İleride)

**Mevcut Durum:**
- MngKeeper'da JWT validation yapılıyor
- Her istekte token doğrulama

**Gateway ile:**
- Gateway'de JWT validation yapılıyor
- MngKeeper'a gelen istekler zaten doğrulanmış

**Değişiklik:**
```csharp
// Önceden
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* KeyCloak validation */ });

// Gateway ile (opsiyonel - ileride)
// JWT validation gateway'de yapılıyor
// MngKeeper'da sadece token'dan bilgi okuma (claims parsing)
// Authentication middleware'i kaldırılabilir veya basitleştirilebilir
```

**Fayda:**
- ✅ MngKeeper'da JWT validation kaldırılabilir (performans artışı)
- ✅ Token doğrulama tek yerden (gateway)
- ✅ KeyCloak bağlantı sayısı azalır

**Not:** Şimdilik JWT validation'ı bırakabiliriz (güvenlik için). İleride gateway'den gelen isteklerin doğrulandığını garanti edersek kaldırabiliriz.

### 3. SSL/TLS Termination (Opsiyonel - İleride)

**Mevcut Durum:**
- MngKeeper HTTPS ile çalışıyor
- Sertifika yönetimi MngKeeper'da

**Gateway ile:**
- Gateway SSL termination yapıyor
- MngKeeper HTTP ile çalışabilir (internal network)

**Değişiklik:**
```yaml
# docker-compose.yml
mngkeeper:
  environment:
    - MngKeeperSettings__Server__Scheme=http  # https yerine http
  # Port exposure kaldırılabilir (sadece internal)
  # ports:
  #   - "5001:5001"  # ← Kaldırılabilir
```

**Fayda:**
- ✅ Sertifika yönetimi sadece gateway'de
- ✅ MngKeeper'da sertifika yükü yok
- ✅ Backend servisler HTTP ile çalışır (daha hızlı)

**Not:** Şimdilik HTTPS bırakabiliriz (güvenlik için). İleride internal network'te HTTP yeterli olacak.

### 4. Port Exposure (Opsiyonel - İleride)

**Mevcut Durum:**
- MngKeeper port 5001 dışarıdan erişilebilir

**Gateway ile:**
- Gateway üzerinden erişim yeterli
- Port exposure kaldırılabilir

**Değişiklik:**
```yaml
# docker-compose.yml
mngkeeper:
  # Port exposure kaldırılabilir
  # ports:
  #   - "5001:5001"  # ← Kaldırılabilir
  expose:
    - "5001"  # Sadece internal network için
```

**Fayda:**
- ✅ Güvenlik artışı (backend servisler dışarıdan erişilemez)
- ✅ Port çakışması riski azalır
- ✅ Network izolasyonu

**Not:** Şimdilik port'u açık bırakabiliriz (geriye dönük uyumluluk için).

### 5. OpenAPI Server Path Güncellemesi (Önerilen)

**Mevcut Durum:**
```json
{
  "OpenApiServerPath": "https://localhost:5001"
}
```

**Gateway ile:**
```json
{
  "OpenApiServerPath": "https://api.monitra.local/keeper"
}
```

**Fayda:**
- ✅ Swagger/OpenAPI dokümantasyonu gateway URL'ini gösterir
- ✅ Frontend geliştiriciler doğru endpoint'i görür

### 6. Gateway Routing Test (Gerekli)

**Yapılacak:**
- Gateway üzerinden MngKeeper endpoint'lerini test et
- Tüm CRUD operasyonlarını gateway üzerinden test et
- Authentication flow'u gateway üzerinden test et

**Test Senaryoları:**
- ✅ `GET /keeper/api/domain` - Domain listesi
- ✅ `GET /keeper/api/user` - User listesi
- ✅ `POST /keeper/api/auth/token` - Token alma
- ✅ `GET /keeper/api/group` - Group listesi

## 📈 Beklenen Faydalar

### 1. Performans İyileştirmeleri

**CORS Kontrolü:**
- Önceden: Her istekte CORS kontrolü (MngKeeper'da)
- Gateway ile: CORS kontrolü sadece gateway'de
- **Kazanç:** ~5-10ms per request

**JWT Validation:**
- Önceden: Her istekte JWT validation (MngKeeper'da)
- Gateway ile: JWT validation sadece gateway'de
- **Kazanç:** ~10-20ms per request

**Toplam Performans Kazancı:** ~15-30ms per request

### 2. Güvenlik İyileştirmeleri

- ✅ Backend servisler dışarıdan erişilemez (port exposure kaldırılırsa)
- ✅ Merkezi authentication (gateway'de)
- ✅ Rate limiting (gateway'de)
- ✅ SSL/TLS termination (gateway'de)

### 3. Yönetilebilirlik İyileştirmeleri

- ✅ CORS policy tek yerden yönetilir
- ✅ JWT validation tek yerden yönetilir
- ✅ Sertifika yönetimi tek yerden
- ✅ Logging merkezi (gateway'de)

### 4. Kod Basitleştirmesi

- ✅ MngKeeper'da CORS kodu kaldırılabilir
- ✅ MngKeeper'da JWT validation basitleştirilebilir
- ✅ Sertifika yönetimi kaldırılabilir

## 🚀 Uygulama Stratejisi

### Aşama 1: Gateway Routing Test (ŞİMDİ)

**Yapılacaklar:**
1. Gateway üzerinden MngKeeper endpoint'lerini test et
2. Tüm CRUD operasyonlarını gateway üzerinden test et
3. Authentication flow'u gateway üzerinden test et

**Beklenen Sonuç:**
- ✅ Tüm endpoint'ler gateway üzerinden çalışıyor
- ✅ Authentication gateway üzerinden çalışıyor
- ✅ CORS gateway'de çalışıyor

### Aşama 2: Minimal Değişiklikler (ŞİMDİ)

**Yapılacaklar:**
1. OpenAPI Server Path güncellemesi
2. Gateway routing testleri
3. Dokümantasyon güncellemesi

**Beklenen Sonuç:**
- ✅ Gateway üzerinden erişim çalışıyor
- ✅ Mevcut direkt erişim de çalışmaya devam ediyor (geriye dönük uyumluluk)

### Aşama 3: Optimizasyonlar (İLERİDE)

**Yapılacaklar:**
1. CORS kaldırma (MngKeeper'dan)
2. JWT validation basitleştirme (MngKeeper'dan)
3. Port exposure kaldırma
4. HTTP'ye geçiş (HTTPS yerine)

**Beklenen Sonuç:**
- ✅ Performans artışı
- ✅ Güvenlik artışı
- ✅ Kod basitleştirmesi

## ⚠️ Dikkat Edilmesi Gerekenler

### 1. Geriye Dönük Uyumluluk

- Şimdilik mevcut direkt erişim çalışmaya devam etmeli
- Port exposure'ı kaldırmadan önce frontend gateway'e geçmeli
- CORS'u kaldırmadan önce gateway CORS'unun çalıştığından emin olmalı

### 2. Güvenlik

- JWT validation'ı kaldırmadan önce gateway'den gelen isteklerin doğrulandığından emin olmalı
- Internal network'te HTTP kullanmadan önce network izolasyonunu kontrol etmeli

### 3. Test

- Her değişiklikten sonra kapsamlı test yapılmalı
- Gateway üzerinden ve direkt erişimle test edilmeli

## 📝 Özet

### Şimdi Yapılacaklar (Minimal Değişiklik)

1. ✅ Gateway routing testleri
2. ✅ OpenAPI Server Path güncellemesi
3. ✅ Dokümantasyon güncellemesi

### İleride Yapılabilecekler (Optimizasyon)

1. CORS kaldırma (MngKeeper'dan)
2. JWT validation basitleştirme
3. Port exposure kaldırma
4. HTTP'ye geçiş

### Beklenen Faydalar

- ✅ Performans: ~15-30ms per request
- ✅ Güvenlik: Backend izolasyonu
- ✅ Yönetilebilirlik: Merkezi yönetim
- ✅ Kod basitleştirmesi

## 🎯 Sonuç

**Şu anda minimal değişiklik yeterli!** Gateway routing testleri yapıp, OpenAPI path'i güncelleyebiliriz. Optimizasyonlar ileride yapılabilir.

