# Odak SIEM — dokümantasyon

Canlı durum ve sıradaki adımlar: **[current_status.md](./current_status.md)**

| Konu | Doküman | Route |
|------|---------|--------|
| Keşif ve kapsam (Coverage) | [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md) | `/apps/siem-center/discovery` |
| Host Analytics (Windows + Linux) | [HOST_ANALYTICS.md](./HOST_ANALYTICS.md) | `/apps/siem-center/hosts/[hostname]` |
| Event Log paketleri | [mnglogs/POLICY_EVENTLOG_PACKAGES.md](./mnglogs/POLICY_EVENTLOG_PACKAGES.md) | Settings → Katalog |

**Park (dönülecek):** Host Analytics L3 / genel Analytics dönüşü; ajansız host aksiyonları — `HOST_ANALYTICS.md`, `DISCOVERY_COVERAGE.md`, `current_status.md`.

## Ürün ayrımı (G3)

| Klasör | Rol |
|--------|-----|
| `MngLogCollector/` | Sunucu ingest API → OpenSearch (`mnglogcollector`, port **5091**) |
| `MngLogs/` | Saha Windows ajanı + Nuxt yerel UI (`127.0.0.1:5092`) |

## Geçiş fazları (özet)

| Faz | Durum |
|-----|--------|
| G0 OpenSearch | ✓ |
| G1 Dual-write (Reactor) | ✓ |
| G2 UI → OpenSearch okuma | ✓ |
| G3 Collector + MngLogs ajan + service watch + Edge UI | ✓ |
| **MngLogs Faz 1 metrik toplama** | ✓ (30 Tem) — host + top-process ship |
| G4 Cutover | Bekliyor |

### Metrik özeti (Faz 1)

`host.up` · CPU/bellek/disk · `process.top_cpu` / `process.top_memory` (özet, flood yok).  
Detay: `current_status.md` → **MngLogs Faz 1 metrik toplama**.

Geçici OS test UI: `scripts/tests/MngLogs/os-test/start-os-test-dashboard.ps1`.

Detay ve onay kuyruğu `current_status.md` içindedir.
