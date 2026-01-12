# Development vs Production Mail Configuration

**Tarih:** 11 Ocak 2026  
**Versiyon:** 1.0.0

---

## 🎯 ÖZET

**Development ortamında da mail gönderebilirsiniz!** Production mail sunucusunu (`mail.monitrang.com:587`) kullanarak authentication ile mail gönderebilirsiniz.

---

## 📧 DEVELOPMENT ORTAMI

### SMTP Ayarları

**appsettings.Development.json:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "Provider": "SMTP",
      "DefaultFrom": {
        "email": "noreply@monitrang.com",
        "name": "MonitraNG"
      },
      "Smtp": {
        "Host": "mail.monitrang.com",
        "Port": 587,
        "Username": "noreply@monitrang.com",
        "Password": "!2345Qawsedrf*",
        "EnableSsl": true
      }
    }
  }
}
```

**Özellikler:**
- ✅ Production mail sunucusunu kullanır (`mail.monitrang.com`)
- ✅ Port `587` (TLS/STARTTLS)
- ✅ Authentication gerekli (`noreply@monitrang.com` / `!2345Qawsedrf*`)
- ✅ Internet bağlantısı gerekli (production sunucuya erişim)

**Avantajlar:**
- Development'ta gerçek mail gönderme test edilebilir
- Production ile aynı mail sunucusu kullanılır
- Mail gönderimi doğrulanabilir

**Dezavantajlar:**
- Internet bağlantısı gerekli
- Production mail sunucusuna bağımlılık

---

## 🚀 PRODUCTION ORTAMI

### SMTP Ayarları (Sunucu İçinden)

**appsettings.json veya Environment Variables:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "Provider": "SMTP",
      "DefaultFrom": {
        "email": "noreply@monitrang.com",
        "name": "MonitraNG"
      },
      "Smtp": {
        "Host": "127.0.0.1",
        "Port": 25,
        "Username": "",
        "Password": "",
        "EnableSsl": false
      }
    }
  }
}
```

**Özellikler:**
- ✅ Local Mailu container kullanır (`127.0.0.1`)
- ✅ Port `25` (plain SMTP)
- ✅ Authentication gerektirmez (sunucu içinden)
- ✅ Daha hızlı (local network)

**Avantajlar:**
- Authentication gerektirmez
- Daha hızlı (local network)
- Internet bağlantısı gerektirmez (mail sunucusu aynı sunucuda)

**Dezavantajlar:**
- Sadece sunucu içinden çalışır
- Local Mailu container'ın çalışıyor olması gerekir

---

## 🔄 KARŞILAŞTIRMA

| Özellik | Development | Production |
|---------|-------------|------------|
| **SMTP Host** | `mail.monitrang.com` | `127.0.0.1` |
| **SMTP Port** | `587` | `25` |
| **Authentication** | ✅ Gerekli | ❌ Gerekmez |
| **Username** | `noreply@monitrang.com` | `""` (boş) |
| **Password** | `!2345Qawsedrf*` | `""` (boş) |
| **SSL/TLS** | ✅ `true` (STARTTLS) | ❌ `false` |
| **Internet** | ✅ Gerekli | ❌ Gerekmez |
| **Mail Sunucusu** | Production (`mail.monitrang.com`) | Local (`127.0.0.1`) |

---

## 📝 KULLANIM ÖRNEKLERİ

### Development Ortamında Test

**1. appsettings.Development.json'u ayarlayın:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "Smtp": {
        "Host": "mail.monitrang.com",
        "Port": 587,
        "Username": "noreply@monitrang.com",
        "Password": "!2345Qawsedrf*",
        "EnableSsl": true
      }
    }
  }
}
```

**2. MngNotifier servisini çalıştırın:**
```bash
cd Presentation/MngNotifier.Api
dotnet run
```

**3. Mail gönderin:**
```http
POST http://localhost:5030/api/v1/notifications/send
Content-Type: application/json

{
  "to": ["serkan.meral@outlook.com"],
  "subject": "Test Mail from Development",
  "body": "<h1>Hello from Development!</h1>"
}
```

**Sonuç:** Mail `mail.monitrang.com:587` üzerinden authentication ile gönderilir.

---

### Production Ortamında

**1. Environment Variables ayarlayın:**
```bash
MngNotifierSettings__Mail__Smtp__Host=127.0.0.1
MngNotifierSettings__Mail__Smtp__Port=25
MngNotifierSettings__Mail__Smtp__Username=
MngNotifierSettings__Mail__Smtp__Password=
MngNotifierSettings__Mail__Smtp__EnableSsl=false
```

**2. Docker container içinde çalışır:**
- Local Mailu container'a (`127.0.0.1:25`) bağlanır
- Authentication gerektirmez
- Hızlı ve güvenli

---

## ⚠️ ÖNEMLİ NOTLAR

### Development Ortamı

1. **Internet Bağlantısı:** `mail.monitrang.com`'a erişim gerekli
2. **Firewall:** Port `587` açık olmalı (genellikle açık)
3. **Authentication:** Credentials doğru olmalı
4. **Rate Limiting:** Production mail sunucusunda rate limiting olabilir

### Production Ortamı

1. **Local Mailu:** Mailu container'ın çalışıyor olması gerekir
2. **Network:** Container'lar aynı Docker network'ünde olmalı
3. **Port 25:** Local port `25` açık olmalı (Mailu tarafından sağlanır)

---

## 🔧 SORUN GİDERME

### Development: "Connection refused" veya "Timeout"

**Sorun:** `mail.monitrang.com:587`'ye bağlanılamıyor.

**Çözüm:**
1. Internet bağlantısını kontrol edin
2. Firewall'da port `587` açık mı kontrol edin
3. `mail.monitrang.com` DNS çözümlemesini test edin:
   ```bash
   ping mail.monitrang.com
   telnet mail.monitrang.com 587
   ```

### Development: "Authentication failed"

**Sorun:** Username/password yanlış.

**Çözüm:**
1. `noreply@monitrang.com` / `!2345Qawsedrf*` credentials'larını kontrol edin
2. Mailu admin panelinden kullanıcı durumunu kontrol edin
3. Password'un doğru olduğundan emin olun

### Production: "Connection refused"

**Sorun:** `127.0.0.1:25`'e bağlanılamıyor.

**Çözüm:**
1. Mailu container'ın çalıştığını kontrol edin:
   ```bash
   docker ps | grep mailu
   ```
2. Container'ların aynı network'te olduğunu kontrol edin
3. Mailu'nun port `25`'i expose ettiğini kontrol edin

---

## 📌 SONUÇ

**Development ortamında da mail gönderebilirsiniz!** Production mail sunucusunu (`mail.monitrang.com:587`) kullanarak authentication ile mail gönderebilirsiniz. Bu sayede development'ta gerçek mail gönderme test edilebilir.

**Production'da** ise local Mailu container (`127.0.0.1:25`) kullanılır ve authentication gerektirmez.

---

**Son Güncelleme:** 11 Ocak 2026
