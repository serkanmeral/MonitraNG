# Çoklu Dil Desteği (i18n) Özet Dokümantasyonu

## Hızlı Özet

MonitraNG projesi için **çoklu dil desteği** implementasyonu:

- **Strateji**: Backend error code döndürür → Frontend çevirir
- **Diller**: Türkçe (varsayılan), İngilizce (fallback), Çince, Arapça
- **Teknoloji**: Vue I18n (zaten kurulu)

## Temel Yaklaşım

```
Backend API → Error Code → Frontend i18n → Çevrilmiş Mesaj
```

**Örnek:**
- Backend: `{ code: "VALIDATION_REQUIRED_FIELD", field: "email" }`
- Frontend: `t('errors.validation.requiredField', { field: 'E-posta' })`
- Sonuç: **TR:** "E-posta gereklidir" | **EN:** "Email is required"

## Ana Bileşenler

### 1. Frontend

**Dil Dosyaları:**
- `utils/locales/tr.json` - Türkçe çeviriler
- `utils/locales/en.json` - İngilizce çeviriler

**Store:**
- `stores/locale.ts` - Locale yönetimi (Pinia)

**Plugin:**
- `plugins/locale.client.ts` - Locale initialization

**Component:**
- `components/lc/Full/vertical-header/LanguageDD.vue` - Dil seçici

**Composable:**
- `composables/useErrorHandler.ts` - Error translation

### 2. Backend

**Constants:**
- `ErrorCodes.cs` - Standart error code'lar

**DTO:**
- `ValidationErrorDto` - Code, Params, MessageKey desteği

**Services:**
- `ValidationService.cs` - Error code kullanımı
- `ControllerHelper.cs` - Error response helpers

## Implementasyon Aşamaları

### ✅ Phase 1: Frontend Altyapı (1-2 gün)
- Locale store oluştur
- Dil dosyalarını hazırla
- LanguageDD component güncelle

### ✅ Phase 2: Backend Error Code Sistemi (1-2 gün)
- ErrorCodes constants oluştur
- ValidationErrorDto güncelle
- ValidationService güncelle

### ✅ Phase 3: Frontend Error Handling (1 gün)
- useErrorHandler composable oluştur
- API service entegrasyonu

### ✅ Phase 4: Temel Sayfalar (2-3 gün)
- Login, Dashboard, Domain, Dataset, User sayfaları

### ✅ Phase 5: Validation Mesajları (1-2 gün)
- Form validation i18n
- Dataset validation i18n

### ⏳ Phase 6: Kalan Sayfalar (Sürekli)
- Yeni sayfalar i18n ile başlar
- Mevcut sayfalar aşamalı çevrilir

## Önemli Notlar

### Key Naming Convention
```
category.subcategory.key
```
**Örnek:** `errors.validation.requiredField`

### Kategoriler
- `common.*` - Ortak metinler
- `pages.*` - Sayfa metinleri
- `menu.*` - Menü öğeleri
- `forms.*` - Form ve validation
- `errors.*` - Hata mesajları
- `messages.*` - Başarı/bilgi mesajları

### Parametreli Çeviriler
```typescript
t('errors.validation.minLength', { field: 'Kullanıcı Adı', min: 5 })
// → "Kullanıcı Adı en az 5 karakter olmalıdır"
```

## Detaylı Dokümantasyon

- Tam implementasyon detayları: [ROADMAP.md](./ROADMAP.md)
- Çince ve Arapça desteği: [CHINESE_ARABIC_SUPPORT.md](./CHINESE_ARABIC_SUPPORT.md)
- Offline/Local Network desteği: [OFFLINE_SUPPORT.md](./OFFLINE_SUPPORT.md)
- Kod şablonları: [IMPLEMENTATION_TEMPLATES.md](./IMPLEMENTATION_TEMPLATES.md)

## Offline Çalışma

✅ **Çeviriler statik dosyalar olarak kod ile birlikte deploy edilir**
- Runtime'da internet bağlantısı **GEREKMEZ**
- Offline/local network kullanıcıları dil desteğini kullanabilir

## Hızlı Başlangıç

1. **Locale Store Oluştur:**
   ```typescript
   // stores/locale.ts
   export const useLocaleStore = defineStore('locale', {
     state: () => ({ locale: 'tr' }),
     actions: { setLocale(locale) { ... } }
   })
   ```

2. **Dil Dosyası Ekle:**
   ```json
   // utils/locales/tr.json
   {
     "common": { "actions": { "save": "Kaydet" } }
   }
   ```

3. **Component'te Kullan:**
   ```vue
   <script setup>
   const { t } = useI18n()
   </script>
   <template>
     <button>{{ t('common.actions.save') }}</button>
   </template>
   ```

## Test Checklist

- [ ] Dil değiştirme butonu çalışıyor
- [ ] localStorage'a kayıt yapılıyor
- [ ] Error mesajları çevriliyor
- [ ] Validation mesajları çevriliyor
- [ ] Tüm sayfalarda dil desteği var
- [ ] Vuetify component mesajları doğru dilde

---

**Sonraki Adım:** [ROADMAP.md](./ROADMAP.md) dosyasını inceleyerek detaylı implementasyona başlayın.
