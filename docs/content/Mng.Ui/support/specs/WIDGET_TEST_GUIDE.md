# Widget Sistemi Test Rehberi

Bu doküman, widget sisteminin test edilmesi için adım adım rehber içerir.

## 📋 Test Öncesi Gereksinimler

1. **MngDataGateway** servisi çalışıyor olmalı
2. **Mng.Ui** uygulaması çalışıyor olmalı
3. **Giriş yapılmış** olmalı (authentication token gerekli)

---

## 🚀 Test Adımları

### 1. Dataset'leri Oluşturma

#### 1.1. Widget Categories Dataset'i Oluştur

**Yöntem 1: API ile (Postman/Thunder Client/curl)**

```bash
POST /api/v1/datasets
Content-Type: application/json
Authorization: Bearer {token}

Body:
{
  "name": "@widget_categories",
  "description": "Widget kategorileri dataset'i",
  "forceSchema": true,
  "logging": "none",
  "publishMode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "name",
      "title": "Kategori Adı",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "description",
      "title": "Açıklama",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "icon",
      "title": "Icon",
      "mandatory": false
    },
    {
      "fieldType": "text",
      "name": "color",
      "title": "Renk",
      "mandatory": false
    },
    {
      "fieldType": "number",
      "name": "order",
      "title": "Sıralama",
      "mandatory": false,
      "defaultValue": 0
    },
    {
      "fieldType": "bool",
      "name": "isActive",
      "title": "Aktif",
      "mandatory": true,
      "defaultValue": true
    }
  ],
  "indexList": [
    {
      "name": "idx_name",
      "fields": { "name": 1 },
      "unique": true
    },
    {
      "name": "idx_order",
      "fields": { "order": 1 }
    }
  ]
}
```

**Yöntem 2: UI ile**
- `/apps/datasets` sayfasına git
- "Yeni Dataset" butonuna tıkla
- `docs/Mng.Ui/specs/datasets/widget-categories-dataset-create.json` dosyasındaki içeriği kopyala-yapıştır

#### 1.2. Widgets Dataset'i Oluştur

Aynı yöntemle `@widgets` dataset'ini oluştur:
- Dosya: `docs/Mng.Ui/specs/datasets/widgets-dataset-create.json`

---

### 2. Seed Data Ekleme (Widget Categories)

#### 2.1. Kategorileri Ekle

```bash
POST /api/v1/data/@widget_categories
Content-Type: application/json
Authorization: Bearer {token}

Body: (Array)
[
  {
    "name": "card",
    "description": "Card widget'ları - istatistik kartları, hızlı erişim kartları vb.",
    "icon": "mdi-card",
    "color": "primary",
    "order": 1,
    "isActive": true
  },
  {
    "name": "chart",
    "description": "Grafik widget'ları - line, bar, pie, donut grafikleri vb.",
    "icon": "mdi-chart-line",
    "color": "info",
    "order": 2,
    "isActive": true
  },
  {
    "name": "table",
    "description": "Tablo widget'ları - veri tabloları, listeler vb.",
    "icon": "mdi-table",
    "color": "success",
    "order": 3,
    "isActive": true
  },
  {
    "name": "banner",
    "description": "Banner widget'ları - bilgilendirme banner'ları, duyurular vb.",
    "icon": "mdi-view-dashboard",
    "color": "warning",
    "order": 4,
    "isActive": true
  }
]
```

**Not:** `docs/Mng.Ui/specs/datasets/widget-categories-seed.json` dosyasındaki içeriği kullanabilirsiniz.

---

### 3. Test Widget'ları Oluşturma

#### 3.1. Örnek StatCard Widget (Basit)

```bash
POST /api/v1/data/@widgets
Content-Type: application/json
Authorization: Bearer {token}

Body:
{
  "name": "test_total_users",
  "title": "Toplam Kullanıcı",
  "description": "Sistemdeki toplam kullanıcı sayısı",
  "category": "{category_id}",  // "card" kategorisinin __dataId'si
  "type": "card",
  "isActive": true,
  "order": 1,
  "dataSource": {
    "type": "data",
    "dataset": "@users",  // Veya mevcut bir dataset
    "getMethod": "default",
    "default": {
      "limit": 1,
      "fields": "count"
    }
  },
  "config": {
    "icon": "mdi-account-group",
    "color": "primary",
    "format": "number",
    "decimalPlaces": 0
  }
}
```

