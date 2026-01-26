# Mng.Ui - Güncel Durum

**Son Güncelleme:** 12 Ocak 2026  
**Aktif Çalışma:** Automated Forms (AF) - Geliştirme ve İyileştirmeler

---

## 📍 Son Çalışılan Konu

**Automated Forms (AF) - Relation Field Konfigürasyonu ve Array Field Desteği**

Relation field'lar için ID ve display field seçimi, field layout ayarları ve array field'lar için multiple seçim desteği eklendi. Hata mesajı gösterimi iyileştirildi ve route parametresi değişikliği bug'ı düzeltildi.

---

## ✅ Tamamlanan İşler

### Automated Forms - Temel Altyapı
- ✅ `@automated_forms` dataset'i backend'de oluşturuldu
- ✅ `automatedForms` Pinia store oluşturuldu
- ✅ Form listesi sayfası (`/apps/automated-forms`)
- ✅ Form oluşturma sayfası (`/apps/automated-forms/create`)
- ✅ Form düzenleme sayfası (`/apps/automated-forms/edit/[formCode]`)

### Automated Forms - Form Tanımlama
- ✅ Temel bilgiler formu (formName, formCode, description, datasetName, isActive)
- ✅ Liste ayarları (columns konfigürasyonu - visible, order, sortable, filterable)
- ✅ Form ayarları (visibleFields, readonlyFields, fieldOrder)
- ✅ Tab yapısı (Temel Bilgiler, Liste Ayarları, Form Ayarları)
- ✅ enableSearch özelliği eklendi
- ✅ **Relation Field Konfigürasyonu**: Her relation field için ID ve display field seçimi
- ✅ **Field Layout Ayarları**: Her field için column span ve group ayarları

### Automated Forms - Side Menu Entegrasyonu
- ✅ Side Menu Manager'a "Kayıtlı Formlar" dropdown'u eklendi
- ✅ Form seçildiğinde otomatik route path oluşturuluyor (`/apps/automated-forms/view/{formCode}`)
- ✅ Edit modunda form seçimi ön-seçili geliyor

### Automated Forms - Runtime Sayfası
- ✅ Form runtime sayfası (`/apps/automated-forms/view/[formCode]`)
- ✅ Dinamik liste görünümü (v-data-table)
- ✅ Server-side pagination
- ✅ Server-side sorting (MongoDB style: "field1,-field2")
- ✅ Field-based filtering (filterable fields için)
- ✅ Global search (enableSearch true ise)
- ✅ Liste ayarlarına göre sütun görünürlüğü ve sıralaması

### Automated Forms - CRUD İşlemleri
- ✅ **DynamicFormField Component**: Tüm field type'ları destekleniyor
  - text, number, bool, datetime, object, relation, persons, personGroups, incremental
  - Validation rules uygulanıyor
  - Readonly/disabled desteği
  - **Array field desteği**: `isArray: true` olan relation field'lar için multiple seçim
  - **Relation field display**: Form config'den display field kullanımı
