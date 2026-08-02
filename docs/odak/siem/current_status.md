# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 2 Ağustos 2026 (Linux Host Analytics L1/L2 park; Analytics’e sonra dönülecek)  
**Ortam notu:** Odak production `odak@192.168.20.8`; Collector `:5091`; UI local Nuxt → prod API sık kullanılıyor.  
**Canlı pilot Windows:** `MngLogs.Agent` **v1.0.4** → collector `:5091`; hostId=`TERMINAL-pilot`.  
**Canlı pilot Linux:** `MngLogs.Agent.Linux` **v0.3.x** → `monitrang` 192.168.20.20 → aynı collector; Local UI `:5092`; hostId=`monitrang-linux-pilot`.

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- **Host Analytics L3 / genel Analytics dönüşü** (cilâ, UX derinliği)  
- **Ajansız host aksiyonları** (Discovery — rehber / olay deep-link; kararlar açık)  
- Periyodik discovery scan  
- P5 parser · Alarm/Notifier · Hard publish · Settings iskelet redesign · Host paket ataması (E3)  
- **UI’den parametreli agent indir** (MSI/deb + modal; ayrıca konuşulacak)  
- L2 topoloji / NetBox / Armis-OT (bilinçli ertelendi)  
**Freeze:** Eski SIEM security paneli.

---

## Son çalışılan konu

**Linux Host Analytics (L1 + L2)** — Discovery modal Journal + metrik eşleme + tam sayfa Host Analytics Linux uyarlaması.  
Detay: [HOST_ANALYTICS.md](./HOST_ANALYTICS.md)

Kullanıcı Analytics sayfalarına sonra dönecek; şu an için tamam kabul edildi.

---

## Tamamlananlar

### Linux Host Analytics L1/L2 ✓ (2 Ağu 2026)

- **L1 modal:** Event Log → `linux-journal`; sekme Journal; Unit/Aksiyon; Windows paket ataması Linux’ta gizli
- **Metrik eşleme:** scan-IP hostname ↔ `host.up.machine` / `source.host` (`siemDiscoveryHostMatch.ts`)
- **Metrik UX:** kullanım % birincil; disk free/total merge
- **L2 tam sayfa:** SSH/sudo oturum geçmişi; Journal özeti; bellek % KPI/chart; IP route ile host çözümleme
- Pilot: `monitrang` / `192.168.20.20`
- Doküman: [HOST_ANALYTICS.md](./HOST_ANALYTICS.md)

### Discovery Coverage ✓ (2 Ağu 2026)

- **Faz 1:** Gerçek coverage KPI, tıklanabilir filtre, “Keşfedildi, ajan yok” vurgusu; dil: Keşif ve kapsam / Kapsamda (canlı/sessiz)
- **Faz 2:** HTTP/TLS/SSH fingerprint (`ServiceFingerprintProbe`, `IdentityClassifier`) + UI kart/detay
- **Faz 3:** Prefix tablosu + LPM, Mongo `discovery_prefix_tables`, GET/PUT `/discovery/prefixes`; Odak `192.168.20.0/24` → “Odak ofis”; UI `resolveBestSiteBucket` fix
- **UX:** Birleşik header, Görünüm/Keşfet/Diğer işlemler, OS ikon coverage renkleri, ortak host node (graph+grid), AD opsiyonel metin
- Facet: sadece subnet/site (VLAN/DHCP/AP gizli)
- Doküman: [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md)

### Host Analytics Windows ✓ (31 Tem — UI + Reactor prod)

- Tek sayfa host paneli; Discovery modal CTA
- Oturum geçmişi: 4624/4634/4625/4647 + RDP 21/23/24/25; kullanıcı filtresi; sayfalama/detay
- Watch + Event Log özeti; `sec-events/by-id`
- Doküman: [HOST_ANALYTICS.md](./HOST_ANALYTICS.md)

### E1 Event Log paket ayarları ✓ (kod; Collector prod deploy ayrı)

- Mongo katalog + Settings Catalog + soft Yayınla

### Linux P3a–P3c ✓ (pilot)

- Metrik, watch, journald; detay: [MngLogs current_status](../../content/MngLogs/current_status.md)

### Önceki ✓

- Discovery A1 prod · host modal · ajan 1.0.4 watch prune · Reactor Fields

---

## Sıradaki adım

**Analytics’e dönüşte:** L3 cilâ + genel Host Analytics UX derinliği — [HOST_ANALYTICS.md § Sıradaki](./HOST_ANALYTICS.md).

**Discovery’ye dönüşte:** **Ajansız host aksiyonları** — MVP: kurulum rehberi + olaylara git; IoT’de ajan CTA gizle. Kararlar: [DISCOVERY_COVERAGE.md § Park](./DISCOVERY_COVERAGE.md#park-noktası--ajansız-host-aksiyonları-dönülecek).

**Paralel / diğer kuyruk (onaylı dilim):**

1. P3d (.deb) — [MngLogs current_status](../../content/MngLogs/current_status.md)  
2. Collector Odak prod deploy + Settings Catalog E1 doğrula (gerekirse)  
3. E3 host paket ataması (ayrı onay)  
4. Periyodik discovery scan  
5. P5 parser (ayrı onay)

---

## Nerede kalmıştık

Linux Host Analytics L1/L2 tamam (modal Journal + tam sayfa SSH/sudo/journal).  
**Kaldığımız nokta:** Analytics L3 / genel dönüş ve Discovery ajansız host aksiyonları — ikisi de park; kullanıcı istediğinde dönülecek.
