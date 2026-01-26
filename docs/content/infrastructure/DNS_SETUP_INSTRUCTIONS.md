# DNS A Kayıtları Ekleme Rehberi - Hosting Dünyam

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Production Sunucu IP:** `45.141.151.52`  
**Mevcut Ana Domain IP:** `91.151.95.152` (güncellenecek)

---

## 📋 Adım Adım Talimatlar

### Adım 1: Ana Domain A Kaydını Güncelleme

**Mevcut Durum:**
- Type: `A`
- Name: `monitrang.com.`
- Content: `91.151.95.152` ⚠️ (Bu IP güncellenecek)

**Yapılacaklar:**

1. DNS kayıtları tablosunda `monitrang.com.` için A kaydını bulun
2. Bu kaydın sağındaki **"Düzenle"** (mavi link) butonuna tıklayın
3. Açılan düzenleme formunda:
   - **Content** alanını bulun
   - Değeri `91.151.95.152` → `45.141.151.52` olarak değiştirin
   - **TTL** değerini kontrol edin (3600 olmalı)
4. **Kaydet** veya **Güncelle** butonuna tıklayın

**Sonuç:**
```
Type: A
Name: monitrang.com.
Content: 45.141.151.52 ✅
TTL: 3600
```

---

### Adım 2: Yeni A Kayıtları Ekleme

Her bir subdomain için yeni A kaydı eklemeniz gerekiyor. Tabloda **"Yeni Kayıt Ekle"**, **"Add Record"** veya benzeri bir buton arayın.

#### 2.1. app Subdomain A Kaydı

1. **Yeni Kayıt Ekle** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Type:** `A` (dropdown menüden seçin)
   - **Name:** `app` (sadece subdomain adı, nokta veya domain adı eklemeyin)
   - **Content:** `45.141.151.52`
   - **TTL:** `3600`
3. **Kaydet** veya **Add** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: A
Name: app
Content: 45.141.151.52
TTL: 3600
```

#### 2.2. api Subdomain A Kaydı

Aynı adımları tekrarlayın:
- **Type:** `A`
- **Name:** `api`
- **Content:** `45.141.151.52`
- **TTL:** `3600`

#### 2.3. auth Subdomain A Kaydı

- **Type:** `A`
- **Name:** `auth`
- **Content:** `45.141.151.52`
- **TTL:** `3600`

#### 2.4. docs Subdomain A Kaydı

- **Type:** `A`
- **Name:** `docs`
- **Content:** `45.141.151.52`
- **TTL:** `3600`

#### 2.5. gitlab Subdomain A Kaydı

- **Type:** `A`
- **Name:** `gitlab`
- **Content:** `45.141.151.52`
- **TTL:** `3600`

---

## ✅ Kontrol Listesi

Ekleme/güncelleme işlemlerinden sonra tabloda şu kayıtlar olmalı:

- [ ] `monitrang.com.` → `45.141.151.52` (A kaydı - GÜNCELLENDİ)
- [ ] `app` → `45.141.151.52` (A kaydı - YENİ EKLENDİ)
- [ ] `api` → `45.141.151.52` (A kaydı - YENİ EKLENDİ)
- [ ] `auth` → `45.141.151.151.52` (A kaydı - YENİ EKLENDİ)
- [ ] `docs` → `45.141.151.52` (A kaydı - YENİ EKLENDİ)
- [ ] `gitlab` → `45.141.151.52` (A kaydı - YENİ EKLENDİ)
- [ ] `www.monitrang.com.` → `monitrang.com.` (CNAME kaydı - DEĞİŞTİRİLMEYECEK)

---

## ⚠️ Önemli Notlar

### Name Alanı İçin Format

- ✅ **Doğru:** `app`, `api`, `auth` (sadece subdomain adı)
- ❌ **Yanlış:** `app.`, `app.monitrang.com`, `app.monitrang.com.`

Hosting Dünyam DNS paneli muhtemelen otomatik olarak domain adını ekleyecektir, bu yüzden sadece subdomain adını yazmanız yeterli.

### Mevcut Kayıtlar

Ekran görüntüsünde görünen diğer kayıtlar (TXT, MX, SRV, NS) **değiştirilmemeli**. Bunlar mail sunucusu ve diğer servisler için gerekli kayıtlar.

### TTL Değeri

TTL (Time To Live) değeri `3600` (1 saat) olarak ayarlanmalı. Bu, DNS değişikliklerinin daha hızlı yayılmasını sağlar.

---

## 🔍 DNS Propagation Kontrolü

Kayıtları ekledikten sonra, değişikliklerin yayılmasını kontrol edin:

### Online Araçlar

1. **DNS Checker:** https://dnschecker.org/
   - Domain: `app.monitrang.com`
   - Record Type: `A`
   - Tüm DNS sunucularında `45.141.151.52` görünene kadar bekleyin

2. **What's My DNS:** https://www.whatsmydns.net/
   - Domain: `api.monitrang.com`
   - Record Type: `A`

### Komut Satırı (Windows PowerShell)

```powershell
# Her subdomain için kontrol
Resolve-DnsName -Name app.monitrang.com -Type A
Resolve-DnsName -Name api.monitrang.com -Type A
Resolve-DnsName -Name auth.monitrang.com -Type A
Resolve-DnsName -Name docs.monitrang.com -Type A
Resolve-DnsName -Name gitlab.monitrang.com -Type A
```

**Beklenen Sonuç:**
```
Name           Type   TTL   Section    IPAddress
----           ----   ---   -------    ---------
app.monitrang.com A    3600   Answer    45.141.151.52
```

### Komut Satırı (Linux/Mac)

```bash
# Her subdomain için kontrol
dig app.monitrang.com A
dig api.monitrang.com A
dig auth.monitrang.com A
dig docs.monitrang.com A
dig gitlab.monitrang.com A
```

---

## 📝 Özet

**Yapılacaklar:**
1. ✅ `monitrang.com.` A kaydını `91.151.95.152` → `45.141.151.52` olarak güncelle
2. ✅ `app` A kaydı ekle → `45.141.151.52`
3. ✅ `api` A kaydı ekle → `45.141.151.52`
4. ✅ `auth` A kaydı ekle → `45.141.151.52`
5. ✅ `docs` A kaydı ekle → `45.141.151.52`
6. ✅ `gitlab` A kaydı ekle → `45.141.151.52`

**Değiştirilmeyecek Kayıtlar:**
- ❌ `www.monitrang.com.` CNAME kaydı (zaten doğru)
- ❌ TXT kayıtları (SPF, DKIM)
- ❌ MX kayıtları (mail sunucusu)
- ❌ NS kayıtları (nameserver'lar)
- ❌ SRV kayıtları

---

**Son Güncelleme:** 2 Ocak 2026  
**Durum:** ✅ DNS kayıtları başarıyla eklendi ve çalışıyor

