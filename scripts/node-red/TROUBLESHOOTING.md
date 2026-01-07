# Node-RED Mail Sender - Sorun Giderme

## 🔴 Port 25 Bağlantı Hatası

### Hata Mesajı
```
Error: connect ECONNREFUSED 45.141.151.52:25
```

### Neden
- **ISP Blokajı:** Çoğu ISP (Internet Service Provider) port 25'i bloklar (spam önleme)
- **Firewall:** Windows Firewall veya antivirus yazılımı port 25'i engelliyor olabilir
- **Güvenlik:** Local makineden production sunucusuna direkt port 25 bağlantısı güvenlik nedeniyle kısıtlanmış olabilir

### Çözüm 1: Port 587 Kullan (Önerilen) ✅

Flow zaten port 587 ve authentication kullanacak şekilde yapılandırılmış. Kontrol edin:

1. **Email node ayarlarını kontrol edin:**
   - Port: `587` (25 değil)
   - TLS: `true` (STARTTLS)
   - Authentication: `basic`
   - Username: `noreply@monitrang.com`
   - Password: `!2345Qawsedrf*`

2. **Port 587 bağlantısını test edin:**
   ```powershell
   Test-NetConnection -ComputerName mail.monitrang.com -Port 587
   ```

   Veya:
   ```bash
   telnet mail.monitrang.com 587
   ```

### Çözüm 2: Production Sunucusunda Node-RED Kullan

Local makineden gönderemiyorsanız, production sunucusunda Node-RED çalıştırın:

1. **Production sunucusuna SSH ile bağlanın:**
   ```bash
   ssh root@monitrang-server
   ```

2. **Production sunucusunda Node-RED kurun:**
   ```bash
   npm install -g node-red
   ```

3. **Production sunucusunda Node-RED'i başlatın:**
   ```bash
   node-red
   ```

4. **Flow'u production sunucusundaki Node-RED'e yükleyin**

5. **Email node ayarlarını güncelleyin:**
   - Server: `mail.monitrang.com` (veya `127.0.0.1`)
   - Port: `25` (production sunucusundan gönderim için authentication gerekmez)
   - TLS: `false`
   - Authentication: `none`

### Çözüm 3: SSH Tunnel Kullan

Local makineden SSH tunnel ile port 25'e bağlanın:

1. **SSH tunnel oluşturun:**
   ```bash
   ssh -L 2525:mail.monitrang.com:25 root@monitrang-server
   ```

2. **Email node ayarlarını güncelleyin:**
   - Server: `127.0.0.1` (localhost)
   - Port: `2525` (tunnel port)
   - TLS: `false`
   - Authentication: `none`

### Çözüm 4: SMTP Relay Kullan

Alternatif bir SMTP relay servisi kullanın (SendGrid, Mailgun, vb.):

1. **SMTP relay servisine kaydolun**
2. **Email node ayarlarını güncelleyin:**
   - Server: Relay servisinin SMTP sunucusu
   - Port: `587` veya `465`
   - TLS: `true`
   - Authentication: `basic`
   - Username/Password: Relay servisinin verdiği bilgiler

## 🔍 Bağlantı Testi

### Port 587 Testi
```powershell
# PowerShell
Test-NetConnection -ComputerName mail.monitrang.com -Port 587

# Telnet
telnet mail.monitrang.com 587
```

### Port 25 Testi
```powershell
# PowerShell
Test-NetConnection -ComputerName mail.monitrang.com -Port 25

# Telnet
telnet mail.monitrang.com 25
```

**Beklenen Sonuç:**
- Port 587: ✅ Bağlantı başarılı olmalı
- Port 25: ❌ Bağlantı başarısız olabilir (ISP blokajı)

## 📝 Email Node Ayarları (Port 587)

```json
{
    "server": "mail.monitrang.com",
    "port": "587",
    "secure": false,
    "tls": true,
    "authtype": "basic",
    "username": "noreply@monitrang.com",
    "password": "!2345Qawsedrf*"
}
```

## 📝 Email Node Ayarları (Production Sunucusundan - Port 25)

```json
{
    "server": "mail.monitrang.com",
    "port": "25",
    "secure": false,
    "tls": false,
    "authtype": "none",
    "username": "",
    "password": ""
}
```

## 🐛 Diğer Hatalar

### "Authentication failed" Hatası

- Username ve password'ün doğru olduğundan emin olun
- Port 587 kullanıyorsanız TLS'nin açık olduğundan emin olun

### "Connection timeout" Hatası

- Firewall kurallarını kontrol edin
- Antivirus yazılımını kontrol edin
- Network bağlantısını kontrol edin

### "Mail gönderildi ama gelmedi" Sorunu

- Spam klasörünü kontrol edin
- DMARC policy'yi kontrol edin (`p=quarantine` veya `p=none` olmalı)
- Mail sunucusu log'larını kontrol edin

---

**Son Güncelleme:** 3 Ocak 2026

