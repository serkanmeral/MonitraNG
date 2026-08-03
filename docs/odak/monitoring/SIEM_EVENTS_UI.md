# SIEM güvenlik olay arama UI

**Durum:** ✅ Filtre kataloğu modal (3 Ağu 2026)  
**Route:** `/apps/siem-center/events`  
**Menü:** Side bar → **Güvenlik Merkezi** → Güvenlik olayları

---

## Bileşenler

| Katman | Dosya / endpoint |
|--------|------------------|
| API | `GET /reactor/api/v1/sec-events` — filtre + sayfalama |
| API | `GET /reactor/api/v1/sec-events/{id}` · `by-id?id=` |
| UI proxy | `Mng.Ui/server/api/reactor/[...path].ts` |
| UI service | `services/secEventService.ts` |
| UI sayfa | `pages/apps/siem-center/events/index.vue` |
| UI explorer | `components/apps/siem-center/AcSecEventsExplorer.vue` |
| Filtre modal | `AcSecEventFilterCatalogDialog.vue` (+ Tree / Editor) |
| Katalog | `types/apps/secEventFilterCatalog.ts` · `secEventFilterCatalogSeed.ts` · `secEventFilterCatalogService.ts` (localStorage + sistem seed) |

## Sütun sözlüğü

| UI sütunu | Alan | Anlam |
|-----------|------|--------|
| Kaynak | `source.type` | Mantıksal kaynak sınıfı: `endpoint`, `ad`, `firewall`, `windows-eventlog`, `metric` (**IP değil**) |
| Host | `source.host` | Logu üreten / forward eden cihaz adı |
| Kaynak IP | `network.srcIp` | Olaydaki istemci IP (SSH: `from` adresi) |
| Hedef | `network.dstIp` | Ağ akışı hedef IP — **auth loglarında genelde boş** (normal) |
| Kullanıcı | `actor.user` | İlgili hesap |

## Filtre modeli (katalog)

Ana ekran: tam metin arama · **zaman** (1s/24s/7g/özel) · Filtre ekle · aktif chip’ler · tablo.

**Modal (Filtre ekle):**

- Sol: kategori tree (Sistem/RDP/Host/Kimlik + Benim); filtre yaprakları  
- Sağ: kapsam **Type / Product / Host** (Tümü veya dropdown; Host çoklu) + dinamik alan satırları  
- Kaydet / Farklı kaydet (sistem filtre düzenlenemez → kopya) · Uygula  
- **Zaman kayıtlı filtrede yok** — yalnızca panel toolbar

Seed örnekleri: RDP oturum hareketleri (`product=rdp-session`, code in 21–25); Disconnect/Reconnect (24,25); Logon (21).

Query map → Reactor: `sourceType`, `sourceProduct`, `sourceHost`/`sourceHosts`, `eventCode`/`eventCodes`, `eventActionPrefix`, …  
RDP için ham `event.action` döneminde `product` + `event.code` (+ prefix genişlemesi) güvenilir.

URL: `?filterId=…&timeRange=24h&sourceProduct=rdp-session&…`

## API query (Reactor)

| Param | Not |
|-------|-----|
| `from` / `to` | Zaman (yoksa API ~24s) |
| `sourceType` | Exact `source.type` |
| `sourceProduct` | Exact `source.product` |
| `sourceHost` / `sourceHosts` | Contains / CSV OR |
| `eventCode` / `eventCodes` | Exact / CSV OR |
| `eventAction` / `eventActions` / `eventActionPrefix` | Exact / CSV OR / prefix (`rdp.` → code+product OR) |
| `search` | multi_match / regex |
| `excludeUnknown` | UI varsayılan: bilinmeyenleri göster (`false`) |

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
- Kayıtlı kullanıcı filtreleri şimdilik tarayıcı `localStorage` (sunucu katalog API sonraki dilim)

## Doğrulama

Lokal Nuxt → prod Gateway: `/apps/siem-center/events` · Filtre ekle → RDP → Disconnect/Reconnect → Uygula  
Prod API (token ile):

```powershell
pwsh scripts/tests/MngDataGateway/auth/get-token.ps1 -KeeperBaseUrl http://192.168.20.8:5040 -DomainName odak -Username odak_admin -Password 'Admin123!'
$token = (Get-Content $env:TEMP\serkan_token.txt -Raw).Trim()
Invoke-RestMethod -Uri "http://192.168.20.8:5040/reactor/api/v1/sec-events?limit=5&sourceProduct=rdp-session" `
  -Headers @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak" }
```
