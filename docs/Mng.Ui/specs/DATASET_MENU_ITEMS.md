# Dataset Menu Items - Side Menu Ekleme Rehberi

## Genel Bakış

Bu dokümantasyon, Dataset yönetim sayfalarını Side Menu'ye eklemek için gerekli bilgileri içerir.

## Side Menu Manager'a Erişim

1. **URL**: `/apps/side-menu-manager`
2. **Erişim**: Manager veya Admin yetkisi gereklidir
3. **Kullanım**: Side Menu Manager sayfasında sol taraftan "Yeni Menu Item" veya "Yeni Header" butonuna tıklayarak yeni menu item'ları ekleyebilirsiniz.

---

## Dataset Menu Items - Önerilen Yapı

### Senaryo 1: Dataset Categories ve Datasets Ayrı Menu Items (Önerilen)

#### 1. Dataset Categories Menu Item

**Zorunlu Alanlar:**
- **Item Tipi**: `Menu Item`
- **Sayfa Tipi**: `admin` (veya `manager`)
- **Menü Başlığı**: `Dataset Kategorileri` (veya `Dataset Categories`)
- **Route Path**: `/apps/dataset-categories`
- **Link Tipi**: `Internal`
- **Sıralama (Order)**: İstediğiniz sıraya göre (örn: `100`)
- **Parent Menu Item**: `(Yok - Root)` veya bir Header seçebilirsiniz
- **Seviye (Level)**: `0` (eğer root ise) veya parent'ın level + 1
- **Devre Dışı**: `Hayır`

**Opsiyonel Alanlar:**
- **Sayfa Kodu (Page Code)**: `dataset-categories` (otomatik oluşturulabilir - Route butonuna tıklayın)
- **Icon Type**: `tabler` (veya `mdi`)
- **Icon**: `DatabaseIcon` (tabler için) veya `mdi-database` (mdi için)
- **Alt Başlık (Sub Caption)**: `Dataset kategorilerini yönetin`
- **Chip Metni**: Boş (veya istediğiniz bir badge)

**Örnek Konfigürasyon:**
```
Item Tipi: Menu Item
Sayfa Tipi: admin
Menü Başlığı: Dataset Kategorileri
Route Path: /apps/dataset-categories
Link Tipi: Internal
Icon Type: tabler
Icon: DatabaseIcon
Sayfa Kodu: dataset-categories (otomatik oluşturulabilir)
Sıralama: 100
Parent: (Yok - Root)
Level: 0
Devre Dışı: Hayır
```

---

#### 2. Datasets Menu Item (Ana Menü)

**Zorunlu Alanlar:**
- **Item Tipi**: `Menu Item`
- **Sayfa Tipi**: `admin` (veya `manager`)
- **Menü Başlığı**: `Datasets` (veya `Dataset Yönetimi`)
- **Route Path**: `/apps/datasets`
- **Link Tipi**: `Internal`
- **Sıralama (Order)**: İstediğiniz sıraya göre (örn: `101`)
- **Parent Menu Item**: `(Yok - Root)` veya bir Header seçebilirsiniz
- **Seviye (Level)**: `0` (eğer root ise) veya parent'ın level + 1
- **Devre Dışı**: `Hayır`

**Opsiyonel Alanlar:**
- **Sayfa Kodu (Page Code)**: `datasets` (otomatik oluşturulabilir)
- **Icon Type**: `tabler`
- **Icon**: `DatabaseIcon` (tabler için) veya `mdi-database` (mdi için)
- **Alt Başlık (Sub Caption)**: `Dataset şemalarını yönetin`
- **Chip Metni**: Boş

**Örnek Konfigürasyon:**
```
Item Tipi: Menu Item
Sayfa Tipi: admin
Menü Başlığı: Datasets
Route Path: /apps/datasets
Link Tipi: Internal
Icon Type: tabler
Icon: DatabaseIcon
Sayfa Kodu: datasets (otomatik oluşturulabilir)
Sıralama: 101
Parent: (Yok - Root)
Level: 0
Devre Dışı: Hayır
```

**Not**: Datasets menu item'ı, Dataset List sayfasına (`/apps/datasets`) yönlendirir. Create, Edit ve Detail sayfaları bu list sayfasından erişilir.

---

### Senaryo 2: "Dataset Yönetimi" Header Altında Gruplama (Alternatif)

Eğer Dataset ile ilgili tüm sayfaları bir grup altında toplamak isterseniz:

#### 1. Dataset Yönetimi Header

**Zorunlu Alanlar:**
- **Item Tipi**: `Header`
- **Sayfa Tipi**: `admin` (veya `manager`)
- **Header Metni**: `Dataset Yönetimi` (veya `Dataset Management`)
- **Sıralama (Order)**: İstediğiniz sıraya göre (örn: `100`)
- **Parent Menu Item**: `(Yok - Root)`
- **Seviye (Level)**: `0`
- **Devre Dışı**: `Hayır`

