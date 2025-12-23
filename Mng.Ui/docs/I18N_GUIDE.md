# Dil Desteği (i18n) Rehberi

## Genel Bakış

MonitraNG uygulaması **Türkçe** ve **İngilizce** dil desteği ile gelir. Dil desteği `vue-i18n` kütüphanesi kullanılarak sağlanmaktadır.

## Yapılandırma

### Dil Dosyaları

Dil çevirileri `Mng.Ui/utils/locales/` klasöründe JSON dosyaları olarak saklanır:

- `tr.json` - Türkçe çeviriler
- `en.json` - İngilizce çeviriler

### Varsayılan Dil

Varsayılan dil **Türkçe (tr)** olarak ayarlanmıştır. Uygulama ilk açıldığında:

1. Önce `localStorage`'dan kaydedilmiş dil tercihi kontrol edilir
2. Eğer kayıtlı tercih yoksa, tarayıcı diline göre otomatik seçim yapılır
3. Tarayıcı dili Türkçe ise Türkçe, değilse İngilizce seçilir

### Dil Değiştirme

Kullanıcılar header'daki dil seçici butonundan dil değiştirebilir. Seçilen dil `localStorage`'a kaydedilir ve sonraki ziyaretlerde otomatik olarak yüklenir.

## Kullanım

### Component'lerde Kullanım

```vue
<script setup lang="ts">
const { t } = useI18n();

const message = t('Kullanıcı Adı'); // "Username" veya "Kullanıcı Adı"
</script>

<template>
  <div>{{ t('Giriş Yap') }}</div>
</template>
```

### Store'larda Kullanım

```typescript
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
const errorMessage = t('Giriş başarısız');
```

## Yeni Çeviri Ekleme

### 1. Dil Dosyalarına Ekleme

Her iki dil dosyasına da aynı key ile çeviriyi ekleyin:

**tr.json:**
```json
{
  "Yeni Metin": "Yeni Metin"
}
```

**en.json:**
```json
{
  "Yeni Metin": "New Text"
}
```

### 2. Component'te Kullanım

```vue
<template>
  <div>{{ t('Yeni Metin') }}</div>
</template>
```

## Locale Store

Dil yönetimi için `useLocaleStore` kullanılır:

```typescript
import { useLocaleStore } from '@/stores/locale';

const localeStore = useLocaleStore();

// Mevcut dili al
const currentLang = localeStore.currentLocale; // "tr" veya "en"

// Dil değiştir
localeStore.setLocale('en');

// Türkçe mi kontrol et
if (localeStore.isTurkish) {
  // ...
}
```

## Mevcut Çeviriler

Aşağıdaki alanlar için çeviriler mevcuttur:

- **Giriş Sayfası**: Kullanıcı adı, şifre, domain seçimi, hata mesajları
- **Kullanıcı Profili**: Kullanıcı bilgileri, çıkış butonu
- **Sidebar**: Kullanıcı adı, çıkış yap
- **Genel**: Dashboard, Apps, Forms, Tables vb. menü öğeleri

## Gelecek Geliştirmeler

- Sidebar menü öğelerinin tam çevirisi
- Dinamik hata mesajlarının çevirisi
- Tarih/saat formatlarının dil bazlı gösterimi
- Sayı formatlarının dil bazlı gösterimi

## Notlar

- Çeviri key'leri genellikle Türkçe metin olarak tutulmuştur (ör: "Kullanıcı Adı")
- Bu yaklaşım, Türkçe geliştirme yaparken daha kolay kullanım sağlar
- İngilizce çeviriler `en.json` dosyasında saklanır
- Eksik çeviriler için fallback olarak key'in kendisi gösterilir

