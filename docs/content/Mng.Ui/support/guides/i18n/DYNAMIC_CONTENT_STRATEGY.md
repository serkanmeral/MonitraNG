# Dinamik İçerik İçin i18n Stratejisi

## Genel Bakış

Side Menu ve Dataset field'ları gibi **veritabanından gelen dinamik içerikler** için i18n yaklaşımı.

---

## Side Menu i18n Stratejisi

### Mevcut Durum

**Veritabanı Yapısı:**
```json
{
  "title": "Datasets",
  "pageCode": "datasets",
  "itemType": "item",
  "header": "Apps" // header için
}
```

**Component Kullanımı:**
- `NavItem/index.vue`: `{{ item.title ? $t(item.title) : '' }}`
- Template zaten `$t()` kullanıyor ama `title` direkt i18n key olarak kullanılıyor

### Önerilen Yaklaşım: pageCode Bazlı i18n

**Strateji:**
- `pageCode` → i18n key: `menu.{pageCode}`
- `title` → Fallback (backward compatibility)
- Header'lar için: `menu.headers.{header}` veya direkt `header` field'ı

### 1. i18n Key Yapısı

**Dil Dosyaları (`tr.json`, `en.json`):**

```json
{
  "menu": {
    "datasets": "Dataset'ler",
    "users": "Kullanıcılar",
    "domains": "Domainler",
    "groups": "Gruplar",
    "side-menu-manager": "Side Menu Yönetimi",
    "headers": {
      "apps": "Uygulamalar",
      "dashboards": "Kontrol Panelleri",
      "pages": "Sayfalar"
    }
  }
}
```

### 2. NavItem Component Güncelleme

**Önce:**
```vue
<v-list-item-title>{{ item.title ? $t(item.title) : '' }}</v-list-item-title>
```

**Sonra:**
```vue
<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Props {
  item: {
    title?: string
    pageCode?: string
    itemType?: 'header' | 'item'
    header?: string
    // ... diğer alanlar
  }
}

const props = defineProps<Props>()

const getMenuTitle = (item: Props['item']): string => {
  if (item.itemType === 'header' && item.header) {
    // Header için: menu.headers.{header} veya fallback
    const headerKey = `menu.headers.${item.header.toLowerCase()}`
    const translated = t(headerKey)
    return translated !== headerKey ? translated : item.header
  }
  
  if (item.pageCode) {
    // Item için: menu.{pageCode}
    const menuKey = `menu.${item.pageCode}`
    const translated = t(menuKey)
    return translated !== menuKey ? translated : (item.title || item.pageCode)
  }
  
  // Fallback: direkt title kullan
  return item.title || ''
}
</script>

<template>
  <v-list-item-title>{{ getMenuTitle(item) }}</v-list-item-title>
</template>
```

**Alternatif: Composable Kullanımı**

```typescript
// composables/useMenuTranslation.ts
import { useI18n } from 'vue-i18n'

export const useMenuTranslation = () => {
  const { t } = useI18n()
  
  const translateMenuTitle = (item: {
    title?: string
    pageCode?: string
    itemType?: 'header' | 'item'
    header?: string
  }): string => {
    if (item.itemType === 'header' && item.header) {
      const headerKey = `menu.headers.${item.header.toLowerCase()}`
      const translated = t(headerKey)
      return translated !== headerKey ? translated : item.header
    }
    
    if (item.pageCode) {
      const menuKey = `menu.${item.pageCode}`
      const translated = t(menuKey)
      return translated !== menuKey ? translated : (item.title || item.pageCode)
    }
    
    return item.title || ''
  }
  
  return { translateMenuTitle }
}
```

**Component'te Kullanım:**
```vue
<script setup lang="ts">
import { useMenuTranslation } from '@/composables/useMenuTranslation'

const { translateMenuTitle } = useMenuTranslation()
</script>

<template>
  <v-list-item-title>{{ translateMenuTitle(item) }}</v-list-item-title>
</template>
```

### 3. Store'da Çeviri (Opsiyonel)

Eğer store'da çeviri yapmak isterseniz:

```typescript
// stores/apps/sideMenu.ts
import { useI18n } from 'vue-i18n'

// convertToMenuFormat içinde:
convertToMenuFormat(item: SideMenuItem): menu {
  const { t } = useI18n()
  
  const getTitle = () => {
    if (item.itemType === 'header' && item.header) {
      const headerKey = `menu.headers.${item.header.toLowerCase()}`
      const translated = t(headerKey)
      return translated !== headerKey ? translated : item.header
    }
    
    if (item.pageCode) {
      const menuKey = `menu.${item.pageCode}`
      const translated = t(menuKey)
      return translated !== menuKey ? translated : (item.title || item.pageCode)
    }
    
    return item.title || ''
  }
  
  return {
    title: getTitle(),
    // ... diğer alanlar
  }
}
```

