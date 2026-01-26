# @user_notes Dataset

Bu dataset, kullanıcıların kişisel notlarını saklamak için kullanılır.

## Dataset Bilgileri

- **Dataset Adı**: `@user_notes`
- **Açıklama**: Kullanıcı notları - Her kullanıcı kendi notlarını kaydedebilir
- **Force Schema**: `true` (önerilen)
- **Logging**: `none`
- **Publish Mode**: `none`

## Field Tanımları

```json
[
  {
    "fieldType": "text",
    "name": "userId",
    "title": "Kullanıcı ID",
    "mandatory": true
  },
  {
    "fieldType": "text",
    "name": "title",
    "title": "Not Başlığı/İçeriği",
    "mandatory": true
  },
  {
    "fieldType": "text",
    "name": "color",
    "title": "Not Rengi",
    "mandatory": false,
    "validation": {
      "pattern": "^(primary|secondary|error|warning|success|info)$",
      "message": "Geçerli bir renk seçiniz (primary, secondary, error, warning, success, info)"
    }
  },
  {
    "fieldType": "datetime",
    "name": "createdAt",
    "title": "Oluşturulma Tarihi",
    "mandatory": false
  },
  {
    "fieldType": "datetime",
    "name": "updatedAt",
    "title": "Güncellenme Tarihi",
    "mandatory": false
  }
]
```

## Index Tanımları

```json
[
  {
    "name": "userId_index",
    "fields": {
      "userId": 1
    },
    "unique": false
  },
  {
    "name": "userId_createdAt_index",
    "fields": {
      "userId": 1,
      "createdAt": -1
    },
    "unique": false
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
  "name": "@user_notes",
  "description": "Kullanıcı notları - Her kullanıcı kendi notlarını kaydedebilir",
  "forceSchema": true,
  "logging": "none",
  "publishMode": "none",
  "fields": [
    {
      "fieldType": "text",
      "name": "userId",
      "title": "Kullanıcı ID",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "title",
      "title": "Not Başlığı/İçeriği",
      "mandatory": true
    },
    {
      "fieldType": "text",
      "name": "color",
      "title": "Not Rengi",
      "mandatory": false,
      "validation": {
        "pattern": "^(primary|secondary|error|warning|success|info)$",
        "message": "Geçerli bir renk seçiniz"
      }
    },
    {
      "fieldType": "datetime",
      "name": "createdAt",
      "title": "Oluşturulma Tarihi",
      "mandatory": false
    },
    {
      "fieldType": "datetime",
      "name": "updatedAt",
      "title": "Güncellenme Tarihi",
      "mandatory": false
    }
  ],
  "indexList": [
    {
      "name": "userId_index",
      "fields": {
        "userId": 1
      },
      "unique": false
    },
    {
      "name": "userId_createdAt_index",
      "fields": {
        "userId": 1,
        "createdAt": -1
      },
      "unique": false
    }
  ]
}
```

## Frontend Entegrasyonu

Frontend'de `useUserNotesStore` kullanılarak notlar yönetilir:

```typescript
import { useUserNotesStore } from '@/stores/apps/userNotes';

const notesStore = useUserNotesStore();

// Notları yükle
await notesStore.fetchNotes();

// Yeni not ekle
await notesStore.addNote({
  title: 'Not içeriği',
  color: 'primary'
});

// Not güncelle
await notesStore.updateNote(noteId, {
  title: 'Güncellenmiş içerik',
  color: 'success'
});

// Not sil
await notesStore.deleteNote(noteId);
```

## Güvenlik

- Her kullanıcı sadece kendi notlarını görebilir ve yönetebilir
- `userId` field'ı ile filtreleme yapılır
- Dataset permissions ayarları ile erişim kontrolü yapılabilir
