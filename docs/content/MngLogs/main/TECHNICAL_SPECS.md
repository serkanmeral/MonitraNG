---
title: MngLogs Technical Specs
service: MngLogs
category: main
tags: [agent, local-ui, api, cli]
---

# MngLogs Technical Specs

Saha agent Local UI API (`http://{LocalUiHost}:{LocalUiPort}/api/...`) ve CLI referansı.
Varsayılan: `http://127.0.0.1:5092`.

## Kimlik / oturum

| Header | Tip | Zorunlu | Açıklama |
|--------|-----|---------|----------|
| `X-Local-Ui-Token` | string | Yazma API’lerinde | `POST /auth/setup` veya `/auth/unlock` sonrası session token |

PIN yoksa politika yazma reddedilir; önce setup gerekir.

## Auth

| Method | Path | Amaç |
|--------|------|------|
| GET | `/api/auth/status` | `configured`, `unlocked`, lockout |
| POST | `/api/auth/setup` | İlk PIN (`pin`, `pinConfirm`) → token |
| POST | `/api/auth/unlock` | PIN → token |
| POST | `/api/auth/lock` | Oturumu kapat |
| POST | `/api/auth/change-pin` | Oturum + mevcut/yeni PIN |

## Config

| Method | Path | Auth | Amaç |
|--------|------|------|------|
| GET | `/api/config` | — | Sistem (API key maskeli) + politika |
| POST | `/api/config/system` | Token | collectorUrl, apiKey, hostId |
| POST | `/api/config/policy` | Token | Tam `PolicyConfig` |

### EventLog politika alanları (özet)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `packages` | array | Legacy tam-liste (override yoksa tek başına efektif) |
| `agentOverrides` | array | İsimle sunucu paketini değiştirir veya ekler |
| `disabledServerPackages` | string[] | Sunucu paketini efektif listeden çıkarır |
| `packageCatalogSyncIntervalSeconds` | number | Katalog sync aralığı (sn) |

## Event Log katalog

| Method | Path | Auth | Amaç |
|--------|------|------|------|
| GET | `/api/eventlog/known-packages` | — | defaults / optional / all |
| GET | `/api/eventlog/package-plan` | — | server, overrides, disabled, effective, legacyMode |
| POST | `/api/eventlog/sync-catalog` | Token | Katalog önbelleğini yenile |

## Host yardımcıları

| Method | Path | Auth | Amaç |
|--------|------|------|------|
| GET | `/api/host/services` | Token | Windows servis listesi |
| POST | `/api/host/browse-executable` | Token | Native `.exe` seçici → path |

## Diğer Local UI

| Method | Path | Amaç |
|--------|------|------|
| GET | `/api/status` | Runtime özet, metrikler, watch snapshot |
| GET | `/api/sources` | Salt-okunur kaynak kataloğu |
| GET | `/api/queue` | Disk kuyruk peeki |
| GET/DELETE | `/api/events` | Son üretilen/gönderilen olay tamponu |
| GET | `/health` | Sağlık |

## CLI

Binary: `MngLogs.Agent.exe` (web host başlamaz).

```text
status [--data-dir <path>]
pin status|reset|set [--yes] [--pin] [--confirm] [--data-dir]
port show|check|set <port> [--data-dir]
```

Exit kodları (özet): `0` OK, `2` kullanım hatası, `3` port dolu / check başarısız.
