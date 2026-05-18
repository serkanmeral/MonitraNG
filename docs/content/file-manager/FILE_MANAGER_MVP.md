# File Manager — MVP ürün kapsamı

**Durum:** Taslak (ürün + teknik MVP maddeleri)  
**Tarih:** 29 Nisan 2026  
**Amaç:** Confluence tarzı, daha sade bir dosya yönetimi için MVP ürün kapsamı, **gerekli geliştirme özeti** ve **UI yönü** bu belgede toplanır. Ayrı mikroservis kararı ve MkDocs güncellemesi ayrı takip edilir.

---

## 1. Vizyon (MVP)

Domain içinde kullanıcıların dosyalarını **tek bir yerde toplaması**, **bulması** ve **kontrollü şekilde paylaşması**. Wiki/ek dosya hissi; tam sayfa editörü veya gelişmiş iş birliği MVP’de zorunlu değildir.

---

## 2. MVP kapsamı — fonksiyonlar

### 2.1 Göz atma ve düzen

- Klasör veya “alan” hiyerarşisi (ör. üst alan → alt klasör; alternatif olarak düz kök + etiket ileride genişletilebilir).
- Listeleme: tablo veya grid; sıralama (ad, yükleme tarihi, boyut).
- **Taşı**, **yeniden adlandır**, **sil** (silme politikası: kalıcı veya çöp kutusu — teknik fazda netleştirilecek).
- Boş klasör oluşturma (kullanıcı deneyimi olarak; depolama detayı ayrı).

### 2.2 Yükleme ve indirme

- Tek ve çoklu dosya yükleme; sürükle-bırak.
- İndirme (tek dosya).
- Aynı isimde dosya çakışması: **üzerine yaz**, **yeni ad ile kaydet** veya **iptal** seçeneklerinden en az biri (tercihen kullanıcıya sorulacak şekilde).

### 2.3 Önizleme

- Tarayıcıda makul önizleme (ör. PDF, görseller).
- Önizlenemeyen türler: indirme ile yetinme (MVP için yeterli).

### 2.4 Arama

- Dosya adında arama (minimum MVP).
- İsteğe bağlı sonraki faz: etiket, “son kullanılanlar” kısayolları.

### 2.5 Erişim ve paylaşım (temel)

- Domain / grup (veya rol) ile uyumlu **okuma / yazma** ayrımı (tam matris teknik taslakta tanımlanacak).
- MVP’de “herkese açık anonim link” zorunlu tutulmayabilir; varsa süreli veya kısıtlı tercih edilir.

### 2.6 Sürüm (MVP — bilinçli sade seçenek)

- **Önerilen minimum:** Yalnızca “güncel dosya” + metadata (`uploadedAt`, `uploadedBy`); üzerine yazıldığında geçmiş tutulmuyorsa kullanıcıya net mesaj.
- **İsteğe bağlı MVP+:** Son sürüm + önceki sürümleri listeleme/indirme (ek iş yükü kabul edilirse).

### 2.7 Kota ve kurallar (hafif)

- İzin verilen uzantılar ve maksimum dosya boyutu için ürün kuralları (uyarı veya red).
- Domain veya alan bazlı kota için en azından **uyarı eşiği** veya basit limit (tam kota motoru sonraya bırakılabilir).

### 2.8 Denetim (minimum)

- Yükleme, silme, taşıma, yeniden adlandırma gibi işlemler için kısa audit izi (kim, ne, ne zaman).

### 2.9 Entegrasyon (MVP’de “hafif”)

- Diğer uygulamalardan “dosya seç” kaynağı olarak kullanılabilirlik hedefi; detaylı senaryolar (task, form, chat) teknik planda bağlanacak.

---

## 3. Bilinçli olarak MVP dışı (sonra)

- Tam metin / içerik içi arama.
- Klasörün tamamını zip ile indirme.
- Gelişmiş Office içi önizleme (harici viewer olmadan).
- CDN, çok bölge replikasyon.
- Gelişmiş iş birliği (eşzamanlı düzenleme, yorum akışı).

