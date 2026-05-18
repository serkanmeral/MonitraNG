# MngReactor Test Scriptleri

MngReactor API'sini manuel/script tabanli test etmek icin PowerShell scriptleri.

## Onkosullar

- **MngReactor** calisiyor (varsayilan: `http://localhost:15010`)
- **MngKeeper** calisiyor (token icin)
- **DataGateway** (Monitoring CRUD, Ingest icin)
- Test domain (ornegin `meral`) ve kullanici mevcut
- Test verisi icin: `setup-monitoring-datasets.ps1` (DG) ve `seed-monitoring-test-data.ps1`

## Token

Scriptler `auth/load-token.ps1` veya `auth/get-token.ps1` kullanir. Bu scriptler MngDataGateway auth'u kullanir (MngKeeper uzerinden token alir).

Varsayilan: `KeeperBaseUrl=https://localhost:5040`, `Domain=meral`, `Username=meral_admin`, `Password=Admin123!`

## Scriptler

| Script | Aciklama |
|--------|----------|
| `seed-monitoring-test-data.ps1` | DG uzerinden test verisi olusturur (Engine, Agent, Asset, vb.) |
| `test-health.ps1` | Health, live, ready endpoint'leri |
| `test-config-sync.ps1` | Engine config ve config-string |
| `test-ingest.ps1` | POST /api/v1/ingest/metrics - metrik batch gonderimi |
| `test-domain-init.ps1` | POST /api/v1/admin/domain/{domain}/init - mon_schedules, mon_collection_periods |
| `test-monitoring-crud.ps1` | Engine, Agent, Asset CRUD |

## Kullanim

```powershell
# Health test (auth gerektirmez)
.\test-health.ps1

# Farkli port
.\test-health.ps1 -BaseUrl "http://localhost:15010"

# Config sync (token gerekli)
.\test-config-sync.ps1

# Belirli engineId ile
.\test-config-sync.ps1 -EngineId "my-engine-id"

# Test verisi olustur (once setup-monitoring-datasets.ps1 calistir)
.\seed-monitoring-test-data.ps1

# Monitoring CRUD (token gerekli)
.\test-monitoring-crud.ps1

# Ingest (Engine, Agent, Asset gerekli - seed script ile)
.\test-ingest.ps1

# Domain init (mon_schedules, mon_collection_periods)
.\test-domain-init.ps1 -Domain meral
```

## CI / Pipeline

```powershell
cd scripts/tests/MngReactor
.\test-health.ps1
if ($LASTEXITCODE -ne 0) { exit 1 }
```

## Iliskili Dokumanlar

- [MNGREACTOR_TEST_PLAN.md](../../../docs/content/monitoring_plans/MNGREACTOR_TEST_PLAN.md)
- [MONITORING_IMPLEMENTATION_PLAN.md](../../../docs/content/monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md)
