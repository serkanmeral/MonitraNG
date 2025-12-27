# MngGateway Migration Guide

## 📋 Genel Bakış

Bu doküman, mevcut servislerin MngGateway kullanımına geçişi için rehberdir.

## 🔄 Mevcut Durum vs Gateway Kullanımı

### Mevcut Durum (Gateway Olmadan)

```
Frontend → MngKeeper:5001 (HTTPS)
Frontend → MngDataGateway:5010 (HTTPS)
Frontend → MngHub:5020 (HTTP)
```

### Gateway Kullanımı (Önerilen)

```
Frontend → MngGateway:5000 (HTTPS)
           ↓
           ├─→ MngKeeper:5001 (HTTP, internal)
           ├─→ MngDataGateway:5010 (HTTP, internal)
           └─→ MngHub:5020 (HTTP, internal)
```

## ✅ Güncelleme Gereksinimleri

### 1. Backend Servisler (Opsiyonel Güncellemeler)

**Şu anda güncelleme gerekmiyor!** Mevcut servisler aynı şekilde çalışmaya devam edecek.

#### İleride Yapılabilecek Optimizasyonlar:

**A. Port Exposure Kaldırma (Opsiyonel)**

Mevcut docker-compose.yml'de backend servislerin port'ları expose ediliyor:
```yaml
mngkeeper:
  ports:
    - "5001:5001"  # ← Bu kaldırılabilir (sadece internal network)
```

Gateway kullanıldığında bu port'ları expose etmeye gerek yok. Ancak:
- ✅ **Şimdilik bırakabilirsiniz** (geriye dönük uyumluluk için)
- ✅ **İleride kaldırabilirsiniz** (gateway kullanımına geçildiğinde)

**B. HTTP'ye Geçiş (Opsiyonel)**

Gateway SSL termination yaptığı için backend servisler HTTP ile çalışabilir:
```yaml
mngkeeper:
  environment:
    - MngKeeperSettings__Server__Scheme=http  # ← https yerine http
```

Ancak:
- ✅ **Şimdilik HTTPS bırakabilirsiniz** (güvenlik için)
- ✅ **İleride HTTP'ye geçebilirsiniz** (gateway kullanımına geçildiğinde)

### 2. Frontend Güncellemeleri (Gerekli)

Frontend'de API endpoint'lerini güncellemeniz gerekecek:

**Önceden:**
```typescript
const keeperUrl = 'https://localhost:5001/api';
const dataUrl = 'https://localhost:5010/api';
```

**Gateway ile:**
```typescript
const apiUrl = 'https://api.monitra.local'; // veya http://localhost:5000
const keeperUrl = `${apiUrl}/keeper/api`;
const dataUrl = `${apiUrl}/data/api`;
```

### 3. Docker Compose Güncellemeleri

MngGateway servisi docker-compose.yml'e eklendi. Mevcut servislerde değişiklik yapmaya gerek yok.

## 🚀 Geçiş Stratejisi

### Aşama 1: Gateway Ekleme (Tamamlandı ✅)
- MngGateway servisi eklendi
- Mevcut servisler aynı şekilde çalışıyor
- Gateway ve backend servisler birlikte çalışabilir

### Aşama 2: Frontend Güncellemesi (Sonraki Adım)
- Frontend'de API endpoint'lerini gateway'e yönlendir
- Test et
- Geriye dönük uyumluluk için backend servislerin port'larını açık bırak

### Aşama 3: Optimizasyon (İleride)
- Backend servislerin port'larını kaldır (sadece internal network)
- Backend servisleri HTTP'ye geçir (gateway SSL termination yapıyor)
- Sertifika yönetimini sadece gateway'de yap

## 📝 Notlar

1. **Geriye Dönük Uyumluluk:** Mevcut servislerin port'larını açık bırakarak, gateway kullanımına geçiş yapmadan önce test edebilirsiniz.

2. **Kademeli Geçiş:** Frontend'i gateway'e yönlendirirken, backend servislerin port'larını açık bırakarak geriye dönük uyumluluğu koruyabilirsiniz.

3. **Production:** Production'da backend servislerin port'larını kaldırıp, sadece gateway üzerinden erişim sağlayabilirsiniz.

## ✅ Sonuç

**Şu anda mevcut servislerde güncelleme gerekmiyor!** Gateway eklendi, mevcut servisler aynı şekilde çalışmaya devam edecek. İleride gateway kullanımına geçildiğinde yukarıdaki optimizasyonları yapabilirsiniz.