#### 3.2. Örnek StatCard Widget (Query ile)

```bash
POST /api/v1/data/@widgets
Content-Type: application/json
Authorization: Bearer {token}

Body:
{
  "name": "test_active_tasks",
  "title": "Aktif Görevler",
  "description": "Aktif durumdaki görev sayısı",
  "category": "{category_id}",
  "type": "card",
  "isActive": true,
  "order": 2,
  "dataSource": {
    "type": "data",
    "dataset": "@tasks",  // Veya mevcut bir dataset
    "getMethod": "query",
    "query": {
      "match": {
        "status": "active"
      },
      "limit": 1
    }
  },
  "config": {
    "icon": "mdi-check-circle",
    "color": "success",
    "format": "number",
    "valueField": "count"
  }
}
```

#### 3.3. Örnek StatCard Widget (Aggregate ile)

```bash
POST /api/v1/data/@widgets
Content-Type: application/json
Authorization: Bearer {token}

Body:
{
  "name": "test_total_revenue",
  "title": "Toplam Gelir",
  "description": "Toplam gelir miktarı",
  "category": "{category_id}",
  "type": "card",
  "isActive": true,
  "order": 3,
  "dataSource": {
    "type": "data",
    "dataset": "@orders",  // Veya mevcut bir dataset
    "getMethod": "aggregate",
    "aggregate": {
      "pipeline": [
        {
          "$group": {
            "_id": null,
            "total": { "$sum": "$amount" }
          }
        }
      ]
    }
  },
  "config": {
    "icon": "mdi-currency-usd",
    "color": "success",
    "format": "currency",
    "decimalPlaces": 2,
    "valueField": "total"
  }
}
```

**Önemli:** `category` alanına, `@widget_categories` dataset'inden aldığınız "card" kategorisinin `__dataId` değerini yazmalısınız.

---

### 4. Dashboard'da Widget Kullanma

#### 4.1. Dashboard Oluştur/Düzenle

1. `/apps/dashboards` sayfasına git
2. Yeni dashboard oluştur veya mevcut bir dashboard'u düzenle
3. Layout Editor'de bir column seç
4. "Widget Seç" butonuna tıkla (mdi-widgets ikonu)
5. Widget Picker Modal'da:
   - Kategorilere göre filtrele
   - Tipe göre filtrele
   - Arama yap
   - Bir widget seç
6. Widget ID otomatik olarak column'a atanır
7. Dashboard'u kaydet

#### 4.2. Dashboard'u Görüntüle

1. Dashboard listesinden bir dashboard'u seç
2. "Önizleme" butonuna tıkla veya slug ile `/dashboards/{slug}` sayfasına git
3. Widget'ların render edildiğini kontrol et:
   - ✅ Widget yükleniyor mu?
   - ✅ Veri çekiliyor mu?
   - ✅ StatCard doğru görünüyor mu?
   - ✅ Loading state çalışıyor mu?
   - ✅ Error state çalışıyor mu?

---

### 5. Test Senaryoları

#### Senaryo 1: Basit StatCard Widget
- **Amaç:** Default getMethod ile veri çekme
- **Beklenen:** Widget render edilir, veri gösterilir

#### Senaryo 2: Query ile StatCard Widget
- **Amaç:** Query getMethod ile filtreli veri çekme
- **Beklenen:** Filtrelenmiş veri gösterilir

#### Senaryo 3: Aggregate ile StatCard Widget
- **Amaç:** Aggregate getMethod ile toplam/hesaplama
- **Beklenen:** Hesaplanmış değer gösterilir

#### Senaryo 4: Widget Picker
- **Amaç:** Widget seçimi ve filtreleme
- **Beklenen:** 
  - Widget listesi yüklenir
  - Kategori filtreleme çalışır
  - Tip filtreleme çalışır
  - Arama çalışır
  - Widget seçimi çalışır

#### Senaryo 5: Hata Durumları
- **Amaç:** Hata handling testi
- **Testler:**
  - Geçersiz widget ID
  - Dataset bulunamadı
  - Veri çekilemedi
  - Widget tipi desteklenmiyor

#### Senaryo 6: Nested Rows ile Widget
- **Amaç:** İç içe satırlarda widget kullanımı
- **Beklenen:** Nested row içindeki widget'lar da render edilir

