# Widget List Sayfası Test Rehberi

**Sayfa:** `/apps/widgets`  
**Dosya:** `Mng.Ui/pages/apps/widgets/index.vue`  
**Tarih:** Ocak 2026

---

## 📋 Test Öncesi Gereksinimler

1. **MngDataGateway** servisi çalışıyor olmalı
2. **Mng.Ui** uygulaması çalışıyor olmalı
3. **Giriş yapılmış** olmalı (authentication token gerekli)
4. **Dataset'ler oluşturulmuş** olmalı:
   - `@widget_categories` dataset'i mevcut
   - `@widgets` dataset'i mevcut
5. **Test verileri** hazır olmalı:
   - En az 3-5 widget kategorisi
   - En az 10-15 widget (farklı kategoriler, tipler, aktif/pasif durumları)

---

## 🎯 Test Kategorileri

### 1. Sayfa Yükleme ve İlk Görünüm

#### 1.1. Sayfa Erişimi
- [ ] `/apps/widgets` URL'sine gidildiğinde sayfa açılıyor mu?
- [ ] Sayfa başlığı doğru görünüyor mu? (`widgets.title` translation key)
- [ ] Breadcrumb doğru görünüyor mu? (Home → Widgets)
- [ ] Sayfa layout'u doğru mu? (default layout kullanılıyor mu?)

#### 1.2. İlk Yükleme
- [ ] Sayfa açıldığında widget kategorileri yükleniyor mu?
- [ ] Sayfa açıldığında widget listesi yükleniyor mu?
- [ ] Loading state gösteriliyor mu? (ilk yükleme sırasında)
- [ ] Loading state kayboluyor mu? (veri yüklendikten sonra)

#### 1.3. Boş Durum (Empty State)
- [ ] Widget yoksa "Widget bulunamadı" mesajı gösteriliyor mu?
- [ ] "İlk Widget'ı Oluştur" butonu görünüyor mu?
- [ ] Butona tıklandığında `/apps/widgets/new` sayfasına yönlendiriliyor mu?

---

### 2. Widget Listesi Görüntüleme

#### 2.1. Tablo Yapısı
- [ ] Tablo başlıkları doğru görünüyor mu?
  - [ ] Ad (name)
  - [ ] Başlık (title)
  - [ ] Kategori (category)
  - [ ] Tip (type)
  - [ ] Aktif (isActive)
  - [ ] Sıra (order)
  - [ ] Oluşturulma (createdAt)
  - [ ] İşlemler (actions)
- [ ] Tablo sütunları doğru sırada mı?
- [ ] Tablo responsive mi? (mobil görünümde scroll edilebiliyor mu?)

