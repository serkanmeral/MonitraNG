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

## Mevcut Çeviriler

Aşağıdaki sayfalar için tam dil desteği mevcuttur:

- ✅ **Event Mesajları** (`events`)
- ✅ **Kullanıcı Grupları** (`groups`)
- ✅ **Giriş Sayfası** (`login`)
- ✅ **Side Menu Manager** (`side-menu-manager`)

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

## Referans Dosyalar

- **Örnek Implementasyon:** `Mng.Ui/pages/apps/groups/index.vue`
- **Locale Store:** `Mng.Ui/stores/locale.ts`
- **i18n Plugin:** `Mng.Ui/plugins/vuetify.ts`
- **Locale Files:** `Mng.Ui/utils/locales/*.json`

## Gelecek Geliştirmeler

- [ ] Tüm sayfalar için dil desteği
- [ ] Dinamik çeviri yükleme (MinIO'dan)
- [ ] Çeviri editörü UI
- [ ] Otomatik çeviri entegrasyonu (MngLLM)
- [ ] Çeviri key type safety (TypeScript)

## Notlar

- Çeviri key'leri hierarchical yapıda tutulmalıdır
- Her sayfa için ayrı bir namespace kullanın (örn: `groups`, `users`, `events`)
- Ortak çeviriler için `common` namespace'ini kullanın
- Eksik çeviriler için fallback mekanizması her zaman çalışmalıdır
- Legacy mode kullanıldığı için `useI18n()` composable'ı çalışmaz
