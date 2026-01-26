# Banner Widget Örnekleri - tst_books Dataset

Bu dokümanda `tst_books` dataset'i için çeşitli banner widget örnekleri bulunmaktadır.

## Dataset Field'ları

- `title` - Kitap başlığı (text)
- `name` - Kitap adı (text)
- `price` - Fiyat (number)
- `pageCount` - Sayfa sayısı (number)
- `publicationDate` - Yayın tarihi (datetime)
- `publisher` - Yayınevi (relation - ID)
- `author` - Yazar (persons - ID)
- `language` - Dil (text)
- `isbn`, `bookCode` - Kodlar

---

## Örnek 1: Düşük Fiyatlı Kitaplar Uyarısı

**Amaç:** Fiyatı 20'den düşük kitapları uyarmak

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "price": { "$lt": 20 }
    },
    "limit": 1,
    "sort": { "price": 1 }
  }
}
```

### Config:
```json
{
  "type": "warning",
  "variant": "tonal",
  "title": "Düşük Fiyat Uyarısı",
  "content": "Kitap: {title} - Fiyat: {price} TL (Düşük fiyatlı kitap tespit edildi)",
  "icon": "mdi-alert",
  "showIcon": true,
  "dismissible": true
}
```

---

## Örnek 2: Az Sayfalı Kitaplar Bilgilendirmesi

**Amaç:** Sayfa sayısı 50'den az olan kitapları bilgilendirmek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "pageCount": { "$lt": 50 }
    },
    "limit": 1,
    "sort": { "pageCount": 1 }
  }
}
```

### Config:
```json
{
  "type": "info",
  "variant": "tonal",
  "title": "Kısa Kitap",
  "content": "{title} - {pageCount} sayfa (Kısa içerik uyarısı)",
  "icon": "mdi-information",
  "showIcon": true,
  "dismissible": false
}
```

---

## Örnek 3: Yüksek Fiyatlı Kitaplar

**Amaç:** Fiyatı 100'den yüksek kitapları vurgulamak

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "price": { "$gt": 100 }
    },
    "limit": 1,
    "sort": { "price": -1 }
  }
}
```

### Config:
```json
{
  "type": "success",
  "variant": "filled",
  "title": "Premium Kitap",
  "content": "{title} - {price} TL (Yüksek değerli kitap)",
  "icon": "mdi-star",
  "showIcon": true,
  "dismissible": true,
  "action": {
    "enabled": true,
    "label": "Detayları Gör",
    "icon": "mdi-eye",
    "color": "white",
    "onClick": "viewBookDetails"
  }
}
```

---

## Örnek 4: Yeni Yayınlanan Kitaplar

**Amaç:** Son 30 gün içinde yayınlanan kitapları göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "publicationDate": {
        "$gte": "2026-01-01T00:00:00.000Z"
      }
    },
    "limit": 1,
    "sort": { "publicationDate": -1 }
  }
}
```

### Config:
```json
{
  "type": "success",
  "variant": "tonal",
  "title": "Yeni Yayın",
  "content": "{title} - Yayın Tarihi: {publicationDate}",
  "icon": "mdi-new-box",
  "showIcon": true,
  "dismissible": true
}
```

---

## Örnek 5: Çok Sayfalı Kitaplar

**Amaç:** Sayfa sayısı 200'den fazla olan kitapları bilgilendirmek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "pageCount": { "$gt": 200 }
    },
    "limit": 1,
    "sort": { "pageCount": -1 }
  }
}
```

### Config:
```json
{
  "type": "info",
  "variant": "outlined",
  "title": "Kapsamlı Kitap",
  "content": "{title} - {pageCount} sayfa (Detaylı içerik)",
  "icon": "mdi-book-open-variant",
  "showIcon": true,
  "dismissible": true
}
```

---

## Örnek 6: Fiyat/Sayfa Oranı Uyarısı

**Amaç:** Sayfa başına düşen fiyatı yüksek olan kitapları uyarmak (price/pageCount > 1)

### DataSource (Aggregate Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "aggregate",
  "aggregate": {
    "pipeline": [
      {
        "$match": {
          "price": { "$exists": true, "$ne": null },
          "pageCount": { "$exists": true, "$ne": null, "$gt": 0 }
        }
      },
      {
        "$addFields": {
          "pricePerPage": {
            "$divide": ["$price", "$pageCount"]
          }
        }
      },
      {
        "$match": {
          "pricePerPage": { "$gt": 1 }
        }
      },
      {
        "$sort": { "pricePerPage": -1 }
      },
      {
        "$limit": 1
      }
    ]
  }
}
```

### Config:
```json
{
  "type": "warning",
  "variant": "tonal",
  "title": "Yüksek Fiyat/Sayfa Oranı",
  "content": "{title} - Fiyat: {price} TL, Sayfa: {pageCount}, Oran: {pricePerPage} TL/sayfa",
  "icon": "mdi-alert-circle",
  "showIcon": true,
  "dismissible": true
}
```

---

## Örnek 7: Boş/Geçersiz Veri Uyarısı

**Amaç:** Eksik bilgileri olan kitapları tespit etmek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "$or": [
        { "price": { "$exists": false } },
        { "price": null },
        { "price": "" },
        { "pageCount": { "$exists": false } },
        { "pageCount": null },
        { "pageCount": "" }
      ]
    },
    "limit": 1
  }
}
```

### Config:
```json
{
  "type": "error",
  "variant": "tonal",
  "title": "Eksik Veri Uyarısı",
  "content": "{title} - Bu kitapta eksik bilgiler var (Fiyat veya Sayfa sayısı eksik)",
  "icon": "mdi-alert-circle",
  "showIcon": true,
  "dismissible": true,
  "action": {
    "enabled": true,
    "label": "Düzelt",
    "icon": "mdi-pencil",
    "color": "error",
    "onClick": "editBook"
  }
}
```

---

## Örnek 8: Özel Banner (Image ile)

**Amaç:** Özel tasarımlı banner (cover image ile)

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 1,
    "sort": { "publicationDate": -1 }
  }
}
```

### Config:
```json
{
  "type": "custom",
  "variant": "tonal",
  "title": "Öne Çıkan Kitap",
  "content": "{title} - {price} TL",
  "icon": "mdi-star",
  "showIcon": true,
  "showImage": true,
  "image": "https://via.placeholder.com/300x400?text=Book+Cover",
  "customColor": "primary",
  "dismissible": true,
  "action": {
    "enabled": true,
    "label": "Satın Al",
    "icon": "mdi-cart",
    "color": "primary",
    "onClick": "buyBook"
  }
}
```

---

## Notlar

1. **Template String Kullanımı:**
   - `{title}`, `{price}`, `{pageCount}` gibi field'lar doğrudan kullanılabilir
   - Nested field'lar için: `{publisher.name}` (ancak relation field'lar ID döndürür, lookup gerekebilir)

2. **Date Formatting:**
   - `{publicationDate}` raw datetime döndürür, formatlamak için frontend'de işlem gerekebilir

3. **Relation Field'lar:**
   - `publisher`, `author`, `genres` gibi field'lar ID döndürür
   - Gerçek değerleri göstermek için aggregate pipeline'da `$lookup` kullanılmalı

4. **Multiple Banners:**
   - Şu anda widget tek bir banner gösterir (ilk kayıt)
   - Birden fazla banner için widget'ı genişletmek gerekir

5. **Action Events:**
   - `onClick` event'leri şu anda console'a log basıyor
   - Dashboard seviyesinde event handling eklenebilir
