# MngNotifier Configuration

**Tarih:** 11 Ocak 2026  
**Versiyon:** 1.0.0

---

## 📋 İÇİNDEKİLER

1. [appsettings.json Yapısı](#appsettingsjson-yapısı)
2. [Mail Configuration](#mail-configuration)
3. [From Bilgisi Yönetimi](#from-bilgisi-yönetimi)
4. [Environment Variables](#environment-variables)

---

## 📝 APPSETTINGS.JSON YAPISI

### Tam Yapı

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@localhost:27017"
  },
  "MngNotifierSettings": {
    "MongoDB": {
      "DatabaseName": "mngnotifier"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/"
    },
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
    },
    "DataGateway": {
      "BaseUrl": "http://mngdatagateway:5010",
      "ApiVersion": "v1"
    },
    "Serilog": {
      "MinimumLevel": "Information",
      "WriteTo": [
        {
          "Name": "Console"
        },
        {
          "Name": "Seq",
          "Args": {
            "serverUrl": "http://localhost:5341"
          }
        }
      ]
    }
  }
}
```

---

## 📧 MAIL CONFIGURATION

### Development vs Production Ayarları

**Development Ortamı:**
- SMTP Host: `mail.monitrang.com` (production mail sunucusu)
- SMTP Port: `587` (TLS/STARTTLS)
- Authentication: Gerekli (`noreply@monitrang.com` / `!2345Qawsedrf*`)
- From: `noreply@monitrang.com`

**Production Ortamı (Sunucu İçinden):**
- SMTP Host: `127.0.0.1` (local Mailu container)
- SMTP Port: `25` (authentication gerektirmez)
- Authentication: Gerekmez
- From: `noreply@monitrang.com`

**Not:** Development ortamında da production mail sunucusunu kullanabilirsiniz. `mail.monitrang.com:587` üzerinden authentication ile mail gönderebilirsiniz.

### DefaultFrom Ayarları

**Yapı:**
```json
{
  "Mail": {
    "DefaultFrom": {
      "email": "noreply@example.com",  // Required
      "name": "MonitraNG"  // Optional
    }
  }
}
```

**Açıklama:**
- `email`: Default gönderen e-posta adresi (zorunlu)
- `name`: Default gönderen adı (opsiyonel, e-posta istemcilerinde görünen isim)

**Kullanım:**
- Endpoint'lerde `from` parametresi gönderilmezse, bu değer kullanılır
- Endpoint'lerde `from` parametresi gönderilirse, bu değer override edilir

### SMTP Ayarları

**Development Ortamı (appsettings.Development.json):**
```json
{
  "Mail": {
    "Provider": "SMTP",
    "Smtp": {
      "Host": "mail.monitrang.com",
      "Port": 587,
      "Username": "noreply@monitrang.com",
      "Password": "!2345Qawsedrf*",
      "EnableSsl": true
    }
  }
}
```

**Production Ortamı (appsettings.json veya Environment Variables):**
```json
{
  "Mail": {
    "Provider": "SMTP",
    "Smtp": {
      "Host": "127.0.0.1",
      "Port": 25,
      "Username": "",
      "Password": "",
      "EnableSsl": false
    }
  }
}
```

**Açıklama:**
- `Host`: SMTP sunucu adresi
  - Development: `mail.monitrang.com` (production mail sunucusu)
  - Production: `127.0.0.1` (local Mailu container)
- `Port`: SMTP port
  - Development: `587` (TLS/STARTTLS)
  - Production: `25` (plain, authentication gerektirmez)
- `Username`: SMTP kullanıcı adı
  - Development: `noreply@monitrang.com` (authentication gerekli)
  - Production: `""` (authentication gerektirmez)
- `Password`: SMTP şifresi
  - Development: `!2345Qawsedrf*` (authentication gerekli)
  - Production: `""` (authentication gerektirmez)
- `EnableSsl`: SSL/TLS kullanımı
  - Development: `true` (STARTTLS)
  - Production: `false` (local, plain connection)

---

## 🔄 FROM BİLGİSİ YÖNETİMİ

### Default From (appsettings.json)

**appsettings.json:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "DefaultFrom": {
        "email": "noreply@example.com",
        "name": "MonitraNG"
      }
    }
  }
}
```

### Override From (Endpoint'lerde)

**Direct API Endpoint:**
```json
{
  "to": ["user@example.com"],
  "from": {  // Optional - appsettings'ten default alınır
    "email": "custom@example.com",
    "name": "Custom Name"
  },
  "subject": "Test Email",
  "body": "<h1>Hello</h1>"
}
```

**Template-Based API Endpoint:**
```json
{
  "to": ["user@example.com"],
  "from": {  // Optional - appsettings'ten default alınır
    "email": "orders@example.com",
    "name": "Order System"
  },
  "templateId": "order-confirmation",
  "messageObject": {...}
}
```

**RabbitMQ Event:**
```json
{
  "eventType": "MailNotificationEvent",
  "to": ["user@example.com"],
  "from": {  // Optional - appsettings'ten default alınır
    "email": "system@example.com",
    "name": "System"
  },
  "subject": "Welcome",
  "body": "<h1>Welcome!</h1>"
}
```

### From Bilgisi Öncelik Sırası

1. **Endpoint/Event'te `from` parametresi varsa:** Bu değer kullanılır
2. **Endpoint/Event'te `from` parametresi yoksa:** `appsettings.json` içindeki `DefaultFrom` kullanılır
3. **Her ikisi de yoksa:** Hata fırlatılır (validation)

---

## 🌍 ENVIRONMENT VARIABLES

### Mail Configuration

```bash
# Default From
MngNotifierSettings__Mail__DefaultFrom__Email=noreply@example.com
MngNotifierSettings__Mail__DefaultFrom__Name=MonitraNG

# SMTP Settings (Development)
MngNotifierSettings__Mail__Smtp__Host=mail.monitrang.com
MngNotifierSettings__Mail__Smtp__Port=587
MngNotifierSettings__Mail__Smtp__Username=noreply@monitrang.com
MngNotifierSettings__Mail__Smtp__Password=!2345Qawsedrf*
MngNotifierSettings__Mail__Smtp__EnableSsl=true

# SMTP Settings (Production - Sunucu İçinden)
# MngNotifierSettings__Mail__Smtp__Host=127.0.0.1
# MngNotifierSettings__Mail__Smtp__Port=25
# MngNotifierSettings__Mail__Smtp__Username=
# MngNotifierSettings__Mail__Smtp__Password=
# MngNotifierSettings__Mail__Smtp__EnableSsl=false
```

### MongoDB Configuration

```bash
ConnectionStrings__MongoDB=mongodb://admin:admin123@localhost:27017
MngNotifierSettings__MongoDB__DatabaseName=mngnotifier
```

### RabbitMQ Configuration

```bash
MngNotifierSettings__RabbitMQ__Host=localhost
MngNotifierSettings__RabbitMQ__Port=5672
MngNotifierSettings__RabbitMQ__Username=admin
MngNotifierSettings__RabbitMQ__Password=admin123
MngNotifierSettings__RabbitMQ__VirtualHost=/
```

### DataGateway Configuration

```bash
MngNotifierSettings__DataGateway__BaseUrl=http://mngdatagateway:5010
MngNotifierSettings__DataGateway__ApiVersion=v1
```

---

## 📝 ÖRNEKLER

### Örnek 1: Default From Kullanımı

**appsettings.json:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "DefaultFrom": {
        "email": "noreply@monitra.local",
        "name": "MonitraNG"
      }
    }
  }
}
```

**Request (from parametresi yok):**
```json
{
  "to": ["user@example.com"],
  "subject": "Test Email",
  "body": "<h1>Hello</h1>"
}
```

**Sonuç:** Mail `noreply@monitra.local` (MonitraNG) adresinden gönderilir.

---

### Örnek 2: Override From Kullanımı

**appsettings.json:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "DefaultFrom": {
        "email": "noreply@monitra.local",
        "name": "MonitraNG"
      }
    }
  }
}
```

**Request (from parametresi var):**
```json
{
  "to": ["user@example.com"],
  "from": {
    "email": "orders@example.com",
    "name": "Order System"
  },
  "subject": "Order Confirmation",
  "body": "<h1>Your order is confirmed</h1>"
}
```

**Sonuç:** Mail `orders@example.com` (Order System) adresinden gönderilir.

---

### Örnek 3: Sadece Email (Name Yok)

**Request:**
```json
{
  "to": ["user@example.com"],
  "from": {
    "email": "support@example.com"
    // name yok
  },
  "subject": "Support Request",
  "body": "<h1>Support</h1>"
}
```

**Sonuç:** Mail `support@example.com` adresinden gönderilir (name yok, sadece email görünür).

---

## 🔒 GÜVENLİK NOTLARI

1. **SMTP Credentials:** Production'da environment variables kullanın, appsettings.json'a yazmayın
2. **DefaultFrom:** Production domain'e uygun bir e-posta adresi kullanın
3. **From Override:** Rate limiting ve validation ile abuse önlenebilir (gelecekte)

---

**Son Güncelleme:** 11 Ocak 2026
