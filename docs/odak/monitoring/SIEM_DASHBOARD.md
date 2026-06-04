# SIEM güvenlik paneli (dashboard MVP)

**Durum:** ✅ MVP (4 Haz 2026)  
**Route:** `/apps/siem-center`  
**Menü:** **Güvenlik Merkezi → Güvenlik paneli**

---

## Özet

LogAlarm parite hedefindeki “dashboard” gap’inin ilk adımı: son 24 saat olay/ alarm özeti ve hızlı bağlantılar.

| Kart | Kaynak |
|------|--------|
| Toplam olay | `GET /reactor/api/v1/sec-events` (24s) |
| Açık alarm (≥6) | `GET /alarm/api/v1/alarms?openOnly=true&minSeverity=6` |
| Başarısız giriş | `eventAction=login_failed` |
| Engellenen akış | `eventAction=denied_flow` |
| Yeni akış (U7) | `eventAction=new_flow` (`baseline.newFlowPair`) |

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
- ~~U1–U7 senaryo kartları (son alarm zamanı)~~ ✅ `scenarios` widget
- ~~Özelleştirilebilir widget düzeni~~ ✅ (A4 — localStorage v2, `AcSiemCenterDashboard`)

Bkz. [SIEM_LOGALARM_PARITY_ROADMAP.md](./SIEM_LOGALARM_PARITY_ROADMAP.md)
