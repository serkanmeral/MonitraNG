# Mail Sunucusu DNS Kayıtları Kurulum Rehberi

**Tarih:** 2 Ocak 2026  
**Domain:** `monitrang.com`  
**Mail Sunucusu:** Mailu  
**Hosting:** Hosting Dünyam

---

## 📋 Genel Bakış

Mailu mail sunucusu için gerekli DNS kayıtlarını Hosting Dünyam DNS panelinde oluşturma rehberi.

---

## 🌐 Gerekli DNS Kayıtları

### 1. A Kaydı (Mail Subdomain)

**Kayıt Bilgileri:**
- **Type:** `A`
- **Name:** `mail`
- **Content:** `45.141.151.52`
- **TTL:** `3600`

**DNS Paneline Ekleme:**
1. Hosting Dünyam DNS panelinde "Yeni Kayıt Ekle" butonuna tıklayın
2. **Type:** `A` seçin
3. **Name:** `mail` girin (sadece subdomain adı, nokta veya domain adı eklemeyin)
4. **Content:** `45.141.151.52` girin
5. **TTL:** `3600` girin
6. **Kaydet** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: A
Name: mail
Content: 45.141.151.52
TTL: 3600
```

---

### 2. MX Kaydı (Mail Exchange)

**✅ DURUM:** DNS kontrolünde MX kaydı zaten var ve doğru yapılandırılmış:
```
monitrang.com MX → mail.monitrang.com (priority: 10)
```

**İşlem:** MX kaydı zaten doğru, **değişiklik yapmanıza gerek yok.**

**Eğer MX kaydı yoksa veya yanlışsa:**

**Kayıt Bilgileri:**
- **Type:** `MX`
- **Name:** `@` veya `monitrang.com.`
- **Content:** `mail.monitrang.com`
- **Priority:** `10`
- **TTL:** `3600`

**DNS Paneline Ekleme:**
1. "Yeni Kayıt Ekle" butonuna tıklayın
2. **Type:** `MX` seçin
3. **Name:** `@` veya `monitrang.com.` girin (ana domain)
4. **Content/Value:** `mail.monitrang.com` girin
5. **Priority:** `10` girin
6. **TTL:** `3600` girin
7. **Kaydet** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: MX
Name: monitrang.com.
Content: mail.monitrang.com
Priority: 10
TTL: 3600
```

**Açıklama:**
- MX kaydı, domain'e gelen maillerin hangi sunucuya yönlendirileceğini belirler
- Priority değeri düşük olan önceliklidir (10 = yüksek öncelik)

---

### 3. SPF Kaydı (Sender Policy Framework)

**⚠️ ÖNEMLİ:** DNS kontrolünde iki SPF kaydı görüldü:
1. Bizim istediğimiz: `v=spf1 mx a:mail.monitrang.com ~all`
2. Hosting Dünyam'ın: `v=spf1 +a +mx +a:domaincontrol.hostingdunyam.net -all`

**Çözüm:** Hosting Dünyam'ın SPF kaydını **silip** bizim kaydı kullanmalıyız.

**Kayıt Bilgileri:**
- **Type:** `TXT`
- **Name:** `@` veya `monitrang.com.`
- **Content:** `v=spf1 mx a:mail.monitrang.com ~all`
- **TTL:** `3600`

**DNS Paneline İşlemler:**

**Adım 1: Eski SPF Kaydını Silme**
1. DNS panelinde `monitrang.com.` için TXT kayıtlarını bulun
2. Hosting Dünyam'ın SPF kaydını bulun: `v=spf1 +a +mx +a:domaincontrol.hostingdunyam.net -all`
3. Bu kaydın yanındaki **"Sil"** veya **"Delete"** butonuna tıklayın

**Adım 2: Yeni SPF Kaydını Ekleme (Eğer yoksa)**
1. DNS kontrolünde bizim SPF kaydı zaten var görünüyor: `v=spf1 mx a:mail.monitrang.com ~all`
2. Eğer bu kayıt yoksa:
   - "Yeni Kayıt Ekle" butonuna tıklayın
   - **Type:** `TXT` seçin
   - **Name:** `@` veya `monitrang.com.` girin (ana domain)
   - **Content/Value:** `v=spf1 mx a:mail.monitrang.com ~all` girin (tırnak işareti olmadan)
   - **TTL:** `3600` girin
   - **Kaydet** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: TXT
Name: monitrang.com.
Content: v=spf1 mx a:mail.monitrang.com ~all
TTL: 3600
```

**Açıklama:**
- `v=spf1`: SPF versiyonu
- `mx`: MX kaydında belirtilen sunucular mail gönderebilir
- `a:mail.monitrang.com`: mail.monitrang.com IP'sinden mail gönderilebilir
- `~all`: Diğer kaynaklardan gelen mailler "soft fail" (test için uygun)
- Production'da `-all` (hard fail) kullanılabilir

---

### 4. DMARC Kaydı (Domain-based Message Authentication)

**Kayıt Bilgileri:**
- **Type:** `TXT`
- **Name:** `_dmarc` (alt çizgi ile başlar)
- **Content:** `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com`
- **TTL:** `3600`

**DNS Paneline Ekleme:**
1. "Yeni Kayıt Ekle" butonuna tıklayın
2. **Type:** `TXT` seçin
3. **Name:** `_dmarc` girin (alt çizgi ile başlar, domain adı eklenmez)
4. **Content/Value:** `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com` girin
5. **TTL:** `3600` girin
6. **Kaydet** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: TXT
Name: _dmarc
Content: v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com; ruf=mailto:admin@monitrang.com
TTL: 3600
```

