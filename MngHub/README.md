# MngHub - WebSocket Gateway

**Microservice:** Real-time WebSocket Gateway  
**Version:** 1.0.0  
**Port:** 5020 (HTTPS) / 5234 (HTTP - Development)

---

## 🎯 Genel Bakış

**MngHub**, RabbitMQ event'lerini WebSocket (SignalR) bağlantılarına bridge eden, real-time communication sağlayan bir mikroservistir.

### Temel Özellikler

- ✅ **SignalR Hub** - WebSocket connection management
- ✅ **RabbitMQ Consumer** - Topic subscription ve message forwarding
- ✅ **JWT Authentication** - MngKeeper ile token validation
- ✅ **Domain-based Isolation** - Multi-tenant message filtering
- ✅ **Connection Management** - Lifecycle ve reconnection handling
- ✅ **Clean Architecture** - MngDataGateway pattern'i takip eder

---

## 🚀 Hızlı Başlangıç

### 1. Projeyi Çalıştırma

```bash
cd Presentation/MngHub.Api
dotnet run
```

Proje şu portlarda çalışacak:
- **HTTPS:** `https://localhost:5020`
- **HTTP:** `http://localhost:5234` (Development)

### 2. Test Etme

```bash
# Test scriptini çalıştır
cd tests
pwsh -ExecutionPolicy Bypass -File test-mnghub.ps1
```

### 3. Endpoint'ler

- **Health Check:** `GET /health`
- **Status:** `GET /api/test/status`
- **Connections:** `GET /api/test/connections`
- **SignalR Hub:** `ws://localhost:5234/ws?access_token=<JWT_TOKEN>`

---

## 📋 Gereksinimler

- .NET 9.0 SDK
- RabbitMQ Server (localhost:5672)
- MngKeeper API (JWT token validation için)

---

## ⚙️ Yapılandırma

`appsettings.json` dosyasında:

```json
{
  "MngHubSettings": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5020,
      "Scheme": "https"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "ExchangeName": "mng.topics"
    },
    "Actors": {
      "MngKeeper": "https://localhost:5001"
    }
  }
}
```

---

## 🔌 SignalR Bağlantısı

### JavaScript Client

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:5020/ws", {
    accessTokenFactory: () => getJwtToken()
  })
  .withAutomaticReconnect()
  .build();

connection.on("ReceiveMessage", (message) => {
  console.log("Message received:", message);
});

await connection.start();
```

### C# Client

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5020/ws", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .Build();

connection.On<MessageDto>("ReceiveMessage", message =>
{
    Console.WriteLine($"Message: {message.RoutingKey}");
});

await connection.StartAsync();
```

---

## 🏗️ Mimari

### Clean Architecture Katmanları

```
MngHub/
├── Core/
│   ├── MngHub.Domain/          # Entities, Exceptions, Constants
│   └── MngHub.Application/     # Interfaces, DTOs, Configuration
├── Infrastructure/
│   ├── MngHub.Infrastructure/  # RabbitMQ, SignalR, JWT
│   └── MngHub.Persistence/      # Connection tracking
└── Presentation/
    └── MngHub.Api/             # Program.cs, Controllers
```

---

## 📝 Domain-based Room Yapısı

Her domain kendi SignalR room'una sahiptir:
- **Global Room:** `global` (tüm kullanıcılar)
- **Domain Room:** `domain.{domainName}` (örn: `domain.seven`)

RabbitMQ routing keys:
- **Global Events:** `global.*`
- **Domain Events:** `domain.{domainName}.#`

---

## 🧪 Test

Test scripti çalıştırma:

```powershell
cd tests
pwsh -ExecutionPolicy Bypass -File test-mnghub.ps1
```

---

## 📚 Daha Fazla Bilgi

- [Architecture Plan](docs/ARCHITECTURE_PLAN.md)
- [MngDataGateway Architecture Guide](../MngDataGateway/docs/ARCHITECTURE_GUIDE.md)

---

**Son Güncelleme:** 16 Aralık 2025

