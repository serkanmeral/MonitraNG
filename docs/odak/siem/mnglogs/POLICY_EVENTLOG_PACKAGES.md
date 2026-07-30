# Event Log paket kataloğu — Collector API

Agent, sunucuyu paket kaynağı olarak çeker. Bu belge P2 sözleşmesinin kısa kaydıdır.

## Endpoint

```http
GET /api/v1/policy/eventlog-packages
X-MngLogs-ApiKey: <ingest ile aynı key>
```

Auth: ingest ile aynı (`IngestApiKey`). Key boş yapılandırılmışsa gate kapalı (dev).

`ETag` = `"version"`. Agent, collector kaynaklı cache varken `If-None-Match` gönderir; değişmemişse **304** ve yerel katalog korunur.

## Yanıt

```json
{
  "version": "2026-07-30.1",
  "source": "collector",
  "generatedUtc": "2026-07-30T00:00:00Z",
  "packages": [
    { "name": "system-lifecycle", "channel": "System", "eventIds": [41, 104, 6005] }
  ],
  "optionalPackages": [
    { "name": "security-auth", "channel": "Security", "eventIds": [4624, 4625] }
  ]
}
```

| Alan | Agent kullanımı |
|------|------------------|
| `packages` | Sunucu tabanı → merge (override / disabled) |
| `optionalPackages` | UI’da açılabilir opsiyoneller |
| `version` / `source` | `server-packages.json` cache meta |

## Agent davranışı

1. `PackageCatalogSyncWorker` periyodik `RefreshAsync`
2. Başarılı pull → `%ProgramData%\MngLogs\Agent\server-packages.json`, `source=collector`
3. Collector yok / hata → son iyi cache; yoksa `builtin`
4. Manuel: Local UI `POST /api/eventlog/sync-catalog` (PIN)

## Notlar

- İlk sürüm katalog collector içinde **builtin** seed (`BuiltinEventLogPackageCatalogService`); admin CRUD sonra.
- Parser kuralları bu endpoint’e henüz dahil değil (P5).
- MSI/Service smoke: admin yetkisi olan ortamda sonra test (2026-07-30 notu).
