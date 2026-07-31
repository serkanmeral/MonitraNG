# Windows Host Analytics (Host paneli)

**Durum:** ✅ MVP canlı (Odak prod `192.168.20.8` — 31 Temmuz 2026)  
**Route:** `/apps/siem-center/hosts/[hostname]`  
**Giriş:** Discovery host modal → **Host paneline git** (yeni sekme)

Tek scroll sayfa: zaman aralığı + KPI + kaynak grafikleri + oturumlar + watch + Event Log özeti.

---

## Bileşenler

| Parça | Dosya |
|-------|--------|
| Sayfa | `Mng.Ui/pages/apps/siem-center/hosts/[hostname].vue` |
| Shell | `AcSiemHostDashboard.vue` |
| KPI | `AcSiemHostKpiStrip.vue` |
| CPU/RAM/disk | `AcSiemHostResourceCharts.vue` |
| Oturumlar | `AcSiemHostSessionsCard.vue` |
| Watch | `AcSiemHostWatchSummary.vue` |
| Event Log | `AcSiemHostEventLogSummary.vue` |
| Veri | `composables/useSiemHostAnalytics.ts` |
| Security/RDP parse | `utils/windowsSecurityLogonParse.ts` |

---

## Veri kaynakları

| Bölüm | Kaynak | Aralık |
|-------|--------|--------|
| KPI / chart | `host.cpu` / `host.memory` / `host.disk` (+ `host.up`) | Dashboard picker |
| Aktif oturumlar | `host.up` → WTS sessions | Anlık |
| Oturum geçmişi | Security **4624/4634/4625/4647** + RDP **21/23/24/25** | Dashboard picker |
| Watch hedefler | Son `watch.inventory` | En güncel (aralıktan bağımsız) |
| Watch aktivite | service/app watch olayları | Dashboard picker |
| Event Log | `windows-eventlog` örneklem | Dashboard picker |

### Oturum geçmişi notları

- Varsayılan filtre **Kullanıcı**: interaktif Security tipleri (2/7/10/11) + failed + RDP kanalı; servis (5) / `HOST$` gizli.
- RDP **reconnect** çoğu zaman tip **10** üretmez; Event **25** + tip **7** unlock görülür.
- Host’lu arama (`search=TERMINAL`) kullanılır; çıplak `search=4624` prod index’te boş dönebilir.
- New Logon hesabı Subject (`TERMINAL$`) üzerine tercih edilir; `eventAction` mesaj blob’u parse edilir.

### Event Log detay / id

Windows kanal id’leri `/` içerebilir (`...LocalSessionManager/Operational:2543:25`).  
Path GET 404 verir → **`GET /reactor/api/v1/sec-events/by-id?id=`** (UI: `secEventService.secEventGet`). Catch-all `{**id}` de desteklenir.

### Event Log pasta filtresi

Kanal dilimi veya alttaki kanal chip’i tabloyu `channelFilterKey` ile süzer (Security / System / Application / RDP / Other).

---

## Deploy (prod)

```powershell
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Paths MngReactor,Mng.Ui
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngreactor,mngui
```

Doğrulama: `by-id` slash’li id → 200; UI Ctrl+F5.

---

## Sıradaki (isteğe bağlı)

- Collector/katalog E1–E3 (ayrı hat)
- P5 parser (park)
- Event Log örneklem limiti / sayfalama derinliği
- RDP–Security oturum korelasyonu (Logon ID)
