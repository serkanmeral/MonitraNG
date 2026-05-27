# MngOperations — Gateway ve Odak deploy

**Son güncelleme:** 26 Mayıs 2026

---

## 1. Gateway routing (öneri)

MngWorkflow / MngScheduler ile paralel:

| Upstream (gateway) | Downstream (MO) |
|--------------------|-----------------|
| `/operations/api/v1/{everything}` | `http://mngoperations:5086/api/v1/{everything}` |
| `/operations/api/{everything}` | `http://mngoperations:5086/api/{everything}` (fallback) |

**Örnek çağrı (Odak):**

```http
POST http://192.168.20.20:5040/operations/api/v1/work-items
Authorization: Bearer {token}
```

`ocelot.json` route eklendi — [MngGateway/ocelot.json](../../../../MngGateway/Presentation/MngGateway.Api/ocelot.json) (`/operations/api/v1`, swagger).

---

## 2. Port

| Ortam | Port | Not |
|-------|------|-----|
| **Container / host (karar)** | **5086** | 26 May 2026 onaylandı (Q1) |
| Host (Odak) | 5086 | `docker-compose.odak.yml` expose |

---

## 3. Docker / compose

**Dosyalar:** `ApplicationResources/mng_apps/docker-compose.yml` (+ `docker-compose.odak.yml`, `docker-compose.production.yml`)  
**Dockerfile:** [MngOperations.Api/Dockerfile](../../../../MngOperations/Presentation/MngOperations.Api/Dockerfile)

```yaml
mngoperations:
  build: ../../MngOperations
  dockerfile: Presentation/MngOperations.Api/Dockerfile
  ports:
    - "5086:5086"
  environment:
    - MngOperationsSettings__Server__Port=5086
    - MngOperationsSettings__DataGateway__BaseUrl=http://mngdatagateway:5010
    - MngOperationsSettings__MngNotifiers__BaseUrl=http://mngnotifier:5070
    - MngOperationsSettings__RabbitMq__Host=rabbitmq
    - Jwt__Authority=http://keycloak:8080/keycloak/realms/monitra  # Odak overlay
  networks:
    - mng_common_mng_network
```

Development override (host IP):

```bash
MngOperationsSettings__Actors__MngKeeper=http://192.168.20.20:5001
Jwt__Authority=http://192.168.20.20:8080/keycloak/realms/odak
MngOperationsSettings__DataGateway__BaseUrl=http://192.168.20.20:5010
```

Örnek: [appsettings.Development.example.json](../../../../MngOperations/Presentation/MngOperations.Api/appsettings.Development.example.json)

Gateway env:

```text
MngGatewaySettings__BackendServices__MngOperations=http://mngoperations:5086
```

---

## 4. Odak checklist (deploy zamanı — Q12)

**Sıra:** MngOperations + gateway **önce**; OC UI **sonra** (API smoke ve seed workspace MO ile doğrulanır).

1. OC dataset’ler kurulu ([scripts](../scripts/README.md)); mevcut kurulumda `publish_mode` → **`none`** ([patch-op-publish-mode-none.ps1](../scripts/patch-op-publish-mode-none.ps1))
2. `mngoperations` image build + compose up
3. `ocelot.json` operations route
4. Seq: `Application = 'MngOperations.Api'`
5. Smoke: token → `GET /operations/api/v1/health` → (opsiyonel) `seed-*.ps1` demo veri
6. OC UI route / menü (ayrı plan)

---

## 5. appsettings.Development.example.json

Odak Seq URL: `http://192.168.20.20:5341` (Scheduler örneği ile aynı).

DataGateway:

```json
"DataGateway": {
  "BaseUrl": "http://192.168.20.20:5010",
  "ApiVersion": "v1"
}
```

Geliştirme PC’den doğrudan MO portu veya yalnızca gateway üzerinden test.

---

## 6. İlgili dokümanlar

- [MNG_APPS_ODAK.md](../../setup/MNG_APPS_ODAK.md) — mevcut servis portları
- [ODAK_FULL_SETUP.md](../../ODAK_FULL_SETUP.md)
