# Süreç

**Süreç haritası**, resmi süreç gerçeğinin bir draw.io belgesi olduğunun kaydıdır. Çizmek resmi yapmak değildir. Bu sekmede BPMN motoru ve otomatik senkron yoktur.

## Ekranda ne vardır?

Filtreli tablo: **Tümü**, **Taslak**, **Belgesiz**, **Resmi**.

**Süreç haritası ekle** modalında: ad, tür (prosedür, iş akışı, organizasyon, diğer), belge, WBS (boş = proje düzeyi), durum (taslak / resmi / yürürlükten kalktı), not. Kimlik yapıştırılmaz.

Belge iki yoldan gelir: **kütüphaneden seç** veya **Çiz** (boş draw.io, proje kütüphanesine yazılır). Kütüphane menüsündeki **Yeni çizim** de aynı dosyayı üretir; süreç kaydı ayrıca açılır.

Belge adına tıklayınca yerinde önizleme. Önizlemeden **Diyagramı düzenle**, **Kütüphanede aç**, sürüm geçmişi. Listede belgesiz satırda **Çiz**, belge bağlıysa **Diyagramı düzenle**. Editör draw.io’dur; kaydetmek DI’da yeni sürüm üretir.

**Resmi yap** belge bağlı taslağı yürürlükteki süreç yapar. Resmi yapmak için belge gerekir. Yürürlükten kaldırmak not ister. Taslak ve belgesiz kayıtlar Durum’a yansır.

Kayıt silmek draw.io dosyasını silmez.

## Örnek

**Süreç haritası ekle** → ad `Saha kabul akışı`, tür İş akışı → **Çiz**. Diyagramı kaydedin. Listede **Resmi yap**.

Akış değişince **Diyagramı düzenle** (yeni sürüm) veya yeni kayıt açıp eskisini yürürlükten kaldırın. Bu sekme BPMN çalıştırmaz; OC geçişleri hâlâ OC kurallarıdır.

## Ne değildir?

- BPMN / iş akışı motoru değildir
- Gantt değildir
- Kapı listesi değildir; resmi süreci kapı kriterine referans verebilirsiniz
- Kayıt silmek çizimi silmez

## Bu eğitim setini bitirmek

Plan (Genel, Gantt, WBS, FS, Durum) → Teslimat (Karar, Kapı, Paket, Kütüphane) → Kontrol ve Taraflar yardımcı kayıtlardır.

Boş bir projede pratik sıra: Paketler veya WBS → Kütüphane → bağla → Kararlar/Kapılar → Durum.
