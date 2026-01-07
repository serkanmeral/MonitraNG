# Mail DNS Kayıtları Çakışma Çözümü

**Tarih:** 2 Ocak 2026  
**Durum:** Mevcut DNS kayıtları tespit edildi, çakışma çözümü

---

## 🔍 Mevcut DNS Durumu

DNS kontrolü sonuçları:

### ✅ MX Kaydı - Doğru
```
monitrang.com MX → mail.monitrang.com (priority: 10)
```
**Durum:** Zaten doğru yapılandırılmış, değişiklik gerekmiyor.

### ⚠️ SPF Kaydı - Çakışma Var

**İki SPF kaydı tespit edildi:**

1. **Bizim istediğimiz (Doğru):**
   ```
   v=spf1 mx a:mail.monitrang.com ~all
   ```

2. **Hosting Dünyam'ın (Eski):**
   ```
   v=spf1 +a +mx +a:domaincontrol.hostingdunyam.net -all
   ```

**Sorun:** SPF için sadece **tek bir TXT kaydı** olmalı. İki farklı SPF kaydı çakışmaya neden olur ve mail gönderiminde sorun yaratabilir.

---

## 🔧 Çözüm: SPF Kaydı Güncelleme

### Seçenek 1: Hosting Dünyam Kaydını Sil (Önerilen)

Eğer Hosting Dünyam'ın mail sunucusunu kullanmıyorsanız:

1. **DNS panelinde:**
   - `monitrang.com.` için TXT kayıtlarını bulun
   - Hosting Dünyam'ın SPF kaydını bulun: `v=spf1 +a +mx +a:domaincontrol.hostingdunyam.net -all`
   - Bu kaydın yanındaki **"Sil"** veya **"Delete"** butonuna tıklayın

2. **Kontrol:**
   - Bizim SPF kaydının kaldığını doğrulayın: `v=spf1 mx a:mail.monitrang.com ~all`
   - Eğer bu kayıt yoksa, yeni ekleyin (detaylar için `mail-dns-setup.md` dosyasına bakın)

### Seçenek 2: Birleşik SPF Kaydı (Her İki Sunucuyu da Desteklemek İçin)

Eğer hem Mailu hem de Hosting Dünyam mail sunucusunu kullanmak istiyorsanız:

**Yeni SPF Kaydı:**
```
v=spf1 mx a:mail.monitrang.com a:domaincontrol.hostingdunyam.net ~all
```

**DNS Paneline İşlemler:**
1. Her iki eski SPF kaydını da silin
2. Yeni birleşik SPF kaydını ekleyin:
   - **Type:** `TXT`
   - **Name:** `@` veya `monitrang.com.`
   - **Content:** `v=spf1 mx a:mail.monitrang.com a:domaincontrol.hostingdunyam.net ~all`
   - **TTL:** `3600`

**Not:** Bu seçenek genellikle gerekli değildir. Mailu kullanacaksanız sadece Mailu'yu destekleyen SPF kaydı yeterlidir.

---

## ✅ Yapılacaklar Özeti

### Yapılması Gerekenler

1. ✅ **MX Kaydı:** Zaten doğru, değişiklik yok
2. ⚠️ **SPF Kaydı:** Hosting Dünyam'ın eski kaydını sil
3. ✅ **SPF Kaydı:** Bizim kaydın varlığını kontrol et (zaten var görünüyor)
4. ⏳ **DMARC Kaydı:** Yeni eklenecek (henüz yok)
5. ⏳ **DKIM Kaydı:** Mailu kurulumundan sonra eklenecek

### Yapılmayacaklar

- ❌ MX kaydını değiştirme (zaten doğru)
- ❌ Yeni MX kaydı ekleme (zaten var)

---

## 🔍 Kontrol Komutları

### SPF Kayıtlarını Kontrol

**PowerShell:**
```powershell
# Tüm TXT kayıtlarını listele
Resolve-DnsName -Name monitrang.com -Type TXT

# Sadece SPF kayıtlarını filtrele
Resolve-DnsName -Name monitrang.com -Type TXT | Where-Object {$_.Strings -like "*spf*"}
```

**Beklenen Sonuç (Çakışma çözüldükten sonra):**
```
Name    : monitrang.com
Type    : TXT
TTL     : 3600
Strings : {v=spf1 mx a:mail.monitrang.com ~all}
```

**Sadece bir SPF kaydı olmalı!**

### MX Kaydını Kontrol

```powershell
Resolve-DnsName -Name monitrang.com -Type MX
```

**Beklenen Sonuç:**
```
Name          Type TTL  Section NameExchange       Preference
----          ---- ---  ------- ------------       ----------
monitrang.com MX   3600 Answer  mail.monitrang.com 10
```

---

## 📝 Önemli Notlar

1. **SPF Çakışması:** İki SPF kaydı mail gönderiminde sorun yaratabilir. Mutlaka çözülmeli.

2. **DNS Propagation:** Değişikliklerden sonra 5 dakika - 4 saat arası bekleme süresi olabilir.

3. **Test:** SPF kaydını silip güncelledikten sonra kontrol komutlarını çalıştırarak doğrulayın.

4. **Hosting Dünyam Mail:** Eğer Hosting Dünyam'ın mail sunucusunu hiç kullanmıyorsanız, eski SPF kaydını silmek sorun yaratmaz.

---

## 🔗 İlgili Dokümantasyon

- [Mail DNS Setup Guide](mail-dns-setup.md) - Detaylı kurulum rehberi
- [Domain DNS Configuration](domain-dns.md) - Genel DNS yapılandırması

---

**Son Güncelleme:** 2 Ocak 2026

