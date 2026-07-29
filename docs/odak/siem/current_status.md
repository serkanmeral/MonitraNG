# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 30 Temmuz 2026 (MngLogs Faz 1 metrik toplama tamam)  
**Ortam notu:** Odak production `odak@192.168.20.8`; merkezi Mng.Ui local `npm run dev`; UI prod deploy sadece istekte.  
**Canlı pilot:** `MngLogs.Agent` → collector `http://192.168.20.8:5091`; **Türkçe Nuxt UI** `http://127.0.0.1:5092/`; hostId=`TERMINAL-pilot`. Event Log: admin bekleniyor.

## Çalışma kuralı (bu oturumdan itibaren)

Her implementasyon adımından **önce**:
1. Ne yapılacağı (kısa kapsam)
2. Ne kazanılacağı (somut fayda)
3. Kullanıcı onayı → sonra kod / değişiklik

Onaysız büyük adım yok. Bu dosya **yapılan / yapılacak** listesinin güncel kaynağıdır.

---

## Son çalışılan konu

**MngLogs Faz 1 metrik toplama** ✓ (Durum UI + host metrikleri + üst süreç ship).  
Önceki: Yol **A** G0–G3 + Nuxt Edge UI ✓.

---

## Yapılanlar

### Planlama — Master (kilitli)

- Ürün: siber güvenlik suite; SIEM ana sütun
- Faz 1 = LogAlarm parity + TRTEST tasarım + OC/Workflow
- Faz 2 = SIEM dışı dikeyler; Faz 3 = lisans SKU + managed hizmet
- Depo: OpenSearch (ham); Mongo (alarm + suite); on-prem/air-gap; cloud yok

### Planlama — Alt planlar 1–11 (üst seviye tamam)

| # | Konu | Özet kilit |
|---|------|------------|
| 1 | MngLogs | Event Log paketleri; metrik=up; servis watch; HTTPS collector; Win/Linux |
| 2 | Network discovery | AD/DHCP + sınırlı tarama; coverage = metrik |
| 3 | OpenSearch | mng_common tek node; domain’li indeks; ILM 90/30g; REST aynı; MngAdmin snapshot; Dashboards opsiyonel |
| 4 | Normalize / map | Sunucu katalog; action sözlüğü; raw+rawPreview; FW aynı hat; geçiş Reactor → collector |
| 5 | Detect | MngAlarm; observation; siem-mvp-v1; OC/Workflow; çift alarm yok |
| 6–11 | UI, rsyslog, TRTEST, geçiş, lisans, managed | üst seviye kilitli |

### G0 — OpenSearch (mng_common) ✓

- Compose + ISM/template; Odak `http://192.168.20.8:9200`

### G1 — Dual-write ✓

- Reactor Mongo insert sonrası OpenSearch `_bulk`; `_id` = Mongo ObjectId

### G2 — OpenSearch read ✓

- `SecEvents:OpenSearchReadEnabled=true` Odak; UI aynı REST

### Standartlar ✓

MngLogs golden path:

| Klasör | Rol |
|--------|-----|
| **`MngLogCollector/`** | Sunucu backend (Domain…Api), Docker `mnglogcollector`:5091 |
| **`MngLogs/`** | Saha ajanı only (`MngLogs.Agent`) — sunucu stack’ten bağımsız |

### G3 dilim 1 — Merkez collector ✓ (29 Tem)

**Ne yapıldı**

- `MngLogCollector.Api` (eski ad: MngLogs.Api) → OpenSearch
- Compose servisi: **`mnglogcollector`** port **5091**
- API key: `X-MngLogs-ApiKey` (`MngLogCollectorSettings__Ingest__ApiKey`)

| Parça | Nerede | Durum |
|-------|--------|--------|
| Collector API (`mnglogcollector`) | Sunucu | ✓ |
| Saha ajanı (`MngLogs.Agent`) | Müşteri Windows | Dilim 2–3 ✓ |

### G3 dilim 2 — Saha ajanı iskeleti ✓ (29 Tem)

**Ne yapıldı**

- `Presentation/MngLogs.Agent` — Windows Service capable (`UseWindowsService`), console ile de çalışır
- Sistem vs politika config (`system.json` / `policy.json` under `%ProgramData%\MngLogs\Agent`)
- Disk outbound kuyruk + HTTPS/HTTP shipper → collector `/api/v1/ingest/batches`
- Yerel UI: `http://127.0.0.1:5092/` + `/api/status` + `/api/config`

### G3 dilim 3 — Event Log + metrik ✓ (29 Tem)

**Ne yapıldı**

- Varsayılan paketler: `security-auth` (4624,4625,4634,4648,4672,4720,4726,4740), `system-lifecycle` (6005,6006,7045)
- Bookmark: `eventlog-bookmarks.json`; ilk çalıştırmada “şimdi” seed (tarihçe flood yok)
- Metrikler: `host.up`, `cpu.percent`, `memory.available_bytes`, disk free/total
- Policy: `EventLog` + `Metrics` bölümleri; boş `Packages` → defaults
- Unit test (service watch ile birlikte **12/12**)

**Not:** Security kanalı için ajan LocalSystem / admin gerekir.

### G3 dilim 4 — Servis watch ✓ (29 Tem)

**Ne yapıldı**

