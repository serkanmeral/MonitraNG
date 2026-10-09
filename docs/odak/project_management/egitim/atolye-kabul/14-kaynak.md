# Kaynak

**Kaynak**, kaba iş gücü atamasıdır. Kişi veya adlı kaynak, bir WBS kalemi ve planlanan saat. Haftalık 40 saat eşiği aşılınca **Aşırı yük** görünür. Tarih kaydırılmaz. Dengeleme yoktur.

Soldaki menüde **Kontrol → Kaynak**.

Üstteki cümle bunu söyler. Saat, maliyet değildir. Para **Bütçe** sekmesindedir. Kaynak saati bütçe tutarını kendiliğinden doldurmaz.

## İki tablo

Üstte **Özet**, altta **Atamalar**. İkisi de aramalı ve sayfalıdır. Kart listesi yoktur.

Özet satırı:

| Kolon | Ne işe yarar |
|---|---|
| Kaynak | Kişinin veya adlı kaynağın adı. |
| Uygun / Aşırı yük | Haftalardan biri 40 saati aştıysa aşırı yük. Hiçbiri aşmadıysa uygun. |
| Toplam saat | Bu projedeki atamalarının saati. |
| Tarihsiz | Tarihi olmayan saatler. “Tarihsiz n saat” diye durur. |
| Haftalar | Satır açılınca hafta çipleri. “n hafta” veya “n hafta · n aşırı”. |

Üst şerit: “n aşırı yük · n kaynak · n atama”.

Atama satırı:

| Kolon | Ne işe yarar |
|---|---|
| Kaynak | Ad. |
| Rol | Planlamacı, teknisyen gibi serbest rol. OC’deki görevli alanını doldurmaz. |
| Saat | Planlanan saat. Yanında “s” durur. |
| Pencere | Başlangıç ve bitiş. Boşsa WBS plan tarihleri kullanılır. WBS tarihi de yoksa saatler tarihsiz kovaya düşer. |
| WBS | Hangi kaleme yazıldığı. |

**Atama ekle** penceresi:

| Alan | Ne işe yarar |
|---|---|
| Kaynak | Ad. Giriş hesabı olmak zorunda değildir. |
| Rol | Ne iş yaptığı. |
| Saat | Bu penceredeki plan. 40, haftalık tavan değildir. Tavan, saatlerin düştüğü her hafta için 40’tır. |
| Plan başlangıç | Pencerenin ilk günü. Boş bırakılırsa WBS planı kullanılır. |
| Plan bitiş | Pencerenin son günü. |
| WBS | Saatin yapıştığı kalem. |

Altındaki cümle: boş tarih WBS planına düşer. O da yoksa saatler tarihsiz sayılır.

Silmek atamayı siler. WBS ve işler durur.

## Saat nasıl dağılır?

Sistem saati, pencerenin haftalarına böler ve her haftayı 40 ile karşılaştırır. Aşan hafta aşırı yüktür. Diğer işi sonraki haftaya ötelemez. Siz saati veya pencereyi değiştirirsiniz.

Örnek: Ayşe, `1.1 Kontrol listesi` için 60 saat, 6–17 Ekim 2026. İki hafta vardır. Eşik 40 + 40 = 80 saattir. 60 bu pencereye sığabilir. Uygun görünür.

Aynı Ayşe’ye aynı haftalara bir 30 saat daha yazarsanız bir hafta 40’ı aşar. Özet çipi **Aşırı yük** olur. Durum’da **Aşırı yük** satırı çıkar. Sistem öbür 30 saati 20 Ekim’e taşımaz.

Tarihi boş bırakıp WBS’in de tarihi yoksa 60 saat tarihsiz kovadadır. Haftalık 40 hesabına girmez. Uyarı da o kovadan çıkmaz. Tarihi yazın ki hafta belli olsun.

## Adımlar

1. Atölye kabulünde **Kaynak** sekmesini açın. “Henüz kaynak ataması yok” doğrudur. Yürüyüşe atama eklemeyin. Durum listesi sade kalsın.
2. Kendi denemenizde **Atama ekle**. Kaynak `Ayşe`, rol `Teknisyen`, saat `60`, tarihler 6 Ekim ve 17 Ekim, WBS `1.1`. Kaydedin.
3. Özet satırının **Uygun** olduğunu görün.
4. Aynı haftalara 30 saatlik ikinci atamayı yazın. Çipin **Aşırı yük** olduğunu görün. Tarihi kendiliğinden değişmediğini doğrulayın.
5. Denemeyi silin.

## Ne değildir?

- Gantt kaynak histogramı değildir.
- İzin veya vardiya takvimi değildir.
- Maliyet hesabı değildir. Bütçe ayrıdır.
- OC’deki görevli alanını kendiliğinden doldurmaz.
- Aşırı yük, tarihi düzeltmez. Uyarıdır.

Sıradaki: [Bütçe](./15-butce.md)
