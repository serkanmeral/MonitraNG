# SIEM güvenlik olay arama UI

**Durum:** ✅ Filtre kataloğu v2 + FortiGate/firewall liste-detay-filtre (4 Ağu 2026)  
**Route:** `/apps/siem-center/events`  
**Menü:** Side bar → **Güvenlik Merkezi** → Güvenlik olayları

---

## Bileşenler

| Katman | Dosya / endpoint |
|--------|------------------|
| API | `GET /reactor/api/v1/sec-events` — filtre + sayfalama |
| API | `GET /reactor/api/v1/sec-events/scope-options` — canlı Type/Product/Host |
| API | `GET /reactor/api/v1/sec-events/parse-rules/target-fields` — alan kataloğu |
| API | `GET /reactor/api/v1/sec-events/{id}` · `by-id?id=` |
| UI proxy | `Mng.Ui/server/api/reactor/[...path].ts` |
| UI service | `services/secEventService.ts` · `secEventParseRuleCatalogService.ts` |
| UI sayfa | `pages/apps/siem-center/events/index.vue` |
| UI explorer | `components/apps/siem-center/AcSecEventsExplorer.vue` |
| Filtre modal | `AcSecEventFilterCatalogDialog.vue` (+ Tree / Editor) |
| Katalog | `secEventFilterCatalog*` (localStorage + sistem seed) · `secEventFilterFieldSchema.ts` · `secEventFilterQueryMap.ts` |

## Sütun sözlüğü

| UI sütunu | Alan | Anlam |
|-----------|------|--------|
| Kaynak | `source.type` | Mantıksal kaynak sınıfı: `endpoint`, `ad`, `firewall`, `windows-eventlog`, `metric` (**IP değil**) |
| Host | `source.host` | Logu üreten / forward eden cihaz adı |
| Kaynak IP | `network.srcIp` | Olaydaki istemci IP (SSH: `from` adresi) |
| Hedef | `network.dstIp` | Ağ akışı hedef IP — **auth loglarında genelde boş** (normal) |
| Kullanıcı | `actor.user` | İlgili hesap |
| (Firewall 2. satır) | `custom.policy_id` · `custom.service` · `network.dstPort` | Actor/net satırında `policy · service · :port` (`secEventFirewallDisplay.ts`) |

## Firewall / FortiGate

- Parser: `firewall.vendor.v1` → ExtraFields + list `NetworkDstPort` / `NetworkProtocol` ([SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md))  
- Seed kategori: `cat-firewall` (tümü / denied / allowed / rule_change / kritik portlar 22,445,3389)  
- Detay drawer: **Firewall akışı** + filtre kısayolları (`AcSecEventDetailPanel.vue`)  
- Alan filtreleri: `custom.policy_id`, `custom.service` (+ mevcut `network.dstPort`)

## Filtre modeli (katalog)

Ana ekran: tam metin arama · **zaman** (1s/24s/7g/özel) · Filtre ekle · aktif chip’ler · tablo.

**Modal (Filtre ekle):**

- Sol: kategori tree (Sistem/RDP/Host/Kimlik + Benim); kullanıcı satırlarında ⋮ (rename / sil / kategori değiştir)  
- Sağ kapsam: **Product + Host** birincil; **Type** gelişmiş bölümde  
  - Seçenekler: canlı `scope-options` ∪ paket kataloğu ∪ statik fallback; Host serbest yazım  
- Alan filtreleri: Event Log **target-fields** (dinamik); Product’a göre parse extract daraltması  
- Kaydet / Farklı kaydet (sistem → kopya + hedef kategori) · Uygula  
- **Zaman kayıtlı filtrede yok** — yalnızca panel toolbar

Seed örnekleri: RDP oturum hareketleri (`product=rdp-session`, code in 21–25); Disconnect/Reconnect (24,25); Logon (21).

Query map → Reactor: dedicated param’lar + `fieldFilters` JSON (`custom.*`, `message`, …).  
RDP için `product` + `event.code` (+ `eventActionPrefix=rdp.`) güvenilir.

URL: `?filterId=…&timeRange=24h&sourceProduct=rdp-session&fieldFilters=[…]&…`

## API query (Reactor)

| Param | Not |
|-------|-----|
| `from` / `to` | Zaman (yoksa API ~24s) |
| `sourceType` | Exact `source.type` |
| `sourceProduct` | Exact `source.product` |
| `sourceHost` / `sourceHosts` | Contains / CSV OR |
| `eventCode` / `eventCodes` | Exact / CSV OR |
| `eventAction` / `eventActions` / `eventActionPrefix` | Exact / CSV OR / prefix (`rdp.` → code+product OR) |
| `actorUser` / `srcIp` / `dstIp` / `dstPort` | Dedicated hot-path |
| `fieldFilters` | JSON dizi: `[{"field","op","value"}]` — katalog alanları (`custom.*`, `message`, …) |
| `search` | multi_match / regex |
| `excludeUnknown` | UI varsayılan: bilinmeyenleri göster (`false`) |

**Kapsam seçenekleri:** `GET …/sec-events/scope-options?rangeHours=168`

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
`fieldFilters` / `scope-options` için **güncel `mngreactor`** gerekir.

```powershell
pwsh scripts/tests/MngDataGateway/auth/get-token.ps1 -KeeperBaseUrl http://192.168.20.8:5040 -DomainName odak -Username odak_admin -Password 'Admin123!'
$token = (Get-Content $env:TEMP\serkan_token.txt -Raw).Trim()
Invoke-RestMethod -Uri "http://192.168.20.8:5040/reactor/api/v1/sec-events?limit=5&sourceProduct=rdp-session" `
  -Headers @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak" }
Invoke-RestMethod -Uri "http://192.168.20.8:5040/reactor/api/v1/sec-events/scope-options?rangeHours=168" `
  -Headers @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak" }
```