**Not:** Store'da çeviri yapmak genellikle önerilmez çünkü store reactive değil. Component'te yapmak daha iyi.

### 4. Migration Stratejisi

**Adım 1: Composable Oluştur**
- `composables/useMenuTranslation.ts` oluştur
- `translateMenuTitle` fonksiyonu ekle

**Adım 2: NavItem Component Güncelle**
- Composable'ı kullan
- Test et

**Adım 3: Dil Dosyalarına Key'leri Ekle**
- Mevcut menu item'ların `pageCode`'larını topla
- `menu.{pageCode}` key'lerini dil dosyalarına ekle
- Header'lar için `menu.headers.{header}` key'lerini ekle

**Adım 4: Backward Compatibility**
- Eğer çeviri yoksa, `title` field'ını fallback olarak kullan
- Mevcut menu item'lar çalışmaya devam eder

### 5. Yeni Menu Item Ekleme

**Yeni menu item eklendiğinde:**
1. Veritabanına `pageCode` ekle (örn: `"new-feature"`)
2. Dil dosyalarına key ekle:
   ```json
   {
     "menu": {
       "new-feature": "Yeni Özellik" // TR
       // veya
       "new-feature": "New Feature" // EN
     }
   }
   ```
3. Menu item otomatik olarak çevrilir

---

## Dataset Field'ları i18n Stratejisi

### Mevcut Durum

Dataset field'ları schema'dan geliyor:
```json
{
  "name": "email",
  "title": "E-posta",
  "fieldType": "text"
}
```

### Önerilen Yaklaşım

**Strateji 1: title Field'ını Direkt Kullan (Basit)**
- Dataset field'larının `title` field'ı zaten dil bazlı olabilir
- Veritabanında her domain için farklı `title` değerleri tutulabilir
- Frontend'de i18n gerekmez (backend'den dil bazlı gelir)

**Strateji 2: i18n Key Kullan (Esnek)**
- Schema'da `titleKey` field'ı ekle (opsiyonel)
- Format: `dataset.fields.{datasetName}.{fieldName}`
- Fallback: `title` field'ı

**Örnek:**
```json
// Schema
{
  "name": "email",
  "title": "E-posta", // Fallback
  "titleKey": "dataset.fields.users.email" // Opsiyonel i18n key
}
```

**Dil Dosyası:**
```json
{
  "dataset": {
    "fields": {
      "users": {
        "email": "E-posta",
        "username": "Kullanıcı Adı"
      },
      "products": {
        "name": "Ürün Adı",
        "price": "Fiyat"
      }
    }
  }
}
```

**Component'te Kullanım:**
```typescript
const getFieldTitle = (field: FieldDefinition, datasetName: string): string => {
  if (field.titleKey) {
    const translated = t(field.titleKey)
    if (translated !== field.titleKey) {
      return translated
    }
  }
  
  // Fallback: dataset.fields.{datasetName}.{fieldName}
  const fieldKey = `dataset.fields.${datasetName}.${field.name}`
  const translated = t(fieldKey)
  if (translated !== fieldKey) {
    return translated
  }
  
  // Son fallback: title field'ı
  return field.title || field.name
}
```

---

## Özet

### Side Menu

| Özellik | Değer |
|---------|-------|
| **i18n Key Format** | `menu.{pageCode}` |
| **Header Key Format** | `menu.headers.{header}` |
| **Fallback** | `title` field'ı |
| **Uygulama Yeri** | NavItem component veya composable |
| **Migration** | Backward compatible (title fallback) |

### Dataset Fields

| Özellik | Değer |
|---------|-------|
| **Strateji 1** | `title` field'ı direkt kullan (backend'den dil bazlı) |
| **Strateji 2** | `titleKey` field'ı + `dataset.fields.{datasetName}.{fieldName}` |
| **Fallback** | `title` field'ı |
| **Öneri** | Strateji 1 (basit) veya Strateji 2 (esnek) |

---

## Önemli Notlar

1. **Backward Compatibility**: Mevcut menu item'lar çalışmaya devam etmeli
2. **Fallback Mekanizması**: Çeviri yoksa, `title` field'ı kullanılmalı
3. **pageCode Zorunluluğu**: Yeni menu item'lar için `pageCode` zorunlu olmalı
4. **Performance**: Composable kullanımı reactive ve performanslı
5. **Migration**: Aşamalı migration yapılabilir (önce composable, sonra dil dosyaları)

---

## Sonraki Adımlar

1. ✅ Side Menu stratejisi planlandı
2. ⏳ Dataset field'ları stratejisi daha sonra detaylandırılacak
3. ⏳ Implementation: Composable oluşturma
4. ⏳ Implementation: NavItem component güncelleme
5. ⏳ Implementation: Dil dosyalarına key'leri ekleme

---

**Not:** Bu dokümantasyon, dinamik içerikler için genel bir strateji sağlar. Detaylı implementasyon, kod review ve test sonrası finalize edilecektir.