- Policy: `serviceWatch.enabled` + `services[]` (`name`, `restartAllowed`)
- `ServiceWatchWorker`: SCM poll → `service.failed` / `service.recovered` / `service.missing`; opsiyonel restart event
- Status UI: `serviceWatchEventsProduced`, `lastServiceWatchUtc`
- Pilot: Spooler + `MngLogsDoesNotExist` → OS’te `source.type=service-watch`, `event.action=service.missing`

### MngLogs yerel UI (Nuxt) ✓ (29 Tem)

**Ne yapıldı**

- `Presentation/MngLogs.UI` — Nuxt 3 + @nuxt/ui (MngEngine Edge kalıbı)
- Sekmeler: Durum · Kuyruk · Kaynaklar · Loglar (üretilen/gönderilen) · Politika
- Build: `MngLogs/scripts/build-frontend.ps1` → `Agent/wwwroot`
- API: `/api/status`, `/queue`, `/sources`, `/events`, `/config` (+ system/policy POST)
- Arayüz dili: **Türkçe** (ürün/kod adları İngilizce kalabilir)

### Odak `mnglogcollector` deploy ✓ (29 Tem)

- Servis adı: **`mnglogcollector`** (eski `mnglogs` kaldırıldı)
- URL: `http://192.168.20.8:5091`
- Settings: `MngLogCollectorSettings__…`
- OS: `parser.id=mnglogcollector`

### MngLogs Faz 1 metrik toplama ✓ (30 Tem) — KAPANDI

**Kilit:** Üst süreç listesi Faz 1 **metrik ana maddesi** (yerel Durum + collector özet event; process flood yok).

| Madde | Durum |
|-------|--------|
| `host.up` | ✓ |
| CPU / bellek / disk (`IncludeHostResources`) | ✓ |
| Üst süreç yerel UI (Top-N CPU/RAM) | ✓ |
| Üst süreç → collector: `process.top_cpu` / `process.top_memory` | ✓ |
| Politika: `includeTopProcesses`, `topProcessCount` (varsayılan 5) | ✓ |

**Ship biçimi**

- Kalp atışı döngüsünde özet event (`source=metric`)
- `fields.metric` / `event.action`: `process.top_cpu` | `process.top_memory`
- `fields.processes[]`: `name`, `pid`, `cpuPercent` veya `workingSetBytes`
- `fields.value`: listedeki 1. sürecin skaler değeri; `fields.count`: N

**Durum UI (30 Tem)**

- Kompakt durum header (host, toplayıcı, kuyruk, gönderim)
- İç sekmeler: **Metrik** · Olay günlüğü · Servis izleme · Aktivite
- Metrik sağlık karoları: ömür boyu sayaç yerine tazelik / son okuma / son gönderim
- Üst süreç tablolarında tıklanabilir sıralama (Süreç / PID / CPU|RAM)

**Geçici OS doğrulama**

- `scripts/tests/MngLogs/os-test/start-os-test-dashboard.ps1` → `http://127.0.0.1:5099/`
- OpenSearch `mng-{domain}-sec-events-*` (Odak: `http://192.168.20.8:9200`); ürün Dashboards değil

**Bilinçli erteleme (metrik dışı / sonra)**

- Eşik aşımında ekstra top-process event
- Agent self metrikleri (kuyruk gecikmesi vb.) zorunlu değil
- Merkez SIEM’de dedicated top-process kartı

---

## Bilinçli erteleme / sonra

- G2b MngAdmin OpenSearch snapshot (G4 öncesi)
- MSI+GPO; Linux parite
- G4 cutover
- Faz 2 / 3, TRTEST lab, UI prod deploy
- Bu PC’de Event Log (admin gelince)
- OpenSearch Dashboards (opsiyonel)

---

## Sıradaki adım (bekleyen onay)

### A — Admin gelince Event Log (bu PC)

| | |
|--|--|
| **Ne** | Elevated / LocalSystem ajan; `EventLog:Enabled=true` |
| **Kazanım** | Security + System paketleri Odak OS’e |

### B — MSI / kurulum paketi (önerilen sonraki ürün adımı)

| | |
|--|--|
| **Ne** | Agent MSI + temel GPO notları |
| **Kazanım** | Saha dağıtımı |

### C — G2b MngAdmin OS snapshot / G4 cutover planı

### D — Durum UI: Olay günlüğü / Servis izleme sekmelerini derinleştirme

---

## Plan dosyaları

`C:\Users\monitra\.cursor\plans\`

- `siem_master_plan_1d443b06.plan.md`
- `mnglogs_siem_vizyonu_2d47d54f.plan.md`
- `network_discovery_bf70a6d6.plan.md`
- `opensearch_siem_store_a1b2c3d4.plan.md`
- `normalize_map_catalog_e7f8a9b0.plan.md`
- `detect_correlation_c1d2e3f4.plan.md`
- `siem_ui_a4b5c6d7.plan.md`
- `rsyslog_reuse_b8c9d0e1.plan.md`
- `trtest_gap_c3d4e5f6.plan.md`
- `siem_gecis_cutover_d4e5f6a7.plan.md`
- `siem_lisans_olcum_e5f6a7b8.plan.md`
- `siem_managed_telemetry_f6a7b8c9.plan.md`
