# MngLogs — Son durum

**Son güncelleme:** 2026-07-30

## Son çalışılan konu

Saha agent Local UI olgunlaştırma + PIN/port CLI kurtarma + Event Log sunucu/override paket modeli.

## Tamamlanan

- Durum / Kaynaklar / Politika tab UI; Loglar detay modal
- Politika PIN; host services + exe browse
- CLI: status, pin, port; port bind fail-fast
- Event Log catalog store + merger + Policy UI (sunucu vs override)
- Unit testler (PIN, port probe, package merge, watch/enricher)

## Devam / sonraki

- Collector’dan gerçek paket/parser pull
- Parser kural seti tartışması / implementasyon
- CLI: collector/host-id; isteğe bağlı auto port

## Önemli notlar

- Exe: `MngLogs\Presentation\MngLogs.Agent\bin\Release\net9.0-windows\MngLogs.Agent.exe`
- TFM: `net9.0-windows`
- Doküman: `docs/content/MngLogs/`
