---
title: "Mng.Ui Gateway Integration"
category: "guides"
tags: ["ui", "gateway", "integration", "frontend"]
service: "Mng.Ui"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# Mng.Ui - Gateway Entegrasyonu

**Tarih**: 31 Aralık 2025  
**Versiyon**: 1.0.0  
**Durum**: ✅ Tamamlandı

## Genel Bakış

Mng.Ui frontend uygulaması, API Gateway (MngGateway) üzerinden backend servislere erişebilir hale getirildi. Bu değişiklik, merkezi yönetim, SSL termination, rate limiting ve güvenlik sağlar.

## Yapılan Değişiklikler

### 1. Environment Variables

**Dosya**: `nuxt.config.ts`

Yeni environment variable eklendi:
- `GATEWAY_URL`: API Gateway base URL'i (örn: `https://localhost:5040`)

Mevcut environment variable'lar güncellendi:
- `KEEPER_URL`: MngKeeper direkt URL'i (gateway kullanılmadığında)
- `DATAGATEWAY_URL` / `SERVER_URL`: MngDataGateway direkt URL'i (gateway kullanılmadığında)
- `HUB_URL`: MngHub direkt URL'i (gateway kullanılmadığında)

---

### 2. Runtime Configuration

**Dosya**: `nuxt.config.ts`

```typescript
runtimeConfig: {
  public: {
    // Gateway URL (if using API Gateway, set this and leave other URLs empty)
    gatewayUrl: process.env.GATEWAY_URL || '',
    // Individual service URLs (used if gatewayUrl is not set)
    keeperUrl: process.env.KEEPER_URL || 'https://localhost:5001',
    reactorUrl: process.env.SERVER_URL || process.env.DATAGATEWAY_URL || 'https://localhost:5010',
    hubUrl: process.env.HUB_URL || 'http://localhost:5020'
  }
}
```

**Mantık**:
- `GATEWAY_URL` tanımlı ise, tüm servisler gateway üzerinden erişilir
- `GATEWAY_URL` tanımlı değilse, her servis için direkt URL kullanılır

---

### 3. Server-Side API Routes

#### MngKeeper Proxy (`server/api/keeper/[...path].ts`)

**Değişiklik**: Gateway URL desteği eklendi

```typescript
const keeperUrl = config.public.gatewayUrl 
  ? `${config.public.gatewayUrl}/keeper`
  : (config.public.keeperUrl || 'https://localhost:5001');
```

**Endpoint Mapping**:
- Gateway: `{GATEWAY_URL}/keeper/api/*` → `{KEEPER_URL}/api/*`
- Direkt: `{KEEPER_URL}/api/*` → `{KEEPER_URL}/api/*`

#### Authentication Proxy (`server/api/auth/token.post.ts`)

**Değişiklik**: Gateway URL desteği eklendi

```typescript
const keeperUrl = config.public.gatewayUrl 
  ? `${config.public.gatewayUrl}/keeper`
  : (config.public.keeperUrl || 'https://localhost:5001');
```

**Endpoint Mapping**:
- Gateway: `{GATEWAY_URL}/keeper/api/auth/token` → `{KEEPER_URL}/api/auth/token`
- Direkt: `{KEEPER_URL}/api/auth/token` → `{KEEPER_URL}/api/auth/token`

---

### 4. Client-Side API Service

#### DataGateway Service (`services/apiService.ts`)

**Değişiklik**: Gateway URL desteği eklendi

```typescript
const baseUrl = config.public.gatewayUrl 
  ? `${config.public.gatewayUrl}/data`
  : (config.public.reactorUrl || 'https://localhost:5010');
```

**Endpoint Mapping**:
- Gateway: `{GATEWAY_URL}/data/api/*` → `{DATAGATEWAY_URL}/api/*`
- Direkt: `{DATAGATEWAY_URL}/api/*` → `{DATAGATEWAY_URL}/api/*`

---

### 5. SignalR Hub Connection

#### Events Page (`pages/apps/events/index.vue`)

**Değişiklik**: Gateway URL desteği eklendi

```typescript
const hubBaseUrl = config.public.gatewayUrl 
  ? `${config.public.gatewayUrl}/hub`
  : (config.public.hubUrl || 'http://localhost:5020');
const connectionUrl = `${hubBaseUrl}/ws?access_token=${encodeURIComponent(token)}`;
```

