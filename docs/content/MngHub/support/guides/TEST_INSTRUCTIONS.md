# MngHub Test Talimatları

## 🚀 Projeyi Çalıştırma

### Yöntem 1: Terminal'den

```bash
cd Presentation/MngHub.Api
dotnet run
```

### Yöntem 2: Visual Studio / Rider

1. `MngHub.sln` dosyasını açın
2. `MngHub.Api` projesini startup project olarak ayarlayın
3. F5 ile çalıştırın

---

## ✅ Başarılı Başlatma Kontrolü

Proje başarıyla başladığında şu logları görmelisiniz:

```
[INFO] Starting MngHub API on 0.0.0.0:5020
[INFO] RabbitMQ connection initialized
[INFO] Now listening on: http://0.0.0.0:5020
```

---

## 🧪 Test Etme

### 1. Health Check

```bash
curl http://localhost:5020/health
```

Beklenen yanıt:
```json
{
  "status": "healthy",
  "service": "MngHub",
  "timestamp": "2025-12-16T..."
}
```

### 2. Status Endpoint

```bash
curl http://localhost:5020/api/test/status
```

### 3. Connections Endpoint

```bash
curl http://localhost:5020/api/test/connections
```

### 4. Swagger UI

Tarayıcıda açın:
```
http://localhost:5020/swagger
```

### 5. PowerShell Test Script

```powershell
cd tests
pwsh -ExecutionPolicy Bypass -File test-mnghub.ps1
```

---

## 🔌 SignalR Testi

### JavaScript Client Örneği

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@latest/dist/browser/signalr.min.js"></script>
</head>
<body>
    <script>
        // MngKeeper'dan JWT token alın
        const token = "YOUR_JWT_TOKEN_HERE";
        
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`http://localhost:5020/ws?access_token=${token}`)
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveMessage", (message) => {
            console.log("Message received:", message);
        });

        connection.start()
            .then(() => console.log("Connected!"))
            .catch(err => console.error("Connection error:", err));
    </script>
</body>
</html>
```

---

## ⚠️ Olası Sorunlar ve Çözümler

### 1. RabbitMQ Bağlantı Hatası

**Hata:**
```
Failed to connect to RabbitMQ on startup
```

**Çözüm:**
- RabbitMQ'nun çalıştığından emin olun
- `appsettings.json`'daki RabbitMQ ayarlarını kontrol edin
- Proje yine de başlar, sadece warning verir

### 2. Port Kullanımda

**Hata:**
```
Address already in use
```

**Çözüm:**
- Port 5020'i kullanan başka bir uygulama olup olmadığını kontrol edin
- `appsettings.json`'da farklı bir port kullanın

### 3. JWT Token Hatası

**Hata:**
```
Connection rejected: Invalid token claims
```

**Çözüm:**
- MngKeeper'dan geçerli bir JWT token alın
- Token'da `domain_name` ve `sub` claim'lerinin olduğundan emin olun

---

## 📊 Beklenen Davranış

### Başarılı Bağlantı

1. Client SignalR hub'a bağlanır (`/ws?access_token=...`)
2. JWT token validate edilir
3. Connection kaydedilir
4. Domain room'una eklenir (`domain.{domainName}`)
5. Global room'a eklenir (`global`)
6. RabbitMQ topic'lerine subscribe olur

### Mesaj Akışı

1. RabbitMQ'ya mesaj gelir (örn: `domain.seven.user.created`)
2. RabbitMQ consumer mesajı alır
3. SignalR hub mesajı ilgili room'a gönderir
4. Client mesajı alır

---

## 🔍 Debug İpuçları

### Log Seviyesini Artırma

`appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MngHub": "Debug"
    }
  }
}
```

### Connection Durumunu Kontrol

```bash
curl http://localhost:5020/api/test/connections
```

---

**Son Güncelleme:** 16 Aralık 2025

