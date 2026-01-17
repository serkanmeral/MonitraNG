---
title: "MngKeeper Gateway Integration"
category: "guides"
tags: ["keeper", "gateway", "integration", "routing"]
service: "MngKeeper"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# MngKeeper - Gateway Entegrasyonu

Bu dokümantasyon, MngKeeper'ın MngGateway ile entegrasyonunu açıklar.

## Genel Bakış

MngKeeper artık MngGateway üzerinden erişilebilir. Tüm API endpoint'leri gateway üzerinden `/keeper/api/*` path'i ile erişilebilir.

## Gateway URL Yapılandırması

### Development
- **Gateway URL**: `http://localhost:5040`
- **MngKeeper Base URL**: `http://localhost:5040/keeper`
- **API Endpoints**: `http://localhost:5040/keeper/api/*`

### Production
- **Gateway URL**: `https://api.monitra.local`
- **MngKeeper Base URL**: `https://api.monitra.local/keeper`
- **API Endpoints**: `https://api.monitra.local/keeper/api/*`

## OpenAPI Server Path

MngKeeper'ın Swagger dokümantasyonunda gösterilen server URL'i gateway URL'ini kullanır:

- **Development**: `https://api.monitra.local/keeper` (appsettings.json)
- **Production**: Environment variable ile yapılandırılabilir

## Endpoint Yapısı

### Authentication Endpoints

**Not**: Şu anda authentication endpoint'leri gateway üzerinden çalışmıyor. Token almak için direkt MngKeeper'a bağlanın:

```powershell
# Token alma (direkt MngKeeper)
$token = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        username = "serkan.meral"
        password = "Serkan123!"
        domain = "meral"
    } | ConvertTo-Json) `
    -SkipCertificateCheck
```

### Diğer Endpoints

Tüm diğer endpoint'ler gateway üzerinden erişilebilir:

```powershell
# Gateway üzerinden domain listesi
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$domains = Invoke-RestMethod -Uri "http://localhost:5040/keeper/api/domain" `
    -Method GET `
    -Headers $headers `
    -SkipCertificateCheck
```

## Gateway Routing

Gateway'de MngKeeper için aşağıdaki routing yapılandırması mevcuttur:

### Auth Endpoints (Priority: 2)
- **Upstream**: `/keeper/api/auth/*`
- **Downstream**: `http://mngkeeper:5001/api/auth/*`
- **Authentication**: Gerekli değil
- **Rate Limit**: 30 requests/minute

### Diğer Endpoints (Priority: 1)
- **Upstream**: `/keeper/api/*`
- **Downstream**: `http://mngkeeper:5001/api/*`
- **Authentication**: Bearer token gerekli
- **Rate Limit**: 100 requests/minute

## Test Scriptleri

Gateway üzerinden MngKeeper'ı test etmek için:

```powershell
cd scripts/tests/MngGateway
.\test-gateway-keeper.ps1
```

## Yapılan Değişiklikler

### 1. OpenAPI Server Path Güncellemesi
- `appsettings.json` içinde `OpenApiServerPath` güncellendi: `https://api.monitra.local/keeper`
- Swagger dokümantasyonunda gateway URL'i gösterilir

### 2. SwaggerConfiguration Güncellemesi
- `SwaggerConfiguration.cs` içinde `AddSwaggerConfiguration` metoduna `openApiServerPath` parametresi eklendi
- Server URL'i dinamik olarak yapılandırılabilir

### 3. Extensions.cs Güncellemesi
- `InitOpenApi` metodunda `OpenApiServerPath` configuration'dan okunuyor
- SwaggerConfiguration'a parametre olarak geçiliyor

## Gelecek İyileştirmeler

1. **Auth Endpoint Gateway Desteği**: Authentication endpoint'lerinin gateway üzerinden çalışması
2. **CORS Basitleştirme**: Gateway'de CORS yönetildiği için MngKeeper'daki CORS yapılandırması basitleştirilebilir
3. **JWT Validation Optimizasyonu**: Gateway'de validation yapıldığı için MngKeeper'daki validation basitleştirilebilir
4. **Port Exposure Kaldırma**: Backend servislerin dışarıdan erişilebilirliği kaldırılabilir (sadece internal network)

## Notlar

- Gateway üzerinden authentication endpoint'leri şu anda çalışmıyor
- Token almak için direkt MngKeeper'a bağlanın (`https://localhost:5001`)
- Diğer tüm endpoint'ler gateway üzerinden çalışıyor
- Gateway'de rate limiting aktif (auth: 30/min, diğer: 100/min)

