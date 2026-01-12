# Automated Forms Kullanım Kılavuzu

**Son Güncelleme:** 12 Ocak 2026  
**Versiyon:** 1.0

---

## 📋 Genel Bakış

Automated Forms (AF) sistemi, herhangi bir dataset için otomatik olarak dinamik form oluşturmanıza ve bu form ile CRUD (Create, Read, Update, Delete) işlemleri yapmanıza olanak sağlar.

### Özellikler

- ✅ Dataset bazlı otomatik form oluşturma
- ✅ Dinamik liste görünümü (pagination, sorting, filtering, search)
- ✅ CRUD işlemleri (oluştur, oku, güncelle, sil)
- ✅ Tüm field type desteği (text, number, bool, datetime, object, relation, persons, personGroups, incremental)
- ✅ Relation field'lar için ID ve display field seçimi
- ✅ Array field'lar için multiple seçim
- ✅ Field layout ayarları (column span, group)
- ✅ Validation rules desteği
- ✅ Side Menu entegrasyonu

---

## 🚀 Hızlı Başlangıç

### 1. Form Oluşturma

1. **Automated Forms** sayfasına gidin (`/apps/automated-forms`)
2. **Yeni Form** butonuna tıklayın
3. **Temel Bilgiler** tab'ında:
   - Form Adı: Örn: "Books Management"
   - Form Kodu: Örn: "books-form" (unique, alphanumeric, underscore, dash)
   - Dataset: Form için kullanılacak dataset'i seçin
   - Açıklama: (Opsiyonel)
   - Aktif: Form aktif mi?

### 2. Liste Ayarları

**Liste Ayarları** tab'ında:
- Her field için liste görünümü ayarları:
  - **Görünür**: Liste görünümünde gösterilsin mi?
  - **Sıralama**: Sıralanabilir mi?
  - **Filtreleme**: Filtrelenebilir mi?
  - **Sıra**: Sütun sıralaması (0, 1, 2, ...)
- **Varsayılan Sıralama**: Hangi field'a göre sıralanacak?
- **Sıralama Yönü**: Artan/Azalan
- **Arama Kullan**: Global search aktif olsun mu?

### 3. Form Ayarları

**Form Ayarları** tab'ında:

#### Genel Ayarlar
- **Gösterilecek Field'lar**: Form'da gösterilecek field'ları seçin (boş ise tümü)
- **Read-only Field'lar**: Read-only field'ları seçin

#### Relation Field Ayarları
Her relation field için:
- **Display Field**: Dropdown'da gösterilecek field (örn: `name`, `title`)
- **ID Field**: Değer olarak kullanılacak field (genellikle `__dataId`)

#### Field Layout Ayarları
Her field için:
- **Sütun Genişliği**: 1-12 arası (default: 6, object için 12)
- **Grup**: Field grubu (gelecek geliştirme için)

### 4. Formu Kaydetme

**Oluştur** veya **Güncelle** butonuna tıklayarak formu kaydedin.

---

## 📖 Detaylı Kullanım

### Relation Field Konfigürasyonu

Relation field'lar için dropdown'da gösterilecek field'ı seçebilirsiniz:

**Örnek Senaryo:**
- Dataset: `tst_books`
- Field: `publisher` (relation type, `tst_publishers` dataset'ine referans)
- Display Field: `name` (dropdown'da publisher adı gösterilir)
- ID Field: `__dataId` (değer olarak publisher ID kullanılır)

**Sonuç:**
- Dropdown'da publisher adları gösterilir
- Seçildiğinde publisher ID'si kaydedilir

### Array Field Desteği

`isArray: true` olan relation field'lar için multiple seçim yapabilirsiniz:

**Örnek Senaryo:**
- Field: `genres` (relation type, array, `tst_genres` dataset'ine referans)
- Display Field: `name`
- ID Field: `__dataId`

**Kullanım:**
- Dropdown'da birden fazla genre seçebilirsiniz
- Tek genre seçseniz bile değer otomatik olarak array'e çevrilir: `["genre-id"]`
- Birden fazla seçildiğinde: `["genre-id-1", "genre-id-2"]`

### Field Layout Ayarları

Her field için form görünümünde kaç sütun kaplayacağını ayarlayabilirsiniz:

**Column Span Değerleri:**
- `1-12`: Bootstrap/Vuetify grid sistemi
- Default: Normal field'lar için `6`, object field'lar için `12`

**Örnek:**
- `title` field: Column span = `6` (form genişliğinin yarısı)
- `description` field: Column span = `12` (tam genişlik)
- `customData` (object): Column span = `12` (tam genişlik)

---

## 🔧 API Kullanımı

### Form Metadata CRUD

**List Forms:**
```typescript
GET /api/v1/data/@automated_forms?pageNumber=1&pageSize=20
```

**Get Form by Code:**
```typescript
GET /api/v1/data/@automated_forms?formCode=books-form
```

**Create Form:**
```typescript
POST /api/v1/data/@automated_forms
Body: CreateAutomatedFormDto
```

**Update Form:**
```typescript
PUT /api/v1/data/@automated_forms/{dataId}
Body: UpdateAutomatedFormDto
```

**Delete Form:**
```typescript
DELETE /api/v1/data/@automated_forms/{dataId}
```

### Runtime Data CRUD

**List Data:**
```typescript
GET /api/v1/data/{dataset}?skip=0&limit=20&sort=field1&filter=field:operator:value&search=term
```

**Get Single Item:**
```typescript
GET /api/v1/data/{dataset}/{id}?expand=true
```

**Create Item:**
```typescript
POST /api/v1/data/{dataset}
Body: { fieldName: value, ... }
```

**Update Item:**
```typescript
PUT /api/v1/data/{dataset}/{id}
Body: { fieldName: value, ... }
```

**Delete Item:**
```typescript
DELETE /api/v1/data/{dataset}/{id}
```

---

## 🐛 Sorun Giderme

### Form Yüklenmiyor

- **Sorun**: Form açıldığında yanlış form yükleniyor
- **Çözüm**: Route parametresi değişikliği izleniyor, sayfayı yenileyin

### Relation Field'da ID Gösteriliyor

- **Sorun**: Dropdown'da ID gösteriliyor, text gösterilmiyor
- **Çözüm**: Form Ayarları > Relation Field Ayarları'nda Display Field'ı seçin

### Array Field Tek Değer Gönderiyor

- **Sorun**: Array field için tek değer seçildiğinde array formatında gönderilmiyor
- **Çözüm**: Sistem otomatik olarak array'e çeviriyor, kontrol edin

### Hata Mesajı Gösterilmiyor

- **Sorun**: API'den validation hatası geliyor ama "internal server error" gösteriliyor
- **Çözüm**: Validation error details parse ediliyor, detaylı mesaj gösteriliyor

---

## 📚 İlgili Dokümantasyon

- [Automated Forms Planlama](AUTOMATED_FORMS_PLANNING.md)
- [Güncel Durum](../../Mng.Ui/current_status.md)
- [Dataset Yapısı](../../MngDataGateway/specs/DATASET_SCHEMA.md)

---

## ✅ Sonraki Adımlar

Gelecek geliştirmeler:
- Permission kontrolü entegrasyonu
- Kullanıcı bazlı sütun ayarları (localStorage)
- Export işlemleri (CSV, JSON)
- ColumnSelector component (drag & drop)
- Bulk operations
- Form templates
- Conditional fields
- Form actions (workflow, notification)
- Form versions

---

**Not:** Bu dokümantasyon Automated Forms sisteminin temel kullanımını açıklar. Detaylı teknik bilgiler için planlama dokümantasyonuna bakınız.