**Opsiyonel Alanlar:**
- **Sayfa Kodu (Page Code)**: `dataset-management` (otomatik oluşturulabilir)

**Örnek Konfigürasyon:**
```
Item Tipi: Header
Sayfa Tipi: admin
Header Metni: Dataset Yönetimi
Sayfa Kodu: dataset-management (otomatik oluşturulabilir)
Sıralama: 100
Parent: (Yok - Root)
Level: 0
Devre Dışı: Hayır
```

#### 2. Dataset Categories (Header Altında)

**Zorunlu Alanlar:**
- **Item Tipi**: `Menu Item`
- **Sayfa Tipi**: `admin`
- **Menü Başlığı**: `Kategoriler`
- **Route Path**: `/apps/dataset-categories`
- **Link Tipi**: `Internal`
- **Sıralama (Order)**: `0` (header altında ilk sıra)
- **Parent Menu Item**: `Dataset Yönetimi` (yukarıda oluşturduğunuz header)
- **Seviye (Level)**: `1` (otomatik hesaplanır)
- **Devre Dışı**: `Hayır`

**Opsiyonel Alanlar:**
- **Icon Type**: `tabler`
- **Icon**: `TagIcon` (kategori için uygun)
- **Sayfa Kodu**: `dataset-categories`

#### 3. Datasets (Header Altında)

**Zorunlu Alanlar:**
- **Item Tipi**: `Menu Item`
- **Sayfa Tipi**: `admin`
- **Menü Başlığı**: `Datasets`
- **Route Path**: `/apps/datasets`
- **Link Tipi**: `Internal`
- **Sıralama (Order)**: `1` (header altında ikinci sıra)
- **Parent Menu Item**: `Dataset Yönetimi`
- **Seviye (Level)**: `1` (otomatik hesaplanır)
- **Devre Dışı**: `Hayır`

**Opsiyonel Alanlar:**
- **Icon Type**: `tabler`
- **Icon**: `DatabaseIcon`
- **Sayfa Kodu**: `datasets`

---

## Icon Seçenekleri

### Tabler Icons (Önerilen)
- `DatabaseIcon` - Datasets için
- `TagIcon` - Kategoriler için
- `FileCodeIcon` - Queries için
- `KeyIcon` - Indexes için
- `ListIcon` - List sayfaları için

### MDI Icons (Alternatif)
- `mdi-database` - Datasets için
- `mdi-tag` - Kategoriler için
- `mdi-code-tags` - Queries için
- `mdi-key` - Indexes için
- `mdi-view-list` - List sayfaları için

**Not**: Icon seçimi için Side Menu Manager'daki "Icon Picker" bileşenini kullanabilirsiniz.

---

## Yetkilendirme (Permissions)

**Önerilen Yapı:**
- **Group**: `Managers` veya `Admins`
  - **view**: `true` (menüde görünür)
  - **create**: `true` (yeni kayıt ekleyebilir)
  - **update**: `true` (düzenleyebilir)
  - **delete**: `true` (silebilir)
  - **export**: `false` (export özelliği varsa)

**Örnek Permission Konfigürasyonu:**
```
Group: Managers
  view: true
  create: true
  update: true
  delete: true
  export: false

Group: Admins
  view: true
  create: true
  update: true
  delete: true
  export: true
```

**Not**: Permission ayarları için Side Menu Manager'daki "Permission Editor" bileşenini kullanabilirsiniz.

---

## Önemli Notlar

