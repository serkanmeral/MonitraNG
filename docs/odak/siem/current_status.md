# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 4 Ağustos 2026 (dinamik filtre alanları · kapsam scope-options · katalog tree yönetimi)  
**Ortam:** Günlük çalışma = **Odak production** (`192.168.20.8`). Lokal Nuxt ve MngLogs agent collector varsayılanı prod.  
**Detay cutover:** [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md) · Events UI: [../monitoring/SIEM_EVENTS_UI.md](../monitoring/SIEM_EVENTS_UI.md)

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- Host Analytics L3 / genel Analytics dönüşü  
- Ajansız host aksiyonları (Discovery)  
- Firewall parse kurallarının katalog seed’e taşınması (hâlâ C# parser)  
- Periyodik discovery scan · Hard publish · Host paket ataması (E3)  
- UI’den parametreli agent indir  
- G4 kalan: alarm/Mongo köprü (gözlem)  
- P3d .deb  
- Sunucu tarafı kayıtlı filtre katalog API (şimdilik `localStorage`)  
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest · intent-only filter dialog (yerini katalog modal aldı)

---

## Son çalışılan konu

**Güvenlik Olayları filtre UX v2:** Event Log alan kataloğundan dinamik “Alan ekle” · Reactor `fieldFilters` · canlı `scope-options` (Type/Product/Host) · Product+Host birincil / Type gelişmiş · kullanıcı kategori/filtre tree yönetimi (rename / sil / taşı).

---

## Tamamlananlar (bu dilim — 4 Ağu)

### Dinamik alan filtreleri ✓

- “Alan ekle” → `GET …/parse-rules/target-fields` (core + `custom.*`)  
- Product seçilince parse extract hedeflerine göre menü daralır (core alanlar her zaman)  
- Reactor: `fieldFilters` JSON (Mongo `$getField` + OpenSearch); dedicated param’lar korundu  
- Kataloga `event.code` eklendi; OpenSearch dual-write `fields` bag yazar

### Kapsam combobox ✓

- `GET /sec-events/scope-options` — canlı distinct type/product/host  
- Product listesine Event Log paket kataloğu merge  
- Host: serbest yazım + discovery/canlı öneriler (`v-combobox`)  
- Layout: Product + Host ana satır; Type → “Gelişmiş”

### Katalog tree yönetimi ✓

- Kullanıcı kategori: yeniden adlandır / sil (filtreler “Benim”e taşınır)  
- Kullanıcı filtre: yeniden adlandır / kategori değiştir / sil  
- Sistem seed kilitli; Farklı kaydet’te hedef kategori seçilebilir

### Önceki dilimler ✓

Filtre kataloğu modal · Reactor `sourceProduct`/`eventCodes` · RDP normalize · host agent cutover — [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md)

---

## Deploy durumu

| Bileşen | Durum |
|---------|--------|
| `mnglogcollector` | ✅ prod (önceki dilim) |
| `mngreactor` | ⏳ **yeniden deploy gerekli** (`fieldFilters` + `scope-options`) |
| `mngui` | ⏳ deploy edilmedi — lokal Nuxt |

---

## Sıradaki adım

1. Prod **`mngreactor`** (+ isteğe bağlı **`mngui`**) deploy  
2. Lokal/prod ile RDP + kullanıcı logon + `custom.*` alan filtresi smoke  
3. Yeni RDP ingest: `event.action=rdp.*` + actor/network promote  
4. Park maddeleri

---

## Nerede kalmıştık

Filtre UX v2 kodu hazır (dinamik alanlar · scope-options · tree CRUD).  
**Kaldığımız nokta:** prod `mngreactor` / `mngui` deploy; ardından canlı doğrulama.
