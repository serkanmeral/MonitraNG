# Widget Create Ekranı - Query (Match JSON) Örnekleri

Bu dokümanda widget create ekranındaki **Match JSON** textarea'sına girilecek örnekler bulunmaktadır.

## Widget Create Ekranı Yapısı

- **Match JSON** (textarea): Sadece `match` objesi (MongoDB match query)
- **Skip** (number field): Atlanacak kayıt sayısı
- **Limit** (number field): Döndürülecek kayıt sayısı

---

## Örnek 1: Tüm Kitaplar (İlk Kayıt)

**Match JSON:**
```json
{}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Tüm kitaplardan ilk kaydı getirir.

---

## Örnek 2: Düşük Fiyatlı Kitaplar

**Match JSON:**
```json
{
  "price": {
    "$lt": 20
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 20'den düşük kitapları getirir.

---

## Örnek 3: Yüksek Fiyatlı Kitaplar

**Match JSON:**
```json
{
  "price": {
    "$gt": 100
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 100'den yüksek kitapları getirir.

---

## Örnek 4: Fiyat Aralığı

**Match JSON:**
```json
{
  "price": {
    "$gte": 10,
    "$lte": 50
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 10 ile 50 arasında olan kitapları getirir.

---

## Örnek 5: Az Sayfalı Kitaplar

**Match JSON:**
```json
{
  "pageCount": {
    "$lt": 50
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Sayfa sayısı 50'den az olan kitapları getirir.

---

## Örnek 6: Çok Sayfalı Kitaplar

**Match JSON:**
```json
{
  "pageCount": {
    "$gt": 200
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Sayfa sayısı 200'den fazla olan kitapları getirir.

---

## Örnek 7: Yeni Yayınlanan Kitaplar (Tarih Filtresi)

**Match JSON:**
```json
{
  "publicationDate": {
    "$gte": "2026-01-01T00:00:00.000Z"
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** 2026-01-01 tarihinden sonra yayınlanan kitapları getirir.

---

## Örnek 8: Belirli Bir Yayınevi

**Match JSON:**
```json
{
  "publisher": "7ea65f04-fc7b-488e-be3f-cd840d73de4d"
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Belirli bir yayınevine ait kitapları getirir (publisher ID).

---

## Örnek 9: Belirli Bir Yazar

**Match JSON:**
```json
{
  "author": "696fce16cf334ee26894f0f3"
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Belirli bir yazara ait kitapları getirir (author ID).

---

## Örnek 10: Çoklu Koşul (AND)

**Match JSON:**
```json
{
  "$and": [
    {
      "price": {
        "$lt": 50
      }
    },
    {
      "pageCount": {
        "$gt": 100
      }
    }
  ]
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 50'den düşük VE sayfa sayısı 100'den fazla olan kitapları getirir.

---

## Örnek 11: Çoklu Koşul (OR)

**Match JSON:**
```json
{
  "$or": [
    {
      "price": {
        "$lt": 10
      }
    },
    {
      "pageCount": {
        "$lt": 50
      }
    }
  ]
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 10'dan düşük VEYA sayfa sayısı 50'den az olan kitapları getirir.

---

## Örnek 12: Boş/Null Olmayan Alanlar

**Match JSON:**
```json
{
  "$and": [
    {
      "price": {
        "$exists": true,
        "$ne": null,
        "$ne": ""
      }
    },
    {
      "pageCount": {
        "$exists": true,
        "$ne": null,
        "$ne": ""
      }
    }
  ]
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyat ve sayfa sayısı dolu olan kitapları getirir.

---

## Örnek 13: Text İçinde Arama (Regex)

**Match JSON:**
```json
{
  "title": {
    "$regex": "b1",
    "$options": "i"
  }
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Başlığında "b1" geçen kitapları getirir (case-insensitive).

---

## Örnek 14: Belirli Bir Dil

**Match JSON:**
```json
{
  "language": "tr-TT"
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Belirli bir dildeki kitapları getirir.

---

## Örnek 15: Karmaşık Sorgu (Fiyat + Sayfa + Tarih)

**Match JSON:**
```json
{
  "$and": [
    {
      "price": {
        "$gte": 10,
        "$lte": 100
      }
    },
    {
      "pageCount": {
        "$gte": 50
      }
    },
    {
      "publicationDate": {
        "$gte": "2026-01-01T00:00:00.000Z"
      }
    }
  ]
}
```

**Limit:** `1`  
**Skip:** (boş veya `0`)

**Açıklama:** Fiyatı 10-100 arası, sayfa sayısı 50'den fazla ve 2026'dan sonra yayınlanan kitapları getirir.

---

## MongoDB Match Operatörleri

- `$lt`: Küçüktür (<)
- `$lte`: Küçük eşittir (<=)
- `$gt`: Büyüktür (>)
- `$gte`: Büyük eşittir (>=)
- `$ne`: Eşit değildir (!=)
- `$in`: İçinde (IN)
- `$nin`: İçinde değil (NOT IN)
- `$exists`: Var mı?
- `$regex`: Regex eşleşmesi
- `$and`: VE (AND)
- `$or`: VEYA (OR)
- `$nor`: VEYA DEĞİL (NOR)

---

## Notlar

1. **Match JSON sadece `match` objesini içerir** - `limit`, `skip` ayrı field'lar olarak girilir
2. **Tarih formatı:** ISO 8601 formatında string olarak girilmelidir: `"2026-01-01T00:00:00.000Z"`
3. **Relation field'lar:** `publisher`, `author`, `genres` gibi field'lar ID olarak saklanır, direkt ID ile eşleştirme yapılır
4. **Array field'lar:** `genres` gibi array field'lar için `$in` operatörü kullanılabilir
5. **Boş değerler:** `null`, `""` (boş string) ve `undefined` farklı şekilde kontrol edilir
