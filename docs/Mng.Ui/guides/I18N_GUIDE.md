# Dil Desteği (i18n) Rehberi

## Genel Bakış

MonitraNG uygulaması **5 dil** desteği ile gelir:
- 🇹🇷 **Türkçe (tr)** - Varsayılan dil
- 🇬🇧 **İngilizce (en)** - Fallback dil
- 🇫🇷 **Fransızca (fr)**
- 🇸🇦 **Arapça (ar)** - RTL desteği
- 🇨🇳 **Çince (zh)**

Dil desteği `vue-i18n` kütüphanesi kullanılarak sağlanmaktadır. **Legacy mode** kullanılmaktadır.

## Yapılandırma

### Dil Dosyaları

Dil çevirileri `Mng.Ui/utils/locales/` klasöründe JSON dosyaları olarak saklanır:

- `tr.json` - Türkçe çeviriler
- `en.json` - İngilizce çeviriler
- `ar.json` - Arapça çeviriler
- `fr.json` - Fransızca çeviriler
- `zh.json` - Çince çeviriler

**Not:** Arapça için `messages.ts` dosyasında `'ro'` key'i kullanılmaktadır (iç mapping).

### Varsayılan Dil

Varsayılan dil **Türkçe (tr)** olarak ayarlanmıştır. Uygulama ilk açıldığında:

1. Önce `localStorage`'dan kaydedilmiş dil tercihi kontrol edilir
2. Eğer kayıtlı tercih yoksa, tarayıcı diline göre otomatik seçim yapılır
3. Tarayıcı dili desteklenen dillerden biri ise o dil seçilir, değilse Türkçe seçilir

### Dil Değiştirme

Kullanıcılar header'daki dil seçici butonundan dil değiştirebilir. Seçilen dil `localStorage`'a kaydedilir ve sonraki ziyaretlerde otomatik olarak yüklenir.

## Sayfa Bazlı Dil Desteği Ekleme

### Adım 1: Locale Dosyalarına Çevirileri Ekle

Her dil dosyasına (`tr.json`, `en.json`, `ar.json`, `fr.json`, `zh.json`) sayfa için çevirileri ekleyin.

**Örnek Yapı (groups sayfası için):**

```json
{
  "groups": {
    "title": "Grup Yönetimi",
    "breadcrumbs": {
      "home": "Ana Sayfa",
      "groups": "Grup Yönetimi",
      "create": "Yeni Grup",
      "edit": "Grup Düzenle",
      "details": "Grup Detayı"
    },
    "list": {
      "search": "Grup Ara",
      "status": "Durum",
      "statusAll": "Tümü",
      "statusActive": "Aktif",
      "statusInactive": "Pasif",
      "export": "Dışa Aktar",
      "refresh": "Yenile",
      "newGroup": "Yeni Grup"
    },
    "table": {
      "name": "Grup Adı",
      "memberCount": "Kişi Sayısı",
      "createdAt": "Oluşturulma",
      "actions": "İşlemler",
      "view": "Görüntüle",
      "edit": "Düzenle",
      "delete": "Sil"
    },
    "validation": {
      "nameRequired": "Grup adı gereklidir",
      "nameMinLength": "Grup adı en az 2 karakter olmalıdır"
    },
    "errors": {
      "load": "Grup yüklenirken bir hata oluştu",
      "create": "Grup oluşturulurken bir hata oluştu",
      "update": "Grup güncellenirken bir hata oluştu",
      "delete": "Grup silinirken bir hata oluştu"
    }
  }
}
```

**Çeviri Key Yapısı Önerileri:**
- Hierarchical yapı kullanın: `{sayfa}.{bölüm}.{key}`
- Örnek: `groups.list.search`, `groups.table.name`
- Kısa ve açıklayıcı key'ler kullanın
- Ortak çeviriler için `common` key'ini kullanın

### Adım 2: Script Setup İçinde i18n Kullanımı

