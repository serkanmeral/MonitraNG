# MngKeeper - Production Migration Plan

**Tarih**: 31 Aralık 2025  
**Versiyon**: 1.1.0  
**Durum**: 📋 Planlandı (Production için)

## Mevcut Durum (Development)

### ✅ İki Erişim Yolu Mevcut

1. **Direkt Erişim**: `https://localhost:5001`
   - Development için uygun
   - Debugging ve test için kolaylık

2. **Gateway Üzerinden Erişim**: `https://localhost:5040/keeper/api/*`
   - Production mimarisini test etmek için
   - Gateway entegrasyonu doğrulaması için

### Mevcut Yapılandırma

```yaml
mngkeeper:
  ports:
    - "5001:5001"  # ← Development için açık
  environment:
    - MngKeeperSettings__Server__Scheme=https
    - MngKeeperSettings__CertificateSettings__DNS=mngkeeper
```

---

## Production Migration Plan

### 1. Port Exposure Kaldırma

**Değişiklik**:
```yaml
mngkeeper:
  # ports:
  #   - "5001:5001"  # ← Kaldırılacak
  # Artık sadece internal network'te erişilebilir
```

**Faydaları**:
- ✅ Güvenlik artışı (dışarıdan direkt erişim yok)
- ✅ Network izolasyonu
- ✅ Backend servisler sadece internal network'te
- ✅ Saldırı yüzeyi azalır

**Sonuç**: 
- Direkt erişim: ❌ Kaldırılacak
- Gateway erişim: ✅ Çalışmaya devam edecek

---

### 2. HTTP'ye Geçiş (Opsiyonel)

**Değişiklik**:
```yaml
- MngKeeperSettings__Server__Scheme=http  # https yerine http
```

**Faydaları**:
- ✅ Sertifika yönetimi sadece gateway'de
- ✅ Daha hızlı (SSL/TLS overhead yok - internal network)
- ✅ Basitleştirilmiş yapılandırma
- ✅ Gateway SSL termination yapıyor

**Not**: Gateway ile MngKeeper arasındaki trafik internal network'te olduğu için HTTP yeterli. Gateway'de SSL termination yapılıyor, client'a kadar şifrelenmiş.

**Sonuç**:
- Gateway → MngKeeper: HTTP (internal)
- Client → Gateway: HTTPS (public)

---

### 3. Sertifika Yönetimi

**Mevcut Durum (Development)**:
- Self-signed sertifika (`mngkeeper` DNS ile)
- Her servis kendi sertifikasını yönetiyor

**Production Önerisi**:
- Gateway'de production sertifikası (Let's Encrypt veya CA'dan)
- MngKeeper'da sertifika yönetimi kaldırılabilir (HTTP'ye geçilirse)
- Veya internal CA sertifikaları kullanılabilir

---

## Production Yapılandırması (Örnek)

### docker-compose.production.yml

```yaml
mngkeeper:
  # ports kaldırıldı - sadece internal network
  # ports:
  #   - "5001:5001"
  
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    
    # Server Configuration
    - MngKeeperSettings__Server__Host=0.0.0.0
    - MngKeeperSettings__Server__Port=5001
    - MngKeeperSettings__Server__Scheme=http  # HTTP (internal network)
    - MngKeeperSettings__OpenApiServerPath=https://api.monitra.local/keeper
    
    # Certificate Settings - Artık gerekli değil (HTTP'ye geçildi)
    # - MngKeeperSettings__CertificateSettings__DNS=mngkeeper  # ← Kaldırılabilir
  
  networks:
    - mng_common_mng_network
  # Sadece gateway üzerinden erişilebilir
```

### Ocelot.json (Gateway)

Gateway'de MngKeeper route'u HTTP olacak:
```json
{
  "DownstreamPathTemplate": "/api/{everything}",
  "DownstreamScheme": "http",  // https yerine http
  "DownstreamHostAndPorts": [
    {
      "Host": "mngkeeper",
      "Port": 5001
    }
  ]
}
```

---

## Migration Adımları

### Adım 1: Port'u Kapat

1. `docker-compose.production.yml` içinde `ports` satırını kaldır veya comment out yap
2. Gateway üzerinden erişimi test et
3. Direkt erişimin çalışmadığını doğrula

### Adım 2: HTTP'ye Geç (Opsiyonel)

1. `MngKeeperSettings__Server__Scheme=http` yap
2. Gateway'de `DownstreamScheme=http` yap
3. Certificate settings'i kaldır (artık gerekli değil)
4. Test et

### Adım 3: Production Sertifikası (Gateway)

1. Production sertifikasını gateway'e ekle
2. Gateway HTTPS üzerinden çalıştır
3. SSL termination gateway'de yapılır

---

## Güvenlik Karşılaştırması

### Development (Mevcut)

| Özellik | Durum |
|---------|-------|
| Port Exposure | ✅ Açık (development için) |
| Direkt Erişim | ✅ Mümkün |
| Gateway Erişim | ✅ Mümkün |
| HTTPS (MngKeeper) | ✅ Self-signed |
| HTTPS (Gateway) | ✅ Self-signed |

### Production (Planlanan)

| Özellik | Durum |
|---------|-------|
| Port Exposure | ❌ Kapalı (sadece internal) |
| Direkt Erişim | ❌ Mümkün değil |
| Gateway Erişim | ✅ Tek erişim yolu |
| HTTPS (MngKeeper) | ❌ HTTP (internal) |
| HTTPS (Gateway) | ✅ Production sertifikası |

---

## Faydaları

### Güvenlik
- ✅ Backend servisler dışarıdan erişilemez
- ✅ Saldırı yüzeyi azalır
- ✅ Network izolasyonu artar
- ✅ Güvenlik duvarı kuralları basitleşir

### Yönetilebilirlik
- ✅ Sertifika yönetimi tek yerden (gateway)
- ✅ Port yönetimi basitleşir
- ✅ Monitoring tek yerden (gateway)

### Performans
- ✅ Internal network'te HTTP daha hızlı (SSL overhead yok)
- ✅ Gateway'de SSL termination optimize edilmiş

---

## Notlar

- ⚠️ Production'a geçmeden önce tüm gateway routing'lerini test et
- ⚠️ Health check endpoint'lerinin internal network'ten erişilebilir olduğundan emin ol
- ⚠️ Monitoring ve logging için internal network erişimi gerekebilir
- ⚠️ Debug için direkt erişim gerekirse, geçici olarak port açılabilir

---

**Son Güncelleme**: 31 Aralık 2025  
**Durum**: Development aşamasında - Production migration planlandı

