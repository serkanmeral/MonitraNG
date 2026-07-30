# MngLogs Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

### Added
- **Local UI sekmeleri** — Durum, Kaynaklar, Politika sayfalarında tab’lı düzen; Loglar satır detay modalı.
- **Politika PIN koruması** — PBKDF2 hash (`ui-auth.json`), oturum token’ı, kilit / unlock / setup; yazma API’leri korumalı.
- **CLI kurtarma** — `MngLogs.Agent.exe status|pin|port` (PIN reset/set, port check/set); port doluyken açılışta net hata + CLI ipucu.
- **Servis seçici / exe gözat** — `GET /api/host/services`, `POST /api/host/browse-executable` (native OpenFileDialog).
- **Event Log paket modeli** — Sunucu katalog önbelleği (`server-packages.json`) ⊕ agent override / disabled; periyodik sync worker; Politika UI ayrımı.
- **Service / uygulama izleme** — Watch snapshot, OS SCM enricher, restart cooldown, `watch.inventory` metrik özeti.
- **Kaynaklar** — Salt-okunur config kataloğu (üreticiler, metrik tanımları, paketler, izleme, gönderim).

### Changed
- Agent hedef çerçeve: `net9.0-windows` (WinForms OpenFileDialog + ServiceController).
- Event Log resolve: boş override → sunucu/builtin katalog; legacy `packages` tam-liste modu korunur.

### Fixed
- Loglar yön filtresi (`USelectMenu` Nuxt UI v2 `options` API).
- `formatDate` / API tutarlılığı (Kaynaklar, Kuyruk).

## [0.1.0] - 2026-07

### Added
- Faz 1 metrikler (host up, CPU/bellek/disk, top process ship) ve Durum UI.
