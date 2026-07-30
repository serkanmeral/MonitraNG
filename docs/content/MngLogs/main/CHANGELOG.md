# MngLogs Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

### Added
- **MSI / GPO paketleme** — WiX per-machine MSI (`MngLogsAgent` servisi), IT helper kılavuzları, self-update yok (MajorUpgrade).
- **CLI genişleme** — `config show|set`, `catalog show|sync`.
- **Collector katalog pull** — `GET /api/v1/policy/eventlog-packages` + ETag/304; agent HTTP sync.
- **SIEM Center agent health (ince)** — `AcAgentHealthPanel` (`host.up` / `watch.inventory`).
- **Local UI sekmeleri** — Durum, Kaynaklar, Politika sayfalarında tab’lı düzen; Loglar satır detay modalı.
- **Politika PIN koruması** — PBKDF2 hash (`ui-auth.json`), oturum token’ı, kilit / unlock / setup; yazma API’leri korumalı.
- **CLI kurtarma** — `status|pin|port`; port doluyken açılışta net hata + CLI ipucu.
- **Servis seçici / exe gözat** — `GET /api/host/services`, `POST /api/host/browse-executable`.
- **Event Log paket modeli** — Sunucu katalog ⊕ agent override / disabled; Politika UI.
- **Service / uygulama izleme** — Watch snapshot, OS SCM enricher, `watch.inventory`.
- **Kaynaklar** — Salt-okunur config kataloğu.

### Changed
- Agent hedef çerçeve: `net9.0-windows`.
- Event Log resolve: sunucu/builtin ⊕ override; legacy `packages` korunur.
- Boş `HostId` → PC adı (`Environment.MachineName`) persist + Politika API.
- Ingest OpenSearch: `fields["event.action"]` öncelikli.

### Fixed
- Loglar yön filtresi (`USelectMenu` Nuxt UI v2 `options` API).
- `formatDate` / API tutarlılığı (Kaynaklar, Kuyruk).

### Parked (2026-07-30)
- MSI/Service smoke (admin), P5 parser, P3 Linux — `docs/content/MngLogs/current_status.md`.

## [0.1.0] - 2026-07

### Added
- Faz 1 metrikler (host up, CPU/bellek/disk, top process ship) ve Durum UI.