**ÖNEMLİ:** Vue-i18n **legacy mode** kullanıldığı için `useI18n()` composable'ı çalışmaz. Bunun yerine `useNuxtApp()` ile i18n instance'ına erişin:

```vue
<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  // Fallback: try global.t if available
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const localeStore = useLocaleStore();
const router = useRouter();

// Kullanım örnekleri
const page = computed(() => ({ title: t('groups.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('groups.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('groups.breadcrumbs.groups'),
    disabled: true,
    href: '#',
  },
]);
</script>
```

### Adım 3: Template İçinde Kullanım

Template içinde hem `t()` fonksiyonu hem de `$t` kullanılabilir:

```vue
<template>
  <!-- Script setup'te tanımlanan t() fonksiyonu -->
  <h1>{{ t('groups.title') }}</h1>
  
  <!-- Veya direkt $t kullanımı -->
  <p>{{ $t('groups.description') }}</p>
  
  <!-- Parametreli çeviri -->
  <p>{{ t('groups.delete.error', { count: memberCount }) }}</p>
</template>
```

### Adım 4: Tarih Formatlama

Tarih formatlama fonksiyonlarını locale'a göre güncelleyin:

```typescript
const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    const localeMap: Record<string, string> = {
      tr: 'tr-TR',
      en: 'en-US',
      fr: 'fr-FR',
      ar: 'ar-SA',
      zh: 'zh-CN',
    };
    const locale = localeMap[localeStore.locale] || 'tr-TR';
    
    return new Date(date).toLocaleDateString(locale, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  } catch {
    return '-';
  }
};
```

### Adım 5: Validation Mesajları

Form validation mesajlarını da i18n ile güncelleyin:

```typescript
import * as yup from 'yup';

// Validation schema
const schema = computed(() => yup.object({
  name: yup.string()
    .required(t('groups.validation.nameRequired'))
    .min(2, t('groups.validation.nameMinLength')),
  description: yup.string()
    .max(500, t('groups.validation.descriptionMaxLength')),
}));
```

### Adım 6: Error Mesajları

Error mesajlarını i18n ile güncelleyin:

```typescript
try {
  await groupStore.createGroup(groupData);
} catch (error: any) {
  errorMessage.value = error.message || t('groups.errors.create');
}
```

## Tam Örnek: Sayfa İmplementasyonu

### Liste Sayfası (index.vue)

```vue
<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const localeStore = useLocaleStore();
const groupStore = useGroupStore();
const router = useRouter();
const route = useRoute();

const page = computed(() => ({ title: t('groups.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('groups.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('groups.breadcrumbs.groups'),
    disabled: true,
    href: '#',
  },
]);

const headers = computed(() => [
  { title: t('groups.table.name'), key: 'name', sortable: true },
  { title: t('groups.table.memberCount'), key: 'memberCount', sortable: true },
  { title: t('groups.table.createdAt'), key: 'createdAt', sortable: true },
  { title: t('groups.table.actions'), key: 'actions', sortable: false, align: 'end' },
]);

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    const localeMap: Record<string, string> = {
      tr: 'tr-TR',
      en: 'en-US',
      fr: 'fr-FR',
      ar: 'ar-SA',
      zh: 'zh-CN',
    };
    const locale = localeMap[localeStore.locale] || 'tr-TR';
    return new Date(date).toLocaleDateString(locale, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  } catch {
    return '-';
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <v-text-field
        v-model="search"
        :label="t('groups.list.search')"
        variant="outlined"
        density="compact"
      />
      
      <v-data-table
        :headers="headers"
        :items="groupStore.groups"
      >
        <template v-slot:item.actions="{ item }">
          <v-btn @click="viewGroup(item)">
            {{ t('groups.table.view') }}
          </v-btn>
        </template>
        
        <template v-slot:no-data>
          <p>{{ t('groups.list.noData') }}</p>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>
</template>
```

## Locale Store Kullanımı

Dil yönetimi için `useLocaleStore` kullanılır:

