---
title: "Giriş Yapma (Login)"
category: "ui-guides"
tags: ["authentication", "login", "auth", "security"]
service: "Mng.Ui"
route: "/auth/login"
difficulty: "beginner"
estimated_time: "2 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Giriş Sayfasına Git"
    route: "/auth/login"
    action: "Tarayıcıda '/auth/login' adresine gidin veya uygulama sizi yönlendirir"
    expected_result: "Giriş sayfası açılır"
  - order: 2
    title: "Kullanıcı Bilgilerini Gir"
    route: "/auth/login"
    action: "Username ve Password alanlarını doldurun"
    expected_result: "Form doldurulur"
  - order: 3
    title: "Giriş Yap"
    route: "/auth/login"
    action: "'Giriş Yap' butonuna tıklayın"
    expected_result: "Başarılı giriş yapılır ve dashboard'a yönlendirilirsiniz"
prerequisites:
  - "Geçerli kullanıcı hesabı"
  - "Kullanıcı aktif olmalı"
related_guides:
  - "User Management"
---

# Giriş Yapma (Login)

## Özet
MonitraNG platformuna giriş yapma rehberi.

## Önkoşullar
- Geçerli kullanıcı hesabı
- Kullanıcı aktif olmalı

## Özellikler
- ✅ Username/Password ile giriş
- ✅ JWT token authentication
- ✅ Otomatik token refresh
- ✅ Remember me (opsiyonel)
- ✅ Hata mesajları

## Adımlar

### 1. Giriş Sayfasına Git
**Route:** `/auth/login`

**Yöntem:**
1. Tarayıcıda `/auth/login` adresine gidin
2. Veya uygulama sizi otomatik olarak yönlendirir

**Beklenen Sonuç:** Giriş sayfası açılır

### 2. Kullanıcı Bilgilerini Gir
**Route:** `/auth/login`

**Yöntem:**
1. **Username** alanına kullanıcı adınızı girin
2. **Password** alanına şifrenizi girin
3. **Remember Me** (opsiyonel) - Oturum bilgilerini hatırla

**Beklenen Sonuç:** Form doldurulur

### 3. Giriş Yap
**Route:** `/auth/login`

**Yöntem:**
1. **"Giriş Yap"** butonuna tıklayın
2. Sistem kullanıcı bilgilerini doğrular
3. Başarılı giriş sonrası dashboard'a yönlendirilirsiniz

**Beklenen Sonuç:** Başarılı giriş yapılır ve dashboard'a yönlendirilirsiniz

## Hata Durumları

### Geçersiz Kullanıcı Adı veya Şifre
**Hata Mesajı:** "Kullanıcı adı veya şifre hatalı"

**Çözüm:** Kullanıcı adı ve şifrenizi kontrol edin

### Pasif Kullanıcı
**Hata Mesajı:** "Kullanıcı hesabı pasif"

**Çözüm:** Admin ile iletişime geçin

### Token Hatası
**Hata Mesajı:** "Token alınamadı"

**Çözüm:** Sayfayı yenileyin veya tekrar giriş yapın

## Token Yönetimi

Giriş yaptıktan sonra:
- **Access Token:** Cookie'de saklanır
- **Refresh Token:** Cookie'de saklanır
- **Otomatik Refresh:** Token süresi dolmadan otomatik yenilenir

## İlgili Linkler
- [User Management](../users/user-management.md)
- [Forgot Password](../authentication/forgot-password.md)

---

**Son Güncelleme:** 16 Ocak 2026
