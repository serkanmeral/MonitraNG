# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 3 Ağustos 2026 (filtre kataloğu UX + Reactor query + prod deploy)  
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
**Freeze:** Eski SIEM security paneli · NXLog / Linux rsyslog host ingest · intent-only filter dialog (yerini katalog modal aldı)

---

## Son çalışılan konu

**Güvenlik Olayları filtre kataloğu** (modal: kategori tree + Type/Product/Host + dinamik alanlar; zaman panelde) · Reactor `sourceProduct` / `eventCodes` / `sourceHosts` · OS lab temizliği + RDP doğrulama · prod `mngreactor` + `mnglogcollector` deploy (UI deploy bekliyor).

---

## Tamamlananlar (bu dilim)

### Filtre kataloğu UX ✓

- Ana ekran: eski toolbar (arama · zaman · Filtre ekle · chip’ler · tablo)  
- Modal: sol kategori/filtre tree · sağ kapsam + alan editörü · kaydet / farklı kaydet (sistem → kopya)  
- Seed: RDP (oturumlar / disconnect-reconnect / logon) · Host · Kimlik · Benim  
- Katalog: `localStorage` + sistem seed (`secEventFilterCatalog*`)  
- Zaman kayıtlı filtrede **yok**

### Reactor query ✓

- `sourceProduct`, `eventCodes`, `sourceHosts` (Mongo + OpenSearch)  
- Prod deploy sonrası RDP API: `sourceProduct=rdp-session` → **total=2** (TERMINAL 24/25)

### Ortam / doğrulama ✓

- OS sec-events indeksleri sıfırlandı (geliştirme verisi); agent yeniden yazıyor  
- RDP agent yolu OS’te doğrulandı (`rdp-session`, code 24/25)  
- Prod: `mngreactor` + `mnglogcollector` güncel; **`mngui` henüz deploy edilmedi** (lokal Nuxt)

### Önceki dilimler ✓

Host agent cutover · RDP normalize · prod sabitleme · Parse Rules P5 — [HOST_TELEMETRY_CUTOVER.md](./HOST_TELEMETRY_CUTOVER.md)

---

## Sıradaki adım

1. İsteğe bağlı: prod **`mngui`** deploy (filtre modal canlı UI)  
2. Collector normalizer sonrası yeni RDP olaylarında `event.action=rdp.*` + actor/network promote kontrolü  
3. Park: Analytics L3 · Discovery · firewall katalog · G4 · P3d  

---

## Nerede kalmıştık

Filtre kataloğu kodu + Reactor query + prod backend deploy hazır.  
**Kaldığımız nokta:** lokal Nuxt ile filtre modal doğrulama; kullanıcı isterse `mngui` deploy; park maddeleri.
