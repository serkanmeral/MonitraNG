# MngLogs Roadmap

> **2026-08-03:** Linux P3a–P3c + host cutover (agent-only → prod Collector) tamam. Günlük hedef: Odak **prod** `192.168.20.8:5091`.

## Tamamlanan

- Metrik + Event Log + service/app watch + disk kuyruk + collector ship
- Yerel Nuxt UI (Durum / Kuyruk / Kaynaklar / Loglar / Politika)
- PIN ile politika yazma koruması
- CLI: PIN / port / config / catalog
- Event Log: sunucu katalog ⊕ agent override; collector policy pull (ETag)
- MSI paketi + IT helper; HostId varsayılan = PC adı
- SIEM Center ince agent health paneli
- **Linux agent** (Core + journal + systemd watch + Local UI)
- **Host NXLog/rsyslog cutover** — SIEM host yolu yalnızca agent (FortiGate syslog ayrı)

## Sıradaki

- **P3d** — `.deb` paketleme
- Host Analytics L3 (SIEM Center)
- MSI / Windows Service kurulum smoke — admin ortam
- Opt-in otomatik alternatif port — düşük öncelik
- Windows → Core refactor (devam)

## Kararlar

- Local UI loopback / LAN bind; yazma işlemleri PIN + session ister.
- Port çakışmasında fail-fast + CLI; sessiz rastgele port yok (varsayılan).
- Paket kaynağı: sunucu gerçek; agent override istisna.
- Self-update yok → GPO/MSI MajorUpgrade.
- **Collector varsayılanı = Odak prod** (`http://192.168.20.8:5091`); test ayrı script.
- **Linux rsyslog köprüsü** artık birincil yol değil (Engine/Reactor Linux syslog ingest kapalı).
