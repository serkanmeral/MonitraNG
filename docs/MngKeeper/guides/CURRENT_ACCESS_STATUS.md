# MngKeeper - Mevcut Erişim Durumu

**Tarih**: 31 Aralık 2025  
**Versiyon**: 1.1.0

## Mevcut Durum

### ✅ Port Tanımı VAR

**Docker Compose**:
```yaml
mngkeeper:
  ports:
    - "5001:5001"  # ← Port açık
```

**Port Mapping**: `0.0.0.0:5001->5001/tcp`

**Sonuç**: MngKeeper port 5001 üzerinden dışarıya açık.

---

### ✅ Sertifika Tanımı VAR

**Docker Compose Environment Variables**:
```yaml
- MngKeeperSettings__Server__Scheme=https
- MngKeeperSettings__CertificateSettings__DNS=mngkeeper
```

**Sertifika Yönetimi**:
- Self-signed sertifika otomatik oluşturuluyor
- DNS: `mngkeeper`
- Scheme: `https`

**Sonuç**: HTTPS sertifikası mevcut ve kullanılıyor.

---

### ✅ Dışarıdan Erişilebilir

**Erişim Yolları**:

1. **Direkt Erişim**:
   - URL: `https://localhost:5001`
   - Durum: ✅ Çalışıyor

2. **Gateway Üzerinden Erişim**:
   - URL: `https://localhost:5040/keeper/api/*`
   - Durum: ✅ Çalışıyor

**Sonuç**: Hem direkt hem gateway üzerinden erişilebilir.

---

## Özet

| Özellik | Durum | Detay |
|---------|-------|-------|
| **Port** | ✅ Açık | `5001:5001` |
| **Sertifika** | ✅ Mevcut | Self-signed (`mngkeeper`) |
| **HTTPS** | ✅ Aktif | `Scheme: https` |
| **Direkt Erişim** | ✅ Mümkün | `https://localhost:5001` |
| **Gateway Erişim** | ✅ Mümkün | `https://localhost:5040/keeper/api/*` |

---

## İleri Seviye Optimizasyon (Opsiyonel)

### Port'u Kapatma

Gateway üzerinden erişim yeterli olduğunda, port exposure kaldırılabilir:

```yaml
mngkeeper:
  # ports:
  #   - "5001:5001"  # ← Kaldırılabilir
```

**Faydaları**:
- ✅ Güvenlik artışı (dışarıdan direkt erişim yok)
- ✅ Network izolasyonu
- ✅ Backend servisler sadece internal network'te

**Dezavantajları**:
- ⚠️ Direkt erişim kalmaz (gateway üzerinden erişim gerekli)
- ⚠️ Development için biraz daha karmaşık olabilir

### HTTP'ye Geçiş

Gateway SSL termination yaptığı için, internal network'te HTTP yeterli:

```yaml
- MngKeeperSettings__Server__Scheme=http  # https yerine http
```

**Faydaları**:
- ✅ Sertifika yönetimi sadece gateway'de
- ✅ Daha hızlı (SSL/TLS overhead yok)
- ✅ Basitleştirilmiş yapılandırma

**Dezavantajları**:
- ⚠️ Internal network'te şifrelenmemiş trafik (gateway'de şifreleniyor)

---

## Öneri

**Şu Anki Durum**: Hem direkt hem gateway üzerinden erişim mevcut. Bu durum development için uygun.

**Production İçin Öneri**:
1. Port exposure'ı kaldır (sadece internal network)
2. HTTP'ye geç (gateway SSL termination yapıyor)
3. Sertifika yönetimini gateway'e bırak

**Not**: Bu değişiklikler opsiyonel. Şu anki durum çalışıyor ve güvenli.

---

**Son Güncelleme**: 31 Aralık 2025

