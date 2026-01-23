# Chart Widget Örnekleri - tst_books Dataset

Bu dokümanda `tst_books` dataset'i için çeşitli chart widget örnekleri bulunmaktadır.

## Dataset Field'ları

- `title` - Kitap başlığı (text)
- `name` - Kitap adı (text)
- `price` - Fiyat (number)
- `pageCount` - Sayfa sayısı (number)
- `publicationDate` - Yayın tarihi (datetime)
- `publisher` - Yayınevi (relation - ID)
- `author` - Yazar (persons - ID)
- `language` - Dil (text)

---

## Örnek 1: Basit Bar Chart - Fiyatlar

**Amaç:** Kitapların fiyatlarını bar chart olarak göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 10,
    "sort": { "price": -1 }
  }
}
```

### Config:
```json
{
  "type": "bar",
  "height": 350,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "yAxis": {
    "field": "price",
    "label": "Fiyat (TL)"
  },
  "options": {
    "showLegend": false,
    "showDataLabels": false,
    "showGrid": true,
    "showToolbar": false,
    "colors": ["#5D87FF"]
  }
}
```

---

## Örnek 2: Line Chart - Sayfa Sayıları

**Amaç:** Kitapların sayfa sayılarını line chart olarak göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 10,
    "sort": { "pageCount": -1 }
  }
}
```

### Config:
```json
{
  "type": "line",
  "height": 350,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "yAxis": {
    "field": "pageCount",
    "label": "Sayfa Sayısı"
  },
  "options": {
    "showLegend": false,
    "showDataLabels": false,
    "showGrid": true,
    "colors": ["#49BEFF"]
  }
}
```

---

## Örnek 3: Area Chart - Fiyat ve Sayfa Sayısı

**Amaç:** Fiyat ve sayfa sayısını birlikte göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 10
  }
}
```

### Config:
```json
{
  "type": "area",
  "height": 350,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "series": [
    {
      "name": "Fiyat",
      "field": "price",
      "type": "area"
    },
    {
      "name": "Sayfa Sayısı",
      "field": "pageCount",
      "type": "area"
    }
  ],
  "options": {
    "showLegend": true,
    "showDataLabels": false,
    "showGrid": true,
    "colors": ["#5D87FF", "#49BEFF"]
  }
}
```

---

## Örnek 4: Pie Chart - Dillere Göre Dağılım

**Amaç:** Kitapların dillere göre dağılımını göstermek

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
          "language": { "$exists": true, "$ne": "", "$ne": null }
        }
      },
      {
        "$group": {
          "_id": "$language",
          "count": { "$sum": 1 }
        }
      },
      {
        "$project": {
          "language": "$_id",
          "count": 1,
          "_id": 0
        }
      }
    ]
  }
}
```

### Config:
```json
{
  "type": "pie",
  "height": 350,
  "xAxis": {
    "field": "language",
    "label": "Dil"
  },
  "yAxis": {
    "field": "count",
    "label": "Kitap Sayısı"
  },
  "options": {
    "showLegend": true,
    "showDataLabels": true,
    "colors": ["#5D87FF", "#49BEFF", "#13DEB9", "#FFAE1F", "#FA896B"]
  }
}
```

---

## Örnek 5: Donut Chart - Yayınevine Göre Dağılım

**Amaç:** Yayınevlerine göre kitap sayısını göstermek

### DataSource (Aggregate Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "aggregate",
  "aggregate": {
    "pipeline": [
      {
        "$group": {
          "_id": "$publisher",
          "count": { "$sum": 1 }
        }
      },
      {
        "$project": {
          "publisher": "$_id",
          "count": 1,
          "_id": 0
        }
      }
    ]
  }
}
```

### Config:
```json
{
  "type": "donut",
  "height": 350,
  "xAxis": {
    "field": "publisher",
    "label": "Yayınevi"
  },
  "yAxis": {
    "field": "count",
    "label": "Kitap Sayısı"
  },
  "options": {
    "showLegend": true,
    "showDataLabels": true,
    "colors": ["#5D87FF", "#49BEFF", "#13DEB9", "#FFAE1F"]
  }
}
```

---

## Örnek 6: Stacked Bar Chart - Fiyat ve Sayfa Sayısı

**Amaç:** Fiyat ve sayfa sayısını stacked bar chart olarak göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 10
  }
}
```

### Config:
```json
{
  "type": "bar",
  "height": 350,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "series": [
    {
      "name": "Fiyat",
      "field": "price",
      "type": "bar"
    },
    {
      "name": "Sayfa Sayısı",
      "field": "pageCount",
      "type": "bar"
    }
  ],
  "options": {
    "showLegend": true,
    "showDataLabels": false,
    "stacked": true,
    "colors": ["#5D87FF", "#49BEFF"]
  }
}
```

---

## Örnek 7: Horizontal Bar Chart - En Yüksek Fiyatlı Kitaplar

**Amaç:** En yüksek fiyatlı kitapları yatay bar chart olarak göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {},
    "limit": 10,
    "sort": { "price": -1 }
  }
}
```

### Config:
```json
{
  "type": "bar",
  "height": 400,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "yAxis": {
    "field": "price",
    "label": "Fiyat (TL)"
  },
  "options": {
    "showLegend": false,
    "showDataLabels": true,
    "horizontal": true,
    "colors": ["#13DEB9"]
  }
}
```

---

## Örnek 8: Gruplu Bar Chart - Yayınevine Göre Ortalama Fiyat

**Amaç:** Yayınevlerine göre ortalama fiyatları göstermek

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
          "price": { "$exists": true, "$ne": null, "$ne": "" }
        }
      },
      {
        "$group": {
          "_id": "$publisher",
          "avgPrice": { "$avg": "$price" }
        }
      },
      {
        "$project": {
          "publisher": "$_id",
          "avgPrice": { "$round": ["$avgPrice", 2] },
          "_id": 0
        }
      }
    ]
  }
}
```

