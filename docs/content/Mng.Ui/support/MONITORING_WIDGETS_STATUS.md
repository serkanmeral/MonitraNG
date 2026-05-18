# Monitoring Widget'lar - Geliştirme Durumu

**Son güncelleme:** 13 Şubat 2025

## Tamamlanan Özellikler

### 1. Temel Altyapı
- ✅ `/apps/monitoring/widgets` listesi
- ✅ `/apps/monitoring/widgets/new` form sayfası
- ✅ `MonitoringWidgetForm` – 3 adımlı wizard (Asset → Collectible → Widget)
- ✅ Monitoring kategorisi otomatik oluşturma (`ensureMonitoringCategory`)
- ✅ `widgetDataService` – mon_metrics için runtime timestamp filtresi

### 2. Chart & Card
- ✅ Chart tipi seçimi: Line, Bar, Area
- ✅ Multi-series (çoklu seri) desteği – birden fazla asset seçildiğinde her biri ayrı çizgi
- ✅ Chart/Card için otomatik config (xAxis: timestamp, yAxis: value)
- ✅ Pivot mantığı: `pivotMonMetricsForMultiSeries` ile mon_metrics verisi dönüştürülüyor

### 3. Veri Ayarları (Widget Designer)
- ✅ Zaman aralığı: Son 20 dk, 1 saat, 6 saat, 1 gün, 7 gün, Tümü
- ✅ Maks. kayıt: 10, 50, 100, 250, 500, 1000, 2000
- ✅ Yenileme: Kapalı, 30s, 60s, 2 dk, 5 dk

### 4. Dashboard Görünümü
- ✅ `LayoutCol.widgetOverrides` – widget başına override desteği
- ✅ `WidgetWithSettings` – monitoring widget’larda dişli ikonu ile ayar popover
- ✅ Dashboard [slug] sayfasında widget ayarları (zaman aralığı, limit, yenileme) değiştirilebilir
- ✅ Değişiklikler dashboard layout’a kaydediliyor
- ✅ `WidgetRenderer` – configOverrides merge, per-widget refresh interval

### 5. Lokalizasyon
- ✅ `monitoring.widgets.*` anahtarları (tr, en, fr, ar, zh)
- ✅ Monitoring sayfasına Widgets linki

## Kalan / Opsiyonel İşler

1. **Edit sayfası:** `/apps/widgets/[id]/edit` – monitoring widget’lar için `MonitoringWidgetForm` veya koşullu form
2. **Dashboard builder:** Layout editörde widget eklenirken `widgetOverrides` başlangıç değerleri atanabilir
3. **Container sayfası:** `/dashboards/container` – widget ayarları için canEdit desteği (opsiyonel)

## İlgili Dosyalar

| Dosya | Açıklama |
|-------|----------|
| `Mng.Ui/pages/apps/monitoring/widgets/index.vue` | Widget listesi |
| `Mng.Ui/pages/apps/monitoring/widgets/new.vue` | Yeni widget form |
| `Mng.Ui/components/apps/monitoring/MonitoringWidgetForm.vue` | Widget wizard formu |
| `Mng.Ui/services/widgetDataService.ts` | Veri çekme, pivot, timestamp filtresi |
| `Mng.Ui/stores/apps/widget.ts` | `ensureMonitoringCategory`, `createWidgetCategory` |
| `Mng.Ui/components/dashboards/WidgetWithSettings.vue` | Dashboard’da widget ayarları popover |
| `Mng.Ui/components/dashboards/DashboardLayoutRenderer.vue` | Layout render, WidgetWithSettings entegrasyonu |
| `Mng.Ui/stores/apps/dashboard.ts` | `WidgetConfigOverrides`, `LayoutCol.widgetOverrides` |

## Widget Config Formatı

```json
{
  "monitoring": true,
  "assetScope": "byType|manual",
  "assetTypeId": "...",
  "assetIds": ["..."],
  "collectibleCode": "...",
  "timeRangeMinutes": 60,
  "limit": 500,
  "refreshIntervalSeconds": 60,
  "type": "line|bar|area",
  "multiSeries": true,
  "series": [{ "name": "...", "field": "...", "type": "..." }]
}
```

## Yarın Devam İçin

1. Edit sayfası entegrasyonu
2. Test: Widget oluşturma, dashboard’a ekleme, ayar değiştirme akışı
3. İhtiyaç halinde ek iyileştirmeler
