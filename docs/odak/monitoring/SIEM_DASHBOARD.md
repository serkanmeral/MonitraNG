# SIEM güvenlik paneli (dashboard MVP)

**Durum:** ✅ MVP + U1–U10 senaryo kartları (4 Haz 2026) · **Prod perf ✅** (7 Tem 2026 — rollup ~0,18 sn)  
**Route:** `/apps/siem-center`  
**Menü:** **Güvenlik Merkezi → Güvenlik paneli**  
**Performans planı:** [SIEM_DASHBOARD_PERFORMANCE_PLAN.md](./SIEM_DASHBOARD_PERFORMANCE_PLAN.md)

---

## API (panel yükleme)

| Veri | Endpoint (gateway) | UI istemci yolu (Odak `mngui`) |
|------|-------------------|--------------------------------|
| Olay özeti (24s) | `GET /reactor/api/v1/sec-events/dashboard-summary` | `GET /api/reactor/v1/sec-events/dashboard-summary` |
| Alarm özeti | `GET /alarm/api/v1/alarms/dashboard-snapshot` | `GET /api/alarm/v1/alarms/dashboard-snapshot` |

Panel tek turda **2 istek** atar (aggregation). Olay arama sayfası mevcut `GET /sec-events` listesini kullanmaya devam eder.

### Production (`mngui` statik SPA — Odak `:3000`)

Lokal `npm run dev` ile production deploy **aynı değildir**:

| Konu | Dev (`npm run dev`) | Production (`npm run generate` + nginx) |
|------|---------------------|----------------------------------------|
| `/api/reactor/*` | Nuxt server route cookie → `Authorization` ekler | **`Mng.Ui/nginx.conf`** → `mngreactor:5003` proxy gerekir |
| JWT | Sunucu tarafında cookie'den taşınır | **`secEventService.ts`** istemciden `Authorization: Bearer …` göndermeli |
| `/api/widgets/batch` | Nuxt BFF var | Yok — widget batch **client-side** fetch (BFF yalnızca dev) |
| Locale MinIO | Opsiyonel override | Keeper 404 normal → build-time `utils/locales/*.json` fallback |

**Odak doğrulama (10 Haz 2026):**

```text
curl -s -o /dev/null -w "%{http_code}" http://192.168.20.20:3000/api/reactor/v1/health   # 200
# dashboard-summary: giriş sonrası tarayıcıda Authorization ile 200
```

Deploy: [../deploy/README.md](../deploy/README.md) — `sync-odak-source.ps1 -Paths Mng.Ui` + `deploy-odak-apps.ps1 -Services mngui`

**Sık hatalar (production-only):**

| Belirti | Kök neden | Fix |
|---------|-----------|-----|
| `hasManifestTableColumns is not defined` | Eksik import | `AcSiemCenterDashboard.vue` |
| `Cannot read properties of undefined (reading 'map')` | Reactor yanıtı bozuk / `hourly` yok | nginx `/api/reactor/` + API normalize |
| `401` dashboard-summary | JWT header yok | `secEventService` → `authHeaders()` |
| `405` `/api/widgets/batch` | Statik deploy'da BFF yok | Beklenen; client fallback |
| `504` dashboard-summary / panel yüklenmiyor | Mongo COLLSCAN (ingestedAt indeks yok) + yüksek hacim | [SIEM_DASHBOARD_PERFORMANCE_PLAN.md §3](./SIEM_DASHBOARD_PERFORMANCE_PLAN.md) · `hotfix-prod-sec-events-ingestedat-index.ps1` |
| Keeper locale `404` | MinIO'da locale dosyası yok | Zararsız; build-time locale kullanılır |

## Özet (legacy not)

| Kart | Kaynak |
|------|--------|
| Toplam olay | dashboard-summary → `eventsTotal` |
| Açık alarm (≥6) | dashboard-snapshot → `openTotal` |
| Başarısız giriş | dashboard-summary → `byAction.login_failed` |
| Engellenen akış | `byAction.denied_flow` |
| Yeni akış (U7) | `byAction.new_flow` |

## Bileşenler

| Katman | Dosya |
|--------|--------|
| Sayfa | `Mng.Ui/pages/apps/siem-center/index.vue` |
| Dashboard | `Mng.Ui/components/apps/siem-center/AcSiemCenterDashboard.vue` |
| Olay arama | `/apps/siem-center/events` (mevcut) |
| **Host Analytics** | `/apps/siem-center/hosts/[hostname]` — [../siem/HOST_ANALYTICS.md](../siem/HOST_ANALYTICS.md) |

## Odak menü

```powershell
.\docs\odak\monitoring\scripts\patch-siem-center-side-menu.ps1
```

## Sonraki adımlar (P2)

- ~~Olay dağılımı çubukları (24s)~~ ✅ panelde
- ~~Saatlik olay hacmi (24s)~~ ✅ `eventTimeline` widget
- ~~U1–U7 senaryo kartları (son alarm zamanı)~~ ✅ U1–U10 `scenarios` widget
- ~~Özelleştirilebilir widget düzeni~~ ✅ (A4 — localStorage v2, `AcSiemCenterDashboard`)

Bkz. [SIEM_LOGALARM_PARITY_ROADMAP.md](./SIEM_LOGALARM_PARITY_ROADMAP.md)