- ✅ **Create Dialog**: Yeni kayıt oluşturma formu
- ✅ **Edit Dialog**: Kayıt düzenleme formu (API'den veri çekip alanları doldurma)
- ✅ **Delete Dialog**: Kayıt silme onay dialog'u
- ✅ **API Entegrasyonu**: POST (create), PUT (update), DELETE (delete), GET (single item)
- ✅ **Array field değer dönüşümü**: Tek değer seçilse bile array field'lar için otomatik array'e çevirme
- ✅ **Gelişmiş hata mesajı gösterimi**: Validation error details parse ediliyor ve detaylı gösteriliyor

---

## 🔄 Devam Eden İşler

### Automated Forms - İyileştirmeler
- ⏳ Permission kontrolü entegrasyonu (usePagePermissions composable ile)
- ⏳ DOM element yetkilendirme (create, update, delete butonları için permission kontrolü)

---

## 📋 Sonraki Adımlar

### Öncelikli (Kısa Vadeli)
1. **Permission Kontrolü**: CRUD işlemlerinde permission kontrolü eklenmeli
   - Create butonu için canCreate kontrolü
   - Edit butonu için canUpdate kontrolü
   - Delete butonu için canDelete kontrolü
   - usePagePermissions composable'ı kullanılmalı

2. **Kullanıcı Bazlı Sütun Ayarları**: Kullanıcıların kendi sütun tercihlerini kaydetmesi
   - localStorage'da kullanıcı bazlı saklama
   - Sütun görünürlüğü ve sıralaması

3. **Export İşlemleri**: ✅ TAMAMLANDI (Client-side CSV ve JSON export)
   - Export butonları eklendi (toolbar'da download menu)
   - JSON ve CSV export fonksiyonları implement edildi
   - Filtre ve arama parametreleri export'a dahil ediliyor
   - **Gelecek**: Server-side streaming export (büyük veri setleri için)

### Orta Vadeli
4. **ColumnSelector Component**: Sütun seçimi için drag & drop component
5. **Form Görüntüleme Sayfası**: Detay görünümü (opsiyonel)
6. **Bulk Operations**: Toplu işlemler (seçili kayıtları sil, toplu güncelleme)

### Uzun Vadeli (Future Enhancements)
7. **Form Template'leri**: Önceden tanımlanmış form şablonları
8. **Conditional Fields**: Koşullu field gösterimi
9. **Form Actions**: Özel form action'ları (workflow, notification)
10. **Form Versions**: Form versiyonlama

---

## 🔧 Teknik Notlar

### API Endpoint'leri
- **List**: `GET /api/v1/data/{dataset}?skip=0&limit=20&sort=field1&filter=field:operator:value&search=term`
- **Get Single**: `GET /api/v1/data/{dataset}/{id}?expand=true` (array döndürüyor, ilk eleman kullanılmalı)
- **Create**: `POST /api/v1/data/{dataset}` (body: field values)
- **Update**: `PUT /api/v1/data/{dataset}/{id}` (body: field values)
- **Delete**: `DELETE /api/v1/data/{dataset}/{id}`

### Form Config Yapısı
- **listConfig.columns**: Her field için visible, order, sortable, filterable ayarları
- **formConfig.visibleFields**: Form'da gösterilecek field'lar
- **formConfig.readonlyFields**: Read-only field'lar
- **formConfig.fieldOrder**: Field sıralaması
- **formConfig.relationFieldConfig**: Relation field'lar için ID ve display field seçimi
  ```typescript
  {
    [fieldName: string]: {
      idField: string;        // Değer olarak kullanılacak field (default: '__dataId')
      displayField: string;   // Dropdown'da gösterilecek field
    }
  }
  ```
- **formConfig.fieldLayout**: Field layout ayarları (column span, group)
  ```typescript
  {
    [fieldName: string]: {
      columnSpan?: number;    // 1-12 (default: 6 for normal, 12 for object)
      group?: string;         // Field group name
    }
  }
  ```

### Önemli Düzeltmeler
- ✅ Backend API response formatı: GetById her zaman array döndürüyor (tek item olsa bile)
- ✅ Skip/Limit kullanımı: Backend pageNumber/pageSize kullanmıyor, skip/limit kullanıyor
- ✅ Sort formatı: MongoDB style ("field1" veya "-field1")
- ✅ Filter formatı: RESTful style ("field:operator:value")
- ✅ **Route parametresi değişikliği izleme**: FormCode route parametresi değiştiğinde form otomatik yeniden yükleniyor
- ✅ **FormCode doğrulaması**: Store'da fetchFormByCode metodunda exact match kontrolü
- ✅ **Cache temizleme**: Route değiştiğinde store'daki currentForm temizleniyor

---

## 📝 Önemli Notlar

1. **DynamicFormField Component**: Tüm field type'ları destekleniyor, validation rules uygulanıyor
2. **Form Dialog**: Create ve Edit için aynı dialog kullanılıyor, mode'a göre davranış değişiyor
3. **Liste Ayarları**: Form tanımlama ekranında detaylı sütun konfigürasyonu yapılabiliyor
4. **Side Menu**: Form seçimi dropdown'dan yapılabiliyor, otomatik route path oluşturuluyor
5. **API Entegrasyonu**: Tüm CRUD işlemleri çalışıyor, test edildi
6. **Relation Field Konfigürasyonu**: Form tanımlama ekranında "Form Ayarları" tab'ında relation field'lar için ID ve display field seçimi yapılabiliyor
7. **Array Field Desteği**: `isArray: true` olan relation field'lar için multiple seçim yapılabiliyor, değerler otomatik olarak array formatına çevriliyor
8. **Field Layout**: Her field için form görünümünde kaç sütun kaplayacağı (column span) ve hangi gruba ait olduğu ayarlanabiliyor
9. **Hata Mesajı Gösterimi**: API'den gelen validation error details parse ediliyor ve detaylı olarak gösteriliyor
10. **Route Değişikliği İzleme**: FormCode route parametresi değiştiğinde form otomatik olarak yeniden yükleniyor

---

## 🐛 Bilinen Sorunlar

Şu anda bilinen bir sorun yok. CRUD işlemleri başarıyla çalışıyor, relation field'lar ve array field'lar düzgün çalışıyor.

---

## 📚 İlgili Dosyalar

### Kod Dosyaları
- `Mng.Ui/stores/apps/automatedForms.ts` - Automated Forms store
- `Mng.Ui/pages/apps/automated-forms/index.vue` - Form listesi sayfası
- `Mng.Ui/pages/apps/automated-forms/create.vue` - Form oluşturma sayfası
- `Mng.Ui/pages/apps/automated-forms/edit/[formCode].vue` - Form düzenleme sayfası
- `Mng.Ui/pages/apps/automated-forms/view/[formCode].vue` - Runtime CRUD sayfası
- `Mng.Ui/components/apps/automated-forms/AutomatedFormForm.vue` - Form tanımlama component'i
- `Mng.Ui/components/apps/automated-forms/DynamicFormField.vue` - Dinamik form field component'i

### Dokümantasyon
- `docs/Mng.Ui/specs/AUTOMATED_FORMS_PLANNING.md` - Planlama dokümantasyonu
- `docs/Mng.Ui/guides/AUTOMATED_FORMS_USAGE.md` - Kullanım kılavuzu
- `docs/Mng.Ui/current_status.md` - Güncel durum (bu dosya)

---

**Not:** Bu dosya her çalışma oturumunun sonunda güncellenmelidir.
