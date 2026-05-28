# MngOperations — Kimlik, token ve konfigürasyon

**Son güncelleme:** 28 Mayıs 2026  
**Kararlar:** MO → DG doğrudan + Bearer forward (Q2); **MngKeeper / Keycloak** adresleri appsettings + env (token kaynağı ve doğrulama).

---

## 1. İstek akışı

```text
Client (UI / modül)
  Authorization: Bearer {access_token}    ← MngKeeper / Keycloak (POST /keeper/api/auth/token)
       ↓
API Gateway  /operations/api/v1/...
       ↓
MngOperations.Api
  → JWT doğrulama (Jwt:Authority / Keycloak JWKS — appsettings)
  → IRequestContext (claim parse — permission, actor)
  → IMngDataGatewayClient
       Authorization: Bearer {aynı token}   ← otomatik forward
       ↓
MngDataGateway  /api/v1/data/...
  → domain DB seçimi, permission, audit (token claim’leri)
```

**Token kaynağı:** Oturum açma **MngKeeper** üzerinden; access token pratikte **Keycloak realm** JWT’sidir (`iss` örn. `…/realms/odak`). MO yapılandırmasında hem **Keeper** hem **Keycloak tabanı** tanımlanır.

MO, Faz 1’de DG’ye **servis hesabı token’ı kullanmaz**. Her operasyonel işlem **çağıran kullanıcının** token’ı ile yürür.

---

## 2. MngDataGateway `BaseUrl` (Q2)

| Ortam | Örnek `DataGateway:BaseUrl` | Not |
|--------|-----------------------------|-----|
| **Docker / Production (compose)** | `http://mngdatagateway:5010` | Servisler arası doğrudan |
| **Odak Development (host IP)** | `http://192.168.20.20:5010` | Geliştirme PC → sunucu DG portu |
| **Yerel tüm stack** | `http://localhost:5010` | İsteğe bağlı |

**UI → MO** yine gateway üzerinden (`http://192.168.20.20:5040/operations/...`). Yalnızca **MO → DG** bu `BaseUrl` ile doğrudan gider.

### 2.1 appsettings

```json
{
  "MngOperationsSettings": {
    "Server": { "Port": 5086 },
    "DataGateway": {
      "BaseUrl": "http://mngdatagateway:5010",
      "ApiVersion": "v1"
    }
  }
}
```

HttpClient tam adres: `{BaseUrl}/api/{ApiVersion}/` (Scheduler ile aynı).

### 2.2 Environment değişkenleri (compose / Odak)

ASP.NET Core convention (`__`):

```bash
MngOperationsSettings__DataGateway__BaseUrl=http://mngdatagateway:5010
MngOperationsSettings__DataGateway__ApiVersion=v1
MngOperationsSettings__Server__Port=5086
```

Development override örneği (sunucuda debug veya PC’den MO’ya doğrudan):

```bash
MngOperationsSettings__DataGateway__BaseUrl=http://192.168.20.20:5010
```

Production `docker-compose.production.yml` içinde `${MNGDATAGATEWAY_URL:-http://mngdatagateway:5010}` pattern’i (Scheduler ile hizalı).

---

## 3. MngKeeper ve JWT doğrulama

Token **üretimi** MngKeeper API’sindedir; MO’nun erişilemeyen istekleri reddetmesi için **JWT doğrulama** yapılandırması gerekir (DG / MngScheduler ile aynı aile).

### 3.1 İki adres türü

| Ayar | Amaç | Docker (iç) | Odak Development (host) |
|------|------|-------------|-------------------------|
| **`Actors.MngKeeper`** | Keeper API (auth, ileride refresh/introspection) | `http://mngkeeper:5001` | `http://192.168.20.20:5001` |
| **`Jwt:KeycloakBaseUrl`** | Realm / JWKS kökü (imza doğrulama) | `http://keycloak:8080/keycloak` | `http://192.168.20.20:8080/keycloak` |
| **`Jwt:Authority`** | `AddJwtBearer` Authority (sabit realm) | `http://keycloak:8080/keycloak/realms/odak` | `http://192.168.20.20:8080/keycloak/realms/odak` |

