# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 2 Ağustos 2026 (Discovery Faz 1–3 park; ajansız host aksiyonları konuşuldu, kod yok)  
**Ortam notu:** Odak production `odak@192.168.20.8`; Collector `:5091`; UI local Nuxt → prod API sık kullanılıyor.  
**Canlı pilot Windows:** `MngLogs.Agent` **v1.0.4** → collector `:5091`; hostId=`TERMINAL-pilot`.  
**Canlı pilot Linux:** `MngLogs.Agent.Linux` **v0.3.x** → `monitrang` 192.168.20.20 → aynı collector; Local UI `:5092`; hostId=`monitrang-linux-pilot`.

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- **Ajansız host aksiyonları** (Discovery — rehber / olay deep-link; kararlar açık)  
- Periyodik discovery scan  
- P5 parser · Alarm/Notifier · Hard publish · Settings iskelet redesign · Host paket ataması (E3)  
- **UI’den parametreli agent indir** (MSI/deb + modal; ayrıca konuşulacak)  
- L2 topoloji / NetBox / Armis-OT (bilinçli ertelendi)  
**Freeze:** Eski SIEM security paneli.

---

## Son çalışılan konu

**SIEM Discovery — Keşif ve kapsam** (Coverage gap + identity + prefix site).  
Detay: [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md)

Konuşuldu, implement edilmedi: **ajansız host aksiyonları** (kurulum rehberi, olaylara git, opsiyonel kurulum komutu). Kullanıcı başka işlere geçti; buraya sonra dönülecek.

---

## Tamamlananlar

### Discovery Coverage ✓ (2 Ağu 2026)

- **Faz 1:** Gerçek coverage KPI, tıklanabilir filtre, “Keşfedildi, ajan yok” vurgusu; dil: Keşif ve kapsam / Kapsamda (canlı/sessiz)
- **Faz 2:** HTTP/TLS/SSH fingerprint (`ServiceFingerprintProbe`, `IdentityClassifier`) + UI kart/detay
- **Faz 3:** Prefix tablosu + LPM, Mongo `discovery_prefix_tables`, GET/PUT `/discovery/prefixes`; Odak `192.168.20.0/24` → “Odak ofis”; UI `resolveBestSiteBucket` fix
- **UX:** Birleşik header, Görünüm/Keşfet/Diğer işlemler, OS ikon coverage renkleri, ortak host node (graph+grid), AD opsiyonel metin
- Facet: sadece subnet/site (VLAN/DHCP/AP gizli)
- Doküman: [DISCOVERY_COVERAGE.md](./DISCOVERY_COVERAGE.md)

### Host Analytics ✓ (31 Tem — UI + Reactor prod)

- Tek sayfa host paneli; Discovery modal CTA
- Oturum geçmişi: 4624/4634/4625/4647 + RDP 21/23/24/25; kullanıcı filtresi; sayfalama/detay
- Watch: tanımlı hedeflerin son inventory durumu + aralık aktivitesi
- Event Log: sayfalı/sıralanabilir tablo, pasta→kanal filtresi, detay
- `sec-events/by-id` + `{**id}` (slash’li Windows id 404 düzeltmesi)
- Doküman: [HOST_ANALYTICS.md](./HOST_ANALYTICS.md)

### E1 Event Log paket ayarları ✓ (kod; Collector prod deploy ayrı)

- Mongo katalog + Settings Catalog + soft Yayınla

### Linux P3a–P3c ✓ (pilot)

- Metrik, watch, journald; detay: [MngLogs current_status](../../content/MngLogs/current_status.md)

### Önceki ✓

- Discovery A1 prod · host modal · ajan 1.0.4 watch prune · Reactor Fields

---

## Sıradaki adım

**Discovery’ye dönüşte (öncelik):** **Ajansız host aksiyonları** — MVP önerisi: kurulum rehberi + olaylara git; IoT’de ajan CTA gizle. Kararlar: [DISCOVERY_COVERAGE.md § Park](./DISCOVERY_COVERAGE.md#park-noktası--ajansız-host-aksiyonları-dönülecek).

**Paralel / diğer kuyruk (onaylı dilim):**

1. Kullanıcının şu anki diğer işleri (bu oturum dışı)  
2. P3d (.deb) veya Host Analytics Linux — [MngLogs current_status](../../content/MngLogs/current_status.md)  
3. Collector Odak prod deploy + Settings Catalog E1 doğrula (gerekirse)  
4. E3 host paket ataması (ayrı onay)  
5. Periyodik discovery scan  
6. P5 parser (ayrı onay)

---

## Nerede kalmıştık

Discovery Faz 1–3 + demo UX tamam; Odak’ta CIDR tarama + site bucket + identity gösterilebilir.  
**Kaldığımız nokta:** ajansız host aksiyonları — ürün konuşması yapıldı, kod yazılmadı; başka işler sonrası buraya dönülecek.  
Host Analytics MVP ve Linux P3c pilot ayrıca canlı.
