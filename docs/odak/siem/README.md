# Odak SIEM — dokümantasyon

Canlı durum ve sıradaki adımlar: **[current_status.md](./current_status.md)**

| Konu | Doküman | Route |
|------|---------|--------|
| Host telemetry cutover (agent-only) | [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md) | — |
| Keşif ve kapsam (Coverage) | [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md) | `/apps/siem-center/discovery` |
| Host Analytics (Windows + Linux) | [HOST_ANALYTICS.md](./HOST_ANALYTICS.md) | `/apps/siem-center/hosts/[hostname]` |
| Event Log paketleri | [mnglogs/POLICY_EVENTLOG_PACKAGES.md](./mnglogs/POLICY_EVENTLOG_PACKAGES.md) | Settings → Event Log → Paket kataloğu |
| Parse kuralları (P5) | [PARSE_RULES_CATALOG.md](./PARSE_RULES_CATALOG.md) | Settings → Event Log → Parse kuralları (`?tab=eventlog&section=parsers`) |
| Güvenlik olayları / filtre kataloğu | [../monitoring/SIEM_EVENTS_UI.md](../monitoring/SIEM_EVENTS_UI.md) | `/apps/siem-center/events` |

**Park (dönülecek):** Host Analytics L3 / genel Analytics dönüşü; ajansız host aksiyonları — `HOST_ANALYTICS.md`, `DISCOVERY_COVERAGE.md`, `current_status.md`.  
**P5:** Parse Rules Dilím 0–5 ✅ (spec + API + sihirbazlar + seed) — [PARSE_RULES_CATALOG.md](./PARSE_RULES_CATALOG.md).

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
| **Host NXLog/rsyslog cutover** | ✓ (3 Ağu) — agent-only; FortiGate syslog kaldı — [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md) |
| G4 Cutover (alarm/Mongo köprü) | Kısmi / park |

### Metrik özeti (Faz 1)

`host.up` · CPU/bellek/disk · `process.top_cpu` / `process.top_memory` (özet, flood yok).  
Detay: `current_status.md` → **MngLogs Faz 1 metrik toplama**.

Geçici OS test UI: `scripts/tests/MngLogs/os-test/start-os-test-dashboard.ps1`.

Detay ve onay kuyruğu `current_status.md` içindedir.
