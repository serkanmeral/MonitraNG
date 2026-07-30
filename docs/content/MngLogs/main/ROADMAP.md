# MngLogs Roadmap

## Tamamlanan (Faz 1 saha agent)

- Metrik + Event Log + service/app watch + disk kuyruk + collector ship
- Yerel Nuxt UI (Durum / Kuyruk / Kaynaklar / Loglar / Politika)
- PIN ile politika yazma koruması
- CLI: PIN / port kurtarma
- Event Log: sunucu katalog + agent override birleşimi (katalog şimdilik builtin)

## Sıradaki

- **Collector policy pull** — gerçek sunucu paket/parser kataloğu HTTP sync
- **Parser kuralları** — Event ID → structured `event.action` / alan map (sunucu + nadir agent override)
- CLI genişletme: `collector set`, `host-id set`, `catalog sync`, `service restart`
- Opt-in otomatik alternatif port (`AutoSelectLocalUiPort`)
- MSI / Windows Service kurulum paketi
- Mng.Ui merkezde `watch.inventory` widget

## Kararlar

- Local UI loopback; yazma işlemleri PIN + session ister.
- Port çakışmasında fail-fast + CLI; sessiz rastgele port yok (varsayılan).
- Paket kaynağı: sunucu gerçek; agent override istisna.
