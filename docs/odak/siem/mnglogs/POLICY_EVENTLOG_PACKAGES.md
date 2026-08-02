# Event Log paket kataloğu — Collector API

Agent, sunucuyu paket kaynağı olarak çeker. Katalog Mongo’da düzenlenebilir (`eventlog_packages`); **Yayınla** sürümü yükseltir.

## Agent pull

```http
GET /api/v1/policy/eventlog-packages
X-MngLogs-ApiKey: <ingest ile aynı key>
```

`ETag` = `"version"`. `If-None-Match` eşleşirse **304**.

| Alan | Anlam |
|------|--------|
| `packages` | `IsDefault=true` paketler (fleet tabanı) |
| `optionalPackages` | `IsDefault=false` |
| `version` | Son **Yayınla** sürümü |

DB: `MngLogCollectorSettings:MongoDB:EventLogCatalogDatabaseName` (Odak: `mng_odak`). İlk istekte builtin seed.

## Yönetim (Settings UI / BFF)

| Method | Path |
|--------|------|
| GET | `/api/v1/policy/eventlog-packages/manage` |
| GET | `/api/v1/policy/eventlog-packages/channels` |
| POST | `/api/v1/policy/eventlog-packages` |
| PUT | `/api/v1/policy/eventlog-packages/{name}` |
| DELETE | `/api/v1/policy/eventlog-packages/{name}` |
| POST | `/api/v1/policy/eventlog-packages/publish` |

Auth: ingest API key (UI BFF session + key forward).

Hard push yok — ajanlar açılış / `PackageCatalogSyncIntervalSeconds` ile çeker. Acil: Local UI sync.

## Host ataması (E3)

- Mongo: `eventlog_host_assignments` (`HostKey` = kısa hostname).
- Manage: `GET/PUT/DELETE .../eventlog-packages/assignments/{hostname}`
- Agent pull: `X-MngLogs-Hostname` (veya `?hostname=`) → `packages` = **tüm fleet defaults** + enabled optionals; ETag sürümü host+assignment damgası içerir.
- Fleet defaults host’tan kapatılamaz (`DisabledServerPackages` yok sayılır / kayıtlarda temizlenir).
- SIEM Discovery host modal → **Event Log** sekmesi: opsiyonel atama tablosu + olay listesi (tablo filtreleri).

## Notlar

- Paket = tek Windows kanalı + Event ID listesi.
- Parser kuralları henüz yok (P5).
- Host ataması: yukarıdaki E3 bölümü.
