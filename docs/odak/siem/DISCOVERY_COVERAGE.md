# SIEM Discovery — Keşif ve kapsam (Coverage)

**Durum:** ✅ Faz 1–3 + demo UX Odak’ta kullanılabilir (Collector `:5091` + UI)  
**Route:** `/apps/siem-center/discovery`  
**Son güncelleme:** 4 Ağustos 2026 (prod nginx `/api/logcollector/` — Discovery 405 fix)  
**Canlı durum / park noktası:** [current_status.md](./current_status.md)

Falcon Discover tarzı **ajan kapsamı boşluğu** + runZero-lite **kimlik parmak izi**. NetBox / Cisco L2 topoloji ve Armis/OT bilinçli olarak ertelendi.

---

## Ürün dili (kilitli)

| Kullanma | Kullan |
|----------|--------|
| Topoloji | **Keşif ve kapsam** |
| Korunuyor | **Kapsamda (canlı/sessiz)** |
| Korunmuyor | **Keşfedildi, ajan yok** / kapsam dışı |

Facet’ler: yalnızca **subnet / site**. VLAN / DHCP / AP UI’de gizlendi (sahte VLAN yok).

AD taraması **opsiyonel zenginleştirme**; demo’da genelde kapalı. Birincil hikâye ağ taraması + ajan eşlemesi.

---

## Tamamlanan fazlar

### Faz 1 — Coverage gap

- Gerçek kapsam KPI’ları; tıklanabilir filtreler
- Vurgu: **Keşfedildi, ajan yok**
- Host kartları / grafik düğümleri aynı `AcSiemDiscoveryHostNode` (coverage renkli OS ikonları)
- Kapsam özeti collapsible rail (`localStorage`: `siem.discovery.kpiOpen`)

### Faz 2 — Identity (parmak izi)

- `ServiceFingerprintProbe` + `IdentityClassifier` (HTTP title / TLS CN / SSH banner)
- Alanlar host entity + API + kart / detay modal
- Unit testler: `IdentityClassifierTests`

### Faz 3 — Dürüst subnet / prefix (site bucket)

- Prefix tablosu + LPM: `PrefixTableMatcher`
- Mongo: `discovery_prefix_tables`
- API: `GET/PUT /api/v1/discovery/prefixes`
- Varsayılan Odak: `192.168.20.0/24` → **Odak ofis**
- UI site eşlemesi: `resolveBestSiteBucket` (scan/AD IP öncelikli; ajan `primaryIp` ile “kapsam dışı” hatası düzeltildi)
- UI: **Ağ dilimleri** paneli (Settings + discovery dialog); satırda opsiyonel VLAN etiketi

### Demo / UX cilası

- Toolbar: **Görünüm** + **Keşfet** + **Diğer işlemler** (sakin renkler; sadece Temizle error)
- Canlı banner kaldırıldı; hero + toolbar birleşik discovery header
- AD scan metni: opsiyonel isim eşlemesi; checkbox ipucu
- ~2 dk Odak demo: `192.168.20.0/24` tara → ajan yok filtresi → unmanaged detay → managed canlı ajan → site “Odak ofis”

---

## API (Collector)

| Metod | Path | Not |
|-------|------|-----|
| GET | `/discovery/hosts` | Envanter |
| GET | `/discovery/summary` | KPI |
| POST | `/discovery/sync` | Ajan senkron |
| POST | `/discovery/scan` | CIDR tarama (kuyruk) |
| GET | `/discovery/scan/{runId}` | İş durumu |
| POST | `/discovery/scan/{runId}/cancel` | İptal |
| GET/PUT | `/discovery/prefixes` | Prefix tablosu |
| POST | `/discovery/hosts/clear` | Temizle |

### Proxy (kritik — lokal vs prod)

| Ortam | Nasıl | Not |
|-------|--------|-----|
| **Lokal Nuxt** (`npm run dev`) | BFF: `Mng.Ui/server/api/logcollector/[...path].ts` | Cookie/token + isteğe bağlı `X-MngLogs-ApiKey` |
| **Prod SPA** (`mngui` nginx) | `Mng.Ui/nginx.conf` → `location /api/logcollector/` → `http://mnglogcollector:5091` | `rewrite` `/api/logcollector/(.*)` → `/api/$1` |

