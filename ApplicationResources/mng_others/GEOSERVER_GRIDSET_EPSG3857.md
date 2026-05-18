# GeoServer: tr_rail Katmanları için Web Mercator Gridset (EPSG:900913 / EPSG:3857)

Leaflet (OSM/Web Mercator) tile indeksleri ile uyumlu olması için `tr_rail:railways` ve `tr_rail:stations` tile katmanlarında **Web Mercator** gridset'inin etkin olması gerekir.

- **EPSG:900913** ile **EPSG:3857** aynı projeksiyondur (Web Mercator). GeoServer’da çoğu kurulumda listede **EPSG:900913** görünür, **EPSG:3857** görünmeyebilir.
- **EPSG:4326** ve **EPSG:900913** zaten ekliyse ek bir şey yapmanız gerekmez; MngSim tile proxy varsayılan olarak **EPSG:900913** kullanır.

---

## Yöntem 1: GeoServer Web Arayüzü

1. **GeoServer'a giriş**  
   Tarayıcıda `http://localhost:8082/geoserver` (Docker ile 8082; yerelde farklı port kullanıyorsanız ona göre). Kullanıcı: `admin`, şifre: `admin` (veya kendi şifreniz).

2. **Tile Caching → Tile Layers**  
   Sol menüden **Tile Caching** → **Tile Layers** sayfasına gidin.

3. **Katmanı açın**  
   Listeden **tr_rail:railways** (ve gerekirse **tr_rail:stations**) satırındaki katman adına tıklayın.

4. **Gridset ekleyin**  
   Açılan sayfada **Mevcut Grid Dizileri** tablosu ve **Grid alt dizileri ekle: Choose One** açılır menüsü vardır.  
   - Listede **EPSG:3857** yoksa **EPSG:900913** seçin (aynı projeksiyon; çoğu GeoServer kurulumunda 900913 listelenir).  
   - Artı (+) ile ekleyip **Save** ile kaydedin.

5. **Aynı işlemi diğer katman için tekrarlayın**  
   **tr_rail:stations** için de aynı adımları uygulayın.

6. **Değişiklikleri kaydedin**  
   Sayfanın altındaki **Save** butonuna basın.

---

## Yöntem 2: REST API (PowerShell / curl)

Mevcut katman tanımına **EPSG:3857** gridset'ini eklemek için:

1. Katman XML'ini alın (URL'de `:` → `%3A` kullanın):

   ```bash
   curl -u admin:admin "http://localhost:8082/geoserver/gwc/rest/layers/tr_rail%3Arailways.xml" -o railways.xml
   ```

2. `railways.xml` içinde `<gridSubsets>` bölümüne aşağıdaki **gridSubset** bloklarından birini ekleyin (zaten varsa eklemeyin):

   ```xml
   <gridSubset>
     <gridSetName>EPSG:3857</gridSetName>
   </gridSubset>
   ```

   veya (eski adı kullanan GeoServer sürümleri için):

   ```xml
   <gridSubset>
     <gridSetName>EPSG:900913</gridSetName>
   </gridSubset>
   ```

3. Güncellenmiş XML'i geri yükleyin:

   ```bash
   curl -u admin:admin -X PUT -H "Content-Type: text/xml" -d @railways.xml "http://localhost:8082/geoserver/gwc/rest/layers/tr_rail%3Arailways.xml"
   ```

4. **tr_rail:stations** için aynı adımları `tr_rail%3Astations` ile tekrarlayın.

---

## Hazır Script (PowerShell)

Bu klasördeki **enable-geoserver-gridset-3857.ps1** betiği, yukarıdaki REST işlemini otomatik yapar: mevcut katman XML'ini alır, `gridSubsets` içinde **EPSG:3857** yoksa ekler ve PUT ile gönderir.

Kullanım (GeoServer localhost:8082, kullanıcı admin/admin):

```powershell
cd ApplicationResources\mng_others
.\enable-geoserver-gridset-3857.ps1
```

Özelleştirmek için script içindeki `$GeoServerBaseUrl` ve `$Cred` değişkenlerini düzenleyebilirsiniz.

---

## Grid subset sınırlıysa (TileOutOfRange)

Katman **grid subset** ile sınırlıysa GeoServer, Leaflet’ın gönderdiği (x, y) ile kendi (tilecol, tilerow) aralığı uyuşmayabilir; **Column/Row is out of range** hatası alırsınız. Tarayıcıda WMTS URL’sinde `tilecol` ve `tilerow` değerlerini değiştirerek çalışan bir tile bulun (örn. `tilecol=36&tilerow=24` çalışıyorsa, Leaflet’ın isteği `x=40,y=22` idi demektir).

MngSim tile proxy’de bu farkı gidermek için **ofset** ayarı kullanılır (appsettings):

- **TrainSim:GeoServerTileColOffset**: `tilecol = Leaflet_x + bu değer` (örn. 40→36 için **-4**)
- **TrainSim:GeoServerTileRowOffset**: `tilerow = Leaflet_y + bu değer` (örn. 22→24 için **+2**)

Örnek (çalışan tile 36,24; Leaflet 40,22 isteğiyle geliyorsa):

```json
"TrainSim": {
  "GeoServerBaseUrl": "http://localhost:8082",
  "GeoServerTileColOffset": -4,
  "GeoServerTileRowOffset": 2
}
```

Kalıcı çözüm: GeoServer’da katmanın grid subset sınırlarını genişleterek tüm istenen bölgeyi kapsayacak şekilde ayarlayın; böylece ofset gerekmez.

---

## Mng.Ui (Nuxt) — ortam değişkenleri

Mng.Ui harita sayfası tile proxy'si (`/api/tiles/geoserver`) aynı mantığı kullanır. Ortam değişkenleri:

| Değişken | Açıklama | Varsayılan |
|----------|----------|------------|
| `GEOSERVER_BASE_URL` | GeoServer base URL (örn. `http://localhost:8082`) | — |
| `GEOSERVER_TILE_MATRIX_SET` | WMTS tile matrix set | `EPSG:900913` |
| `GEOSERVER_TILE_COL_OFFSET` | tilecol = Leaflet x + bu değer | `0` |
| `GEOSERVER_TILE_ROW_OFFSET` | tilerow = Leaflet y + bu değer | `0` |

- Yerel geliştirme: `Mng.Ui/.env.example` dosyasını `.env` olarak kopyalayıp `GEOSERVER_BASE_URL=http://localhost:8082` verin.
- Docker/compose: `ApplicationResources/mng_apps/env.example` içindeki GeoServer bölümünü kopyalayıp container'ın GeoServer'a erişebileceği URL ile doldurun.

---

## Kontrol

- **Tile Layers** sayfasında ilgili katmanı açıp **Mevcut Grid Dizileri**nde **EPSG:900913** (veya **EPSG:3857**) görünüyor olmalı.  
- WMTS GetTile isteği (GeoServer’da hangi ad varsa onu kullanın): `tilematrixset=EPSG:900913`, `tilematrix=EPSG:900913:{z}`, `tilerow=...`, `tilecol=...`. Ofset kullanıyorsanız proxy, Leaflet (x,y)’yi bu ofsetlerle GeoServer (tilecol,tilerow)’a çevirir.
