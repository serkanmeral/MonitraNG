# Mail Gönderme Test Scriptleri

Production sunucusundan mail göndermek için kullanılan test scriptleri.

## 📋 Scriptler

### 1. `test-send-mail.sh` - Sunucu İçinden

Sunucu üzerinde direkt çalıştırılan bash scripti.

**Kullanım:**
```bash
# SSH ile sunucuya bağlan
ssh root@monitrang-server

# Script dizinine git
cd /root/MonitraNG/scripts/tests/Mailu

# Basit kullanım (varsayılan değerlerle)
./test-send-mail.sh

# Özel alıcı ile
./test-send-mail.sh serkan.meral@outlook.com

# Özel konu ve içerik ile
./test-send-mail.sh serkan.meral@outlook.com "Test Konusu" "Test içeriği"
```

**Özellikler:**
- Sunucu içinden `127.0.0.1:25` kullanır (authentication gerektirmez)
- Otomatik olarak şu araçları dener:
  1. `swaks` (varsa)
  2. `sendmail` (varsa)
  3. `mailx` (varsa)
  4. `python3` (varsa)

**Varsayılan Değerler:**
- Gönderen: `noreply@monitrang.com`
- SMTP Host: `127.0.0.1`
- SMTP Port: `25`
- Alıcı: `serkan.meral@outlook.com` (parametre verilmezse)

---

### 2. `test-send-mail.ps1` - Local Makineden (SSH ile)

Local Windows makineden SSH ile sunucudaki scripti çalıştıran PowerShell scripti.

**Kullanım:**
```powershell
# Basit kullanım (varsayılan değerlerle)
.\test-send-mail.ps1

# Özel alıcı ile
.\test-send-mail.ps1 -ToEmail "serkan.meral@outlook.com"

# Özel konu ve içerik ile
.\test-send-mail.ps1 -ToEmail "serkan.meral@outlook.com" -Subject "Test Konusu" -Body "Test içeriği"

# Farklı sunucu ile
.\test-send-mail.ps1 -ToEmail "serkan.meral@outlook.com" -Server "monitrang-server"
```

**Parametreler:**
- `-ToEmail`: Alıcı email adresi (varsayılan: `serkan.meral@outlook.com`)
- `-Subject`: Mail konusu (varsayılan: `Test Mail from MonitraNG Server`)
- `-Body`: Mail içeriği (varsayılan: `Bu bir test mailidir. Production sunucusundan SSH ile gönderilmiştir.`)
- `-Server`: SSH sunucu adı (varsayılan: `monitrang-server`)

**Gereksinimler:**
- SSH bağlantısı (`ssh root@monitrang-server` çalışmalı)
- Sunucuda `test-send-mail.sh` scripti mevcut olmalı

---

## 🔧 SMTP Konfigürasyonu

### Production Sunucusu İçinden

```yaml
SMTP_HOST: 127.0.0.1
SMTP_PORT: 25
SMTP_USERNAME: ""      # Authentication gerektirmez
SMTP_PASSWORD: ""      # Authentication gerektirmez
SMTP_FROM: noreply@monitrang.com
```

### Lokal Development Ortamı (Gelecekte)

```yaml
SMTP_HOST: mail.monitrang.com
SMTP_PORT: 587
SMTP_USERNAME: noreply@monitrang.com
SMTP_PASSWORD: !2345Qawsedrf*
SMTP_FROM: noreply@monitrang.com
SMTP_ENABLE_TLS: true  # STARTTLS
```

---

## 📝 Örnekler

### Sunucu Üzerinden

```bash
# Hızlı test
ssh root@monitrang-server "cd /root/MonitraNG/scripts/tests/Mailu && ./test-send-mail.sh"

# Özel mesaj ile
ssh root@monitrang-server "cd /root/MonitraNG/scripts/tests/Mailu && ./test-send-mail.sh serkan.meral@outlook.com 'Önemli Bildirim' 'Sistem başarıyla çalışıyor.'"
```

### Local Makineden (PowerShell)

```powershell
# Hızlı test
.\test-send-mail.ps1

# Özel mesaj ile
.\test-send-mail.ps1 -ToEmail "serkan.meral@outlook.com" -Subject "Önemli Bildirim" -Body "Sistem başarıyla çalışıyor."
```

---

## 🐛 Sorun Giderme

### "Relay access denied" Hatası

**Sorun:** `mail.monitrang.com` kullanıldığında relay access denied hatası alınıyor.

**Çözüm:** Sunucu içinden gönderim için `127.0.0.1` kullanın (script zaten bunu yapıyor).

### "Command not found" Hatası

**Sorun:** `swaks`, `sendmail`, `mailx` veya `python3` bulunamıyor.

**Çözüm:** Sunucuda gerekli araçları kurun:
```bash
# swaks (önerilen)
apt-get update && apt-get install -y swaks

# veya sendmail
apt-get install -y sendmail

# veya mailx
apt-get install -y mailutils

# veya python3
apt-get install -y python3
```

### SSH Bağlantı Hatası

**Sorun:** PowerShell scriptinden SSH bağlantısı kurulamıyor.

**Çözüm:**
1. SSH key'in yapılandırıldığından emin olun
2. `ssh root@monitrang-server` komutunu manuel test edin
3. SSH config dosyasında `monitrang-server` hostname'inin tanımlı olduğundan emin olun

---

## 📌 Notlar

- Mail gönderimi sunucu içinden `127.0.0.1:25` kullanır (authentication gerektirmez)
- Lokal development ortamından mail göndermek için gelecekte ayrı bir backend servisi geliştirilecek
- Şu an için test amaçlı scriptler yeterli
- Production'da mail gönderimi için Mailu kullanılıyor (`mail.monitrang.com`)

---

## 🔗 İlgili Dosyalar

- `test-send-mail.sh` - Sunucu içinden çalıştırılan bash scripti
- `test-send-mail.ps1` - Local makineden SSH ile çalıştırılan PowerShell scripti
- `get-dkim-key.sh` - DKIM key'lerini almak için script

