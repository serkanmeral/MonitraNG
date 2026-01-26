# i18n Implementasyon Şablonları

Bu dosya, i18n implementasyonu için hazır kod şablonlarını içerir.

---

## 1. Locale Store Template

**Dosya:** `Mng.Ui/stores/locale.ts`

```typescript
import { defineStore } from 'pinia'
import { useI18n } from 'vue-i18n'

export type SupportedLocale = 'tr' | 'en'

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
    availableLocales: ['tr', 'en'],
    isLoading: false
  }),

  getters: {
    currentLocale: (state): SupportedLocale => state.locale,
    isTurkish: (state): boolean => state.locale === 'tr',
    isEnglish: (state): boolean => state.locale === 'en',
    localeName: (state): string => {
      const names: Record<SupportedLocale, string> = {
        tr: 'Türkçe',
        en: 'English'
      }
      return names[state.locale]
    }
  },

  actions: {
    initializeLocale() {
      if (process.client) {
        const savedLocale = localStorage.getItem(LOCALE_STORAGE_KEY) as SupportedLocale | null
        if (savedLocale && this.availableLocales.includes(savedLocale)) {
          this.setLocale(savedLocale, false)
          return
        }

        const browserLang = navigator.language.split('-')[0] as SupportedLocale
        if (this.availableLocales.includes(browserLang)) {
          this.setLocale(browserLang, true)
          return
        }

        this.setLocale(DEFAULT_LOCALE, true)
      }
    },

    setLocale(locale: SupportedLocale, saveToStorage: boolean = true) {
      if (!this.availableLocales.includes(locale)) {
        console.warn(`Locale ${locale} is not supported, falling back to ${DEFAULT_LOCALE}`)
        locale = DEFAULT_LOCALE
      }

      this.locale = locale
      
      const { locale: i18nLocale } = useI18n()
      i18nLocale.value = locale

      if (process.client && saveToStorage) {
        localStorage.setItem(LOCALE_STORAGE_KEY, locale)
      }
    },

    toggleLocale() {
      const newLocale: SupportedLocale = this.locale === 'tr' ? 'en' : 'tr'
      this.setLocale(newLocale)
    }
  }
})
```

---

## 2. Locale Plugin Template

**Dosya:** `Mng.Ui/plugins/locale.client.ts`

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

---

## 3. Error Handler Composable Template

**Dosya:** `Mng.Ui/composables/useErrorHandler.ts`

```typescript
import { useI18n } from 'vue-i18n'

export const useErrorHandler = () => {
  const { t } = useI18n()

  const translateError = (error: any): string => {
    if (error?.code) {
      const codeKey = `errors.code.${error.code}`
      const translated = t(codeKey, error.params || {})
      
      if (translated !== codeKey) {
        return translated
      }
    }

    if (error?.messageKey) {
      return t(error.messageKey, error.params || {})
    }

    return error?.message || t('errors.general.message')
  }

  const translateValidationError = (error: any): string => {
    const field = error.field || 'field'
    const code = error.code || 'VALIDATION_ERROR'
    
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

## 4. LanguageDD Component Template

**Dosya:** `Mng.Ui/components/lc/Full/vertical-header/LanguageDD.vue`

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useLocaleStore, type SupportedLocale } from '@/stores/locale'

const localeStore = useLocaleStore()

const languages = [
  { code: 'tr' as SupportedLocale, name: 'Türkçe', flag: '/images/flag/icon-flag-tr.svg' },
  { code: 'en' as SupportedLocale, name: 'English', flag: '/images/flag/icon-flag-en.svg' }
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

---

## 5. Backend ErrorCodes Template

**Dosya:** `MngDataGateway/Core/MngDataGateway.Application/Constants/ErrorCodes.cs`

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

---

## 6. ValidationErrorDto Template

**Dosya:** `MngDataGateway/Core/MngDataGateway.Application/DTOs/Validation/ValidationErrorDto.cs`

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

---

## 7. Dil Dosyası Şablonu (tr.json)

**Dosya:** `Mng.Ui/utils/locales/tr.json`

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
    }
  },
  "pages": {
    "login": {
      "title": "Giriş Yap",
      "username": "Kullanıcı Adı",
      "password": "Şifre",
      "submit": "Giriş Yap"
    },
    "dashboard": {
      "title": "Kontrol Paneli"
    }
  },
  "errors": {
    "general": {
      "message": "Bir hata oluştu. Lütfen tekrar deneyiniz."
    },
    "validation": {
      "requiredField": "Bu alan zorunludur",
      "minLength": "En az {min} karakter olmalıdır",
      "maxLength": "En fazla {max} karakter olabilir"
    },
    "code": {
      "VALIDATION_REQUIRED_FIELD": "Bu alan zorunludur",
      "VALIDATION_MIN_LENGTH": "En az {min} karakter olmalıdır",
      "DATASET_NOT_FOUND": "Dataset bulunamadı"
    }
  }
}
```

---

## 8. Component Kullanım Örneği

```vue
<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useErrorHandler } from '@/composables/useErrorHandler'

const { t } = useI18n()
const { translateError } = useErrorHandler()

const handleError = (error: any) => {
  const message = translateError(error)
  // Show toast/notification
  console.error(message)
}
</script>

<template>
  <div>
    <h1>{{ t('pages.dashboard.title') }}</h1>
    <button>{{ t('common.actions.save') }}</button>
  </div>
</template>
```

---

## 9. API Error Handling Örneği

```typescript
import { useErrorHandler } from '@/composables/useErrorHandler'

export async function handleApiError(error: any) {
  const { translateError, translateValidationError } = useErrorHandler()

  if (error.response?.data?.error) {
    const errorData = error.response.data.error

    const mainMessage = translateError({
      code: errorData.code,
      message: errorData.message,
      params: errorData.params
    })

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

  return {
    message: translateError(error)
  }
}
```

---

## 10. ValidationService Örnek Güncelleme

```csharp
using MngDataGateway.Application.Constants;

// ÖNCE (Hardcoded):
errors.Add(new ValidationErrorDto
{
    Field = field.name,
    Message = rules.message ?? $"Field '{field.name}' must be at least {rules.minLength.Value} characters",
    Value = value
});

// SONRA (Error Code ile):
errors.Add(new ValidationErrorDto
{
    Field = field.name,
    Code = ErrorCodes.VALIDATION_MIN_LENGTH,
    Message = rules.message ?? $"Field '{field.name}' must be at least {rules.minLength.Value} characters",
    Params = new Dictionary<string, object>
    {
        { "field", field.name },
        { "min", rules.minLength.Value }
    },
    MessageKey = rules.message?.StartsWith("errors.") || rules.message?.StartsWith("forms.") 
        ? rules.message 
        : null,
    Value = value
});
```

---

## Kullanım Notları

1. **Dil Dosyası Ekleme:** Yeni bir dil eklemek için `tr.json` ve `en.json` dosyalarına paralel bir dosya ekleyin (örn: `de.json`)

2. **Error Code Ekleme:** Yeni error code eklemek için hem `ErrorCodes.cs` hem de `tr.json` / `en.json` içindeki `errors.code.*` bölümünü güncelleyin

3. **Component'te Kullanım:** Tüm metinler için `t()` fonksiyonunu kullanın, hardcoded string kullanmayın

4. **Validation:** Schema `rules.message` alanında i18n key kullanılabilir (örn: `"errors.validation.requiredField"`)

---

**Not:** Bu şablonlar referans amaçlıdır. Proje yapısına göre uyarlanabilir.
