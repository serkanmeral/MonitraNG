# Süreç

**Süreç haritası**, resmi sürecin bir çizim belgesi olduğunun kaydıdır. Çizmek resmi yapmak değildir. **Resmi yap** ayrı bir adımdır. Bu sekmede akış motoru yoktur. Çizim, Operasyon Merkezi’ndeki geçiş kurallarını değiştirmez.

Soldaki menüde **Taraflar → Süreç**.

Üstteki cümle: resmi süreç gerçeği bir draw.io belgesidir. Çizmek kütüphanede sürümler. Resmi yap ayrıdır.

## Liste

Filtreler: **Tümü**, **Taslak**, **Belgesiz**, **Resmi**.

Liste boşsa “Henüz süreç haritası yok” yazar.

| Kolon | Ne işe yarar |
|---|---|
| Ad | Sürecin adı. Ada tıklayınca çizim yerinde önizlenir. |
| Tür | Prosedür, iş akışı, organizasyon veya diğer. |
| Belge | Bağlı çizim. Yoksa satır belgesizdir. |
| WBS | Boşsa proje düzeyidir. |
| Durum | Taslak, resmi veya yürürlükten kalktı. |

Satırdaki düğmeler:

| Düğme | Ne zaman | Ne işe yarar |
|---|---|---|
| Çiz | Belge yokken | Boş bir draw.io açar ve proje kütüphanesine yazar. |
| Diyagramı düzenle | Belge varken | Aynı dosyayı editörde açar. Kaydetmek kütüphanede yeni sürüm üretir. |
| Resmi yap | Belge bağlı taslakta | Kaydı yürürlükteki süreç yapar. Belge yoksa olmaz. |
| Kütüphanede aç | Önizlemeden | Dosyayı kütüphane ekranında açar. |
| Sürüm geçmişi | Önizlemeden | Çizimin eski sürümleri. Süreç kaydının durumu ayrıdır. |

Kütüphane menüsündeki **Yeni çizim** de boş tuval üretir. Süreç kaydı ayrıca açılır. Çizim tek başına bu listede durmaz.

Taslak ve belgesiz kayıtlar Durum’a yansır: **Taslak süreç haritası**, **Belgesiz süreç haritası**.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Ad | Örnek: `Saha kabul akışı`. |
| Tür | **Prosedür** adım adım metin akışı. **İş akışı** süreç haritası. **Organizasyon** şema. **Diğer** bunlara sığmayan çizim. |
| Belge | **Kütüphaneden seç** veya **Çiz**. Seçilmezse “Belge seçilmedi” yazar. Çizmek kaydı resmi yapmaz. |
| WBS | İsteğe bağlı. |
| Durum | **Taslak**: çalışılıyor. **Resmi**: yürürlükte, belge gerekir. **Yürürlükten kalktı**: eski sürüm, not zorunlu. |
| Not | Yürürlükten kaldırmakta zorunlu. |

Kaydı silmek süreç satırını siler. Draw.io dosyası kütüphanede kalır.

## Örnek

1. **Süreç haritası ekle**. Ad `Saha kabul akışı`. Tür **İş akışı**.
2. **Çiz**. Boş tuvale iki kutu koyun: `Kontrol listesi` ve `Saha kabul`. Kaydedin.
3. Listeye dönün. Belge bağlıdır. Durum taslaktır. **Resmi yap** ayrı durur.
4. **Resmi yap** deyin. Durum resmi olur. Belgesiz resmi olmaz.
5. Akış değişince **Diyagramı düzenle**. Kayıt yeni sürüm üretir. Eski resmi kaldırmak istiyorsanız yeni kayıt açıp eskisini yürürlükten kaldırın. Not yazın.
6. Deneme kaydını silin. Çizim dosyası kütüphanede kalır. Onu da istemiyorsanız kütüphaneden silersiniz. İki silme ayrıdır.

Bu sekme akışı çalıştırmaz. Tezgâhın kabul adımları hâlâ WBS, kapı ve OC kurallarındadır. Resmi çizimi kapı kriterine elle referans verebilirsiniz. Sekme kriteri kendisi işaretlemez.

## Ne değildir?

- Akış motoru değildir.
- Gantt değildir.
- Kapı listesi değildir.
- Kayıt silmek çizimi silmez.
- Çizmek, kaydı resmi yapmaz.

Bu set burada biter. Yürüyüş Atölye kabulünde 01’den 08’e kadardı. 09’dan 21’e kadar menünün geri kalanı, aynı derinlikte, tek tek durur.
