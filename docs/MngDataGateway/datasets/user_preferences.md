# @user_preferences Dataset

Bu dataset, kullanıcıların uygulama tercihlerini saklamak için kullanılır.

## Dataset Bilgileri

- **Dataset Adı**: `@user_preferences`
- **Açıklama**: Kullanıcı tercihleri (dil, tema, vb.)
- **Force Schema**: `true` (önerilen)
- **Logging**: `none`

## Field Tanımları

```json
[
  {
    "fieldType": "text",
    "name": "userId",
    "title": "Kullanıcı ID",
    "mandatory": true,
    "unique": true
  },
  {
    "fieldType": "text",
    "name": "locale",
    "title": "Dil Tercihi",
    "mandatory": false,
    "validation": {
      "pattern": "^(tr|en|fr|ar|zh)$",
      "message": "Geçerli bir dil kodu giriniz (tr, en, fr, ar, zh)"
    }
  },
  {
    "fieldType": "text",
    "name": "theme",
    "title": "Tema Tercihi",
    "mandatory": false,
    "validation": {
      "pattern": "^(BLUE_THEME|AQUA_THEME|PURPLE_THEME|GREEN_THEME|CYAN_THEME|ORANGE_THEME|DARK_BLUE_THEME|DARK_AQUA_THEME|DARK_PURPLE_THEME|DARK_GREEN_THEME|DARK_CYAN_THEME|DARK_ORANGE_THEME)$",
      "message": "Geçerli bir tema seçiniz"
    }
  }
]
```

## Index Tanımları

```json
[
  {
    "name": "userId_unique",
    "fields": {
      "userId": 1
    },
    "unique": true
  }
]
```

## Kullanım

### Dataset Oluşturma

Dataset'i oluşturmak için Dataset Management sayfasından veya API üzerinden oluşturabilirsiniz.

**API ile Oluşturma:**

```bash
POST /api/v1/datasets
Content-Type: application/json

{
  "name": "@user_preferences",
  "description": "Kullanıcı tercihleri (dil, tema, vb.)",
  "forceSchema": true,
  "logging": "none",
  "publishMode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "userId",
      "title": "Kullanıcı ID",
      "mandatory": true,
      "unique": true
    },
    {
      "fieldType": "text",
      "name": "locale",
      "title": "Dil Tercihi",
      "mandatory": false,
      "validation": {
        "pattern": "^(tr|en|fr|ar|zh)$",
        "message": "Geçerli bir dil kodu giriniz"
      }
    },
    {
      "fieldType": "text",
      "name": "theme",
      "title": "Tema Tercihi",
      "mandatory": false,
      "validation": {
        "pattern": "^(BLUE_THEME|AQUA_THEME|PURPLE_THEME|GREEN_THEME|CYAN_THEME|ORANGE_THEME|DARK_BLUE_THEME|DARK_AQUA_THEME|DARK_PURPLE_THEME|DARK_GREEN_THEME|DARK_CYAN_THEME|DARK_ORANGE_THEME)$",
        "message": "Geçerli bir tema seçiniz"
      }
    }
  ],
  "indexList": [
    {
      "name": "userId_unique",
      "fields": {
        "userId": 1
      },
      "unique": true
    }
  ]
}
```

## Frontend Entegrasyonu

Frontend'de `useUserPreferencesStore` kullanılarak tercihler yönetilir:

```typescript
import { useUserPreferencesStore } from '@/stores/apps/userPreferences';

const preferencesStore = useUserPreferencesStore();

// Tercihleri yükle
await preferencesStore.loadPreferences(userId);

// Tercihleri kaydet
await preferencesStore.savePreferences({
  locale: 'tr',
  theme: 'BLUE_THEME'
});

// Tercihleri UI'a uygula
preferencesStore.applyPreferences(preferences);
```

## Otomatik Yükleme

Kullanıcı login olduğunda, tercihler otomatik olarak yüklenir ve UI'a uygulanır (`auth.ts` içinde).

## Gelecek Özellikler

Dataset'e eklenebilecek alanlar:
- `timezone`: Zaman dilimi tercihi
- `dateFormat`: Tarih formatı tercihi
- `notifications`: Bildirim tercihleri (object)
- `uiPreferences`: UI tercihleri (object)
