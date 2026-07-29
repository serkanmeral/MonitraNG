# Odak SIEM — dokümantasyon

Canlı durum ve sıradaki adımlar: **[current_status.md](./current_status.md)**

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
| G4 Cutover | Bekliyor |

Detay ve onay kuyruğu `current_status.md` içindedir.
