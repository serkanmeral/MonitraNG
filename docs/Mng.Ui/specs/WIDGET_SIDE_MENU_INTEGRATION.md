# Widget List Side Menu Entegrasyonu

Bu doküman, Widget List sayfasını (`/apps/widgets`) side menu'ye eklemek için gerekli bilgileri içerir.

## 📋 Yöntemler

Side menu'ye ekleme için iki yöntem mevcuttur:

1. **Dinamik Menu (Önerilen)** - `@side_menu` dataset'i üzerinden
2. **Statik Menu** - `sidebarItem.ts` dosyasına hardcoded ekleme

---

## 🚀 Yöntem 1: Dinamik Menu (Önerilen)

### Side Menu Manager ile Ekleme

1. **Side Menu Manager'a Git:**
   - `/apps/side-menu-manager` sayfasına git
   - Veya Side Menu Manager component'ini aç

2. **Yeni Menu Item Oluştur:**
   - "Yeni Menu Item" butonuna tıkla
   - Aşağıdaki bilgileri gir:

#### Menu Item Bilgileri

```json
{
  "order": 100,  // Uygun bir sıra numarası (diğer menu item'larına göre)
  "itemType": "item",
  "level": 0,  // Root level (parent yok)
  "parentId": null,
  "pageType": "admin",  // veya "manager" - yetki seviyesi
  "pageCode": "widgets-list",  // Unique identifier
  "title": "Widget'lar",  // Menüde görünecek başlık
  "icon": "WidgetsIcon",  // Tabler icon adı
  "iconType": "tabler",  // "tabler" veya "mdi"
  "to": "/apps/widgets",  // Route path
  "type": "internal",  // "internal" veya "external"
  "disabled": false,
  "permissions": {
    "groups": {
      "managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": false
      },
      "editors": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": false
      }
    }
  }
}
```

#### Icon Seçenekleri

**Tabler Icons (Önerilen):**
- `WidgetsIcon` - Widget ikonu
- `LayoutIcon` - Layout ikonu
- `BoxIcon` - Kutu ikonu
- `AppsIcon` - Uygulamalar ikonu
- `CardboardsIcon` - Kart ikonu

**MDI Icons:**
- `mdi-widgets` - Widget ikonu
- `mdi-view-dashboard` - Dashboard ikonu
- `mdi-puzzle` - Puzzle ikonu

#### Permissions Yapılandırması

```json
{
  "permissions": {
    "groups": {
      "managers": {
        "view": true,      // Menüde görünür ve sayfaya erişebilir
        "create": true,    // Widget oluşturabilir
        "update": true,    // Widget düzenleyebilir
        "delete": true,    // Widget silebilir
        "export": false   // Export yetkisi (şimdilik false)
      },
      "editors": {
        "view": true,
        "create": true,
        "update": true,
        "delete": false,
        "export": false
      },
      "viewers": {
        "view": true,
        "create": false,
        "update": false,
        "delete": false,
        "export": false
      }
    }
  }
}
```

#### Header Altına Ekleme (Opsiyonel)

Eğer "Apps" header'ı altına eklemek isterseniz:

1. "Apps" header'ının `__dataId`'sini bul
2. Menu item'ın `parentId`'sine bu ID'yi yaz
3. `level` değerini `1` yap

---

## 🔧 Yöntem 2: Statik Menu

Eğer dinamik menu kullanmıyorsanız, statik menu'ye ekleyebilirsiniz.

### Dosya: `Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts`

#### Icon Import Ekleme

Dosyanın başına icon import'u ekleyin (eğer yoksa):

```typescript
import { WidgetsIcon } from "vue-tabler-icons";
```

#### Menu Item Ekleme

`sidebarItem` array'ine ekleyin:

```typescript
const sidebarItem: menu[] = [
  // ... mevcut item'lar
  { header: "Apps" },
  {
    title: "Widget'lar",
    icon: WidgetsIcon,
    to: "/apps/widgets",
    pageCode: "widgets-list",
  },
  // ... diğer item'lar
];
```

