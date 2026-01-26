# DMARC Policy Güncelleme Talimatları

**Tarih:** 3 Ocak 2026  
**Domain:** `monitrang.com`  
**Durum:** Mail spam klasörüne düşüyor - DMARC policy çok sıkı

---

## 📋 Sorun

Mail'ler spam klasörüne düşüyor çünkü DMARC policy çok sıkı (`p=reject`). Test aşamasında daha esnek bir policy kullanmalıyız.

---

## 🔧 Çözüm: DMARC Policy Güncelleme

### Mevcut DMARC Kaydı

```
Name: _dmarc
Type: TXT
Content: v=DMARC1; p=reject; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com; adkim=s; aspf=s
```

### Güncellenecek DMARC Kaydı (Test için)

```
Name: _dmarc
Type: TXT
Content: v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com
TTL: 3600
```

---

## 📝 DNS Panelinde Güncelleme Adımları

### 1. Hosting Dünyam DNS Paneline Giriş

1. Hosting Dünyam web sitesine giriş yapın
2. DNS yönetimi panelini açın
3. `monitrang.com` domain'ini seçin

### 2. Mevcut DMARC Kaydını Bulun

1. DNS kayıtları listesinde `_dmarc` adlı TXT kaydını bulun
2. Kaydın yanındaki **Düzenle** butonuna tıklayın

### 3. DMARC Kaydını Güncelleyin

1. **Content/Value** alanını bulun
2. Mevcut içeriği silin: `v=DMARC1; p=reject; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com; adkim=s; aspf=s`
3. Yeni içeriği girin: `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com`
4. **Kaydet** butonuna tıklayın

### 4. Değişikliği Doğrulayın

DNS kaydının güncellendiğini doğrulamak için:

```bash
# Linux/Mac
dig +short TXT _dmarc.monitrang.com

# Windows PowerShell
Resolve-DnsName -Name _dmarc.monitrang.com -Type TXT
```

**Beklenen Sonuç:**
```
v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com
```

---

## ⏱️ DNS Propagation

DNS değişikliklerinin yayılması genellikle:
- **Minimum:** 5-15 dakika
- **Ortalama:** 1-2 saat
- **Maksimum:** 24-48 saat

Değişiklikten sonra birkaç saat bekleyin, sonra test maili gönderin.

---

## 🔍 Policy Seçenekleri

### `p=quarantine` (Önerilen - Test için)
- SPF/DKIM başarısız mailler spam klasörüne gider
- Mail teslim edilir ama spam olarak işaretlenir
- Test aşaması için uygun

### `p=none` (Sadece raporlama)
- SPF/DKIM başarısız mailler normal klasöre gider
- Sadece raporlama yapar
- Test için en esnek seçenek

### `p=reject` (Production için)
- SPF/DKIM başarısız mailleri reddeder
- En sıkı policy
- Production ortamında kullanılmalı (tüm DNS kayıtları doğru olduğunda)

---

## 📌 Ek Notlar

### Reverse DNS (PTR) Kaydı

Mail'in spam klasörüne düşmesinin bir diğer nedeni yanlış PTR kaydı:

- **Mevcut:** `almahs-ghat.axismess.com.`
- **Olması gereken:** `mail.monitrang.com`

PTR kaydını hosting sağlayıcınızdan (Hosting Dünyam) düzeltmeniz gerekiyor. Destek ekibine başvurun:
- IP: `45.141.151.52`
- PTR kaydı: `mail.monitrang.com`

---

## ✅ Kontrol Listesi

- [ ] DNS panelinde `_dmarc` TXT kaydını buldum
- [ ] DMARC policy'yi `p=quarantine` olarak güncelledim
- [ ] Değişikliği kaydettim
- [ ] DNS propagation için bekliyorum (1-2 saat)
- [ ] Test maili gönderdim
- [ ] Mail normal klasöre geldi (spam klasörüne değil)

---

**Son Güncelleme:** 3 Ocak 2026

