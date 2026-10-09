# Bağımlılıklar

**Bağımlılık (FS)**, “önceleyen bitmeden izleyen başlamaz” okudur. FS, bitiş–başlangıç demektir. Bu sürümde başka ok türü yoktur. Ekran bunu “Faz 1'de yalnızca bitiş-başlangıç (FS) desteklenir” diye yazar.

Soldaki menüde **Plan → Bağımlılıklar (FS)**.

Bu ok, RAID’deki harici bağımlılık değildir. RAID, belediye izni veya tedarikçi gibi dış dünyayı tutar. FS, bu projedeki iki WBS kalemi arasındadır. İkisi birlikte durabilir. İzin bekliyorsanız RAID yazın. “Kontrol bitmeden kabul olmasın” için FS ekleyin.

Ok, tarihleri kendiliğinden kaydırmaz. Gantt’ta çizilir. Açık kapı kilidi, okun izleyenlerine de yayılır.

## Liste

Üstte **Bağımlılık ara** ve **FS bağımlılığı** vardır. Liste boşsa “Bağımlılık yok” yazar. Arama boşsa “Bu aramada bağımlılık yok” yazar.

| Kolon | Ne işe yarar |
|---|---|
| Önceleyen | Önce bitmesi gereken kalem. |
| İzleyen | Ondan sonra başlaması gereken kalem. |
| Tip | Hep **FS · bitiş–başlangıç**. Başka tip seçilmez. |
| Lag (gün) | Aradaki bekleme. 0 ise **Hemen** yazar. 2 ise “2 gün” yazar. İzleyen, önceleyenin bitişinden iki gün sonra başlamalıdır. |

Tarihler bu kurala uymazsa satırda **Plan tarihleri FS ile uyuşmuyor** yazar. Örnek: 1.1’in plan bitişi 17 Ekim, lag 0, 1.2’nin plan bitişi 10 Ekim ise uyarı çıkar. Sistem 1.2’yi 17 Ekim’e çekmez. Tarihi siz WBS’ten düzeltirsiniz veya oku silersiniz.

Silmek oku siler. İki kalemi silmez.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Önceleyen | Bitmesi gereken kalem. Kendisi izleyen olamaz. |
| İzleyen | Başlaması gereken kalem. |
| Lag (gün) | Bekleme. 0 hemen ardından demektir. Eksi gün bu eğitimde kullanılmaz. |

**Kaydet** oku yazar. **Vazgeç** kapatır.

## Örnek

Atölye kabulünde kurmayın. Yürüyüş, oku boş bırakır ki Gantt yalnız çubuk ve elmas göstersin. Kendi işinizde deneyecekseniz:

| Alan | Değer |
|---|---|
| Önceleyen | 1.1 Kontrol listesi |
| İzleyen | 1.2 Saha kabul |
| Lag | 0 |

Anlamı: kontrol listesinin planı bitmeden saha kabul başlamasın. 1.1, 17 Ekim’de biter. 1.2, 24 Ekim’dedir. 24, 17’den sonradır. Uyarı çıkmaz. 1.2’yi 10 Ekim yapsaydınız uyarı çıkardı. Elmas kendiliğinden 17 Ekim’e gitmezdi.

Gantt sekmesinde bu ok **FS** diye çizilir. Oku oradan yazamazsınız.

## Adımlar

1. Atölye kabulünde bu sekmeyi açın. “Bağımlılık yok” yazması doğrudur.
2. Kendi denemeniz için **FS bağımlılığı** deyin. Önceleyen 1.1, izleyen 1.2, lag 0. Kaydedin.
3. Gantt’a geçin. Okun çizildiğini görün.
4. Denemeyi silin. Atölye yürüyüşünün geri kalanı oksuz durur.

## Ne değildir?

- RAID bağımlılığı değildir. Dış izin ve tedarikçi **RAID** sekmesindedir.
- Tarih kaydırmaz. Uyarı verir.
- SS, FF veya SF oku değildir. Yalnız FS vardır.
- Kapının kendisi değildir. Açık kapının kilidi, okun izleyen işlerine de bulaşır.

Sıradaki: [Dashboard](./11-dashboard.md)