Prod image `npm run generate` + nginx static’tir; Nitro BFF **çalışmaz**. Proxy yoksa `/api/logcollector/...` SPA `location /`’e düşer: GET → HTML 200, **POST → 405 Method Not Allowed**.  
4 Ağu 2026’da nginx location eklendi; POST scan doğrulandı (API yanıtı, 405 değil).

Prefix PUT için BFF’de de `PUT` izinli olmalı (lokal); prod nginx tüm metodları proxy eder.

---

## Kod haritası

### Collector (`MngLogCollector`)

| Parça | Dosya / alan |
|-------|----------------|
| Scan kuyruk / runner | `Services/Discovery/DiscoveryScan*.cs` |
| TCP / fingerprint | `TcpPortProbe`, `ServiceFingerprintProbe`, `IdentityClassifier` |
| Prefix LPM | `PrefixTableMatcher`, `NetworkCidrParser` |
| Store | `MongoDiscoveryHostStore`, `MongoDiscoveryPrefixStore`, `MongoDiscoveryScanJobStore` |
| Entity | `DiscoveryHost`, `DiscoveryPrefixTableDocument`, `DiscoveryScanJob` |
| Tests | `DiscoveryServiceTests`, `IdentityClassifierTests`, `PrefixTableMatcherTests` |

### UI (`Mng.Ui`)

| Parça | Dosya |
|-------|--------|
| Sayfa | `pages/apps/siem-center/discovery/index.vue` |
| Harita / KPI / toolbar | `AcSiemDiscoveryCoverageMap.vue` |
| Host kartı | `AcSiemDiscoveryHostNode.vue` |
| Detay | `AcSiemDiscoveryHostDetailDialog.vue` |
| Scan dialog | `AcSiemDiscoveryScanDialog.vue` |
| Prefix paneli | `AcSiemDiscoveryPrefixesPanel.vue` |
| Veri | `composables/useSiemDiscoveryData.ts`, `services/siemDiscoveryService.ts` |
| Prefix yardımcı | `utils/discoveryPrefixTable.ts` |

---

## Bilinçli park / ertelenen

| Konu | Not |
|------|-----|
| **Ajansız host aksiyonları** | Sonraki oturum — aşağıdaki park notu |
| Periyodik / zamanlanmış tarama | Sonra |
| L2 topoloji / NetBox | Bilinçli ertelendi (network control modülleri) |
| Armis / OT | Ertelendi |
| Engelle / izolasyon | Faz 3 müdahale; Discovery MVP dışı |

---

## Park noktası — Ajansız host aksiyonları (dönülecek)

**Ne:** “Keşfedildi, ajan yok” göründükten sonra operatörün net sonraki adımı.

**Tartışılan MVP adayları (kod yok):**

1. **Kurulum rehberi** — doküman / MSI-deb link (kart veya detay)
2. **Olaylara git** — IP/host ile SIEM events deep-link
3. **(İsteğe bağlı)** Kurulum komutu kopyala

**Açık kararlar (geri dönünce netleştir):**

- Sadece rehber mi, kopyalanabilir kurulum da mı?
- Aksiyon yeri: kart menüsü / detay / ikisi
- Printer/IoT (ajan beklenmez) için aynı CTA mı, gizle mi?
- Ticket / Operation Core şimdi mi sonra mı?

**Önerilen ilk paket (kilitsiz):** rehber + olaylara git; IoT’de ajan kurmayı gizle veya “genelde ajan yok” ipucu.

---

## Deploy notu (Odak)

Collector değişiklikleri için `mnglogcollector` rebuild/deploy; UI / nginx proxy için **`mngui` NoCache rebuild** (nginx.conf image’a gömülü).  
Prefix varsayılanı Odak ofis CIDR ile seed / PUT ile doğrulanır.

```powershell
.\scripts\odak\sync-odak-prod.ps1 -PathsCsv "Mng.Ui"
.\scripts\odak\deploy-odak-prod.ps1 -Services mngui -NoCache
```
