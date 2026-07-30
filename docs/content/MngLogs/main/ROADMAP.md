# MngLogs Roadmap

> **2026-07-30 PARK:** Aktif geliştirme duraklatıldı. Detay: `current_status.md`. Sonraki odak: Mng.Ui SIEM Center UI planlaması (ayrı chat). Dönüşte P5 parser.

## Tamamlanan (Faz 1 saha agent)

- Metrik + Event Log + service/app watch + disk kuyruk + collector ship
- Yerel Nuxt UI (Durum / Kuyruk / Kaynaklar / Loglar / Politika)
- PIN ile politika yazma koruması
- CLI: PIN / port / config / catalog
- Event Log: sunucu katalog ⊕ agent override; collector policy pull (ETag)
- MSI paketi + IT helper; HostId varsayılan = PC adı
- SIEM Center ince agent health paneli

## Sıradaki (park sonrası)

- **P5** — Parser kuralları (Event ID → `event.action` / alan map; sunucu ağırlıklı)
- MSI / Windows Service kurulum smoke — **admin yetkili ortamda**
- Opt-in otomatik alternatif port — düşük öncelik
- **P3 Linux iskelet** — ertelendi
- P4 genişletme (opsiyonel)

## Kararlar

- Local UI loopback; yazma işlemleri PIN + session ister.
- Port çakışmasında fail-fast + CLI; sessiz rastgele port yok (varsayılan).
- Paket kaynağı: sunucu gerçek; agent override istisna.
- Self-update yok → GPO/MSI MajorUpgrade.
- Linux acele değil; önce Windows filo + merkez UI + parser.