```typescript
import { useLocaleStore } from '@/stores/locale';

const localeStore = useLocaleStore();

// Mevcut dili al
const currentLang = localeStore.currentLocale; // "tr", "en", "ar", "fr", "zh"

// Dil değiştir
localeStore.setLocale('en');

// Kontroller
if (localeStore.isTurkish) { /* ... */ }
if (localeStore.isEnglish) { /* ... */ }
if (localeStore.isRTL) { /* RTL için özel işlemler */ }

// Locale name
const localeName = localeStore.localeName; // "Türkçe", "English", vb.
```

## RTL (Right-to-Left) Desteği

Arapça için RTL desteği otomatik olarak sağlanır:

- HTML `dir` attribute'u otomatik olarak `rtl` olarak ayarlanır
- Vuetify `v-locale-provider` ile RTL layout aktif edilir
- `localeStore.isRTL` ile RTL kontrolü yapılabilir

## Best Practices

### 1. Çeviri Key Organizasyonu

```json
{
  "{sayfa}": {
    "title": "Sayfa Başlığı",
    "breadcrumbs": {
      "home": "Ana Sayfa",
      "{sayfa}": "Sayfa Adı"
    },
    "list": {
      "search": "Ara",
      "status": "Durum"
    },
    "table": {
      "name": "İsim",
      "actions": "İşlemler"
    },
    "form": {
      "name": "İsim *",
      "save": "Kaydet"
    },
    "validation": {
      "nameRequired": "İsim gereklidir"
    },
    "errors": {
      "load": "Yüklenirken hata oluştu"
    }
  }
}
```

### 2. Parametreli Çeviriler

```json
{
  "groups": {
    "delete": {
      "error": "Bu grup içinde {count} kullanıcı bulunmaktadır."
    }
  }
}
```

```typescript
const errorMessage = t('groups.delete.error', { count: item.memberCount });
```

### 3. Computed Properties Kullanımı

Reactive çeviriler için `computed` kullanın:

```typescript
const page = computed(() => ({ title: t('groups.title') }));
const breadcrumbs = computed(() => [
  { text: t('groups.breadcrumbs.home'), href: '/' },
]);
```

### 4. Fallback Mekanizması

Eksik çeviriler için fallback:

```typescript
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key; // Fallback: key'in kendisini döndür
};
```

## Side Menu Dil Desteği

Side Menu item'ları için dil desteği özel bir yapı kullanır. Menu item'lar veritabanından gelir ve dinamik olarak render edilir.

### Menu Item Yapısı

Menu item'lar için locale dosyalarında iki farklı yapı kullanılabilir:

**1. Sadece Title (String):**
```json
{
  "menu": {
    "apps-users": "Kullanıcı Yönetimi",
    "apps-groups": "Grup Yönetimi"
  }
}
```

**2. Title ve SubCaption (Object):**
```json
{
  "menu": {
    "apps-automated-forms": {
      "title": "Otomatik Formlar",
      "subCaption": "Otomatik formlar oluşturmaya yarar."
    },
    "apps-side-menu-manager": {
      "title": "Side Menu Manager",
      "subCaption": "Menü yönetim sayfası"
    }
  }
}
```

### MenuItemForm ile Otomatik Çeviri

Side Menu Manager sayfasında menu item'ları düzenlerken "Update Locale Files" butonu ile otomatik çeviri yapılabilir:

1. Menu item'ın `title` ve `subCaption` değerleri MngLLM servisi ile çevrilir
2. Çeviriler tüm dil dosyalarına (`tr.json`, `en.json`, `ar.json`, `fr.json`, `zh.json`) eklenir
3. Eğer menu item'da `subCaption` varsa, locale dosyasında object yapısı kullanılır
4. Eğer `subCaption` yoksa, sadece string olarak saklanır

**Örnek Kullanım:**
```vue
<!-- MenuItemForm.vue içinde -->
<v-btn @click="updateLocaleFiles">
  Update Locale Files
</v-btn>
```

