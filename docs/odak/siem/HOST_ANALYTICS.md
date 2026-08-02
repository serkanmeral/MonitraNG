# Host Analytics (Host paneli)

**Durum:** ✅ Windows MVP + Linux L1/L2 (Odak — 2 Ağustos 2026)  
**Route:** `/apps/siem-center/hosts/[hostname]`  
**Giriş:** Discovery host modal → **Host paneline git** (yeni sekme)

Tek scroll sayfa: zaman aralığı + KPI + kaynak grafikleri + oturumlar + watch + Event Log / Journal özeti.

**OS:** `osFamily` (`windows` | `linux`) Discovery `host.up` / OS ipucu ile çözülür. Linux’ta Event Log → **journal** (`linux-journal`); Windows’ta `windows-eventlog`.

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
| Event Log / Journal | `AcSiemHostEventLogSummary.vue` |
| Veri | `composables/useSiemHostAnalytics.ts` |
| Host eşleme | `utils/siemDiscoveryHostMatch.ts` |
| Security/RDP parse | `utils/windowsSecurityLogonParse.ts` (Windows) |
| Journal oturum | `buildLinuxSessionHistoryFromJournal` in `useSiemHostAnalytics.ts` |

Modal (Discovery detay) aynı veri composable’larını kullanır: metrik / watch / event-log-or-journal.

---

## Veri kaynakları

| Bölüm | Windows | Linux | Aralık |
|-------|---------|-------|--------|
| KPI / chart | `host.cpu` / memory / disk (+ `host.up`) | Aynı | Dashboard picker |
| Aktif oturumlar | `host.up` → WTS sessions | Gizli (WTS yok) | Anlık |
| Oturum geçmişi | Security **4624/4634/4625/4647** + RDP **21/23/24/25** | Journal **sshd** / **sudo** (`ssh.login_*`, `sudo.event`) | Dashboard picker |
| Watch hedefler | Son `watch.inventory` | Aynı | En güncel |
| Watch aktivite | service/app watch | Aynı | Dashboard picker |
| Log özeti | `windows-eventlog` | `linux-journal` (Unit / Aksiyon) | Dashboard picker |

### Host adı / IP eşlemesi

Keşif kaydı bazen **scan IP**’yi hostname tutar (`192.168.20.20`); ajan telemetrisi `source.host` / `machine` ile gelir (`monitrang`).

- `host.up` → `machine` okunur; IP hostname ise kart/panel adı makine adına çevrilir.
- Metrik / watch / journal sorguları `preferredSecEventSearchTerm` + `secEventMatchesDiscoveryHost` ile IP ↔ makine eşler.
- Host paneli route’u IP veya kısa ad olabilir; `loadHostDashboardHost` her iki yolu da çözer.

### Oturum geçmişi — Windows

- Varsayılan filtre **Kullanıcı**: interaktif Security tipleri (2/7/10/11) + failed + RDP; servis (5) / `HOST$` gizli.
- RDP **reconnect** çoğu zaman tip **10** üretmez; Event **25** + tip **7** unlock görülür.
- Host’lu arama kullanılır; çıplak `search=4624` prod index’te boş dönebilir.

### Oturum geçmişi — Linux

- Journal satırlarından SSH başarı / başarısız ve sudo; kullanıcı + kaynak IP mesajdan parse edilir.
- Aktif WTS tablosu ve “Kullanıcı/Tümü” gürültü filtresi gösterilmez.

### Event Log detay / id (Windows)

Kanal id’leri `/` içerebilir → **`GET /reactor/api/v1/sec-events/by-id?id=`** (UI: `secEventGet`).

### Metrik UX

- Birincil: **kullanım %** (CPU / bellek / disk); ikincil: used/total GB.
- Disk serisi: aynı timestamp’te `total` + `free` birleştirilir (yalnız total gelince free silinmez).
- Chart: `memoryUsedSeries` varsa bellek ekseni %; yoksa available bytes (Windows).

---

## Deploy (prod)

```powershell
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Paths Mng.Ui
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngui
```

(Reactor `by-id` zaten prod’daysa sadece UI yeter.) Doğrulama: Linux host Ctrl+F5 → KPI + journal + SSH geçmişi.

---

## Sıradaki (isteğe bağlı / park)

- L3 cilâ (etiket / rol dilini sıkılaştırma)
- Analytics sayfalarına genel dönüş (UX derinliği)
- Event Log / journal örneklem limiti / sayfalama
- RDP–Security oturum korelasyonu (Logon ID) — Windows
- Collector/katalog E1–E3; P5 parser (ayrı hat)
