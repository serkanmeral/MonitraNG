# MngDataGateway - Gateway Entegrasyonu

**Tarih**: 31 Aralık 2025  
**Versiyon**: 1.0.0  
**Durum**: ✅ Tamamlandı

## Genel Bakış

MngDataGateway servisi, API Gateway (MngGateway) üzerinden erişilebilir hale getirildi. Bu değişiklikler, Swagger/OpenAPI dokümantasyonunda gateway URL'ini göstermek ve API Gateway üzerinden test edilebilirliği sağlamak için yapıldı.

## Yapılan Değişiklikler

### 1. OpenAPI Server Path Güncellemesi

**Dosya**: `Presentation/MngDataGateway.Api/appsettings.json`

```json
{
  "MngDataGatewaySettings": {
    "OpenApiServerPath": "https://api.monitra.local/data"
  }
}
```

**Önceki Değer**: `https://localhost:5010`  
**Yeni Değer**: `https://api.monitra.local/data`

**Açıklama**: Swagger UI ve Scalar API Reference'da gösterilecek server URL'i gateway URL'ine güncellendi.

---

### 2. SwaggerConfigureOptions Güncellemesi

**Dosya**: `Presentation/MngDataGateway.Api/Config/SwaggerConfigureOptions.cs`

**Değişiklikler**:
- `IConfiguration` dependency injection eklendi
- `Configure` metodunda `OpenApiServerPath` configuration'dan okunuyor
- Server URL OpenAPI dokümantasyonuna ekleniyor

**Kod Örneği**:
```csharp
public class SwaggerConfigureOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly IConfiguration _configuration;

    public SwaggerConfigureOptions(IApiVersionDescriptionProvider provider, IConfiguration configuration)
    {
        _provider = provider;
        _configuration = configuration;
    }

    public void Configure(SwaggerGenOptions options)
    {
        // ... existing code ...

        // Configure Server URL from settings (for API Gateway)
        var openApiServerPath = _configuration["MngDataGatewaySettings:OpenApiServerPath"];
        if (!string.IsNullOrEmpty(openApiServerPath))
        {
            options.AddServer(new OpenApiServer
            {
                Url = openApiServerPath,
                Description = "API Gateway Server"
            });
        }
    }
}
```

---

## Gateway Routing

### Endpoint Yapısı

**Direkt Erişim** (Development):
- `https://localhost:5010/api/v1/*`
- `https://localhost:5010/api/*`

**Gateway Üzerinden Erişim**:
- `https://localhost:5040/data/api/v1/*`
- `https://localhost:5040/data/api/*`

### Ocelot Route Configuration

Gateway'de (`MngGateway/Presentation/MngGateway.Api/ocelot.json`) MngDataGateway için route tanımları:

```json
{
  "DownstreamPathTemplate": "/api/v1/{everything}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    {
      "Host": "mngdatagateway",
      "Port": 5010
    }
  ],
  "UpstreamPathTemplate": "/data/api/v1/{everything}",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ],
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer"
  },
  "RateLimitOptions": {
    "EnableRateLimiting": true,
    "Period": "1m",
    "Limit": 100
  },
  "Priority": 1
}
```

---

## Test

### Swagger UI

1. MngDataGateway'i başlatın: `https://localhost:5010/swagger`
2. Swagger UI'da "Servers" dropdown'ında gateway URL'i görünecek: `https://api.monitra.local/data`
3. Gateway URL'i seçildiğinde, tüm API çağrıları gateway üzerinden yapılacak

### Scalar API Reference

1. MngDataGateway'i başlatın: `https://localhost:5010/scalar/v1`
2. Scalar UI'da gateway URL'i server olarak gösterilecek
3. API testleri gateway üzerinden yapılabilir

### PowerShell Test Scripti

Gateway üzerinden test için:
```powershell
.\scripts\tests\MngGateway\test-gateway-datagateway.ps1
```

---

## Faydaları

1. **Merkezi Yönetim**: Tüm API çağrıları gateway üzerinden yönetilebilir
2. **Swagger Entegrasyonu**: Swagger UI'da gateway URL'i seçilebilir
3. **Test Kolaylığı**: Gateway üzerinden direkt test edilebilir
4. **Production Hazırlığı**: Production'da gateway üzerinden erişim için hazır

---

## Notlar

- ⚠️ Development aşamasında hem direkt hem gateway erişimi mümkün
- ⚠️ Production'da port exposure kaldırılabilir (sadece gateway üzerinden erişim)
- ⚠️ Gateway'de authentication yapılıyor, MngDataGateway'de de JWT validation var

---

**Son Güncelleme**: 31 Aralık 2025  
**Durum**: ✅ Gateway entegrasyonu tamamlandı

