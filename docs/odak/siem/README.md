# Odak SIEM — dokümantasyon

Canlı durum ve sıradaki adımlar: **[current_status.md](./current_status.md)**

**Host paneli (Windows Host Analytics):** [HOST_ANALYTICS.md](./HOST_ANALYTICS.md) · route `/apps/siem-center/hosts/[hostname]`

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
