# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 4 Ağustos 2026 (FortiGate Events UX · Parse Rules ürün parserları · Discovery nginx 405 fix)  
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`). Lokal Nuxt ve MngLogs agent collector varsayılanı prod.  
**Detay cutover:** [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md) · Events UI: [../monitoring/SIEM_EVENTS_UI.md](../monitoring/SIEM_EVENTS_UI.md) · Discovery: [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md)

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- Host Analytics L3 / genel Analytics dönüşü  
- Ajansız host aksiyonları (Discovery)  
- Firewall parse kurallarının katalog seed’e taşınması (hâlâ C# `firewall.vendor.v1`)  
- UTM/VPN parse (FortiGate C) · `new_flow` baseline · allowed-flow volume policy  
- Periyodik discovery scan · Hard publish · Host paket ataması (E3)  
- UI’den parametreli agent indir  
- G4 kalan: alarm/Mongo köprü (gözlem)  
- P3d .deb  
- Sunucu tarafı kayıtlı filtre katalog API (şimdilik `localStorage`)  
- Parser dictionary redesign / kaldırma (ayrı dilim)  
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest · intent-only filter dialog (yerini katalog modal aldı)

---

## Son çalışılan konu

**FortiGate / firewall Events + Discovery prod proxy:** `firewall.vendor.v1` ExtraFields zenginleştirme · Events list/detay/filtre (policy · service · dstPort) · Parse Rules’ta **Ürün parserları** sekmesi · prod `mngui` nginx’e `/api/logcollector/` eklendi (**405 Method Not Allowed** giderildi).

---

## Tamamlananlar (bu dilim — 4 Ağu)

### FortiGate parser enrichment ✓

- `FirewallVendorParser`: `custom.policy_id`, `custom.service`, `custom.log_type`, `custom.log_subtype`, `custom.src_port`, `custom.cfg_path`  
- `event.code` ← `logid`; `source.host` ← `devname`  
- Liste DTO: `NetworkDstPort`, `NetworkProtocol` (BSON + OpenSearch okuyucular)  
- Unit testler güncellendi; **`mngreactor` prod deploy ✓**

### Events UI (firewall) ✓

- Filtre seed: `cat-firewall` + FortiGate preset’leri (all / denied / allowed / rule_change / kritik portlar)  
- Tablo ikinci satır: policy · service · `:port` (`secEventFirewallDisplay.ts`)  
- Detay: **Firewall akışı** bölümü + filtre kısayolları  
- Şema/intent: `custom.policy_id`, `custom.service`  
- **`mngui` prod deploy ✓**

### Parse Rules UI ✓

- Sekmeler: **Katalog kuralları** | **Ürün parserları** (engine `SIEM_PARSERS`, `builtInLocked`, salt okunur)  
- `firewall.vendor.v1` alan haritası yalnızca görünüm dialog’unda

### Discovery 405 fix ✓

- Kök neden: prod `mngui` = nginx static SPA (`npm run generate`); Nuxt BFF `server/api/logcollector` **prod’da çalışmaz**  
- `Mng.Ui/nginx.conf` → `location /api/logcollector/` → `mnglogcollector:5091` (`/api/...` rewrite)  
- Doğrulama: POST scan artık API’ye ulaşıyor (boş body → `400 cidr is required`, artık **405 değil**)  
- Lokal Nuxt: BFF; prod SPA: nginx — ikisi ayrı tutulmalı

### Önceki dilimler ✓

Filtre UX v2 (dinamik alanlar · scope-options · tree CRUD) · RDP/host cutover — önceki `current_status` dilimleri

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mnglogcollector` | ✅ prod (önceki dilimler) |
| `mngreactor` | ✅ FortiGate ExtraFields + list DTO deploy |
| `mngui` | ✅ nginx logcollector proxy + Events/Parse Rules UI deploy |

---

## Sıradaki adım

1. UI’dan Discovery taramasını doğrula (CIDR ile gerçek scan)  
2. Park: UTM/VPN parse · `new_flow` baseline · allowed-flow volume  
3. Park: ajansız host aksiyonları · periyodik scan  
4. İsteğe bağlı: firewall kurallarını katalog seed’e taşıma

---

## Nerede kalmıştık

FortiGate Events UX + Parse Rules ürün parserları + Discovery **405** nginx fix prod’da.  
**Kaldığımız nokta:** Discovery UI’dan canlı scan smoke; ardından park maddeleri (UTM/VPN, ajansız host CTA, volume policy).