Bu buton:
- `title` değerini tüm dillere çevirir
- `subCaption` varsa, onu da tüm dillere çevirir
- Locale dosyalarını günceller (MinIO'ya kaydeder)
- Build-time locale dosyalarını da günceller

### NavItem ve NavCollapse Component'leri

Menu item'ları render eden component'ler (`NavItem/index.vue` ve `NavCollapse/index.vue`) otomatik olarak:

1. `pageCode` değerine göre locale dosyasından çeviriyi bulur
2. Eğer object yapısı varsa, `title` ve `subCaption` property'lerini kullanır
3. Eğer string yapısı varsa, direkt değeri kullanır
4. Fallback olarak database'den gelen `item.title` ve `item.subCaption` değerlerini kullanır

**Önemli Not:** Vue-i18n'de `i18n.t()` fonksiyonu object döndürmeyebilir. Bu yüzden component'lerde direkt messages objesine erişim kullanılır:

```typescript
// NavItem/index.vue içinde
const menuTitle = computed(() => {
  const currentLocale = localeStore.locale;
  
  if (!props.item.pageCode || !i18n) {
    return props.item.title || '';
  }
  
  // Direct access to messages (i18n.t() may not return objects correctly)
  const i18nGlobal = i18n?.global || i18n;
  const messages = i18nGlobal?.messages || {};
  const localeMessages = messages[currentLocale] || messages.value?.[currentLocale] || {};
  
  let menuValue: any = null;
  
  // Try direct access: menu.apps-automated-forms
  if (localeMessages.menu && localeMessages.menu[props.item.pageCode]) {
    menuValue = localeMessages.menu[props.item.pageCode];
  } else {
    // Fallback to i18n.t() if direct access doesn't work
    menuValue = i18n.t(`menu.${props.item.pageCode}`);
  }
  
  // If it's an object, get title property, otherwise use the value directly
  if (typeof menuValue === 'object' && menuValue !== null && menuValue.title) {
    return menuValue.title;
  }
  
  return menuValue || props.item.title;
});
```

### MinIO'dan Locale Yükleme

Locale dosyaları öncelik sırasına göre yüklenir:

1. **MinIO (Öncelikli):** Authenticated kullanıcılar için MinIO'dan locale dosyaları yüklenir
2. **Build-time Files:** Login sayfası veya MinIO'da bulunmayan diller için build-time dosyalar kullanılır

**Deep Merge Stratejisi:**
- MinIO'dan gelen data (source) build-time data'yı (target) override eder
- Eğer build-time'da string olan bir değer MinIO'da object olarak gelirse, tamamen override edilir
- Object yapıları recursive olarak merge edilir

**Örnek:**
```typescript
// Build-time: "apps-automated-forms": "Otomatik Formlar"
// MinIO: "apps-automated-forms": { "title": "Otomatik Formlar 3", "subCaption": "..." }
// Sonuç: MinIO değeri kullanılır (tamamen override)
```

### Reactivity

Menu item'ların dil değişimine reaktif olması için:

1. `computed` property'ler kullanılır
2. `localeStore.locale` değerine erişilir (reactive dependency)
3. Locale değiştiğinde computed property'ler otomatik olarak yeniden hesaplanır

**Örnek:**
```typescript
const menuTitle = computed(() => {
  // Access localeStore.locale to make this computed reactive
  const currentLocale = localeStore.locale;
  // ... rest of the logic
});
```

## Mevcut Çeviriler

Aşağıdaki sayfalar için tam dil desteği mevcuttur:

- ✅ **Event Mesajları** (`events`)
- ✅ **Kullanıcı Grupları** (`groups`)
- ✅ **Giriş Sayfası** (`login`)
- ✅ **Side Menu Manager** (`side-menu-manager`)
- ✅ **Side Menu Items** (`menu.*`) - Title ve SubCaption desteği ile

## Sorun Giderme

### Problem: `useI18n is not defined`

**Çözüm:** Legacy mode kullanıldığı için `useI18n()` çalışmaz. `useNuxtApp()` ile i18n instance'ına erişin:

```typescript
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => i18n?.t?.(key) || key;
```

### Problem: `instance?.proxy?.$t is not a function`

**Çözüm:** `getCurrentInstance()` yerine `useNuxtApp()` kullanın (yukarıdaki örnekteki gibi).

### Problem: Çeviriler güncellenmiyor

**Çözüm:** 
1. Locale store'dan locale değişikliğini kontrol edin
2. i18n instance'ının locale'ını manuel güncelleyin (gerekirse)
3. Component'i force re-render edin (key kullanarak)

### Problem: Side Menu item'ların title'ı dil değişimine uğramıyor (SubCaption'lı item'lar)

**Çözüm:** 
1. Vue-i18n'de `i18n.t()` fonksiyonu object döndürmeyebilir. Direkt messages objesine erişin:
   ```typescript
   const i18nGlobal = i18n?.global || i18n;
   const messages = i18nGlobal?.messages || {};
   const localeMessages = messages[currentLocale] || messages.value?.[currentLocale] || {};
   const menuValue = localeMessages.menu?.[pageCode];
   ```
2. `computed` property kullanın ve `localeStore.locale`'a erişin (reactive dependency)
3. Locale dosyasında object yapısının doğru olduğundan emin olun:
   ```json
   {
     "menu": {
       "apps-automated-forms": {
         "title": "Otomatik Formlar",
         "subCaption": "..."
       }
     }
   }
   ```
4. MinIO'dan locale yükleme işleminin tamamlandığından emin olun (console log'larını kontrol edin)
5. Browser console'da debug fonksiyonlarını kullanın:
   ```javascript
   // Locale cache'i temizle
   clearLocaleCache();
   
   // Locale cache'i kontrol et
   checkLocaleCache();
   
   // i18n messages'ı kontrol et
   checkI18nMessages('tr');
   
   // Locale'leri yeniden yükle
   reloadLocales();
   ```

