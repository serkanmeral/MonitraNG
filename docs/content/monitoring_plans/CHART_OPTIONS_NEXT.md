# Chart seçeneklerini artırma (sonraki adım)

Dashboard/widget seed tamamlandıktan sonra chart widget’ında seçenekleri artırmak için olası yönler.

---

## Olası iyileştirmeler

- **Chart tipi:** Mevcut: line, bar, area. ~~**pie**, **donut**~~ ✅ **Pie ve donut** eklendi (Monitoring widget formunda seçilebilir; ChartWidget zaten destekliyordu). İleride: **radar**, **scatter** (ApexCharts imkânına göre).
- **Eksen/etiket:** X/Y eksen etiketleri, birim, tarih formatı (zaman bilgisi okunabilir formata zaten geçirildi).
- **Çoklu seri:** ~~Aynı grafikte birden fazla collectible veya birden fazla asset (multi-series); mevcut yapı kısmen destekliyor; formda netleştirme ve legend.~~ ✅ Formda çoklu asset seçildiğinde bilgi metni (chartHintMultiSeries) ve legend açıklaması eklendi.
- **Zaman aralığı:** Widget üzerindeki çark (⚙) menüsünden zaman aralığı seçilebiliyor; ayrı preset butonlarına gerek yok.
- **Veri yoğunluğu:** Limit artırma/azaltma, örnekleme (aggregation) ile performans.
- **Dışa aktarma:** Grafik görselini veya veriyi PNG/CSV olarak indirme.

Öncelik önerisi: ~~chart tipi (pie/donut)~~ ✅, ~~multi-series UX~~ ✅ tamamlandı. Sırada: radar/scatter, eksen/etiket, veri yoğunluğu veya dışa aktarma.

---

**İlişkili:** `DASHBOARD_WIDGET_PLAN.md`, `Mng.Ui/components/widgets/chart/ChartWidget.vue`, `MonitoringWidgetForm.vue` (chart tipi seçimi).
