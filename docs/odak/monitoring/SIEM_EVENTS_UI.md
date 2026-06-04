# SIEM güvenlik olay arama UI

**Durum:** ✅ MVP (4 Haz 2026)  
**Route:** `/apps/siem-center/events`  
**Menü:** Side bar → **Güvenlik Merkezi** → Güvenlik olayları

---

## Bileşenler

| Katman | Dosya / endpoint |
|--------|------------------|
| API | `GET /reactor/api/v1/sec-events` — filtre + sayfalama |
| API | `GET /reactor/api/v1/sec-events/{id}` |
| UI proxy | `Mng.Ui/server/api/reactor/[...path].ts` |
| UI service | `services/secEventService.ts` |
| UI sayfa | `pages/apps/siem-center/events/index.vue` |
| UI explorer | `components/apps/siem-center/AcSecEventsExplorer.vue` |

## Filtreler (MVP)

- Zaman aralığı: 1s / 24s / 7g (varsayılan 24s)
- `sourceType`: firewall, ad, **endpoint**, **bastion**
- `eventAction`: login_failed, login_success, denied_flow, allowed_flow, rule_change, privileged_login_outside_window, new_flow (U7), group_member_added (U8), account_created (U9), directory_object_modified (U10)
- `search`: rawPreview, IP, kullanıcı, host (regex, case-insensitive)
- **URL senkronu:** `?eventAction=denied_flow&timeRange=24h` — panel deep link
- **U1–U10 kısayol çipleri** · **U7 rozeti** (`baselineNewFlowPair`)

## Sınırlar

- `limit` max 200, varsayılan 50 (UI 100 kullanır)
- Liste yanıtında yalnızca `rawPreview` (512 byte); tam `raw` yalnızca `GET .../{id}` (max 8192 byte, yeni ingest)
- Eski kayıtlarda `raw` alanı yok — detayda `rawPreview` fallback

## Faz 2 (4 Haz 2026)

- Ingest: Mongo `raw` alanı (`MaxRawBytes=8192`)
- API: `GET /sec-events/{id}` → `raw` + legacy fallback
- UI: detay drawer'da tam ham log (`secEventGet`)

## Doğrulama

Tarayıcı: `http://<gateway-ui>/apps/siem-center/events` (manager rolü) — menü: **Güvenlik Merkezi → Güvenlik olayları**

Odak menü patch: `docs/odak/monitoring/scripts/patch-siem-center-side-menu.ps1`

API (token ile):

```powershell
pwsh scripts/tests/MngDataGateway/auth/get-token.ps1 -KeeperBaseUrl http://192.168.20.20:5040 -DomainName odak -Username odak_admin -Password 'Admin123!'
$token = (Get-Content $env:TEMP\serkan_token.txt -Raw).Trim()
Invoke-RestMethod -Uri "http://192.168.20.20:5040/reactor/api/v1/sec-events?limit=5&eventAction=login_failed" `
  -Headers @{ Authorization = "Bearer $token" }
```

**Odak (4 Haz 2026):** `GET /reactor/api/v1/sec-events?limit=3&eventAction=login_failed` → `total=6973`, 3 kayıt döndü. `GET .../6a21151f497a21a08a2f87b1` → tek kayıt OK.
