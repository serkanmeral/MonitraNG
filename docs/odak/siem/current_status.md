# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 5 Ağustos 2026 (Discovery domain ayarları · SIEM Scenario Studio v2 · Akış Diyagramı Laboratuvarı)
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`). Lokal Nuxt ve MngLogs agent collector varsayılanı prod.  
**Detay cutover:** [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md) · Events UI: [../monitoring/SIEM_EVENTS_UI.md](../monitoring/SIEM_EVENTS_UI.md) · Discovery: [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md)

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- **SIEM Scenario Studio v2:** Backend ve UI tamamlandı; birlikte değerlendirmek üzere park edildi. MngAlarm production’da, Mng.Ui henüz deploy edilmedi.
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

**Akış Diyagramı Laboratuvarı:** Alarm Merkezi altında Node-RED tarzı akış tasarım alternatiflerini değerlendirmek için bağımsız bir UI laboratuvarı oluşturuldu. Sol node paleti, orta akış canvas’ı, sağ özellik paneli, sürükle-bırak, bağlantılar, minimap ve Node-RED/kart/minimal görünüm alternatifleri hazır. Çalışma buradan devam edecek; **Mng.Ui production deploy yapılmadı**.

---

## Tamamlananlar (bu dilim — 5 Ağu)

### Discovery ve Domain yönetimi ✓

- Otomatik `system-siem-discovery-ad-sync` scheduled job production’da pasif yapıldı; ağ keşfi yalnızca kullanıcı tarafından UI’dan başlatılıyor.
- Domain modeline `discoveryRootLabel` eklendi; Discovery kök etiketi domain bazında düzenlenebilir.
- Etiket fallback sırası: `discoveryRootLabel` → domain display name → domain name → oturum domain adı → i18n varsayılanı.
- Domain UI’da yalnız **Manager grupları**, mevcut aktif MngKeeper/Keycloak gruplarından seçilerek güncellenebilir; Admin grupları korunur ve UI’dan değiştirilmez.
- `mngkeeper` production deploy ve health kontrolü tamamlandı; ilgili Mng.Ui değişiklikleri henüz deploy edilmedi.

### SIEM Scenario Studio v2 ✓ — park

- Kanonik `ScenarioDefinition` v2: source, condition tree, aggregation, groupBy, window, sequence, dedup/cooldown, hysteresis ve metadata.
- Legacy `threshold`, `correlation`, `sequence`, `scheduled-staleness` kuralları geriye uyumlu projection/compile katmanıyla çalışır.
- Yaşam döngüsü: `draft → validated → published → archived`; immutable yayın, yeni draft, rollback, audit ve tenant-scoped katalog.
- API: validate, compile, preview, sample tabanlı side-effect-free simulate, publish/archive/rollback ve product-template clone.
- Motor: iç içe `AND/OR/NOT`, kalıcı N-adımlı sequence state, sustained condition, hysteresis/flap ve meta-correlation depth/cycle koruması.
- Scheduled query serbest Mongo/SQL çalıştırmaz; deklaratif model ve provider capability kontrolü kullanır. Production provider tanımlı değilse yayın/çalıştırma güvenli biçimde reddedilir.
- U1–U10, `siem-product-v2` paketinde salt okunur ve sürümlü ürün şablonlarına taşındı; production katalogda 10/10 doğrulandı.
- MngAlarm API + worker production deploy edildi; health `200`. Unit test: **59/59 başarılı**.
- Basit sekiz adımlı sihirbaz, gelişmiş görsel editör, lifecycle/katalog/template clone ve açıklanabilir simülasyon Mng.Ui’da hazır; **UI deploy edilmedi**.

### Akış Diyagramı Laboratuvarı ✓ — aktif çalışma

- Route: `/apps/alarm-center/flow-lab`; manager/admin erişimi ve ana menü girdisi.
- Vue Flow tabanlı sürükle-bırak canvas, bağlantı çizgileri, zoom/pan, minimap ve sağ özellik paneli.
- İlk node paleti: event source, field filter, AND/OR/NOT, count/window, sequence ve alarm output.
- FortiGate deny artışı örnek akışı ve Node-RED/kart/minimal görünüm karşılaştırması.
- `npm run generate`: **215 route başarılı**; Mng.Ui production deploy edilmedi.

### Önceki dilim — 4 Ağu

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

### Daha eski dilimler ✓

Filtre UX v2 (dinamik alanlar · scope-options · tree CRUD) · RDP/host cutover — önceki `current_status` dilimleri

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mnglogcollector` | ✅ prod (önceki dilimler) |
| `mngreactor` | ✅ FortiGate ExtraFields + list DTO deploy |
| `mngui` | ✅ nginx logcollector proxy + Events/Parse Rules UI deploy |
| `mngkeeper` | ✅ Discovery root label + Manager group backend |
| `mngalarm` | ✅ ScenarioDefinition v2 + lifecycle/simulation/template API |
| `mngalarm-worker` | ✅ N-step sequence/stateful evaluation |
| `Mng.Ui` yeni dilim | ⏸️ Scenario Studio + Flow Lab + Domain/Discovery UI lokal hazır, deploy bekliyor |

---

## Sıradaki adım

1. Akış Diyagramı Laboratuvarı üzerinde Node-RED tarzı UX alternatiflerini karşılaştır ve tercih edilen etkileşimi netleştir.
2. Node sözleşmesi, port kuralları, bağlantı doğrulama ve persistence yaklaşımını birlikte kararlaştır.
3. UI deploy kararı verilirse Scenario Studio + Flow Lab + Domain/Discovery UI’ı tek kontrollü dilim olarak deploy et.
4. Park: Scenario Studio ürün kararları · UTM/VPN parse · `new_flow` baseline · allowed-flow volume · ajansız host aksiyonları.

---

## Nerede kalmıştık

Discovery otomatik AD sync kapalı; Domain tabanlı kök etiket ve Manager grup backend’i production’da. Scenario Studio v2 backend’i ve U1–U10 product template kataloğu production’da; Scenario Studio UI park edildi.
**Kaldığımız nokta:** `/apps/alarm-center/flow-lab` üzerinde Node-RED tarzı akış editörü prototiplerini değerlendireceğiz. Mng.Ui değişiklikleri henüz production’a deploy edilmedi.