### Config:
```json
{
  "type": "bar",
  "height": 350,
  "xAxis": {
    "field": "publisher",
    "label": "Yayınevi"
  },
  "yAxis": {
    "field": "avgPrice",
    "label": "Ortalama Fiyat (TL)"
  },
  "options": {
    "showLegend": false,
    "showDataLabels": true,
    "colors": ["#FFAE1F"]
  }
}
```

---

## Örnek 9: Scatter Chart - Fiyat vs Sayfa Sayısı

**Amaç:** Fiyat ve sayfa sayısı arasındaki ilişkiyi göstermek

### DataSource (Query Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "query",
  "query": {
    "match": {
      "$and": [
        { "price": { "$exists": true, "$ne": null, "$ne": "" } },
        { "pageCount": { "$exists": true, "$ne": null, "$ne": "" } }
      ]
    }
  }
}
```

### Config:
```json
{
  "type": "scatter",
  "height": 350,
  "xAxis": {
    "field": "pageCount",
    "label": "Sayfa Sayısı"
  },
  "yAxis": {
    "field": "price",
    "label": "Fiyat (TL)"
  },
  "options": {
    "showLegend": false,
    "showDataLabels": false,
    "showGrid": true,
    "colors": ["#FA896B"]
  }
}
```

---

## Örnek 10: Radial Bar Chart - Toplam İstatistikler

**Amaç:** Toplam kitap sayısı, ortalama fiyat gibi istatistikleri göstermek

### DataSource (Aggregate Method):
```json
{
  "type": "data",
  "dataset": "tst_books",
  "getMethod": "aggregate",
  "aggregate": {
    "pipeline": [
      {
        "$group": {
          "_id": null,
          "totalBooks": { "$sum": 1 },
          "avgPrice": { "$avg": "$price" },
          "avgPages": { "$avg": "$pageCount" }
        }
      }
    ]
  }
}
```

### Config:
```json
{
  "type": "radialBar",
  "height": 350,
  "xAxis": {
    "categories": ["Toplam Kitap", "Ort. Fiyat", "Ort. Sayfa"]
  },
  "series": [
    {
      "name": "Toplam Kitap",
      "field": "totalBooks"
    },
    {
      "name": "Ort. Fiyat",
      "field": "avgPrice"
    },
    {
      "name": "Ort. Sayfa",
      "field": "avgPages"
    }
  ],
  "options": {
    "showLegend": false,
    "showDataLabels": true,
    "colors": ["#5D87FF", "#49BEFF", "#13DEB9"]
  }
}
```

**Not:** Radial Bar Chart için değerler otomatik olarak 0-100 arasına normalize edilir. Eğer direkt yüzde değerleri kullanmak isterseniz, aggregate pipeline'da değerleri normalize edebilirsiniz.

---

## Chart Config Parametreleri

### Chart Types
- `bar` - Bar chart (dikey)
- `line` - Line chart
- `area` - Area chart
- `pie` - Pie chart
- `donut` - Donut chart
- `radialBar` - Radial bar chart
- `scatter` - Scatter chart
- `bubble` - Bubble chart

### X-Axis Configuration
```json
{
  "xAxis": {
    "field": "title",        // Data field for x-axis
    "label": "Kitap Adı",    // X-axis label
    "categories": []         // Static categories (optional)
  }
}
```

### Y-Axis Configuration
```json
{
  "yAxis": {
    "field": "price",        // Data field for y-axis
    "label": "Fiyat (TL)",   // Y-axis label
    "min": 0,                // Minimum value (optional)
    "max": 1000              // Maximum value (optional)
  }
}
```

### Series Configuration (Multiple Series)
```json
{
  "series": [
    {
      "name": "Fiyat",
      "field": "price",
      "type": "bar",         // Optional: bar, line, area
      "color": "#5D87FF"     // Optional: custom color
    }
  ]
}
```

### Grouped/Aggregated Charts
```json
{
  "groupBy": "publisher",   // Field to group by
  "aggregate": {
    "field": "price",        // Field to aggregate
    "function": "avg"        // sum, avg, count, min, max
  }
}
```

### Options
```json
{
  "options": {
    "showLegend": true,      // Show legend
    "showDataLabels": false, // Show data labels
    "showGrid": true,        // Show grid
    "showToolbar": false,    // Show toolbar
    "stacked": false,        // Stacked bars
    "horizontal": false,     // Horizontal bars
    "sparkline": false,      // Sparkline mode
    "colors": ["#5D87FF"]    // Custom colors
  }
}
```

---

## Notlar

1. **Basit Chart:** `xAxis.field` ve `yAxis.field` belirtilerek tek serili chart oluşturulur
2. **Çoklu Series:** `series` array'i kullanılarak birden fazla seri gösterilebilir
3. **Gruplu Chart:** `groupBy` ve `aggregate` kullanılarak veriler gruplanıp toplanabilir
4. **Aggregate Pipeline:** Karmaşık gruplamalar için aggregate method kullanılmalıdır
5. **Relation Field'lar:** `publisher`, `author` gibi field'lar ID olarak saklanır, lookup gerekebilir
6. **Date Formatting:** `publicationDate` gibi tarih field'ları için özel işlem gerekebilir
