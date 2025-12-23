# Token Storage Strategy - Mng.UI

## 📋 Genel Bakış

Bu dokümantasyon, Mng.UI uygulamasında JWT token'ların nasıl saklandığını ve kullanıldığını açıklar.

## 🔐 Token Storage Yapısı

### Mevcut Yapı

1. **Access Token**
   - **Storage:** Cookie (`access_token`)
   - **Pinia Store:** Memory'de (`authStore.accessToken`)
   - **Expiration:** 5 dakika (300 saniye)
   - **Security:** `sameSite: "strict"`, `secure: true` (production)

2. **Refresh Token**
   - **Storage:** Cookie (`refresh_token`)
   - **Pinia Store:** Memory'de (`authStore.refreshToken`)
   - **Expiration:** 30 dakika (1800 saniye)
   - **Security:** `sameSite: "strict"`, `secure: true` (production)

3. **User Info**
   - **Storage:** localStorage (`userInfo`)
   - **Pinia Store:** Memory'de (`authStore.userInfo`)
   - **Content:** JWT decode edilmiş kullanıcı bilgileri

## ❌ Neden localStorage'da Token Tutmuyoruz?

### Güvenlik Riskleri

1. **XSS (Cross-Site Scripting) Saldırıları**
   - localStorage JavaScript ile erişilebilir
   - Kötü niyetli script'ler token'ı çalabilir
   - Cookie'ler daha güvenli (sameSite protection)

2. **JavaScript Erişimi**
   - localStorage'a herhangi bir script erişebilir
   - Cookie'ler `httpOnly` flag ile korunabilir (server-side)

3. **CSRF Koruması**
   - Cookie'ler `sameSite: "strict"` ile CSRF saldırılarına karşı korunur
   - localStorage'da böyle bir koruma yok

## ✅ Mevcut Yapının Avantajları

### Cookie Storage

1. **Güvenlik**
   - `sameSite: "strict"` ile CSRF koruması
   - `secure: true` ile HTTPS zorunluluğu (production)
   - Otomatik expiration

2. **Otomatik Yönetim**
   - Browser otomatik olarak cookie'leri yönetir
   - Expiration otomatik olarak işlenir

3. **Server-Side Erişim**
   - Server-side API route'larında cookie'ler erişilebilir
   - Nuxt server middleware'de kullanılabilir

### Pinia Store (Memory)

1. **Performans**
   - Memory'de tutulduğu için hızlı erişim
   - Reactive state management

2. **Sayfa Yenileme**
   - Sayfa yenilendiğinde cookie'den otomatik yüklenir
   - `initializeAuth()` ile state restore edilir

## 🔄 Token Kullanım Akışı

### 1. Login
```
User Login → MngKeeper API → Token Response
  ↓
Store in Cookie (access_token, refresh_token)
  ↓
Store in Pinia Store (memory)
  ↓
Decode JWT → Store userInfo in localStorage
```

### 2. API Çağrıları
```
Component/Service → fetchFromMngKeeper()
  ↓
Read token from Cookie (useCookie("access_token"))
  ↓
Add to Authorization header
  ↓
Server-side proxy → MngKeeper/MngDataGateway API
```

### 3. Token Refresh
```
Access Token Expired → refreshToken()
  ↓
Use refresh_token from Cookie
  ↓
Call MngKeeper API → New tokens
  ↓
Update Cookies and Pinia Store
```

### 4. Logout
```
User Logout → revokeMngKeeperToken()
  ↓
Clear Cookies
  ↓
Clear Pinia Store
  ↓
Clear localStorage
```

## 📝 Best Practices

### ✅ Yapılması Gerekenler

1. **Token'ları Cookie'de Tut**
   - Access token ve refresh token cookie'de saklanmalı
   - `sameSite: "strict"` kullan
   - Production'da `secure: true` kullan

2. **Memory'de State Tut**
   - Pinia store'da token'ları memory'de tut (performans için)
   - Sayfa yenilendiğinde cookie'den restore et

3. **User Info localStorage'da**
   - Decode edilmiş user info localStorage'da tutulabilir
   - Token'ları localStorage'a koyma!

4. **Helper Functions Kullan**
   - `getAccessToken()` helper function kullan
   - Token'ı direkt cookie'den al, store'dan değil

### ❌ Yapılmaması Gerekenler

1. **localStorage'da Token Tutma**
   - Access token localStorage'da tutulmamalı
   - Refresh token localStorage'da tutulmamalı
   - XSS saldırılarına açık

2. **Token'ı URL'de Taşıma**
   - Token'ı query parameter olarak kullanma
   - Token'ı URL'de gösterme

3. **Token'ı Global Variable'da Tutma**
   - `window.accessToken` gibi global değişkenler kullanma
   - Güvenlik riski

## 🔧 MngDataGateway Entegrasyonu

MngDataGateway API çağrıları için aynı token kullanılır:

```typescript
// MngDataGateway API çağrısı
const token = useCookie("access_token").value;
const response = await fetch(`${gatewayUrl}/api/datasets`, {
  headers: {
    Authorization: `Bearer ${token}`
  }
});
```

## 🛡️ Güvenlik Önerileri

1. **HTTPS Kullan**
   - Production'da mutlaka HTTPS kullan
   - `secure: true` flag'i ile cookie'leri koru

2. **Token Expiration**
   - Kısa expiration süreleri kullan (5 dakika)
   - Refresh token ile otomatik yenileme

3. **Token Revocation**
   - Logout'ta refresh token'ı revoke et
   - Güvenlik ihlali durumunda token'ları iptal et

4. **CSP (Content Security Policy)**
   - XSS saldırılarına karşı CSP header'ları ekle
   - Inline script'leri kısıtla

## 📊 Karşılaştırma

| Özellik | Cookie | localStorage | Memory |
|---------|--------|--------------|--------|
| XSS Koruması | ⚠️ Kısmen | ❌ Yok | ✅ Var |
| CSRF Koruması | ✅ Var (sameSite) | ❌ Yok | ❌ Yok |
| Otomatik Expiration | ✅ Var | ❌ Yok | ❌ Yok |
| Server-Side Erişim | ✅ Var | ❌ Yok | ❌ Yok |
| Performans | ⚠️ Orta | ✅ Hızlı | ✅ Çok Hızlı |
| Sayfa Yenileme | ✅ Kalıcı | ✅ Kalıcı | ❌ Kaybolur |

## 🎯 Sonuç

**Mevcut yapı optimal:**
- ✅ Token'lar cookie'de (güvenlik)
- ✅ State memory'de (performans)
- ✅ User info localStorage'da (convenience)
- ✅ Helper functions ile kolay erişim

**localStorage'da token tutmayın!** Güvenlik riski oluşturur.

