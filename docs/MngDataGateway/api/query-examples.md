# Query Parameter Examples

Bu dosya, farklı parametre tiplerini (text, number, bool, datetime) içeren query örneklerini içerir.

## 1. Number Parametreleri ile Query

### Örnek: Belirli Fiyat Aralığındaki Kitaplar

```json
{
    "name": "books_by_price_range",
    "description": "Get books within a price range",
    "pipeline": [
        {
            "$match": {
                "price": {
                    "$gte": ":minPrice",
                    "$lte": ":maxPrice"
                }
            }
        },
        {
            "$sort": {
                "price": 1,
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "minPrice",
            "type": "number",
            "description": "Minimum price",
            "required": true
        },
        {
            "name": "maxPrice",
            "type": "number",
            "description": "Maximum price",
            "required": true
        }
    ]
}
```

### Örnek: Minimum Sayfa Sayısı ile Kitaplar

```json
{
    "name": "books_by_min_pages",
    "description": "Get books with at least N pages",
    "pipeline": [
        {
            "$match": {
                "pageCount": {
                    "$gte": ":minPages"
                }
            }
        },
        {
            "$sort": {
                "pageCount": -1,
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "minPages",
            "type": "number",
            "description": "Minimum number of pages",
            "required": true
        }
    ]
}
```

## 2. Bool Parametreleri ile Query

### Örnek: Yayında Olan/Olmayan Kitaplar

```json
{
    "name": "books_by_availability",
    "description": "Get books by availability status",
    "pipeline": [
        {
            "$match": {
                "isAvailable": ":isAvailable"
            }
        },
        {
            "$sort": {
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "isAvailable",
            "type": "bool",
            "description": "Whether the book is available",
            "required": true
        }
    ]
}
```

### Örnek: Yayınlanmış ve Belirli Yıldaki Kitaplar

```json
{
    "name": "books_by_published_status",
    "description": "Get published/unpublished books",
    "pipeline": [
        {
            "$match": {
                "$and": [
                    {
                        "isPublished": ":isPublished"
                    },
                    {
                        "publicationDate": {
                            "$exists": true
                        }
                    }
                ]
            }
        },
        {
            "$sort": {
                "publicationDate": -1
            }
        }
    ],
    "parameters": [
        {
            "name": "isPublished",
            "type": "bool",
            "description": "Whether the book is published",
            "required": true
        }
    ]
}
```

## 3. Text Parametreleri ile Query

### Örnek: Yazar Adına Göre Kitaplar

```json
{
    "name": "books_by_author",
    "description": "Get books by author name (case-insensitive partial match)",
    "pipeline": [
        {
            "$match": {
                "author": {
                    "$regex": ":authorName",
                    "$options": "i"
                }
            }
        },
        {
            "$sort": {
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "authorName",
            "type": "text",
            "description": "Author name (partial match, case-insensitive)",
            "required": true
        }
    ]
}
```

### Örnek: Kategori ve Başlık İçeren Kitaplar

```json
{
    "name": "books_by_category_and_title",
    "description": "Get books by category and title contains",
    "pipeline": [
        {
            "$match": {
                "$and": [
                    {
                        "category": ":category"
                    },
                    {
                        "title": {
                            "$regex": ":titleKeyword",
                            "$options": "i"
                        }
                    }
                ]
            }
        },
        {
            "$sort": {
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "category",
            "type": "text",
            "description": "Book category",
            "required": true
        },
        {
            "name": "titleKeyword",
            "type": "text",
            "description": "Title keyword (partial match, case-insensitive)",
            "required": true
        }
    ]
}
```

## 4. Karma Parametre Tipleri ile Query

### Örnek: Fiyat, Tarih ve Durum Filtreleme

