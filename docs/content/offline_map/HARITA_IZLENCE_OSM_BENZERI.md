# Haritayı OSM’e Daha Çok Benzetmek — Adım Adım

Bu rehber, ekrandaki **GeoServer arka plan** görüntüsünü (gri zemin, ray, şehir/ilçe isimleri) adım adım iyileştirmek için yapabileceğiniz işlemleri listeler. Kolay ve hızlı olandan daha kapsamlıya doğru sıralanmıştır.

---

## Gördüğünüz durum (kısa)

- **Arka plan:** Açık gri, düz (yol, su, bina yok).
- **Raylar:** Kırmızı noktalar + ince mavi çizgiler.
- **Yerleşimler:** Şehir/ilçe isimleri (siyah etiket) görünüyor.
- **Katman:** Sadece “GeoServer arka plan” açık.

Aşağıdaki adımları istediğiniz sırada uygulayabilirsiniz; hepsi “OSM’e benzer” görünümü artırır.

---

## Adım 1 — OSM arka planı da aç (en hızlı, çevrimiçi)

**Ne olur:** GeoServer katmanının altına OSM tile’ları eklenir; yollar, su, binalar, etiketler gelir. Ray ve yerleşim isimleri kendi GeoServer’ınızdan, zemin OSM’den olur.

**Yapılacak:**

1. Tren haritası sayfasında **“OSM arka plan (çevrimiçi)”** kutusunu işaretleyin.
2. **“GeoServer arka plan”** kutusu da işaretli kalsın.
3. Sayfayı yenileyin; önce OSM, üstte GeoServer katmanları görünür.

**Not:** İnternet gerekir. Tamamen çevrimdışı istiyorsanız Adım 2–5’e odaklanın.

---

## Adım 2 — Yerleşim etiketlerini doğrula (şehir/ilçe/köy isimleri)

**Ne olur:** Şehir/ilçe isimleri daha net veya daha fazla görünebilir; stil veya cache eksikse düzelir.

**Yapılacak:**

1. **Places etiket stilinin yüklü olduğundan emin olun:**
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts
   .\apply-places-labels-style.ps1
   ```
   (Şifre farklıysa: `.\apply-places-labels-style.ps1 -Password "sizin_sifre"`)

2. **GeoServer tile cache’i temizleyin** (stil değiştiyse veya etiketler eski tile’dan geliyorsa):
   - Tarayıcıda `http://localhost:8082/geoserver` → Giriş (admin / şifreniz).
   - **Tile Caching** (Türkçe: **Önbelleğe Alma** veya **Döşeme Önbelleği**) → **Tile Layers** (**Döşeme Katmanları**) → **tr_rail:places** satırına tıklayın.
   - **Truncate** (Türkçe kurulumda: **Kes** veya **Önbelleği Temizle** / **Tümünü Sil**) ile cache’i silin. Buton sayfa içinde veya **Seed/Truncate** altında olabilir; “cache’i boşalt” anlamındaki seçeneği kullanın.

3. Tren haritasını yenileyin; şehir/ilçe/köy isimleri güncel stille görünmeli.

---

## Adım 3 — Ray çizimini çizgi stiline çevir (kırmızı nokta yerine)

**Ne olur:** Raylar OSM’deki gibi **çizgi** (siyah çizgi + beyaz kesik çizgi) olur; nokta + ince çizgi yerine sürekli hat görünür.

**Yapılacak:**

