# Odak POC — HTTP ve API Gateway (LDAP dönemi)

**Durum:** Bilinçli POC kararı; **HTTPS/Nginx bu aşamada yok** (ileride ayrı faz).  
**Son güncelleme:** 23 Mayıs 2026

---

## Dış erişim: hepsi HTTP

| Bileşen | URL |
|---------|-----|
| MngUI | http://192.168.20.20:3000 |
| MngDomainUI | http://192.168.20.20:3001 |
| **API Gateway** | http://192.168.20.20:5040 |
| MngKeeper (doğrudan, debug/Scalar) | http://192.168.20.20:5001 |
| Keycloak | http://192.168.20.20:8080/keycloak |

`docker-compose.odak.yml`: `MngGatewaySettings__Server__Scheme=http`, Nginx **kapalı** ([MNG_COMMON_ODAK_MUSTERI_ERISIM.md](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) §2).

`https://192.168.20.20:5040` — TLS yok; kullanılmaz.

---

## MngKeeper gateway arkasında mı?

**Evet** (işlevsel). Ocelot (`MngGateway/.../ocelot.json`):

| Upstream (dış) | Downstream (iç) |
|----------------|-----------------|
| `/keeper/api/{everything}` | `http://mngkeeper:5001/api/{everything}` |

Doğrulama:

```http
GET  http://192.168.20.20:5040/keeper/api/version/short  → 200
POST http://192.168.20.20:5040/keeper/api/auth/token     → 200
GET  http://192.168.20.20:5040/api/auth/token            → 404 (yanlış yol)
```

**Mng.Ui** (`GATEWAY_URL=http://192.168.20.20:5040`): token ve API çağrıları `/keeper/api/...` üzerinden gider.

**POC istisnası:** Host’ta `5001` açık (Scalar, debug). Hedef üretimde yalnızca gateway + Nginx TLS — henüz Odak’ta uygulanmadı.

---

## Scalar / Swagger (sunucu)

| UI | URL | Not |
|----|-----|-----|
| Scalar | http://192.168.20.20:5001/scalar/v1 | `EnableSwagger=true`; Production’da da açık (v1.3.0+) |
| Swagger | http://192.168.20.20:5001/api-docs | |
| OpenAPI | http://192.168.20.20:5001/api-docs/v1/swagger.json | |

Scalar **Try it out:** Sunucu seçimi **`/`** veya **`http://192.168.20.20:5001`** (gateway kökü `5040` → 404).

Üretim benzeri API testi: `http://192.168.20.20:5040/keeper/...`

---

## İleride HTTPS (bu dokümanın kapsamı dışı)

1. mng_common Nginx + sertifika  
2. Dış `https://...` → Gateway  
3. İsteğe bağlı `5001` host portunu kapatma  
4. `docker-compose.production.yml` `Scheme=https` (şablon zaten var)

LDAP faz checklist: [DEVAM.md](./DEVAM.md)
