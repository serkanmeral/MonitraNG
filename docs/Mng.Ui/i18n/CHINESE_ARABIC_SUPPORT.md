# Çince ve Arapça Dil Desteği

## Mevcut Durum

✅ **İyi Haber:** Çince ve Arapça için altyapı zaten mevcut!

- `zh.json` (Çince) dosyası mevcut
- `ar.json` (Arapça) dosyası mevcut
- `messages.ts` içinde import edilmişler
- LanguageDD component'inde görünüyorlar

## Yapılması Gerekenler

### 1. Küçük Düzeltme: messages.ts

**Mevcut:**
```typescript
const messages = {
    en: en,
    fr: fr,
    ro: ar,  // ❌ Yanlış: "ro" yerine "ar" olmalı
    zh: zh
};
```

**Düzeltilmiş:**
```typescript
const messages = {
    en: en,
    fr: fr,
    ar: ar,  // ✅ Doğru
    zh: zh
};
```

### 2. Locale Store Güncelleme

**SupportedLocale Type:**
```typescript
export type SupportedLocale = 'tr' | 'en' | 'zh' | 'ar'
```

**availableLocales:**
```typescript
availableLocales: ['tr', 'en', 'zh', 'ar']
```

**localeName Getter:**
```typescript
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
```

### 3. LanguageDD Component Güncelleme

```vue
<script setup lang="ts">
const languages = [
  { code: 'tr' as SupportedLocale, name: 'Türkçe', flag: '/images/flag/icon-flag-tr.svg' },
  { code: 'en' as SupportedLocale, name: 'English', flag: '/images/flag/icon-flag-en.svg' },
  { code: 'zh' as SupportedLocale, name: '中文', flag: '/images/flag/icon-flag-zh.svg' },
  { code: 'ar' as SupportedLocale, name: 'العربية', flag: '/images/flag/icon-flag-ar.svg' }
]
</script>
```

### 4. Vuetify Locale Entegrasyonu

**Güncelleme:** `plugins/vuetify.ts`

```typescript
import { tr, en, zhHans, ar } from 'vuetify/locale'

const vuetify = createVuetify({
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
  }
})
```

### 5. Çeviri İçeriklerini Doldurma

**Şu Anki Durum:**
- `zh.json` ve `ar.json` dosyaları var ama sadece template çevirileri içeriyor
- Uygulama çevirileri eklenmeli

**Yapılacaklar:**
1. `tr.json` ve `en.json` içindeki tüm key'leri `zh.json` ve `ar.json`'a kopyala
2. Çevirileri doldur:
   - Çince çevirileri ekle
   - Arapça çevirileri ekle
3. Error code çevirilerini ekle

**Örnek Yapı:**
```json
// zh.json
{
  "common": {
    "actions": {
      "save": "保存",
      "cancel": "取消",
      "delete": "删除"
    }
  },
  "errors": {
    "code": {
      "VALIDATION_REQUIRED_FIELD": "此字段为必填项",
      "DATASET_NOT_FOUND": "未找到数据集"
    }
  }
}
```

```json
// ar.json
{
  "common": {
    "actions": {
      "save": "حفظ",
      "cancel": "إلغاء",
      "delete": "حذف"
    }
  },
  "errors": {
    "code": {
      "VALIDATION_REQUIRED_FIELD": "هذا الحقل مطلوب",
      "DATASET_NOT_FOUND": "لم يتم العثور على مجموعة البيانات"
    }
  }
}
```

## RTL (Right-to-Left) Desteği

### Arapça için RTL

Vuetify 3 otomatik RTL desteği sağlar. Yapılandırma yapıldıktan sonra Arapça seçildiğinde layout otomatik olarak RTL'ye geçer.

**Vuetify Yapılandırması:**
```typescript
locale: {
  rtl: {
    ar: true
  }
}
```

**Ek Yapılandırma (Gerekirse):**

Layout component'lerinde HTML dir attribute'unu ayarlamak:

```typescript
// plugins/locale.client.ts veya layout component'lerinde
watch(() => localeStore.locale, (newLocale) => {
  if (process.client) {
    document.documentElement.setAttribute('dir', newLocale === 'ar' ? 'rtl' : 'ltr')
    document.documentElement.setAttribute('lang', newLocale)
  }
})
```

**CSS Desteği (Özel durumlar için):**

```scss
// assets/scss/rtl.scss (gerekirse)
[dir="rtl"] {
  .v-list-item {
    text-align: right;
  }
  
  .v-btn {
    direction: rtl;
  }
}
```

## Çince Özel Notları

### Çince Locale Kodu

Vuetify'de Çince için `zhHans` (Basitleştirilmiş Çince) kullanılır:
- `zhHans` - Basitleştirilmiş Çince (Mainland China)
- `zhHant` - Geleneksel Çince (Taiwan, Hong Kong)

Projede `zh` olarak kullanılabilir, Vuetify mapping'de `zh: zhHans` olarak ayarlanır.

## Implementasyon Önceliği

1. **Öncelik 1:** Türkçe ve İngilizce (ana diller)
2. **Öncelik 2:** Çince ve Arapça altyapısı (locale store, component güncellemeleri)
3. **Öncelik 3:** Çince ve Arapça çeviri içerikleri (aşamalı olarak doldurulabilir)

## Test Checklist

- [ ] `messages.ts` düzeltildi (ro → ar)
- [ ] Locale store'a zh ve ar eklendi
- [ ] LanguageDD component güncellendi
- [ ] Vuetify locale entegrasyonu yapıldı
- [ ] RTL desteği test edildi (Arapça)
- [ ] Çince çevirileri eklendi
- [ ] Arapça çevirileri eklendi
- [ ] Dil değiştirme test edildi (tüm diller)
- [ ] Layout RTL/LTR geçişi test edildi

## Sonuç

**Çince ve Arapça eklemek zor değil!** Altyapı zaten mevcut, sadece:
1. Küçük düzeltmeler yapılmalı
2. Locale store güncellenmeli
3. Çeviri içerikleri doldurulmalı
4. RTL desteği yapılandırılmalı (sadece Arapça için)

Toplam iş yükü: **2-3 saat** (çeviri içerikleri hariç, çeviri süresi çeviri büyüklüğüne bağlı)

---

**Not:** Çeviri içerikleri büyük iş yükü gerektirebilir. Öncelikle Türkçe ve İngilizce'yi tamamlayıp, sonra Çince ve Arapça çevirilerini aşamalı olarak eklemek mantıklı olabilir.