1. **Hazır script ile (önerilen):**
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map\scripts
   .\apply-railways-line-style.ps1
   ```
   (Şifre farklıysa: `.\apply-railways-line-style.ps1 -Password "sizin_sifre"`)

2. **Tile cache’i temizleyin:**  
   GeoServer → **Tile Caching** (Türkçe: **Önbelleğe Alma** / **Döşeme Önbelleği**) → **Tile Layers** (**Döşeme Katmanları**) → **tr_rail:railways** → **Truncate** (Türkçe: **Kes** / **Önbelleği Temizle**).

3. Tren haritasını yenileyin; raylar siyah-beyaz çizgi olarak görünür.

**Stil dosyası:** `scripts/styles/railways_line.sld` (projede mevcut). İsterseniz GeoServer arayüzünden **Data → Styles** ile aynı SLD’yi elle yükleyip **tr_rail:railways** katmanının varsayılan stili yapabilirsiniz.

---

## Adım 4 — Daha fazla yerleşim ismi (ilçe, köy, mahalle)

**Ne olur:** Sadece büyük şehirler değil, daha fazla ilçe, köy ve (isteğe bağlı) mahalle/suburb isimleri çıkar.

**Yapılacak:**

1. **osm-filters.json** içinde `places` filtresine ek değerler ekleyin:
   - Örnek: `"values": ["city", "town", "village", "hamlet", "locality", "suburb"]`
   - `locality`: küçük yerleşim; `suburb`: mahalle/semt (çok nokta olabilir).

2. **OSM verisini yeniden üretin:**
   ```powershell
   cd c:\Serkan\iSIM\MonitraNG\docs\content\offline_map
   .\scripts\run-osm-filters.ps1 -ProjectRoot (Get-Location).Path
   ```
   (Docker ile: `-UseDocker` ekleyin.)

3. **PostGIS’e tekrar yükleyin:**  
   `railway-platform.md` Bölüm 7’deki gibi **ogr2ogr** ile `osm_places_raw` doldurup, `places` tablosunu **TRUNCATE** edip `import-staging-to-postgis.sql` içindeki places INSERT’ini çalıştırın.

4. GeoServer’da **tr_rail:places** için tile cache’i **Truncate** edin; haritayı yenileyin.

---

## Adım 5 — Arka plan rengini yumuşat (isteğe bağlı)

**Ne olur:** Tamamen gri zemin yerine hafif renk (ör. çok açık mavi-gri veya bej) kullanılabilir; harita daha “harita” hissi verir.

**Yapılacak:**

- Bu, **harita uygulaması** (MngSim train-map) tarafında yapılır: harita konteynerine bir **arka plan rengi** veya çok hafif bir **gradient** CSS ile verilir.  
- Örnek: `#map { background: #f0f4f8; }` (açık mavi-gri).  
- Değişiklik: `MngSim/wwwroot/train-map.html` içindeki `#map` stilini düzenleyin.

---

## Adım 6 — Tam OSM benzeri arka plan (yol, su, arazi)

**Ne olur:** Kendi sunucunuzda OSM’e çok benzeyen bir arka plan: **yollar**, **su** (akarsu + göl alanları), **arazi kullanımı** (orman, yeşil alan, yerleşim vb.). Tamamen **çevrimdışı** çalışır.

**Nasıl yapılır:** Tüm adımlar tek rehberde, kopyala-yapıştır komutlarla anlatıldı:

- **→ [ADIM_6_TAM_REHBER.md](ADIM_6_TAM_REHBER.md)**  
  Orada sırayla: OSM filtre (6.1), PostGIS tabloları (6.2), ogr2ogr ile yükleme (6.3), staging’den aktarma (6.4), GeoServer katmanları (6.5), tren haritasında görüntüleme (6.6) var. Projede hazır dosyalar: `osm-filters-basemap.json`, `scripts/init-basemap-tables.sql`, `scripts/import-basemap-to-postgis.sql`, `scripts/add-basemap-layers-geoserver.ps1`. MngSim proxy ve train-map bu katmanları zaten destekliyor.

---

## Özet tablo

| Adım | Ne yapıyorsunuz | Zorluk | Çevrimdışı? |
|------|------------------|--------|-------------|
| 1 | OSM arka planı da aç | Çok kolay | Hayır (internet gerekir) |
| 2 | Yerleşim etiket stilini doğrula + cache temizle | Kolay | Evet |
| 3 | Rayları çizgi stiline çevir (SLD) | Orta | Evet |
| 4 | Daha fazla yerleşim (locality, suburb) + veri yenileme | Orta | Evet |
| 5 | Arka plan rengini yumuşat (CSS) | Kolay | Evet |
| 6 | Yol, su, arazi katmanları (tam OSM benzeri) | Zor / orta vadeli | Evet |

Önce **1** ve **2** ile başlamanız, ardından **3** ile ray görünümünü düzeltmeniz pratik bir sıra olur. Tam çevrimdışı ve OSM benzeri görünüm için **2 + 3 + 4 + (isteğe bağlı 5)**; tam arka plan için **6** planlanabilir.