### 1. Page Code (Sayfa Kodu)
- **Zorunlu**: Hayır (opsiyonel)
- **Unique**: Evet (aynı pageCode iki menu item'da olamaz)
- **Otomatik Oluşturma**: Route Path veya Menü Başlığından otomatik oluşturulabilir (form üzerindeki "Otomatik Oluştur" butonuna tıklayın)
- **Format**: Küçük harf, tire (-) ile ayrılmış (örn: `dataset-categories`, `datasets`)

### 2. Route Path
- **Format**: `/apps/dataset-categories` veya `/apps/datasets`
- **Not**: Route path'ler mutlaka `/` ile başlamalıdır
- **Test**: Form üzerindeki "Bu sayfaya git" butonu ile route'un çalıştığını test edebilirsiniz

### 3. Parent-Child İlişkisi
- Sadece `Header` tipindeki menu item'lar parent olarak seçilebilir
- Bir menu item kendisi veya kendi altındaki (descendant) item'ları parent olarak seçemez (circular reference önleme)
- Level, parent'ın level'ına göre otomatik hesaplanır

### 4. Sıralama (Order)
- **Tip**: Number (0, 1, 2, ...)
- **Kullanım**: Aynı parent altındaki menu item'ların görünme sırasını belirler
- **Küçük → Büyük**: Küçük sayılar önce görünür (0, 1, 2, ...)

### 5. Devre Dışı (Disabled)
- **Kullanım**: Geçici olarak menüden gizlemek için kullanılır
- **Silme**: Menu item'ı silmeden sadece gizlemek istediğinizde kullanın

---

## Adım Adım Ekleme Süreci

### 1. Side Menu Manager'a Erişim
1. `/apps/side-menu-manager` adresine gidin
2. Manager veya Admin yetkisiyle giriş yaptığınızdan emin olun

### 2. Yeni Menu Item Oluşturma
1. Sol üst köşedeki **"Yeni Menu Item"** butonuna tıklayın
2. Form açılacaktır (sağ tarafta)

### 3. Form Doldurma
1. **Item Tipi**: `Menu Item` seçin
2. **Sayfa Tipi**: `admin` veya `manager` seçin
3. **Menü Başlığı**: Örn: `Datasets`
4. **Route Path**: `/apps/datasets` yazın
5. **Sayfa Kodu**: Otomatik oluşturmak için "Otomatik Oluştur" butonuna tıklayın veya manuel girin
6. **Icon**: Icon Picker'dan uygun icon'u seçin (örn: `DatabaseIcon`)
7. **Sıralama**: İstediğiniz sayıyı girin (örn: `100`)
8. **Parent**: Root için `(Yok - Root)` seçin veya bir Header seçin

### 4. Yetkilendirme Ayarlama (Opsiyonel)
1. **Yetkilendirme** bölümüne gidin
2. **Permission Editor** ile grup bazlı yetkilendirme ayarlayın
3. Örnek: `Managers` grubuna `view: true, create: true, update: true, delete: true` verin

### 5. Kaydetme
1. **"Kaydet"** butonuna tıklayın
2. Menu item oluşturulacak ve sol taraftaki tree view'da görünecektir

### 6. Test Etme
1. Sidebar'ı yenileyin (sayfayı yenileyin veya sidebar'ı kapatıp açın)
2. Yeni eklediğiniz menu item'ın sidebar'da göründüğünü kontrol edin
3. Menu item'a tıklayarak route'un çalıştığını test edin

---

## Örnek Menu Item JSON Yapısı

### Dataset Categories Menu Item
```json
{
  "itemType": "item",
  "pageType": "admin",
  "title": "Dataset Kategorileri",
  "to": "/apps/dataset-categories",
  "type": "internal",
  "icon": "DatabaseIcon",
  "iconType": "tabler",
  "pageCode": "dataset-categories",
  "order": 100,
  "parentId": null,
  "level": 0,
  "disabled": false,
  "subCaption": "Dataset kategorilerini yönetin",
  "permissions": {
    "groups": {
      "Managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": false
      },
      "Admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      }
    }
  }
}
```

### Datasets Menu Item
```json
{
  "itemType": "item",
  "pageType": "admin",
  "title": "Datasets",
  "to": "/apps/datasets",
  "type": "internal",
  "icon": "DatabaseIcon",
  "iconType": "tabler",
  "pageCode": "datasets",
  "order": 101,
  "parentId": null,
  "level": 0,
  "disabled": false,
  "subCaption": "Dataset şemalarını yönetin",
  "permissions": {
    "groups": {
      "Managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": false
      },
      "Admins": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": true
      }
    }
  }
}
```

---

## Sorun Giderme

### Menu Item Görünmüyor
1. **Yetkilendirme Kontrolü**: Menu item'ın `permissions` ayarlarını kontrol edin. `view: true` olmalıdır.
2. **Disabled Kontrolü**: Menu item'ın `disabled: false` olduğundan emin olun.
3. **Page Type Kontrolü**: Kullanıcının `pageType`'a uygun yetkisi olduğundan emin olun (admin/manager/user).
4. **Cache Temizleme**: Browser cache'ini temizleyin veya hard refresh yapın (Ctrl+F5).

### Route Çalışmıyor
1. **Route Path Kontrolü**: Route path'in `/` ile başladığından emin olun.
2. **Sayfa Kontrolü**: Route path'in gerçekten var olduğundan emin olun.
3. **Browser Console**: Browser console'da hata mesajlarını kontrol edin.

### Icon Görünmüyor
1. **Icon Type Kontrolü**: Icon type'ın doğru olduğundan emin olun (`tabler` veya `mdi`).
2. **Icon Name Kontrolü**: Icon name'in doğru format olduğundan emin olun.
3. **Icon Library**: Icon'un mevcut icon library'de olduğundan emin olun.

---

## İletişim ve Destek

Sorularınız veya sorunlarınız için:
- Side Menu Manager sayfasını kullanarak menu item'ları yönetin
- Dokümantasyonu referans alın
- Gerekirse backend API dokümantasyonunu kontrol edin

---

**Son Güncelleme**: 2025-01-27  
**Durum**: ✅ Aktif - Kullanıma Hazır
