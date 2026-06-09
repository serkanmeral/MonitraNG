# Presentation Preset Katalogu

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama v1  
**Implementasyon:** `Mng.Ui/utils/widgets/presentationPresets.ts`

---

## 1. Amaç

Tema showcase bileşenlerini (`components/widgets/charts/*`) kopyalamadan **görsel çeşitlilik** sağlamak. Her preset → mevcut runtime bileşenine (`ChartWidget`, `StatCard`, …) config map eder.

---

## 2. Preset → bileşen map

| preset ID | kind | Vue bileşen | Not |
|-----------|------|-------------|-----|
| `stat-simple` | stat | `StatCard` | Sayı + ikon |
| `stat-sparkline` | stat | `StatCard` | + mini area (`config.sparkline`) |
| `chart-line-smooth` | chart | `ChartWidget` | `type: line`, smooth stroke |
| `chart-area-gradient` | chart | `ChartWidget` | `type: area`, gradient fill |
| `chart-bar` | chart | `ChartWidget` | `type: bar` |
| `chart-donut-breakup` | chart | `ChartWidget` | `type: donut` |
| `chart-combo-bar-line` | chart | `ChartWidget` | dual axis |
| `chart-pie` | chart | `ChartWidget` | `type: pie` |
| `table-compact` | table | `TableWidget` | dense, pagination |
| `table-drilldown` | table | `TableWidget` | row click → drillDown |
| `list-activity` | list | `TableWidget` *(Faz 1)* | timeline görünümü; Faz 2 `ListWidget` |
| `banner-info` | banner | `BannerWidget` | info tonal |
| `banner-warning` | banner | `BannerWidget` | warning |
| `gauge-threshold` | gauge | `GaugeWidget` | Monitoring — plan dışı |
| `map-assets` | map | `MapWidget` | Monitoring — plan dışı |
| `embed-markdown` | embed | 🔲 `DiMarkdownViewer` | DI snippet — Faz 2 |

---

## 3. Domain → önerilen preset

| Domain | Stat | Chart | Table/List |
|--------|------|-------|------------|
| alarm | `stat-simple` | `chart-area-gradient`, `chart-donut-breakup` | `table-compact` |
| siem | `stat-simple` | `chart-area-gradient` | `table-compact` |
| operation-core | `stat-simple` | `chart-donut-breakup` | `table-compact`, `table-drilldown` |
| document-intelligence | `stat-simple` | — | `list-activity`, `table-compact` |

---

## 4. Config şablonları (manifest `presentation.config`)

### stat-simple

```json
{
  "format": "number",
  "icon": "mdi-chart-box",
  "color": "primary"
}
```

### chart-area-gradient

```json
{
  "type": "area",
  "height": 280,
  "chartOptions": {
    "stroke": { "curve": "smooth", "width": 2 },
    "fill": { "type": "gradient" }
  }
}
```

### table-compact

```json
{
  "dense": true,
  "pageSize": 10,
  "columns": []
}
```

*(Kolonlar template `fieldMap` + data shape’ten designer otomatik önerir.)*

---

## 5. Apex chart tipleri (desteklenen)

Mevcut `ChartWidget`: `line`, `bar`, `area`, `pie`, `donut`, `radialBar`, `scatter`, `bubble`

V1 seed ağırlığı: **line, area, bar, donut** — diğerleri advanced preset olarak eklenir.

---

## 6. Referans kod

| Dosya | Not |
|-------|-----|
| `Mng.Ui/components/widgets/chart/ChartWidget.vue` | Apex entegrasyonu |
| `Mng.Ui/components/widgets/card/StatCard.vue` | Stat kart |
| `Mng.Ui/components/widgets/table/TableWidget.vue` | Tablo |

Tema örnekleri (sadece referans, kopyalanmaz): `Mng.Ui/components/widgets/charts/*`