**Açıklama:**
- `v=DMARC1`: DMARC versiyonu
- `p=quarantine`: SPF/DKIM başarısız mailler quarantine (test için uygun)
  - Production'da `p=reject` kullanılabilir
  - `p=none`: Sadece raporlama (test için)
- `rua=mailto:admin@monitrang.com`: Aggregate raporlar için email adresi
- `ruf=mailto:admin@monitrang.com`: Forensic raporlar için email adresi

---

### 5. DKIM Kaydı (DomainKeys Identified Mail)

**⚠️ ÖNEMLİ:** DKIM kaydı Mailu kurulumundan **sonra** oluşturulacak.

**Kayıt Bilgileri:**
- **Type:** `TXT`
- **Name:** `default._domainkey`
- **Content:** `v=DKIM1; k=rsa; p=[Mailu'dan alınan public key]`
- **TTL:** `3600`

**Mailu'dan DKIM Key Alma:**
1. Mailu admin panelini açın: `https://mail.monitrang.com/admin`
2. **Domains** sekmesine gidin
3. `monitrang.com` domain'ini seçin
4. **DKIM Keys** bölümüne gidin
5. Public key'i kopyalayın

**DNS Paneline Ekleme:**
1. "Yeni Kayıt Ekle" butonuna tıklayın
2. **Type:** `TXT` seçin
3. **Name:** `default._domainkey` girin
4. **Content/Value:** `v=DKIM1; k=rsa; p=[Mailu'dan kopyaladığınız public key]` girin
   - Örnek: `v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC...`
5. **TTL:** `3600` girin
6. **Kaydet** butonuna tıklayın

**Beklenen Sonuç:**
```
Type: TXT
Name: default._domainkey
Content: v=DKIM1; k=rsa; p=[PUBLIC_KEY]
TTL: 3600
```

---

## ✅ DNS Kayıtları Kontrol Listesi

### Öncelikli (Mailu Kurulumundan Önce)

- [ ] **A Record:** `mail` → `45.141.151.52`
- [ ] **MX Record:** `monitrang.com` → `mail.monitrang.com` (priority: 10)
- [ ] **SPF Record:** `monitrang.com` → `v=spf1 mx a:mail.monitrang.com ~all`
- [ ] **DMARC Record:** `_dmarc.monitrang.com` → `v=DMARC1; p=quarantine; rua=mailto:admin@monitrang.com`

### Mailu Kurulumundan Sonra

- [ ] **DKIM Record:** `default._domainkey.monitrang.com` → (Mailu admin panelinden alınacak)

---

## 🔍 DNS Kayıtlarını Kontrol Etme

### Online Araçlar

1. **MXToolbox:** https://mxtoolbox.com/
   - MX record kontrolü: `monitrang.com` → MX Lookup
   - SPF kontrolü: `monitrang.com` → SPF Lookup
   - DMARC kontrolü: `_dmarc.monitrang.com` → DMARC Lookup
   - DKIM kontrolü: `default._domainkey.monitrang.com` → DKIM Lookup

2. **DNS Checker:** https://dnschecker.org/
   - Tüm DNS kayıtlarını kontrol edebilirsiniz

3. **Mail-Tester:** https://www.mail-tester.com/
   - Spam score kontrolü
   - SPF, DKIM, DMARC doğrulama

### Komut Satırı ile Kontrol

**Windows (PowerShell):**
```powershell
# A record kontrolü
Resolve-DnsName -Name mail.monitrang.com -Type A

# MX record kontrolü
Resolve-DnsName -Name monitrang.com -Type MX

# SPF record kontrolü
Resolve-DnsName -Name monitrang.com -Type TXT | Where-Object {$_.Strings -like "*spf1*"}

# DMARC record kontrolü
Resolve-DnsName -Name _dmarc.monitrang.com -Type TXT

# DKIM record kontrolü (Mailu kurulumundan sonra)
Resolve-DnsName -Name default._domainkey.monitrang.com -Type TXT
```

**Linux/Mac:**
```bash
# A record kontrolü
dig mail.monitrang.com A

# MX record kontrolü
dig monitrang.com MX

# SPF record kontrolü
dig monitrang.com TXT | grep spf

# DMARC record kontrolü
dig _dmarc.monitrang.com TXT

# DKIM record kontrolü (Mailu kurulumundan sonra)
dig default._domainkey.monitrang.com TXT
```

---

## ⏱️ DNS Propagation Süresi

DNS kayıtlarının yayılması genellikle:
- **Minimum:** 5-15 dakika
- **Ortalama:** 1-4 saat
- **Maksimum:** 24-48 saat

**Hızlı Kontrol:**
- Farklı DNS sunucularından kontrol edin (Google DNS: 8.8.8.8, Cloudflare: 1.1.1.1)
- Tüm sunucularda aynı sonuç görünene kadar bekleyin

---

## 📝 Önemli Notlar

1. **DKIM Key:** Mailu kurulumundan sonra oluşturulacak, şimdilik eklenmeyecek
2. **SPF `~all`:** Test ortamı için uygun, production'da `-all` kullanılabilir
3. **DMARC `p=quarantine`:** Test ortamı için uygun, production'da `p=reject` kullanılabilir
4. **Reverse DNS (PTR):** Hosting sağlayıcısından talep edilmeli (`45.141.151.52` → `mail.monitrang.com`)
5. **Port 25:** Bazı hosting sağlayıcıları port 25'i bloklar, alternatif olarak port 587 kullanılabilir

---

## 🔗 İlgili Dokümantasyon

- [Domain DNS Yapılandırması](domain-dns.md)
- [Mailu Implementation Planı](mailu-implementation-plan.md)
- [Mail Server Setup Guide](mail-server-setup.md)

---

**Son Güncelleme:** 2 Ocak 2026