**Not:** Statik menu'de permissions kontrolü yoktur. Tüm kullanıcılar görebilir.

---

## 📝 API ile Direkt Ekleme

Side Menu Manager UI kullanmak yerine, API ile direkt ekleyebilirsiniz:

### POST Request

```bash
POST /api/v1/data/@side_menu
Content-Type: application/json
Authorization: Bearer {token}

Body:
{
  "order": 100,
  "itemType": "item",
  "level": 0,
  "parentId": null,
  "pageType": "admin",
  "pageCode": "widgets-list",
  "title": "Widget'lar",
  "icon": "WidgetsIcon",
  "iconType": "tabler",
  "to": "/apps/widgets",
  "type": "internal",
  "disabled": false,
  "permissions": {
    "groups": {
      "managers": {
        "view": true,
        "create": true,
        "update": true,
        "delete": true,
        "export": false
      }
    }
  }
}
```

---

## 🎯 Önerilen Konum

Widget list sayfası genellikle **"Apps"** header'ı altına eklenir:

```
Apps
├── Kullanıcı Yönetimi (/apps/users)
├── Grup Yönetimi (/apps/groups)
├── Dashboard'lar (/apps/dashboards)
├── Widget'lar (/apps/widgets)  ← Buraya ekle
├── Dataset'ler (/apps/datasets)
└── ...
```

---

## ✅ Kontrol Listesi

- [ ] Menu item oluşturuldu
- [ ] `to` path doğru: `/apps/widgets`
- [ ] Icon seçildi ve doğru format (`tabler` veya `mdi`)
- [ ] Permissions yapılandırıldı
- [ ] Order numarası uygun (diğer item'ları ezmeyecek)
- [ ] Test edildi:
  - [ ] Menüde görünüyor mu?
  - [ ] Tıklayınca sayfaya gidiyor mu?
  - [ ] Permissions çalışıyor mu?
  - [ ] Icon görünüyor mu?

---

## 🔍 Mevcut Menu Item Örnekleri

Diğer sayfaların nasıl eklendiğine bakmak için:

### Dashboard'lar
```json
{
  "title": "Dashboard'lar",
  "to": "/apps/dashboards",
  "pageCode": "dashboards-list"
}
```

### Dataset'ler
```json
{
  "title": "Dataset'ler",
  "to": "/apps/datasets",
  "pageCode": "datasets-list"
}
```

### Kullanıcı Yönetimi
```json
{
  "title": "Kullanıcı Yönetimi",
  "to": "/apps/users",
  "pageCode": "users-list"
}
```

---

## 📌 Önemli Notlar

1. **Page Code:** Unique olmalı, i18n key olarak kullanılabilir
2. **Order:** Diğer menu item'larının order değerlerine bakarak uygun bir değer seçin
3. **Permissions:** Side Menu Manager'da permissions yapılandırması yapılabilir
4. **Icon:** Tabler icon kullanıyorsanız, icon adı tam olarak yazılmalı (örn: `WidgetsIcon`)
5. **Route:** `/apps/widgets` path'i mevcut sayfa ile eşleşmeli

---

## 🐛 Sorun Giderme

### Menu item görünmüyor
- ✅ Permissions kontrolü yapın (kullanıcının grup yetkileri)
- ✅ `pageType` doğru mu? (`admin`, `manager`, `user`)
- ✅ `disabled: false` olduğundan emin olun
- ✅ Side Menu cache'i temizleyin (browser console: `localStorage.clear()`)

### Icon görünmüyor
- ✅ Icon adı doğru mu? (`WidgetsIcon` vs `widgets-icon`)
- ✅ `iconType` doğru mu? (`tabler` vs `mdi`)
- ✅ Icon import edilmiş mi? (statik menu için)

### Sayfaya gidemiyor
- ✅ `to` path doğru mu? (`/apps/widgets`)
- ✅ Route tanımlı mı? (Nuxt pages klasöründe)
- ✅ Browser console'da hata var mı?

---

**Son Güncelleme:** 2024-12-19
