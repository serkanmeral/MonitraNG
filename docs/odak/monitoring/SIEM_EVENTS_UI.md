# SIEM güvenlik olay arama UI

**Durum:** ✅ MVP + veri yönetimi UX (5 Haz 2026)  
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

## Sütun sözlüğü

| UI sütunu | Alan | Anlam |
|-----------|------|--------|
| Kaynak | `source.type` | Mantıksal kaynak sınıfı: `endpoint`, `ad`, `firewall` (**IP değil**) |
| Host | `source.host` | Logu üreten / forward eden cihaz adı |
| Kaynak IP | `network.srcIp` | Olaydaki istemci IP (SSH: `from` adresi) |
| Hedef | `network.dstIp` | Ağ akışı hedef IP — **auth loglarında genelde boş** (normal) |
| Kullanıcı | `actor.user` | İlgili hesap |

## Filtreler

- Zaman aralığı: 1s / 24s / 7g (varsayılan 24s)
- `sourceType`: firewall, ad, **endpoint**, **bastion**
- `eventAction`: login_failed, login_success, denied_flow, … (U1–U10)
- `search`: rawPreview, IP, kullanıcı, host (regex, case-insensitive)
- **`excludeUnknown`** (varsayılan `true`): `event.action=unknown` gizlenir
- UI checkbox: **Bilinmeyen olayları göster** → `excludeUnknown=false`
- **URL senkronu:** `?eventAction=denied_flow&timeRange=24h&showUnknown=1`
- **U1–U10 kısayol çipleri** · **U7 rozeti** (`baselineNewFlowPair`)

## Saklama (backend — `SecEventsSettings`)

| Ayar | Varsayılan | Etki |
|------|------------|------|
| `DropUnknownEvents` | `true` | Ingest: unknown persist/observation yok · yanıt `skipped` |
| `HotTtlDays` | `60` | Mongo TTL `idx_timestamp_ttl` on `@timestamp` |
| `PersistFullRaw` | `false` | BSON'da yalnızca `rawPreview` (512 B) |

Odak docker-compose: `MngReactorSettings__SecEvents__*`

## Sınırlar

- `limit` max 200, varsayılan 50 (UI 100 kullanır)
- Liste yanıtında yalnızca `rawPreview`; tam `raw` yalnızca `PersistFullRaw=true` ingest + `GET .../{id}`
- Detay drawer: `raw` yoksa `rawPreview` gösterilir (etiket: "Ham önizleme")

## Doğrulama

Tarayıcı: `http://<gateway-ui>/apps/siem-center/events` (manager rolü)

Lab pilot:

```powershell
pwsh scripts/odak/reset-siem-lab-data.ps1 -Apply
pwsh scripts/odak/run-siem-linux-two-host-pilot.ps1
```

API (token ile):

```powershell
pwsh scripts/tests/MngDataGateway/auth/get-token.ps1 -KeeperBaseUrl http://192.168.20.20:5040 -DomainName odak -Username odak_admin -Password 'Admin123!'
$token = (Get-Content $env:TEMP\serkan_token.txt -Raw).Trim()
Invoke-RestMethod -Uri "http://192.168.20.20:5040/reactor/api/v1/sec-events?limit=5&eventAction=login_failed" `
  -Headers @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak" }
```
