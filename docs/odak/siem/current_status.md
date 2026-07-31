# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 31 Temmuz 2026 (Discovery host modal + ajan 1.0.4 + Event Log UI)  
**Ortam notu:** Odak production `odak@192.168.20.8`; merkezi Mng.Ui local `npm run dev`; UI prod deploy sadece istekte.  
**Canlı pilot:** `MngLogs.Agent` **v1.0.4** Windows Service (`MngLogsAgent`) → collector `http://192.168.20.8:5091`; Local UI `http://127.0.0.1:5092/` ve LAN `http://192.168.20.13:5092/` (`LocalUiHost=0.0.0.0`); hostId=`TERMINAL-pilot`.

## Çalışma kuralı

Her implementasyon adımından **önce**: kapsam → kazanım → onay → kod.  
Onaysız büyük adım yok. Bu dosya **yapılan / yapılacak** listesinin güncel kaynağıdır.

**Park:** MngLogs P5 Event Log parser (Event ID → `event.action`) — kodlama yok.  
**Park:** Alarm / Notifier bağlantısı (service.failed vb.) — henüz erken.  
**Freeze:** Eski SIEM security paneli — dokunulmaz.

---

## Son çalışılan konu

Discovery host detay modalı (Durum / Metrikler / Uygulamalar+Hareketler / Event Log) + ajan watch snapshot prune + Event Log tablo/detay UI.

---

## Bu oturumda tamamlananlar

### Discovery MVP-A1 (prod) ✓

- Collector AD sync → Mongo `discovery_hosts` (~30 host)
- Scheduler `system-siem-discovery-ad-sync`
- UI live coverage (`host.up`)

### Ajan 1.0.4 (TERMINAL servis) ✓

- `host.up`: IP, kullanıcı, boot/uptime, sessions, `localUiPort` / bind
- Watch snapshot **prune** (silinen/yeniden adlandırılan hedefler “İzlenenler”den düşer)
- Process adı normalize (path / `.exe` → kısa ad) + dedupe
- Local UI Politika: uygulama adı normalize; LAN Local UI + firewall 5092
- Notepad restart: Session 0 — masaüstünde pencere yok (beklenen); servis izleme (nxlog) ile transition testi

### Mng.Ui Discovery host modal ✓

- Sol: host bilgisi (IP, kullanıcı, uptime, Local UI link)
- **Durum** — coverage + ajan özeti + oturumlar
- **Metrikler** — CPU/bellek/disk sparkline + süreçler
- **Uygulamalar** — Durum (`watch.inventory`) + **Hareketler** (service/app watch transitions, sıralı/sayfalı)
- **Event Log** — `windows-eventlog` host filtreli tablo (kanal filtresi, sıralama/sayfalama) + detay modalı (`secEventGet`)

### Reactor ✓

- Sec-event listesinde `Fields` (OpenSearch) — Discovery metrik/apps için gerekli

---

## Bilinçli erteleme / sonra

- Alarm / bildirim (`service.failed` → Alarm Center / Notifier)
- MngLogs P5 Event Log parser
- Catalog CRUD
- DHCP / sınırlı ICMP (katman B)
- Discovery KPI → live `dashboard-summary`
- Orphan AD host temizliği
- UI prod deploy (istekte)
- Linux Discovery
- Eski SIEM panel freeze; Detect hub greenfield sonra

---

## Sıradaki adım (öneri)

1. Event Log / Hareketler saha doğrulaması (nxlog stop/start; Security paketi)
2. İstenirse UI prod deploy
3. Alarm dilimi — ayrı onay
4. P5 parser — ayrı onay

---

## Nerede kalmıştık (handoff)

- A1 prod OK; ajan **1.0.4** TERMINAL’de Running.
- Discovery host modal (Metrikler / Uygulamalar+Hareketler / Event Log+detay) local UI’da.
- P5 ve alarm park; eski SIEM panel freeze.

---

## Plan dosyaları

`C:\Users\monitra\.cursor\plans\`

- `siem_master_plan_1d443b06.plan.md`
- `mnglogs_siem_vizyonu_2d47d54f.plan.md`
- `network_discovery_bf70a6d6.plan.md`
- …
