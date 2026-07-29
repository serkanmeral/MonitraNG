# MngLogCollector

Sunucu tarafı log/metrik ingest API. Saha ajanı (**MngLogs**) batch gönderir; olaylar OpenSearch indeksine yazılır.

- Docker servis: `mnglogcollector` (port **5091**)
- Ingest: `POST /api/v1/ingest/batches`
- Health: `GET /health`
- Ayarlar: `MngLogCollectorSettings__…`

Saha ajanı ve yerel UI için bkz. `../MngLogs/`.
