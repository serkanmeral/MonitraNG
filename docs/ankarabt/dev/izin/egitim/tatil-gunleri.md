# Tatil günleri ne işe yarar

**Tatil günleri**, gerçekleşen tatil tarihidir. İzin hesabı yalnız bu listeyi okur. Kalıp, tür ve ayar bu günün yerine geçmez.

Menü: **Personel İzin → Tanımlar → Tatil günleri**.

Her satır tek tarihtir. Aynı tarih ikinci kez yazılmaz.

## Satır nereden gelir

İki yol vardır.

Sabit milli kalıplar, **Tatil yılını aç** düğmesi veya her sabah 07:00’deki iş ile açılır. Aynı tarih duruyorsa üstüne yazılmaz. Tam güne 1, arifeye 0,5 yazılır. Açıklamaya kalıbın adı gelir. Arifede adın sonuna arife eklenir.

Dini bayram ve idari gün elle yazılır. Ramazan üç tam gün, Kurban dört tam gündür. Arifeleri yarım gündür. Kalıp bunu kendiliğinden açmaz. Her gün ayrı satırdır. İdari günde kalıp boş kalır.

## Pencere

**Gün:** tarih, yıl, tür, gün karşılığı.

**Bağ:** kalıp ve açıklama.

| Alan | Anlamı |
|---|---|
| Tarih | Gerçek gün. Aynı tarih bir kez durur. |
| Yıl | Bu günün yılı. |
| Tür | Milli, dini veya idari. |
| Gün karşılığı | Bu günün izne düşen ağırlığı. Haftanın karşılığının yerine geçer. |
| Kalıp | Satır bir kalıptan açıldıysa o kalıp. Elle yazılan günde boş kalabilir. |
| Açıklama | Listede görünen ad. |

## Gün karşılığı

| Sayı | Anlamı |
|---|---|
| 0 | O gün izinden hiç düşmez. |
| 0,5 | Yarım gündür. Öğleden sonrası tatil sayılır. Sabah 0,5 düşer. Öğleden sonra, karşılık 0,5 ise 0 düşer. |
| 1 | Tam iş günü gibi düşer. |

Yılı açan iş tam güne bugün 1 yazar. Arifeye 0,5 yazar. Plandaki tam tatil 0’dır. 1 yazılan resmi tatil, hafta içi gibi izinden düşer. Elle açılan günde sayıyı siz seçersiniz.

Doğum izni bu listeyi kullanmaz. Süresi takvim haftasıdır.

## Ne değildir?

- Tekrar eden tanım değildir. Tanım **Tatil kalıpları** sayfasındadır.
- Haftanın karşılığı değildir. Pazartesi 1, Cumartesi 0,5, Pazar 0 ayardadır. Tatil satırı varsa o sayı geçer.
- İzin türü değildir. Tür, talebin kuralıdır.
