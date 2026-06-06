# SIEM güvenlik paneli (dashboard MVP)

**Durum:** ✅ MVP + U1–U10 senaryo kartları (4 Haz 2026)  
**Route:** `/apps/siem-center`  
**Menü:** **Güvenlik Merkezi → Güvenlik paneli**

---

## API (panel yükleme)

| Veri | Endpoint |
|------|----------|
| Olay özeti (24s) | `GET /reactor/api/v1/sec-events/dashboard-summary` |
| Alarm özeti | `GET /alarm/api/v1/alarms/dashboard-snapshot` |

Panel tek turda **2 istek** atar (aggregation). Olay arama sayfası mevcut `GET /sec-events` listesini kullanmaya devam eder.

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