### Problem: Menu item'ın pageCode'u ile locale key'i eşleşmiyor

**Çözüm:**
1. MongoDB'deki menu item'ın `pageCode` değerini kontrol edin
2. Locale dosyasındaki key'in `menu.{pageCode}` formatında olduğundan emin olun
3. Console'da debug log'larını kontrol edin (NavItem component'inde otomatik log'lar var)

## Referans Dosyalar

- **Örnek Implementasyon:** `Mng.Ui/pages/apps/groups/index.vue`
- **Locale Store:** `Mng.Ui/stores/locale.ts`
- **i18n Plugin:** `Mng.Ui/plugins/vuetify.ts`
- **Locale Loader:** `Mng.Ui/plugins/z-locale-loader.client.ts` (MinIO'dan locale yükleme)
- **Locale Files:** `Mng.Ui/utils/locales/*.json`
- **Side Menu Components:**
  - `Mng.Ui/components/lc/Full/vertical-sidebar/NavItem/index.vue` (Menu item render)
  - `Mng.Ui/components/lc/Full/vertical-sidebar/NavCollapse/index.vue` (Collapsible menu item)
  - `Mng.Ui/components/apps/side-menu-manager/MenuItemForm.vue` (Menu item form ve locale update)

## Gelecek Geliştirmeler

- [ ] Tüm sayfalar için dil desteği
- [x] Dinamik çeviri yükleme (MinIO'dan) ✅
- [ ] Çeviri editörü UI
- [x] Otomatik çeviri entegrasyonu (MngLLM) ✅ (Side Menu için)
- [ ] Çeviri key type safety (TypeScript)

## Notlar

- Çeviri key'leri hierarchical yapıda tutulmalıdır
- Her sayfa için ayrı bir namespace kullanın (örn: `groups`, `users`, `events`)
- Ortak çeviriler için `common` namespace'ini kullanın
- Eksik çeviriler için fallback mekanizması her zaman çalışmalıdır
- Legacy mode kullanıldığı için `useI18n()` composable'ı çalışmaz
