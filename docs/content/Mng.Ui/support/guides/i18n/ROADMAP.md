# Çoklu Dil Desteği (i18n) Yol Haritası 🌐

## Genel Bakış

Bu dokümantasyon, MonitraNG projesi için **Frontend (Mng.Ui)** ve **Backend (MngDataGateway, MngKeeper, vb.)** çoklu dil desteği implementasyonunun kapsamlı yol haritasını içermektedir.

**Temel Strateji:**
- **Backend**: Sadece error code'lar döndürür (dil bağımsız)
- **Frontend**: Tüm mesaj çevirilerini yönetir (i18n)
- **Validation**: Schema-based ve expression-based validation mesajları için i18n key desteği

---

## İçindekiler

1. [Mevcut Durum Analizi](#mevcut-durum-analizi)
2. [Genel Mimari](#genel-mimari)
3. [Frontend i18n Implementasyonu](#frontend-i18n-implementasyonu)
4. [Backend Error Code Sistemi](#backend-error-code-sistemi)
5. [Validation Mesajları](#validation-mesajları)
6. [API Response Formatı](#api-response-formatı)
7. [Implementasyon Aşamaları](#implementasyon-aşamaları)
8. [Best Practices](#best-practices)
9. [Test Stratejisi](#test-stratejisi)

---

## Mevcut Durum Analizi

### Frontend (Mng.Ui)

**✅ Mevcut:**
- `vue-i18n` 9.9.1 kurulu
- `plugins/vuetify.ts` içinde i18n yapılandırması var
- `utils/locales/` klasöründe dil dosyaları mevcut (tr, en, fr, ar, zh)
- `messages.ts` ile dil dosyaları import ediliyor
- Header'da `LanguageDD` component'i var (en, fr, ro, zh)
- ✅ **Locale Store (Pinia)** - `stores/locale.ts` oluşturuldu
- ✅ **Login Sayfası Localization** - Tam i18n desteği eklendi
- ✅ **Side Menu Manager Localization** - Tüm UI metinleri i18n'e çevrildi
- ✅ **RTL/LTR Desteği** - Arapça için dinamik RTL/LTR dönüşümleri
- ✅ **Dinamik Locale Loading** - MinIO'dan runtime locale yükleme (`z-locale-loader.client.ts`)
- ✅ **Locale Cache Management** - localStorage cache ve invalidation mekanizması
- ✅ **API Gateway Entegrasyonu** - MngLLM API çağrıları Gateway üzerinden
- ✅ **ManagerOrAdminAuthorization** - Locale güncelleme yetkilendirmesi

**❌ Eksikler:**
- Component'lerde i18n kullanımı (kısmen tamamlandı - Side Menu Manager ve Login sayfası tamamlandı)
- Vuetify locale entegrasyonu (RTL desteği var, Vuetify component mesajları için eksik)
- Error mesajları için i18n desteği (planlanıyor)

### Backend

**✅ Mevcut:**
- Error response formatı standart (`ErrorResponseDto`, `ErrorDetailDto`)
- Validation mesajları hardcoded İngilizce
- Error code'lar string olarak döndürülüyor ("VALIDATION_ERROR", "DATASET_NOT_FOUND", vb.)

**❌ Eksikler:**
- Standart error code enum/constants yok
- Validation mesajlarında i18n key desteği yok
- Exception'larda error code mapping sistemi yok

---

## Genel Mimari

### Yaklaşım: Error Code + Frontend Translation

```
┌─────────────────┐
│   Backend API   │
│                 │
│  Error Code:    │
│  "VALIDATION.   │
│  REQUIRED_FIELD"│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Frontend (UI)  │
│                 │
│  i18n Key:      │
│  errors.        │
│  validation.    │
│  requiredField  │
│                 │
│  → "Bu alan     │
│     zorunludur" │
│  → "This field  │
│     is required"│
└─────────────────┘
```

### Avantajlar

1. **Backend Dil Bağımsız**: Backend sadece error code döndürür, dil bağımsızdır
2. **Frontend Merkezi Çeviri**: Tüm mesajlar frontend'de merkezi olarak yönetilir
3. **Performans**: Backend'de çeviri yükü yok
4. **Esneklik**: Yeni dil eklemek sadece frontend'de dosya eklemekle olur
5. **Tutarlılık**: Tek çeviri noktası (frontend)
6. **Bakım Kolaylığı**: Çeviri değişiklikleri için backend deploy gerekmez

---

## Frontend i18n Implementasyonu

### 1. Nuxt i18n Modülü

**Seçenek 1: @nuxtjs/i18n (Önerilen)**
- Nuxt 3 için resmi i18n modülü
- Type-safe çeviriler
- Lazy loading
- SEO desteği (SSR için)

**Seçenek 2: Vue I18n (Mevcut)**
- Zaten kurulu (`vue-i18n` 9.9.1)
- Manuel yapılandırma gerekir
- Daha esnek ama daha fazla kod

**Karar: Vue I18n ile devam (mevcut kurulum kullanılacak)**

### 2. Dil Dosyaları Yapısı

**Klasör Yapısı:**
```
Mng.Ui/utils/locales/
├── tr.json          # Türkçe (varsayılan) - YENİ
├── en.json          # İngilizce (fallback) - MEVCUT (güncellenecek)
├── zh.json          # Çince - MEVCUT (çeviri içerikleri eklenecek)
├── ar.json          # Arapça - MEVCUT (çeviri içerikleri eklenecek, RTL gerekli)
├── messages.ts      # Import ve export (güncellenecek)
└── types.ts         # TypeScript type definitions (opsiyonel)
```

**Çeviri Key Yapısı (Hierarchical):**
```json
{
  "common": {
    "actions": {
      "save": "Kaydet",
      "cancel": "İptal",
      "delete": "Sil",
      "edit": "Düzenle",
      "create": "Oluştur",
      "search": "Ara",
      "filter": "Filtrele",
      "export": "Dışa Aktar",
      "import": "İçe Aktar",
      "refresh": "Yenile",
      "reset": "Sıfırla"
    },
    "status": {
      "loading": "Yükleniyor...",
      "saving": "Kaydediliyor...",
      "success": "Başarılı",
      "error": "Hata",
      "warning": "Uyarı",
      "info": "Bilgi"
    },
    "confirm": {
      "delete": "Silmek istediğinize emin misiniz?",
      "cancel": "Değişiklikleri kaydetmeden çıkmak istediğinize emin misiniz?",
      "save": "Değişiklikleri kaydetmek istediğinize emin misiniz?"
    }
  },
  
  "pages": {
    "login": {
      "title": "Giriş Yap",
      "username": "Kullanıcı Adı",
      "password": "Şifre",
      "submit": "Giriş Yap",
      "forgotPassword": "Şifremi Unuttum"
    },
    "dashboard": {
      "title": "Kontrol Paneli",
      "welcome": "Hoş Geldiniz"
    },
    "domains": {
      "title": "Domain Yönetimi",
      "list": {
        "title": "Domain Listesi",
        "create": "Yeni Domain",
        "edit": "Domain Düzenle",
        "delete": "Domain Sil"
      }
    },
    "datasets": {
      "title": "Dataset Yönetimi",
      "list": {
        "title": "Dataset Listesi",
        "create": "Yeni Dataset",
        "edit": "Dataset Düzenle"
      }
    },
    "users": {
      "title": "Kullanıcı Yönetimi",
      "list": {
        "title": "Kullanıcı Listesi",
        "create": "Yeni Kullanıcı"
      }
    }
  },
  
  "menu": {
    "dashboard": "Kontrol Paneli",
    "domains": "Domainler",
    "datasets": "Dataset'ler",
    "users": "Kullanıcılar",
    "groups": "Gruplar",
    "pages": "Sayfalar"
  },
  
  "forms": {
    "validation": {
      "required": "{field} gereklidir",
      "minLength": "{field} en az {min} karakter olmalıdır",
      "maxLength": "{field} en fazla {max} karakter olabilir",
      "min": "{field} en az {min} değerinde olmalıdır",
      "max": "{field} en fazla {max} değerinde olabilir",
      "pattern": "{field} geçerli formatta olmalıdır",
      "email": "Geçerli bir e-posta adresi giriniz",
      "url": "Geçerli bir URL giriniz",
      "number": "{field} sayı olmalıdır"
    },
    "fields": {
      "name": "Ad",
      "email": "E-posta",
      "username": "Kullanıcı Adı",
      "password": "Şifre",
      "confirmPassword": "Şifre Tekrar",
      "phone": "Telefon",
      "address": "Adres"
    }
  },
  
  "errors": {
    "general": {
      "title": "Hata Oluştu",
      "message": "Bir hata oluştu. Lütfen tekrar deneyiniz.",
      "tryAgain": "Tekrar Dene"
    },
    "network": {
      "title": "Bağlantı Hatası",
      "message": "Sunucuya bağlanılamadı. Lütfen internet bağlantınızı kontrol ediniz.",
      "timeout": "İstek zaman aşımına uğradı. Lütfen tekrar deneyiniz."
    },
    "auth": {
      "loginFailed": "Giriş başarısız. Kullanıcı adı veya şifre hatalı.",
      "unauthorized": "Bu işlem için yetkiniz yok.",
      "forbidden": "Bu kaynağa erişim yetkiniz yok.",
      "sessionExpired": "Oturum süresi dolmuş. Lütfen tekrar giriş yapın.",
      "tokenExpired": "Token süresi dolmuş. Lütfen tekrar giriş yapın."
    },
    "validation": {
      "title": "Doğrulama Hatası",
      "requiredField": "Bu alan zorunludur",
      "invalidFormat": "Geçersiz format",
      "minLength": "En az {min} karakter olmalıdır",
      "maxLength": "En fazla {max} karakter olabilir",
      "min": "En az {min} değerinde olmalıdır",
      "max": "En fazla {max} değerinde olmalıdır",
      "pattern": "Geçerli formatta olmalıdır",
      "uniqueConstraint": "{field} zaten kullanılıyor",
      "expressionFailed": "Doğrulama kuralı başarısız: {expression}"
    },
    "notFound": {
      "title": "Bulunamadı",
      "message": "İstediğiniz kaynak bulunamadı.",
      "pageNotFound": "Sayfa bulunamadı",
      "resourceNotFound": "Kaynak bulunamadı"
    },
    "server": {
      "title": "Sunucu Hatası",
      "message": "Sunucuda bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.",
      "internalError": "İç sunucu hatası oluştu."
    },
    "code": {
      "VALIDATION_ERROR": "Doğrulama hatası oluştu",
      "VALIDATION_REQUIRED_FIELD": "Bu alan zorunludur",
      "VALIDATION_INVALID_FORMAT": "Geçersiz format",
      "VALIDATION_MIN_LENGTH": "En az {min} karakter olmalıdır",
      "VALIDATION_MAX_LENGTH": "En fazla {max} karakter olabilir",
      "VALIDATION_UNIQUE_CONSTRAINT": "{field} zaten kullanılıyor",
      "DATASET_NOT_FOUND": "Dataset bulunamadı",
      "DATA_NOT_FOUND": "Veri bulunamadı",
      "UNAUTHORIZED": "Bu işlem için yetkiniz yok",
      "FORBIDDEN": "Bu kaynağa erişim yetkiniz yok",
      "INTERNAL_ERROR": "Sunucu hatası oluştu"
    }
  },
  
  "messages": {
    "success": {
      "created": "{resource} başarıyla oluşturuldu",
      "updated": "{resource} başarıyla güncellendi",
      "deleted": "{resource} başarıyla silindi",
      "saved": "Değişiklikler kaydedildi"
    },
    "info": {
      "noData": "Gösterilecek veri yok",
      "noResults": "Sonuç bulunamadı"
    }
  }
}
```

### 3. Locale Store (Pinia)

**Dosya:** `stores/locale.ts`

```typescript
import { defineStore } from 'pinia'
import { useI18n } from 'vue-i18n'

export type SupportedLocale = 'tr' | 'en' | 'zh' | 'ar'

const LOCALE_STORAGE_KEY = 'monitrang_locale'
const DEFAULT_LOCALE: SupportedLocale = 'tr'
const FALLBACK_LOCALE: SupportedLocale = 'en'

interface LocaleState {
  locale: SupportedLocale
  availableLocales: SupportedLocale[]
  isLoading: boolean
}

export const useLocaleStore = defineStore('locale', {
  state: (): LocaleState => ({
    locale: DEFAULT_LOCALE,
    availableLocales: ['tr', 'en', 'zh', 'ar'],
    isLoading: false
  }),

  getters: {
    currentLocale: (state): SupportedLocale => state.locale,
    isTurkish: (state): boolean => state.locale === 'tr',
    isEnglish: (state): boolean => state.locale === 'en',
    localeName: (state): string => {
      const names: Record<SupportedLocale, string> = {
        tr: 'Türkçe',
        en: 'English',
        zh: '中文',
        ar: 'العربية'
      }
      return names[state.locale]
    },
    isRTL: (state): boolean => state.locale === 'ar'
  },

  actions: {
    /**
     * Initialize locale from localStorage or browser language
     */
    initializeLocale() {
      if (process.client) {
        // 1. Check localStorage
        const savedLocale = localStorage.getItem(LOCALE_STORAGE_KEY) as SupportedLocale | null
        if (savedLocale && this.availableLocales.includes(savedLocale)) {
          this.setLocale(savedLocale, false) // Don't save to localStorage again
          return
        }

        // 2. Check browser language
        const browserLang = navigator.language.split('-')[0] as SupportedLocale
        if (this.availableLocales.includes(browserLang)) {
          this.setLocale(browserLang, true)
          return
        }

        // 3. Default to Turkish
        this.setLocale(DEFAULT_LOCALE, true)
      }
    },

    /**
     * Set locale and save to localStorage
     */
    setLocale(locale: SupportedLocale, saveToStorage: boolean = true) {
      if (!this.availableLocales.includes(locale)) {
        console.warn(`Locale ${locale} is not supported, falling back to ${DEFAULT_LOCALE}`)
        locale = DEFAULT_LOCALE
      }

      this.locale = locale
      
      // Update vue-i18n
      const { locale: i18nLocale } = useI18n()
      i18nLocale.value = locale

      // Save to localStorage
      if (process.client && saveToStorage) {
        localStorage.setItem(LOCALE_STORAGE_KEY, locale)
      }
    },

    /**
     * Toggle between Turkish and English
     */
    toggleLocale() {
      const newLocale: SupportedLocale = this.locale === 'tr' ? 'en' : 'tr'
      this.setLocale(newLocale)
    }
  }
})
```

### 4. Locale Plugin

**Dosya:** `plugins/locale.client.ts`

```typescript
export default defineNuxtPlugin((nuxtApp) => {
  const localeStore = useLocaleStore()

  // Initialize locale on app start
  localeStore.initializeLocale()

  // Watch for locale changes and update i18n
  watch(() => localeStore.locale, (newLocale) => {
    const { locale } = useI18n()
    locale.value = newLocale
  })
})
```

### 5. i18n Yapılandırması Güncelleme

**Dosya:** `plugins/vuetify.ts` (güncelleme)

```typescript
import { createI18n } from "vue-i18n";
import messages from "@/utils/locales/messages";
import { useLocaleStore } from "@/stores/locale";

// Initialize with default locale (will be updated by store)
const i18n = createI18n({
  locale: "tr", // Default to Turkish
  fallbackLocale: "en", // Fallback to English
  messages: messages,
  legacy: false, // Use Composition API mode
  silentTranslationWarn: process.env.NODE_ENV === 'production',
  silentFallbackWarn: process.env.NODE_ENV === 'production',
});

export default defineNuxtPlugin((nuxtApp) => {
  // ... existing vuetify setup ...
  
  nuxtApp.vueApp.use(i18n);
  
  // Initialize locale store after i18n is registered
  const localeStore = useLocaleStore();
  localeStore.initializeLocale();
});
```

### 6. LanguageDD Component Güncelleme

**Dosya:** `components/lc/Full/vertical-header/LanguageDD.vue`

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useLocaleStore, type SupportedLocale } from '@/stores/locale'
import { useI18n } from 'vue-i18n'

const localeStore = useLocaleStore()
const { t } = useI18n()

const languages = [
  { code: 'tr' as SupportedLocale, name: 'Türkçe', flag: '/images/flag/icon-flag-tr.svg' },
  { code: 'en' as SupportedLocale, name: 'English', flag: '/images/flag/icon-flag-en.svg' },
  { code: 'zh' as SupportedLocale, name: '中文', flag: '/images/flag/icon-flag-zh.svg' },
  { code: 'ar' as SupportedLocale, name: 'العربية', flag: '/images/flag/icon-flag-ar.svg' }
]

const currentFlag = computed(() => {
  const lang = languages.find(l => l.code === localeStore.locale)
  return lang?.flag || languages[0].flag
})

const changeLanguage = (locale: SupportedLocale) => {
  localeStore.setLocale(locale)
}
</script>

<template>
  <v-menu :close-on-content-click="false" location="bottom">
    <template v-slot:activator="{ props }">
      <v-btn icon variant="text" color="primary" v-bind="props">
        <v-avatar size="22">
          <img :src="currentFlag" :alt="localeStore.locale" width="22" height="22" class="obj-cover" />
        </v-avatar>
      </v-btn>
    </template>
    <v-sheet rounded="md" width="200" elevation="10">
      <v-list class="theme-list">
        <v-list-item
          v-for="lang in languages"
          :key="lang.code"
          color="primary"
          :active="localeStore.locale === lang.code"
          class="d-flex align-center"
          @click="changeLanguage(lang.code)"
        >
          <template v-slot:prepend>
            <v-avatar size="22">
              <img :src="lang.flag" :alt="lang.code" width="22" height="22" class="obj-cover" />
            </v-avatar>
          </template>
          <v-list-item-title class="text-subtitle-1 font-weight-regular">
            {{ lang.name }}
          </v-list-item-title>
        </v-list-item>
      </v-list>
    </v-sheet>
  </v-menu>
</template>
```

### 7. Vuetify Locale Entegrasyonu

**Vuetify Locale Dosyaları:**
- Vuetify kendi locale dosyalarını destekler
- `vuetify/locale/tr.ts`, `vuetify/locale/en.ts`, `vuetify/locale/zhHans.ts`, `vuetify/locale/ar.ts` kullanılabilir

**Güncelleme:** `plugins/vuetify.ts`

```typescript
import { tr, en, zhHans, ar } from 'vuetify/locale'

export default defineNuxtPlugin((nuxtApp) => {
  const localeStore = useLocaleStore()
  
  const vuetify = createVuetify({
    // ... existing config ...
    locale: {
      locale: localeStore.locale,
      fallback: 'en',
      messages: {
        tr,
        en,
        zh: zhHans, // Çince için zhHans kullanılır
        ar
      },
      rtl: {
        ar: true // Arapça için RTL aktif
      }
    },
    defaults: {
      // RTL için global defaults (opsiyonel)
      VCard: {
        // ... existing defaults ...
      }
    }
  })
  
  // ... rest of config ...
})
```

### 7.1 RTL (Right-to-Left) Desteği

**Arapça için RTL Desteği:**

Vuetify 3 RTL desteği sağlar. Arapça seçildiğinde otomatik olarak RTL layout'a geçer.

**Ek Yapılandırma (Gerekirse):**

```typescript
// plugins/vuetify.ts
const vuetify = createVuetify({
  locale: {
    locale: localeStore.locale,
    rtl: {
      ar: true // Arapça için RTL
    }
  }
})

// Layout component'lerinde (opsiyonel)
watch(() => localeStore.locale, (newLocale) => {
  if (process.client) {
    document.documentElement.setAttribute('dir', newLocale === 'ar' ? 'rtl' : 'ltr')
  }
})
```

**CSS Desteği:**

Vuetify RTL için gerekli CSS'i otomatik olarak uygular. Özel CSS gerekiyorsa:

```scss
// assets/scss/rtl.scss
[dir="rtl"] {
  // RTL-specific styles
  .v-list-item {
    text-align: right;
  }
}
```

### 8. Error Handler i18n Entegrasyonu

**Dosya:** `composables/useErrorHandler.ts` (yeni)

```typescript
import { useI18n } from 'vue-i18n'

export const useErrorHandler = () => {
  const { t } = useI18n()

  /**
   * Translate error code to user-friendly message
   */
  const translateError = (error: any): string => {
    // Check if error has a code
    if (error?.code) {
      const codeKey = `errors.code.${error.code}`
      const translated = t(codeKey, error.params || {})
      
      // If translation exists (not the key itself), return it
      if (translated !== codeKey) {
        return translated
      }
    }

    // Check if error has a message key
    if (error?.messageKey) {
      return t(error.messageKey, error.params || {})
    }

    // Fallback to error message or default
    return error?.message || t('errors.general.message')
  }

  /**
   * Translate validation errors
   */
  const translateValidationError = (error: any): string => {
    const field = error.field || 'field'
    const code = error.code || 'VALIDATION_ERROR'
    
    // Map validation error codes to i18n keys
    const codeMap: Record<string, string> = {
      'VALIDATION_REQUIRED_FIELD': 'errors.validation.requiredField',
      'VALIDATION_MIN_LENGTH': 'errors.validation.minLength',
      'VALIDATION_MAX_LENGTH': 'errors.validation.maxLength',
      'VALIDATION_MIN': 'errors.validation.min',
      'VALIDATION_MAX': 'errors.validation.max',
      'VALIDATION_PATTERN': 'errors.validation.pattern',
      'VALIDATION_UNIQUE_CONSTRAINT': 'errors.validation.uniqueConstraint',
      'VALIDATION_EXPRESSION_FAILED': 'errors.validation.expressionFailed'
    }

    const i18nKey = codeMap[code] || 'errors.validation.title'
    const params = { field, ...error.params }

    return t(i18nKey, params)
  }

  return {
    translateError,
    translateValidationError
  }
}
```

---

## Backend Error Code Sistemi

### 1. Error Code Constants

**Dosya:** `MngDataGateway/Core/MngDataGateway.Application/Constants/ErrorCodes.cs` (yeni)

```csharp
namespace MngDataGateway.Application.Constants;

/// <summary>
/// Standard error codes for API responses
/// These codes are language-independent and will be translated by the frontend
/// </summary>
public static class ErrorCodes
{
    // Validation errors (4xx)
    public const string VALIDATION_ERROR = "VALIDATION_ERROR";
    public const string VALIDATION_REQUIRED_FIELD = "VALIDATION_REQUIRED_FIELD";
    public const string VALIDATION_INVALID_FORMAT = "VALIDATION_INVALID_FORMAT";
    public const string VALIDATION_MIN_LENGTH = "VALIDATION_MIN_LENGTH";
    public const string VALIDATION_MAX_LENGTH = "VALIDATION_MAX_LENGTH";
    public const string VALIDATION_MIN = "VALIDATION_MIN";
    public const string VALIDATION_MAX = "VALIDATION_MAX";
    public const string VALIDATION_PATTERN = "VALIDATION_PATTERN";
    public const string VALIDATION_UNIQUE_CONSTRAINT = "VALIDATION_UNIQUE_CONSTRAINT";
    public const string VALIDATION_EXPRESSION_FAILED = "VALIDATION_EXPRESSION_FAILED";

    // Authentication & Authorization errors (4xx)
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string FORBIDDEN = "FORBIDDEN";
    public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
    public const string SESSION_EXPIRED = "SESSION_EXPIRED";

    // Not found errors (4xx)
    public const string DATASET_NOT_FOUND = "DATASET_NOT_FOUND";
    public const string DATA_NOT_FOUND = "DATA_NOT_FOUND";
    public const string RESOURCE_NOT_FOUND = "RESOURCE_NOT_FOUND";

    // Server errors (5xx)
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string DATABASE_ERROR = "DATABASE_ERROR";
    public const string EXTERNAL_SERVICE_ERROR = "EXTERNAL_SERVICE_ERROR";
}
```

### 2. ValidationErrorDto Güncelleme

**Dosya:** `MngDataGateway/Core/MngDataGateway.Application/DTOs/Validation/ValidationErrorDto.cs` (güncelleme)

```csharp
using System.Collections.Generic;

namespace MngDataGateway.Application.DTOs.Validation;

/// <summary>
/// Validation error details with i18n support
/// </summary>
public class ValidationErrorDto
{
    /// <summary>
    /// Field name that failed validation
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Error code for frontend translation (e.g., "VALIDATION_REQUIRED_FIELD")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message (English, for fallback)
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional parameters for i18n translation (e.g., { "min": 5, "max": 10 })
    /// </summary>
    public Dictionary<string, object>? Params { get; set; }

    /// <summary>
    /// Original value that failed validation
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Optional i18n key override (if provided, frontend will use this instead of Code mapping)
    /// </summary>
    public string? MessageKey { get; set; }
}
```

### 3. ValidationService Güncelleme

**Dosya:** `MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/ValidationService.cs`

**Örnek Güncelleme (Min/Max Length):**

```csharp
// ÖNCE (Hardcoded İngilizce):
errors.Add(new ValidationErrorDto
{
    Field = field.name,
    Message = rules.message ?? $"Field '{field.name}' must be at least {rules.minLength.Value} characters",
    Value = value
});

// SONRA (Error Code + Params):
errors.Add(new ValidationErrorDto
{
    Field = field.name,
    Code = ErrorCodes.VALIDATION_MIN_LENGTH,
    Message = rules.message ?? $"Field '{field.name}' must be at least {rules.minLength.Value} characters", // Fallback
    Params = new Dictionary<string, object>
    {
        { "field", field.name },
        { "min", rules.minLength.Value }
    },
    MessageKey = rules.message, // If rules.message is an i18n key, use it
    Value = value
});
```

### 4. ControllerHelper Güncelleme

**Dosya:** `MngDataGateway/Presentation/MngDataGateway.Api/Helpers/ControllerHelper.cs`

Mevcut metodlar zaten `Code` alanını kullanıyor, sadece ErrorCodes constants'ını kullanacak şekilde güncellenebilir:

```csharp
public static IActionResult HandleValidationError(
    this ControllerBase controller,
    DataGatewayException ex,
    string path,
    ILogger? logger = null)
{
    logger?.LogWarning(ex, "Validation error at {Path}", path);

    return controller.BadRequest(new ErrorResponseDto
    {
        Success = false,
        Error = new ErrorDetailDto
        {
            Code = ErrorCodes.VALIDATION_ERROR, // Use constant
            Message = ex.Message,
            Details = ex.ValidationErrors
        },
        Meta = CreateMeta(path)
    });
}
```

---

## Validation Mesajları

### Schema-based Validation

Dataset schema'larında `rules.message` alanında i18n key kullanımı:

```json
{
  "fields": [
    {
      "name": "email",
      "fieldType": "text",
      "rules": {
        "required": true,
        "pattern": "^[\\w\\.-]+@[\\w\\.-]+\\.[a-zA-Z]{2,}$",
        "message": "forms.validation.email" // i18n key
      }
    }
  ]
}
```

**Backend Logic:**
```csharp
// ValidationService.cs
if (!string.IsNullOrEmpty(rules.message))
{
    // Check if message is an i18n key (starts with common prefixes)
    if (rules.message.Contains('.') && 
        (rules.message.StartsWith("forms.") || 
         rules.message.StartsWith("errors.") || 
         rules.message.StartsWith("validation.")))
    {
        error.MessageKey = rules.message; // Frontend will use this
    }
    else
    {
        error.Message = rules.message; // Use as-is
    }
}
```

### Expression-based Validation

Kompleks validation kuralları için:

```json
{
  "validations": [
    {
      "expression": "price / pageCount <= 10",
      "message": "errors.validation.expressionFailed",
      "params": {
        "expression": "price / pageCount <= 10"
      }
    }
  ]
}
```

---

## API Response Formatı

### Success Response (Değişiklik Yok)

```json
{
  "success": true,
  "data": { ... },
  "meta": {
    "timestamp": "2024-01-15T10:30:00Z",
    "path": "/api/data/datasets"
  }
}
```

### Error Response (Güncellenmiş)

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed", // Fallback message (English)
    "details": [
      {
        "field": "email",
        "code": "VALIDATION_REQUIRED_FIELD",
        "message": "Email field is required", // Fallback
        "params": {
          "field": "email"
        },
        "messageKey": null, // Optional i18n key override
        "value": null
      },
      {
        "field": "username",
        "code": "VALIDATION_UNIQUE_CONSTRAINT",
        "message": "Username already exists",
        "params": {
          "field": "username"
        },
        "value": "john_doe"
      }
    ]
  },
  "meta": {
    "timestamp": "2024-01-15T10:30:00Z",
    "path": "/api/data/users"
  }
}
```

### Frontend Error Handling

**Dosya:** `composables/useApi.ts` veya `services/apiService.ts`

```typescript
import { useErrorHandler } from '@/composables/useErrorHandler'
import { useI18n } from 'vue-i18n'

export async function handleApiError(error: any) {
  const { translateError, translateValidationError } = useErrorHandler()
  const { t } = useI18n()

  if (error.response?.data?.error) {
    const errorData = error.response.data.error

    // Main error message
    const mainMessage = translateError({
      code: errorData.code,
      message: errorData.message,
      params: errorData.params
    })

    // Validation errors
    if (errorData.details && Array.isArray(errorData.details)) {
      const validationErrors = errorData.details.map((err: any) => ({
        field: err.field,
        message: translateValidationError(err)
      }))

      return {
        message: mainMessage,
        validationErrors
      }
    }

    return { message: mainMessage }
  }

  // Network or other errors
  return {
    message: translateError(error)
  }
}
```

---

## Implementasyon Aşamaları

### Phase 1: Frontend Altyapı (1-2 gün)

**Aşama 1.1: Temel Yapılandırma**
- [ ] `tr.json` dil dosyası oluştur
- [ ] `en.json` dil dosyasını genişlet (mevcut dosya güncelle)
- [ ] `messages.ts` dosyasını güncelle (tr ekle)
- [ ] Locale store oluştur (`stores/locale.ts`)
- [ ] Locale plugin oluştur (`plugins/locale.client.ts`)

**Aşama 1.2: i18n Yapılandırması**
- [ ] `plugins/vuetify.ts` içinde i18n yapılandırmasını güncelle
- [ ] Vuetify locale entegrasyonu ekle
- [ ] LanguageDD component'i güncelle

**Aşama 1.3: Test**
- [ ] Dil değiştirme testi
- [ ] localStorage kaydı testi
- [ ] Vuetify component mesajları testi

### Phase 2: Backend Error Code Sistemi (1-2 gün)

**Aşama 2.1: Error Code Constants**
- [ ] `ErrorCodes.cs` dosyası oluştur
- [ ] Tüm servislerde error code'ları güncelle (MngDataGateway, MngKeeper, vb.)

**Aşama 2.2: DTO Güncellemeleri**
- [ ] `ValidationErrorDto` güncelle (Code, Params, MessageKey ekle)
- [ ] `ErrorDetailDto` kontrol et (zaten Code var)

**Aşama 2.3: Validation Service Güncelleme**
- [ ] `ValidationService.cs` güncelle (error code ekle)
- [ ] Tüm validation mesajlarını error code'a dönüştür

**Aşama 2.4: Controller Helper Güncelleme**
- [ ] `ControllerHelper.cs` güncelle (ErrorCodes kullan)
- [ ] Exception handler'ları güncelle

### Phase 3: Frontend Error Handling (1 gün)

**Aşama 3.1: Error Handler Composable**
- [ ] `composables/useErrorHandler.ts` oluştur
- [ ] Error translation logic ekle
- [ ] Validation error translation ekle

**Aşama 3.2: API Service Güncelleme**
- [ ] `services/apiService.ts` güncelle
- [ ] Error handler entegrasyonu
- [ ] Toast/notification mesajlarını i18n'e çevir

### Phase 4: Temel Sayfalar (2-3 gün)

**Aşama 4.1: Login Sayfası**
- [ ] Login sayfası metinlerini i18n'e çevir
- [ ] Error mesajlarını test et

**Aşama 4.2: Dashboard**
- [ ] Dashboard metinlerini i18n'e çevir
- [ ] Menü metinlerini i18n'e çevir

**Aşama 4.3: Domain/Dataset/User Sayfaları**
- [ ] Liste sayfalarını i18n'e çevir
- [ ] Form sayfalarını i18n'e çevir
- [ ] Error mesajlarını test et

### Phase 5: Validation Mesajları (1-2 gün)

**Aşama 5.1: Form Validation**
- [ ] VeeValidate mesajlarını i18n'e çevir
- [ ] Custom validation mesajlarını i18n'e çevir

**Aşama 5.2: Dataset Validation**
- [ ] Dataset validation mesajlarını i18n'e çevir
- [ ] Schema `rules.message` i18n key desteği test et

### Phase 6: Kalan Sayfalar (Sürekli)

**Aşama 6.1: Yeni Sayfalar**
- [ ] Yeni eklenen her sayfa i18n ile başlar
- [ ] Hardcoded metin kullanılmaz

**Aşama 6.2: Mevcut Sayfalar**
- [ ] Mevcut sayfaları aşamalı olarak i18n'e çevir
- [ ] Öncelik: En çok kullanılan sayfalar

---

## Best Practices

### 1. Key Naming Convention

**Kural:** `category.subcategory.key` (hierarchical)

**İyi Örnekler:**
- `common.actions.save`
- `pages.login.title`
- `errors.validation.requiredField`
- `forms.fields.email`

**Kötü Örnekler:**
- `saveButton` (category yok)
- `loginTitle` (hierarchical değil)
- `error_message` (snake_case yerine camelCase)

### 2. Çeviri Key Organizasyonu

**Kategoriler:**
- `common.*` - Ortak kullanılan metinler (butonlar, durumlar, vb.)
- `pages.*` - Sayfa bazlı metinler
- `menu.*` - Menü öğeleri
- `forms.*` - Form metinleri ve validation
- `errors.*` - Hata mesajları
- `messages.*` - Başarı/bilgi mesajları
- `components.*` - Component bazlı metinler (büyük component'ler için)

### 3. Parametreli Çeviriler

**Kullanım:**
```json
{
  "errors": {
    "validation": {
      "minLength": "{field} en az {min} karakter olmalıdır"
    }
  }
}
```

```typescript
t('errors.validation.minLength', { field: 'Kullanıcı Adı', min: 5 })
// → "Kullanıcı Adı en az 5 karakter olmalıdır"
```

### 4. Fallback Mekanizması

**Yapı:**
1. Önce `errors.code.{ERROR_CODE}` key'ini kontrol et
2. Bulunamazsa `errors.code.INTERNAL_ERROR` kullan
3. En son İngilizce fallback mesajı göster

### 5. Development Mode

**Geliştirme:**
- Eksik çeviri uyarıları göster (console.warn)
- Missing key'leri görselleştir (placeholder ile)

**Production:**
- Silent mode (eksik çeviri uyarıları yok)
- Fallback mesajları göster

### 6. TypeScript Type Safety (Opsiyonel)

**Type Definitions:**
```typescript
// types/i18n.d.ts
declare module 'vue-i18n' {
  export interface DefineLocaleMessage {
    common: {
      actions: {
        save: string
        cancel: string
        // ...
      }
    }
    pages: {
      login: {
        title: string
        // ...
      }
    }
    // ...
  }
}
```

---

## Test Stratejisi

### 1. Unit Tests

**Locale Store:**
- `initializeLocale()` testi
- `setLocale()` testi
- localStorage entegrasyonu testi

**Error Handler:**
- `translateError()` testi
- `translateValidationError()` testi
- Fallback mekanizması testi

### 2. Integration Tests

**API Error Handling:**
- Validation error response testi
- Error code translation testi
- Missing translation fallback testi

### 3. E2E Tests

**Dil Değiştirme:**
- Dil değiştirme butonu testi
- Sayfa yenileme sonrası dil korunması testi
- Tüm sayfalarda dil desteği testi

### 4. Manual Testing Checklist

- [ ] Tüm sayfalarda Türkçe metinler doğru görünüyor mu?
- [ ] Tüm sayfalarda İngilizce metinler doğru görünüyor mu?
- [ ] Dil değiştirme butonu çalışıyor mu?
- [ ] localStorage'a kayıt yapılıyor mu?
- [ ] Error mesajları doğru çevriliyor mu?
- [ ] Validation mesajları doğru çevriliyor mu?
- [ ] Vuetify component mesajları doğru dilde mi?
- [ ] Eksik çeviri durumunda fallback çalışıyor mu?

---

## Sonuç ve Özet

### Tamamlandığında:

✅ **Frontend:**
- Türkçe ve İngilizce dil desteği
- Locale store ile merkezi dil yönetimi
- localStorage ile dil tercihi kaydı
- Tüm error/validation mesajları çevrili
- Vuetify locale entegrasyonu

✅ **Backend:**
- Error code sistemi (dil bağımsız)
- Validation mesajlarında i18n key desteği
- Standart error response formatı

✅ **Kullanıcı Deneyimi:**
- Sorunsuz dil değiştirme
- Tüm mesajlar kullanıcı dilinde
- Tutarlı hata mesajları

### Sonraki Adımlar:

1. **Phase 1-3**: Altyapı ve error handling (5-6 gün)
2. **Phase 4-5**: Temel sayfalar ve validation (3-5 gün)
3. **Phase 6**: Kalan sayfalar (sürekli)

### İyileştirme Fırsatları:

- **TypeScript Type Safety**: Çeviri key'leri için type definitions
- **Lazy Loading**: Dil dosyalarını lazy load (büyük projeler için, şu an gerekli değil)
- **Çeviri Yönetim Sistemi**: Online çeviri yönetim aracı (Crowdin, Lokalise, vb.) - Opsiyonel, sadece çeviri sürecini kolaylaştırır
- **RTL Desteği**: ✅ Arapça için RTL desteği Vuetify ile sağlanır (yapılandırma gerekli)
- **Pluralization**: Çoğul form desteği (örn: "1 dosya" vs "5 dosya")
- **Çeviri İçerikleri**: Çince (zh) ve Arapça (ar) için tam çeviri içeriklerinin eklenmesi

### Offline/Local Network Desteği:

✅ **Çeviriler statik dosyalar olarak kod ile birlikte deploy edilir**
- Build time'da JavaScript bundle'ına dahil edilir
- Runtime'da internet bağlantısı **GEREKMEZ**
- Offline/local network kullanıcıları dil desteğini kullanabilir
- Detaylar için: [OFFLINE_SUPPORT.md](./OFFLINE_SUPPORT.md)

---

**Hazırlayan:** AI Assistant  
**Tarih:** 2024  
**Versiyon:** 1.0  
**Son Güncelleme:** 2024