#### 2.2. Widget Verileri
- [ ] Widget'lar tabloda görünüyor mu?
- [ ] Widget adı (name) doğru görünüyor mu?
- [ ] Widget başlığı (title) doğru görünüyor mu?
- [ ] Widget kategorisi doğru görünüyor mu? (chip olarak)
- [ ] Widget tipi doğru görünüyor mu? (chip + icon)
- [ ] Aktif/Pasif durumu doğru görünüyor mu? (chip renkleri: success/error)
- [ ] Sıra (order) değeri doğru görünüyor mu?
- [ ] Oluşturulma tarihi doğru formatlanmış mı? (locale'e göre)
- [ ] Widget icon'u (mdi-widgets) görünüyor mu?

#### 2.3. Veri Formatlama
- [ ] Tarih formatı doğru mu? (locale'e göre: tr-TR, en-US, vb.)
- [ ] Kategori adı doğru görünüyor mu? (category ID yerine kategori adı)
- [ ] Tip icon'ları doğru mu?
  - [ ] card → mdi-card
  - [ ] chart → mdi-chart-line
  - [ ] table → mdi-table
  - [ ] banner → mdi-view-dashboard
- [ ] Aktif/Pasif chip renkleri doğru mu? (success/error)

---

### 3. Arama (Search)

#### 3.1. Arama Input
- [ ] Arama input'u görünüyor mu?
- [ ] Placeholder text doğru mu? ("Widget ara" veya translation)
- [ ] Arama icon'u (mdi-magnify) görünüyor mu?
- [ ] Clear butonu (X) görünüyor mu? (metin girildiğinde)

#### 3.2. Arama Fonksiyonelliği
- [ ] Metin girildiğinde arama yapılıyor mu? (500ms debounce)
- [ ] Arama widget adına (name) göre çalışıyor mu?
- [ ] Arama widget başlığına (title) göre çalışıyor mu?
- [ ] Arama case-insensitive mi? (büyük/küçük harf duyarsız)
- [ ] Arama sonuçları doğru filtreleniyor mu?
- [ ] Arama temizlendiğinde tüm widget'lar tekrar görünüyor mu?
- [ ] Arama yapıldığında sayfa numarası 1'e sıfırlanıyor mu?

#### 3.3. Arama Edge Cases
- [ ] Boş arama yapıldığında tüm widget'lar görünüyor mu?
- [ ] Olmayan bir widget adı arandığında "Widget bulunamadı" mesajı gösteriliyor mu?
- [ ] Özel karakterlerle arama yapılabiliyor mu? (örn: "test-widget", "widget_1")

---

### 4. Filtreleme (Filters)

#### 4.1. Kategori Filtresi
- [ ] Kategori dropdown'u görünüyor mu?
- [ ] "Tümü" seçeneği var mı?
- [ ] Tüm aktif kategoriler listede görünüyor mu?
- [ ] Kategori seçildiğinde widget'lar filtreleniyor mu?
- [ ] Kategori filtresi arama ile birlikte çalışıyor mu?
- [ ] Kategori filtresi temizlendiğinde ("Tümü") tüm widget'lar görünüyor mu?

#### 4.2. Tip Filtresi
- [ ] Tip dropdown'u görünüyor mu?
- [ ] "Tümü" seçeneği var mı?
- [ ] Tüm tipler listede görünüyor mu? (card, chart, table, banner)
- [ ] Tip seçildiğinde widget'lar filtreleniyor mu?
- [ ] Tip filtresi arama ile birlikte çalışıyor mu?
- [ ] Tip filtresi temizlendiğinde ("Tümü") tüm widget'lar görünüyor mu?

#### 4.3. Aktif/Pasif Filtresi
- [ ] Durum dropdown'u görünüyor mu?
- [ ] "Tümü", "Aktif", "Pasif" seçenekleri var mı?
- [ ] "Aktif" seçildiğinde sadece aktif widget'lar görünüyor mu?
- [ ] "Pasif" seçildiğinde sadece pasif widget'lar görünüyor mu?
- [ ] "Tümü" seçildiğinde tüm widget'lar görünüyor mu?
- [ ] Durum filtresi diğer filtrelerle birlikte çalışıyor mu?

#### 4.4. Çoklu Filtreleme
- [ ] Kategori + Tip + Durum filtreleri birlikte çalışıyor mu?
- [ ] Arama + Filtreler birlikte çalışıyor mu?
- [ ] Filtreler değiştirildiğinde sayfa numarası 1'e sıfırlanıyor mu?

---

### 5. Sayfalama (Pagination)

#### 5.1. Sayfalama Kontrolleri
- [ ] Sayfa numarası görünüyor mu?
- [ ] Toplam kayıt sayısı görünüyor mu?
- [ ] "Sayfa başına" dropdown'u görünüyor mu?
- [ ] Sayfa başına seçenekleri doğru mu? (10, 20, 50, 100)
- [ ] Varsayılan sayfa başına değeri 20 mi?

#### 5.2. Sayfalama Fonksiyonelliği
- [ ] Sayfa numarası değiştirildiğinde yeni sayfa yükleniyor mu?
- [ ] Sayfa başına değeri değiştirildiğinde sayfa 1'e sıfırlanıyor mu?
- [ ] Sayfa başına değeri değiştirildiğinde doğru sayıda widget gösteriliyor mu?
- [ ] Toplam sayfa sayısı doğru hesaplanıyor mu?
- [ ] "X - Y / Z gösteriliyor" bilgisi doğru mu?
- [ ] İlk sayfada "Önceki" butonu disabled mı?
- [ ] Son sayfada "Sonraki" butonu disabled mı?

#### 5.3. Sayfalama Edge Cases
- [ ] Tek sayfada tüm widget'lar varsa pagination gizleniyor mu?
- [ ] Toplam kayıt sayısı 0 ise "1 sayfa" gösteriliyor mu?
- [ ] Sayfa numarası toplam sayfa sayısını aşamıyor mu?

---

### 6. CRUD İşlemleri

#### 6.1. Create (Yeni Widget)
- [ ] "Yeni Widget" butonu görünüyor mu?
- [ ] Butona tıklandığında `/apps/widgets/new` sayfasına yönlendiriliyor mu?
- [ ] Buton doğru icon ve text'e sahip mi? (PlusIcon + "Yeni Widget")

#### 6.2. Read (Widget Görüntüleme)
- [ ] Widget listesi doğru yükleniyor mu?
- [ ] Widget detayları doğru görünüyor mu?
- [ ] Widget kategorisi doğru görünüyor mu?

#### 6.3. Update (Widget Düzenleme)
- [ ] Her widget satırında "Düzenle" butonu var mı?
- [ ] Düzenle butonu doğru icon'a sahip mi? (EditIcon)
- [ ] Düzenle butonuna tıklandığında `/apps/widgets/{id}/edit` sayfasına yönlendiriliyor mu?
- [ ] Widget ID doğru şekilde encode ediliyor mu? (URL'de özel karakterler)

#### 6.4. Delete (Widget Silme)
- [ ] Her widget satırında "Sil" butonu var mı?
- [ ] Sil butonu doğru icon'a sahip mi? (TrashIcon)
- [ ] Sil butonuna tıklandığında onay dialog'u açılıyor mu?
- [ ] Dialog başlığı doğru mu? ("Widget'ı sil")
- [ ] Dialog mesajı doğru mu? ("Bu widget'ı silmek istediğinizden emin misiniz?")
- [ ] "İptal" butonu dialog'u kapatıyor mu?
- [ ] "Evet, Sil" butonuna tıklandığında widget siliniyor mu?
- [ ] Silme işlemi başarılı olduğunda liste yenileniyor mu?
- [ ] Silme işlemi başarılı olduğunda dialog kapanıyor mu?
- [ ] Silme işlemi sırasında loading state gösteriliyor mu?
- [ ] Silme işlemi başarısız olduğunda hata mesajı gösteriliyor mu?

---

### 7. Refresh (Yenileme)

#### 7.1. Refresh Butonu
- [ ] Refresh butonu görünüyor mu?
- [ ] Refresh butonu doğru icon'a sahip mi? (RefreshIcon)
- [ ] Refresh butonuna tıklandığında widget listesi yenileniyor mu?
- [ ] Refresh sırasında loading state gösteriliyor mu?
- [ ] Refresh sırasında mevcut filtreler korunuyor mu?
- [ ] Refresh sırasında mevcut sayfa numarası korunuyor mu?

---

### 8. Hata Yönetimi (Error Handling)

#### 8.1. API Hataları
- [ ] Widget listesi yüklenirken hata oluşursa hata mesajı gösteriliyor mu?
- [ ] Hata mesajı kullanıcı dostu mu?
- [ ] Hata mesajı kapatılabilir mi? (closable)
- [ ] Hata mesajı kapatıldığında store'dan temizleniyor mu?
- [ ] Kategori listesi yüklenirken hata oluşursa sayfa çöküyor mu? (çökmemeli)

#### 8.2. Network Hataları
- [ ] Network hatası oluşursa uygun mesaj gösteriliyor mu?
- [ ] Retry mekanizması var mı? (refresh butonu ile)

---

### 9. Loading States

#### 9.1. Loading Göstergeleri
- [ ] İlk yükleme sırasında loading gösteriliyor mu?
- [ ] Refresh sırasında loading gösteriliyor mu?
- [ ] Sayfa değiştiğinde loading gösteriliyor mu?
- [ ] Filtre değiştiğinde loading gösteriliyor mu?
- [ ] Silme işlemi sırasında loading gösteriliyor mu?

---

### 10. Responsive ve UI/UX

#### 10.1. Responsive Tasarım
- [ ] Mobil görünümde tablo scroll edilebiliyor mu?
- [ ] Mobil görünümde filtreler alt alta mı?
- [ ] Mobil görünümde butonlar erişilebilir mi?
- [ ] Tablet görünümde layout doğru mu?

#### 10.2. Kullanıcı Deneyimi
- [ ] Tooltip'ler görünüyor mu? (refresh, edit, delete butonlarında)
- [ ] Hover efektleri çalışıyor mu?
- [ ] Butonlar tıklanabilir görünüyor mu?
- [ ] Disabled state'ler doğru mu? (loading sırasında)

---

### 11. Performans

#### 11.1. Yükleme Performansı
- [ ] Sayfa açılış süresi makul mu? (< 2 saniye)
- [ ] Widget listesi yüklenirken sayfa donmuyor mu?
- [ ] Arama debounce çalışıyor mu? (500ms)

#### 11.2. Veri Yönetimi
- [ ] Gereksiz API çağrıları yapılmıyor mu?
- [ ] Cache mekanizması çalışıyor mu? (store'da)

---

### 12. Entegrasyon Testleri

#### 12.1. Store Entegrasyonu
- [ ] `useWidgetStore` doğru kullanılıyor mu?
- [ ] Widget listesi store'dan geliyor mu?
- [ ] Kategori listesi store'dan geliyor mu?
- [ ] Store state değişiklikleri sayfaya yansıyor mu?

#### 12.2. Router Entegrasyonu
- [ ] Yönlendirmeler doğru çalışıyor mu?
- [ ] URL parametreleri doğru mu?
- [ ] Browser back/forward butonları çalışıyor mu?

#### 12.3. i18n Entegrasyonu
- [ ] Tüm metinler translation key'lerinden geliyor mu?
- [ ] Dil değiştiğinde sayfa metinleri güncelleniyor mu?
- [ ] Fallback metinler doğru mu? (translation yoksa)

---

## ✅ Test Kontrol Listesi

### Kritik Testler (Must Have)
- [ ] Sayfa açılıyor
- [ ] Widget listesi yükleniyor
- [ ] Arama çalışıyor
- [ ] Filtreleme çalışıyor
- [ ] Sayfalama çalışıyor
- [ ] Yeni Widget butonu çalışıyor
- [ ] Düzenle butonu çalışıyor
- [ ] Sil butonu çalışıyor (onay dialog ile)
- [ ] Refresh butonu çalışıyor
- [ ] Hata durumları yönetiliyor

### İyileştirme Testleri (Should Have)
- [ ] Loading state'ler çalışıyor
- [ ] Empty state gösteriliyor
- [ ] Responsive tasarım çalışıyor
- [ ] Tooltip'ler görünüyor
- [ ] Performans makul

### Nice to Have
- [ ] Keyboard navigation
- [ ] Accessibility (ARIA labels)
- [ ] Animasyonlar

---

## 🐛 Bilinen Sorunlar ve Çözümler

### Sorun 1: Widget Icon Import Hatası
**Durum:** ✅ Çözüldü  
**Açıklama:** `WidgetsIcon` `vue-tabler-icons` paketinde yoktu.  
**Çözüm:** Vuetify'nin `mdi-widgets` icon'u kullanıldı.

### Sorun 2: Sayfa Açılmıyordu
**Durum:** ✅ Çözüldü  
**Açıklama:** `definePageMeta` ve icon import hatası nedeniyle sayfa render edilemiyordu.  
**Çözüm:** `definePageMeta` kaldırıldı, icon düzeltildi.

---

## 📝 Test Senaryoları

### Senaryo 1: Temel Liste Görüntüleme
1. `/apps/widgets` sayfasına git
2. Widget listesinin yüklendiğini kontrol et
3. En az 5 widget görünüyor mu kontrol et
4. Tüm sütunların göründüğünü kontrol et

### Senaryo 2: Arama ve Filtreleme
1. Arama kutusuna "test" yaz
2. Sonuçların filtrelendiğini kontrol et
3. Kategori filtresinden bir kategori seç
4. Sonuçların daha da filtrelendiğini kontrol et
5. Tip filtresinden "card" seç
6. Sonuçların doğru filtrelendiğini kontrol et

### Senaryo 3: CRUD İşlemleri
1. "Yeni Widget" butonuna tıkla
2. Widget oluştur
3. Listeye dön ve yeni widget'ın göründüğünü kontrol et
4. Widget'ı düzenle
5. Değişikliklerin yansıdığını kontrol et
6. Widget'ı sil (onay ver)
7. Widget'ın listeden kaldığını kontrol et

### Senaryo 4: Sayfalama
1. Sayfa başına 10 seç
2. İlk 10 widget'ın göründüğünü kontrol et
3. 2. sayfaya git
4. Sonraki 10 widget'ın göründüğünü kontrol et
5. Toplam sayfa sayısının doğru olduğunu kontrol et

---

## 🎯 Başarı Kriterleri

Test başarılı sayılır eğer:
- ✅ Tüm kritik testler geçiyor
- ✅ Sayfa hatasız açılıyor
- ✅ Tüm CRUD işlemleri çalışıyor
- ✅ Arama ve filtreleme çalışıyor
- ✅ Sayfalama çalışıyor
- ✅ Hata durumları yönetiliyor
- ✅ Loading state'ler çalışıyor
- ✅ Responsive tasarım çalışıyor

---

**Son Güncelleme:** Ocak 2026  
**Test Edilen Versiyon:** Widget List Sayfası v1.0