**Not:** Çok domain’de realm = `domain_name` (token `iss` / `domain_name`). Faz 1 Odak tek domain **`odak`** → sabit `Jwt:Authority` yeterli. İleride `Jwt:UseRealmFromToken` veya `iss`’ten realm çözümü.

Gateway üzerinden Keeper (UI pattern): `http://192.168.20.20:5040/keeper` — MO container içinden **doğrudan `mngkeeper:5001`** tercih edilir (gateway döngüsü yok).

### 3.2 appsettings (tam örnek)

```json
{
  "MngOperationsSettings": {
    "Server": { "Port": 5086 },
    "Actors": {
      "MngKeeper": "http://mngkeeper:5001",
      "KeycloakBaseUrl": "http://keycloak:8080/keycloak"
    },
    "DataGateway": {
      "BaseUrl": "http://mngdatagateway:5010",
      "ApiVersion": "v1"
    },
    "MngNotifiers": { "BaseUrl": "http://mngnotifier:5070" }
  },
  "Jwt": {
    "Authority": "http://keycloak:8080/keycloak/realms/odak",
    "RequireHttpsMetadata": false
  }
}
```

Örnek dosya: [appsettings.Development.example.json](../../../../MngOperations/Presentation/MngOperations.Api/appsettings.Development.example.json).

### 3.3 Environment değişkenleri

```bash
# Keeper
MngOperationsSettings__Actors__MngKeeper=http://mngkeeper:5001

# Keycloak / JWT (Odak domain odak)
MngOperationsSettings__Actors__KeycloakBaseUrl=http://keycloak:8080/keycloak
Jwt__Authority=http://keycloak:8080/keycloak/realms/odak
Jwt__RequireHttpsMetadata=false

# DG (Q2)
MngOperationsSettings__DataGateway__BaseUrl=http://mngdatagateway:5010
MngOperationsSettings__Server__Port=5086
```

Development (host IP):

```bash
MngOperationsSettings__Actors__MngKeeper=http://192.168.20.20:5001
MngOperationsSettings__Actors__KeycloakBaseUrl=http://192.168.20.20:8080/keycloak
Jwt__Authority=http://192.168.20.20:8080/keycloak/realms/odak
MngOperationsSettings__DataGateway__BaseUrl=http://192.168.20.20:5010
```

### 3.4 Doğrulama implementasyonu (öneri)

Platform hizası ([MngDataGateway JwtBearer](../../../../MngDataGateway/Presentation/MngDataGateway.Api/Config/Extentions.cs), [MngScheduler](../../../../MngScheduler/Presentation/MngScheduler.Api/Config/Extensions.cs)):