---

## 4. İlgili mevcut sistem (referans)

Dataset kayıtlarındaki **`file` alan tipi** ve MinIO tabanlı yükleme/indirme bugün **MngDataGateway** üzerinden kullanılıyor; file manager MVP’si bununla **aynı mı, üst küme mi, paralel ürün mü** olacağı teknik taslakta netleştirilecek. Ürün belgesi olarak bu dosya o karara bağlı kalmaz.

İlgili teknik özüt: `docs/content/MngDataGateway/support/specs/FILE_FIELD_TYPE_SPECIFICATION.md`

---

## 5. MVP için gerekli geliştirmeler (teknik özet)

Mevcut hat: **MngDataGateway** + MinIO + `file` alanı pipeline’ı (sıkıştırma/şifreleme, indirme/metadata). MVP ürün fonksiyonlarını tam karşılamak için aşağıdaki işler **kodda henüz yok veya ürün seviyesinde tamamlanmalı** olarak kaydedilir. Önerilen uygulama yeri: **öncelikle MngDataGateway** (ayrı backend MVP için zorunlu değildir).

### 5.1 API ve depolama

| İş | Gerekçe |
|----|---------|
| **Prefix / klasör listeleme** | MinIO `ListObjects` ile `data/...` altında nesneleri sayfalı listelemek; ağaç ve tablo beslemesi. |
| **Taşı / yeniden adlandır** | MinIO tarafında copy + delete (veya tek copy ile hedef yol); metadata ve DB kaydı tutuluyorsa tutarlılık. |
| **Silme HTTP API** | `IMinIOFileService.DeleteFileAsync` var; istemciden güvenli silme için **yetki kontrollü DELETE (veya eşdeğeri)** endpoint eksik. |
| **“File manager kökü” yükleme modeli** | Bugün `POST /api/v1/files/upload` gerçek şemada **`file` alanı** + dataset **create** yetkisi ister. Kütüphane kökü için ya sabit dataset/alan, ya da alan-bağımsız upload + aynı pipeline/şifreleme politikası. |
| **Boş klasör** | S3 anlamında klasör = prefix; boş düğüm için **marker nesne** veya **Mongo’da klasör kaydı** (tercih teknik taslakta). |
| **İsim çakışması** | Sunucuda “var mı?” kontrolü + upload öncesi/sonrası politika (üzerine yaz / yeni ad / iptal). |

### 5.2 Arama, kota, denetim

| İş | Gerekçe |
|----|---------|
| **Ada göre arama** | Listeleme + filtre veya ayrı indeks koleksiyonu (performans / tam metin sonraya bırakılabilir). |
| **Kota / limit** | İstemci uyarısı için toplam kullanım hesabı veya basit sayaç (tam kota motoru MVP+ olabilir). |
| **Audit** | Sil / taşı / yeniden adlandır / yükle için yapılandırılmış log (mevcut file API’de ürün düzeyinde yok). |

### 5.3 İyileştirme (MVP’yi kilitlemez; aynı dönemde ele alınabilir)

- Şema **`fileOptions`** (max boyut, uzantı) ile `GetFileOptionsFromField` birleştirmesi (bugün çoğunlukla global config + TODO).
- Gateway route / UI proxy: mevcut Data Gateway deseni ile uyum (yeni path’ler için kontrol listesi).

---

## 6. UI MVP önerisi (Mng.Ui)

Amaç: **sade**, tek ekranda biten akış; mevcut stack (Nuxt 3, Vuetify) ve projedeki **sol ağaç + içerik** örüntüleriyle (ör. organizasyon, `useResizableTreePanel`) hizalı kalmak.

### 6.1 Sayfa iskeleti

- **Sol panel:** Klasör ağacı (dar ekranda `v-navigation-drawer` / drawer). Yeniden boyutlandırılabilir splitter tercih edilebilir.
- **Ana alan:** Üstte araç çubuğu; altta **liste görünümü** (MVP için tablo yeterli: ad, boyut, değiştirilme, yükleyen).
- **Sağ veya alt sheet:** Seçili dosya için **önizleme** (PDF/görsel; diğerleri “İndir” vurgusu). Alternatif: tam genişlikte `v-dialog` önizleme (mevcut `FileUploadField` önizleme davranışına yakın).

