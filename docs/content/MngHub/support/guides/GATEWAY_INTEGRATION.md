---
title: "MngHub Gateway Integration"
category: "guides"
tags: ["hub", "gateway", "integration", "routing"]
service: "MngHub"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# MngHub - Gateway Entegrasyonu

**Tarih**: 31 Aralık 2025  
**Versiyon**: 1.0.0  
**Durum**: ✅ Tamamlandı

## Genel Bakış

MngHub servisi, API Gateway (MngGateway) üzerinden erişilebilir hale getirildi. Bu değişiklikler, Scalar API Reference dokümantasyonunda gateway URL'ini göstermek ve API Gateway üzerinden test edilebilirliği sağlamak için yapıldı.

## Yapılan Değişiklikler

### 1. OpenAPI Server Path Güncellemesi

**Dosya**: `Presentation/MngHub.Api/appsettings.json`

```json
{
  "MngHubSettings": {
    "OpenApiServerPath": "https://api.monitra.local/hub"
  }
}
```

**Önceki Değer**: `https://localhost:5020`  
**Yeni Değer**: `https://api.monitra.local/hub`

**Açıklama**: Scalar API Reference'da gösterilecek server URL'i gateway URL'ine güncellendi.

---

### 2. Program.cs Güncellemesi

**Dosya**: `Presentation/MngHub.Api/Program.cs`

**Değişiklikler**:
- `settings.OpenApiServerPath` configuration'dan okunuyor
- Scalar API Reference yapılandırmasında Server URL ekleniyor

**Kod Örneği**:
```csharp
// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    // Add Scalar API Reference (Modern UI)
    // Get OpenAPI Server Path from configuration (for API Gateway)
    var openApiServerPath = settings.OpenApiServerPath;
    
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("MngHub API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithOpenApiRoutePattern("/openapi/v1.json");
        
        // Configure Server URL from settings (for API Gateway)
        if (!string.IsNullOrEmpty(openApiServerPath))
        {
            options.AddServer(new ScalarServer(openApiServerPath, "API Gateway Server"));
        }
    });
}
```

---

## Gateway Routing

### Endpoint Yapısı

**Direkt Erişim** (Development):
- `http://localhost:5020/ws/*` (SignalR WebSocket)
- `http://localhost:5020/api/v1/*` (REST API)
- `http://localhost:5020/health` (Health Check)

**Gateway Üzerinden Erişim**:
- `https://localhost:5040/hub/ws/*` (SignalR WebSocket)
- `https://localhost:5040/hub/api/v1/*` (REST API)
- `https://localhost:5040/hub/health` (Health Check)

### Ocelot Route Configuration

Gateway'de (`MngGateway/Presentation/MngGateway.Api/ocelot.json`) MngHub için route tanımları:

```json
{
  "DownstreamPathTemplate": "/ws/{everything}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "mnghub",
      "Port": 5020
    }
  ],
  "UpstreamPathTemplate": "/hub/ws/{everything}",
  "UpstreamHttpMethod": [ "GET", "POST" ],
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer"
  },
  "Priority": 1
},
{
  "DownstreamPathTemplate": "/health",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "mnghub",
      "Port": 5020
    }
  ],
  "UpstreamPathTemplate": "/hub/health",
  "UpstreamHttpMethod": [ "GET" ],
  "Priority": 1
}
```

---

## Test

### Scalar API Reference

1. MngHub'i başlatın: `http://localhost:5020/scalar/v1`
2. Scalar UI'da gateway URL'i server olarak gösterilecek
3. API testleri gateway üzerinden yapılabilir

### PowerShell Test Scripti

Gateway üzerinden test için:
```powershell
.\scripts\tests\MngGateway\test-gateway-hub.ps1
```

### SignalR WebSocket Test

Gateway üzerinden SignalR bağlantısı:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5040/hub/ws/v1", {
        accessTokenFactory: () => token
    })
    .build();
```

---

## Özel Notlar

### SignalR WebSocket Routing

⚠️ **Önemli**: SignalR WebSocket bağlantıları için gateway routing'i özel dikkat gerektirir:

1. **WebSocket Upgrade**: Gateway'de WebSocket upgrade desteği olmalı
2. **Sticky Sessions**: WebSocket bağlantıları için load balancing'de sticky sessions gerekebilir
3. **Authentication**: Token query string veya header'dan geçirilmeli

### Health Check

Health check endpoint'i gateway üzerinden erişilebilir:
- `https://localhost:5040/hub/health`

---

## Faydaları

1. **Merkezi Yönetim**: Tüm API çağrıları gateway üzerinden yönetilebilir
2. **Scalar Entegrasyonu**: Scalar UI'da gateway URL'i seçilebilir
3. **Test Kolaylığı**: Gateway üzerinden direkt test edilebilir
4. **Production Hazırlığı**: Production'da gateway üzerinden erişim için hazır
5. **SignalR Gateway Support**: WebSocket bağlantıları gateway üzerinden yönetilebilir

---

## Notlar

- ⚠️ Development aşamasında hem direkt hem gateway erişimi mümkün
- ⚠️ Production'da port exposure kaldırılabilir (sadece gateway üzerinden erişim)
- ⚠️ SignalR WebSocket bağlantıları için gateway'de WebSocket upgrade desteği gerekli
- ⚠️ MngHub HTTP kullanıyor (HTTPS değil), gateway'de SSL termination yapılıyor

---

**Son Güncelleme**: 31 Aralık 2025  
**Durum**: ✅ Gateway entegrasyonu tamamlandı