1. `AddAuthentication().AddJwtBearer` — `options.Authority = configuration["Jwt:Authority"]` (boşsa Faz 1 dev’de gateway-only senaryosu dokümante edilir).
2. Multi-tenant üretimde: `Authority` = `{KeycloakBaseUrl}/realms/{domain_name}` (token’dan).
3. MO yine **parse** ile `IRequestContext` doldurur; doğrulama başarısız → **401** (DG’ye gitmeden).
4. Başarılı istekte token **değişmeden** DG’ye forward ([§4](#4-bearer-token-forward)).

**İsteğe bağlı:** `Actors.MngKeeper` üzerinden token introspection / refresh — Faz 1 zorunlu değil; adres şimdiden env’de tanımlı.

---

## 4. Bearer token forward

`IMngDataGatewayClient` her HTTP isteğinde:

```http
Authorization: Bearer {incomingAccessToken}
```

- Token, `HttpContext`’ten alınır (`IHttpContextAccessor` veya scoped `IRequestContext`).
- Pipeline / background job **kullanıcı token’ı olmadan** DG yazmamalı (Faz 1).
- Token süresi dolduysa DG 401 → MO 401 propagate.

**DelegatingHandler** önerisi: `BearerTokenForwardingHandler` — tüm named HttpClient’lara (`MngDataGateway`, `MngNotifiers`) bağlanabilir; Notifier da tenant bağlamı gerektiriyorsa aynı token.

---

## 5. MO tarafında JWT parse (`IRequestContext`)

MO iş kuralları için claim’leri okur; DG’ye ayrıca “kullanıcı adı header’ı” göndermeye gerek yok — DG zaten JWT’den çözer.

| Claim (örnek) | MO kullanımı | DG kullanımı |
|---------------|--------------|--------------|
| `preferred_username` | Actor, assignee, activity | `__createdBy`, audit |
| `domain_id` | Tenant scope, cache key | Mongo domain DB |
| `domain_name` | Log, event payload | DB adı / realm |
| `user_groups` / `groups` | Group-first permission | Dataset permission |
| `isAdmin` / `is_manager` | Kısıtlı admin (DG: `isAdmin` claim) | — |
| `mng_person_id` | İleride person relation | — |

Parse: `JwtSecurityTokenHandler` veya ASP.NET `User` principal (`AddJwtBearer` sonrası `HttpContext.User`).

**Stateless:** Session yok; her istekte token yeniden parse (veya middleware’de bir kez `IRequestContext` doldurulur).

---

## 6. Multi-tenant disiplin

1. MO, workspace/work item sorgularında **yalnızca token’daki domain** verisine güvenir.
2. İstek gövdesinde “başka domain id” gönderilirse reddet (Faz 1 validation).
3. Metadata cache anahtarı: `{domainId}:...` ([DG_INTEGRATION](./DG_INTEGRATION.md)).
4. RabbitMQ event payload’ında `domainId` zorunlu ([INTEGRATIONS](./INTEGRATIONS.md)).

---

## 7. MO API kimlik doğrulama

- MO endpoint’leri **Bearer zorunlu** (`[Authorize]`; `/health`, `/version` isteğe bağlı anonim).
- **Öneri:** MO kendi `Jwt:Authority` ile doğrular (gateway’e ek savunma); gateway zaten doğruluyorsa bile downstream MO tek başına expose edildiğinde güvenli kalır.
- Doğrulama başarılı → `IRequestContext` + DG forward.

### 7.1 İstisna — MngScheduler → MO (zamanlanmış work item)

Faz 1 genel kuralı «MO, çağıran kullanıcının token’ı ile DG’ye gider» ([§1](#1-istek-akışı)). **MngScheduler** tetik anında kullanıcı oturumu taşımaz; bu yüzden:

1. Scheduler (veya MO execute proxy) **MngKeeper** `POST /api/auth/token` ile teknik kullanıcı adı/şifre → `access_token` alır.
2. Aynı token ile `POST /operations/api/v1/work-items/from-origin` çağrılır.
3. MO bu isteği normal JWT gibi doğrular; DG’ye **aynı Bearer** forward edilir.

Kimlik bilgileri **MngScheduler secret config**’te; UI schedule kaydında değil. Detay: [SCHEDULED_WORK_ITEMS.md §4.1](./SCHEDULED_WORK_ITEMS.md).

---

## 8. Referans kod

| Konu | Repo |
|------|------|
| DG client + token param | [MngDataGatewayClient](../../../../MngScheduler/Infrastructure/MngScheduler.Infrastructure/Clients/MngDataGatewayClient.cs) |
| DG `domain_id`, `user_groups` | [MongoContextService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/MongoContextService.cs), [PermissionService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/PermissionService.cs) |
| Compose env | [docker-compose.yml](../../../../ApplicationResources/mng_apps/docker-compose.yml) |
| Odak gateway JWT | [docker-compose.odak.yml](../../../../ApplicationResources/mng_apps/docker-compose.odak.yml) `MngGatewaySettings__Jwt__Authority` |
| Keeper HTTP | [ODAK_HTTP_AND_GATEWAY.md](../../ldap/ODAK_HTTP_AND_GATEWAY.md) |