---

### 6. Kontrol Listesi

#### Dataset Kontrolleri
- [ ] `@widget_categories` dataset'i oluşturuldu
- [ ] `@widgets` dataset'i oluşturuldu
- [ ] Kategoriler eklendi (card, chart, table, banner)

#### Widget Kontrolleri
- [ ] En az 1 test widget'ı oluşturuldu
- [ ] Widget'lar aktif durumda (`isActive: true`)
- [ ] Widget'ların `category` alanı doğru kategorilere bağlı
- [ ] Widget'ların `dataSource` yapılandırması doğru

#### UI Kontrolleri
- [ ] Widget Picker Modal açılıyor
- [ ] Widget listesi yükleniyor
- [ ] Filtreleme çalışıyor
- [ ] Widget seçimi çalışıyor
- [ ] Dashboard'da widget render ediliyor
- [ ] Widget verisi çekiliyor
- [ ] StatCard görünüyor

#### Fonksiyonel Kontroller
- [ ] Default getMethod çalışıyor
- [ ] Query getMethod çalışıyor
- [ ] Aggregate getMethod çalışıyor
- [ ] Loading state gösteriliyor
- [ ] Error state gösteriliyor
- [ ] Empty state gösteriliyor

---

### 7. Sorun Giderme

#### Widget yüklenmiyor
- ✅ Widget ID doğru mu? (Browser console'da kontrol et)
- ✅ Widget `isActive: true` mi?
- ✅ Widget Store'da widget var mı? (`useWidgetStore().widgets`)

#### Veri çekilmiyor
- ✅ `dataSource.dataset` doğru mu?
- ✅ Dataset mevcut mu?
- ✅ `getMethod` doğru mu?
- ✅ Query/Aggregate yapılandırması doğru mu?
- ✅ Browser console'da hata var mı?
- ✅ Network tab'de API çağrısı başarılı mı?

#### Widget Picker boş
- ✅ `@widgets` dataset'inde widget var mı?
- ✅ Widget'lar `isActive: true` mi?
- ✅ Browser console'da hata var mı?
- ✅ Widget Store'da widget'lar yüklendi mi?

#### StatCard görünmüyor
- ✅ Widget tipi `"card"` mi?
- ✅ `WidgetRenderer` component'i doğru import edilmiş mi?
- ✅ `StatCard` component'i doğru import edilmiş mi?

---

### 8. Hızlı Test Script'i

Browser Console'da çalıştırabileceğiniz test script'i:

```javascript
// Widget Store'u test et
const widgetStore = useWidgetStore();

// Kategorileri yükle
await widgetStore.fetchWidgetCategories();
console.log('Categories:', widgetStore.categories);

// Widget'ları yükle
await widgetStore.fetchWidgets();
console.log('Widgets:', widgetStore.widgets);

// Aktif widget'ları kontrol et
console.log('Active widgets:', widgetStore.activeWidgets);
```

---

## 📝 Notlar

1. **Category ID:** Widget oluştururken `category` alanına string (__dataId) yazılmalı, object değil.

2. **Dataset İsimleri:** Dataset isimleri `@` ile başlamalı (örn: `@widgets`, `@users`).

3. **getMethod:** 
   - `default`: GET request
   - `query`: POST request (match objesi gerekli)
   - `aggregate`: POST request (pipeline array gerekli)
   - `predefined`: POST request (queryName gerekli)

4. **Config:** StatCard için `config` objesi:
   ```json
   {
     "icon": "mdi-icon-name",
     "color": "primary|success|error|warning|info",
     "format": "number|currency|percentage",
     "decimalPlaces": 0,
     "valueField": "fieldName",  // Veriden hangi alanı alacağı
     "trendField": "fieldName"   // Trend için hangi alanı kullanacağı
   }
   ```

---

## ✅ Başarı Kriterleri

Test başarılı sayılır eğer:
- ✅ Tüm dataset'ler oluşturuldu
- ✅ Widget'lar oluşturuldu ve aktif
- ✅ Widget Picker çalışıyor
- ✅ Dashboard'da widget'lar render ediliyor
- ✅ Widget'lar veri çekiyor
- ✅ StatCard doğru görünüyor
- ✅ Loading/Error/Empty state'ler çalışıyor

---

**Son Güncelleme:** 2024-12-19