### 6.2 Araç çubuğu

- **Breadcrumb:** `Kök / … / mevcut klasör` (geri navigasyon + tıklanabilir segmentler).
- **Arama:** Tek satır; istemci filtre veya debounce ile API (hazır olduğunda).
- **Birincil aksiyonlar:** Yükle (dosya seçici + sürükle-bırak alanı overlay veya `v-btn` + drop zone), **Yeni klasör**, yenile.

### 6.3 Etkileşim

- Satır **çift tık:** klasöre gir veya dosyada önizleme/indir.
- **Bağlam menüsü** (`v-menu`): indir, yeniden adlandır, taşı (basit modal: hedef klasör seçimi), sil (onaylı).
- **Çoklu seçim** (isteğe bağlı MVP+): toplu indir sonraya bırakılabilir; MVP’de tek dosya yeterli olabilir.

### 6.4 Boş ve hata durumları

- Klasör boşken **illüstrasyon veya kısa metin** + “Dosya yükle” CTA.
- Yükleme sırasında **lineer ilerleme** veya bloklayıcı overlay; hata mesajları için mevcut `fileUpload` i18n anahtarları genişletilebilir.

### 6.5 Rota ve entegrasyon

- Önerilen rota örneği: `pages/apps/file-manager/index.vue` (veya `.../files/index.vue`) — diğer `apps/*` sayfalarıyla aynı layout.
- İleride “dosya seç” diyaloğu: bu sayfadaki listeyi **embed mod** veya paylaşılan composable ile task/form tarafına bağlamak.

### 6.6 Geri dönüş için kısa UI checklist (konuşma özeti)

Uygulamaya geçerken tek bakışta:

1. **Sol ağaç + sağ içerik:** Klasörler solda; dosyalar ana alanda **tablo** (ad, boyut, tarih, yükleyen). Dar ekranda ağaç **drawer**; geniş ekranda splitter (`useResizableTreePanel` vb. ile organizasyon ekranlarına paralel).
2. **Üst bar:** Breadcrumb, arama, **Yükle** + **Yeni klasör**, yenile.
3. **Önizleme:** Sağ **ince panel** veya `FileUploadField` benzeri **dialog**; PDF/görsel; diğer türlerde belirgin **İndir**.
4. **Eylemler:** Sağ tık / üç nokta: indir, yeniden adlandır, taşı (hedef klasör modalı), sil (onay). **Klasör satırında çift tık** = içeri gir; dosyada çift tık = önizleme veya indir.
5. **Yükleme:** Toolbar + sürükle-bırak; boş klasörde kısa **CTA**; hatalar için `fileUpload` i18n genişletmesi.
6. **Rota:** `pages/apps/file-manager/index.vue` (veya `.../files/index.vue`), diğer `apps/*` layout’u.
7. **Sonra:** Menü / sidebar (`horizontalItems`, `sidebarItem`) ve “dosya seç” embed — implementation fazında.

---

## 7. Çalışma önceliği ve bağlam

- **Şu an:** Başka bir iş kaleminde **chat_room** geliştirmesi öncelikli.
- **Sonra:** File manager MVP’sine bu belge üzerinden dönülecek (§2 ürün kapsamı, §5 teknik işler, §6 UI).
- MkDocs ve menü linkleri, file manager implementasyonu ile birlikte veya hemen öncesi güncellenecek (ayrı görev).

---

## 8. Revizyon

| Sürüm | Tarih       | Not |
|-------|-------------|-----|
| 0.1   | 2026-04-29  | İlk MVP ürün kapsamı taslağı |
| 0.2   | 2026-04-29  | MVP teknik geliştirme listesi + UI önerisi |
| 0.3   | 2026-04-29  | UI konuşma özeti (checklist) + chat_room sonrası dönüş notu |