**Endpoint Mapping**:
- Gateway: `{GATEWAY_URL}/hub/ws` → `{HUB_URL}/ws`
- Direkt: `{HUB_URL}/ws` → `{HUB_URL}/ws`

---

## Kullanım

### Gateway Kullanımı (Önerilen - Production)

`.env` dosyasında:

```env
GATEWAY_URL=https://localhost:5040
```

Tüm servisler gateway üzerinden erişilir:
- Keeper: `https://localhost:5040/keeper/api/*`
- DataGateway: `https://localhost:5040/data/api/*`
- Hub: `https://localhost:5040/hub/ws`

---

### Direkt Servis Erişimi (Development)

`.env` dosyasında:

```env
# GATEWAY_URL tanımlamayın veya boş bırakın

KEEPER_URL=https://localhost:5001
DATAGATEWAY_URL=https://localhost:5010
HUB_URL=http://localhost:5020
```

Her servis direkt erişilir:
- Keeper: `https://localhost:5001/api/*`
- DataGateway: `https://localhost:5010/api/*`
- Hub: `http://localhost:5020/ws`

---

## Gateway Routing

### MngKeeper Routes

| Client Request | Gateway Route | Backend Service |
|----------------|---------------|-----------------|
| `/api/keeper/api/auth/token` | `/keeper/api/auth/token` | `{KEEPER_URL}/api/auth/token` |
| `/api/keeper/api/domain` | `/keeper/api/domain` | `{KEEPER_URL}/api/domain` |
| `/api/keeper/api/user` | `/keeper/api/user` | `{KEEPER_URL}/api/user` |
| `/api/keeper/api/group` | `/keeper/api/group` | `{KEEPER_URL}/api/group` |

### MngDataGateway Routes

| Client Request | Gateway Route | Backend Service |
|----------------|---------------|-----------------|
| `{baseUrl}/api/datasets` | `/data/api/datasets` | `{DATAGATEWAY_URL}/api/datasets` |
| `{baseUrl}/api/data/{name}` | `/data/api/data/{name}` | `{DATAGATEWAY_URL}/api/data/{name}` |

### MngHub Routes

| Client Request | Gateway Route | Backend Service |
|----------------|---------------|-----------------|
| WebSocket: `{hubBaseUrl}/ws` | `/hub/ws` | `{HUB_URL}/ws` |
| Health: `{hubBaseUrl}/health` | `/hub/health` | `{HUB_URL}/health` |

---

## Faydaları

1. **Merkezi Yönetim**: Tüm API çağrıları tek bir noktadan yönetilir
2. **SSL Termination**: Gateway'de SSL yönetimi yapılır
3. **Rate Limiting**: Merkezi rate limiting
4. **Güvenlik**: Backend servisler dışarıdan erişilemez (production'da)
5. **CORS Yönetimi**: Gateway'de merkezi CORS yönetimi
6. **Monitoring**: Tüm trafik gateway üzerinden geçer, monitoring kolaylaşır

---

## Migration Guide

### Development'tan Production'a Geçiş

1. **Gateway'i başlatın**:
   ```bash
   cd ApplicationResources/mng_apps
   docker-compose up -d mnggateway
   ```

2. **Environment Variable Güncellemesi**:
   ```env
   # .env dosyasında
   GATEWAY_URL=https://api.monitra.local
   # veya development için
   GATEWAY_URL=https://localhost:5040
   ```

3. **Test Edin**:
   - Login işlemi gateway üzerinden çalışmalı
   - API çağrıları gateway üzerinden yapılmalı
   - SignalR bağlantısı gateway üzerinden kurulmalı

---

## Notlar

- ⚠️ Gateway kullanıldığında, direkt servis URL'leri göz ardı edilir
- ⚠️ Development için direkt erişim de mümkündür (gateway tanımlanmazsa)
- ⚠️ SignalR WebSocket bağlantıları için gateway'de WebSocket upgrade desteği gerekli
- ⚠️ Production'da backend servislerin port'ları kapatılabilir (sadece gateway üzerinden erişim)

---

**Son Güncelleme**: 31 Aralık 2025  
**Durum**: ✅ Gateway entegrasyonu tamamlandı