```json
{
    "name": "books_by_price_date_and_status",
    "description": "Get books filtered by price, date range, and availability",
    "pipeline": [
        {
            "$match": {
                "$and": [
                    {
                        "price": {
                            "$lte": ":maxPrice"
                        }
                    },
                    {
                        "publicationDate": {
                            "$gte": ":startDate",
                            "$lte": ":endDate"
                        }
                    },
                    {
                        "isAvailable": ":isAvailable"
                    }
                ]
            }
        },
        {
            "$sort": {
                "price": 1,
                "publicationDate": -1
            }
        }
    ],
    "parameters": [
        {
            "name": "maxPrice",
            "type": "number",
            "description": "Maximum price",
            "required": true
        },
        {
            "name": "startDate",
            "type": "datetime",
            "description": "Start date (ISO 8601 format)",
            "required": true
        },
        {
            "name": "endDate",
            "type": "datetime",
            "description": "End date (ISO 8601 format)",
            "required": true
        },
        {
            "name": "isAvailable",
            "type": "bool",
            "description": "Whether the book is available",
            "required": true
        }
    ]
}
```

### Örnek: Yazar, Minimum Sayfa ve Yayın Durumu

```json
{
    "name": "books_by_author_pages_and_published",
    "description": "Get books by author, minimum pages, and published status",
    "pipeline": [
        {
            "$match": {
                "$and": [
                    {
                        "author": {
                            "$regex": ":authorName",
                            "$options": "i"
                        }
                    },
                    {
                        "pageCount": {
                            "$gte": ":minPages"
                        }
                    },
                    {
                        "isPublished": ":isPublished"
                    }
                ]
            }
        },
        {
            "$sort": {
                "pageCount": -1,
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "authorName",
            "type": "text",
            "description": "Author name (partial match)",
            "required": true
        },
        {
            "name": "minPages",
            "type": "number",
            "description": "Minimum number of pages",
            "required": true
        },
        {
            "name": "isPublished",
            "type": "bool",
            "description": "Whether the book is published",
            "required": true
        }
    ]
}
```

## 5. Opsiyonel Parametreler ile Query

### Örnek: Opsiyonel Filtrelerle Kitaplar

```json
{
    "name": "books_with_optional_filters",
    "description": "Get books with optional filters",
    "pipeline": [
        {
            "$match": {
                "$and": [
                    {
                        "price": {
                            "$lte": ":maxPrice"
                        }
                    },
                    {
                        "isAvailable": true
                    }
                ]
            }
        },
        {
            "$sort": {
                "title": 1
            }
        }
    ],
    "parameters": [
        {
            "name": "maxPrice",
            "type": "number",
            "description": "Maximum price (optional)",
            "required": false
        }
    ]
}
```

**Not:** Opsiyonel parametreler için pipeline'da conditional match kullanılabilir, ancak bu daha karmaşık bir yapı gerektirir. Basit kullanım için gerekli parametreler önerilir.

## Parametre Tipi Özeti

| Tip | Açıklama | Örnek Değerler |
|-----|----------|----------------|
| `text` | String değerler | `"John Doe"`, `"Fiction"`, `"Python"` |
| `number` | Sayısal değerler | `100`, `29.99`, `2024` |
| `bool` | Boolean değerler | `true`, `false` |
| `datetime` | Tarih/saat değerleri | `"2025-01-01T00:00:00Z"`, `"2025-12-31T23:59:59Z"` |

## Kullanım Notları

1. **Text Parametreleri**: Regex ile kullanıldığında, kullanıcıdan gelen değerler escape edilmelidir (güvenlik için). Şu anki implementasyonda bu kontrol yapılmıyor, production'da eklenmelidir.

2. **Number Parametreleri**: int, long, double değerleri kabul edilir. String olarak gönderilirse parse edilmeye çalışılır.

3. **Bool Parametreleri**: `true`, `false` string değerleri veya boolean değerler kabul edilir.

4. **Datetime Parametreleri**: ISO 8601 formatında string olarak gönderilmelidir (örn: `"2025-01-01T00:00:00Z"`). Number değerler kabul edilmez.

5. **Required/Optional**: `required: false` olan parametreler için, pipeline'da conditional match kullanılması gerekebilir. Basit kullanım için tüm parametreleri `required: true` yapmak önerilir.

