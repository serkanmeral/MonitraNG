# MngGateway

**API Gateway** servisi - MonitraNG mikroservis ekosisteminin merkezi giriş noktası.

## 📋 Genel Bakış

MngGateway, tüm mikroservislerin tek giriş noktasından yönetilmesini sağlayan bir API Gateway servisidir. Ocelot kullanılarak geliştirilmiştir.

### Temel Özellikler

- ✅ **Tek Giriş Noktası** - Tüm API'ler `https://api.monitra.local` üzerinden erişilebilir
- ✅ **Merkezi Authentication** - JWT validation (KeyCloak)
- ✅ **Rate Limiting** - Client/IP bazlı throttling
- ✅ **CORS Policy** - Frontend origin whitelist
- ✅ **Request/Response Logging** - Serilog ile merkezi loglama
- ✅ **SSL/TLS Termination** - Tek sertifika yönetimi
- ✅ **Backend İzolasyonu** - Servisler external network'e expose edilmez

## 🏗️ Mimari Yapı

### Clean Architecture

```
MngGateway/
├── Core/
│   ├── MngGateway.Domain/          # Entities, Exceptions
│   └── MngGateway.Application/     # Settings, Interfaces
├── Infrastructure/
│   └── MngGateway.Infrastructure/  # Ocelot, JWT, Logging
└── Presentation/
    └── MngGateway.Api/             # Program.cs, Ocelot config
```

## 🔀 Routing Yapısı

```
/keeper/*     → MngKeeper:5001
/data/*       → MngDataGateway:5010
/hub/*        → MngHub:5020
/reactor/*    → MngReactor:5003
/auth/*       → KeyCloak:8080
```

## 🚀 Kurulum

### Development

```bash
cd MngGateway
dotnet restore
dotnet build
dotnet run --project Presentation/MngGateway.Api
```

### Docker

```bash
docker build -t mnggateway -f Presentation/MngGateway.Api/Dockerfile .
docker run -p 5000:5000 mnggateway
```

## ⚙️ Konfigürasyon

### appsettings.json

```json
{
  "MngGatewaySettings": {
    "Server": {
      "Port": 5000,
      "Scheme": "https"
    },
    "Jwt": {
      "Authority": "http://keycloak:8080/realms/monitra",
      "Audience": "account"
    },
    "Cors": {
      "AllowedOrigins": ["https://app.monitra.local"]
    },
    "RateLimit": {
      "EnableRateLimiting": true,
      "AuthenticatedLimit": 100
    }
  }
}
```

### ocelot.json

Ocelot routing yapılandırması `Presentation/MngGateway.Api/ocelot.json` dosyasında tanımlanmıştır.

## 📦 Bağımlılıklar

- **Ocelot** - API Gateway framework
- **JWT Bearer** - Authentication
- **Serilog** - Logging
- **.NET 9.0** - Framework

## 🔒 Güvenlik

- JWT token validation (KeyCloak)
- Rate limiting (30-500 req/min)
- CORS policy
- SSL/TLS termination
- Backend servisler internal network'te

## 📝 Notlar

- Gateway, backend servislerin port/host bilgilerini gizler
- Tüm servisler internal network üzerinde çalışır
- Sertifika yönetimi sadece gateway'de yapılır

## 🔗 İlgili Dokümantasyon

- [ROADMAP.md](./ROADMAP.md) - Geliştirme yol haritası
- [API Documentation](./docs/api/) - API endpoint dokümantasyonu

