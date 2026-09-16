# Kaynak

**Kaynak**, kaba iş gücü atamasıdır: kişi veya adlı kaynak, WBS, planlanan saat. Haftalık 40 saat eşiği aşılınca **Aşırı yük** görünür. Tarih kaydırılmaz, dengeleme yoktur.

## Ekranda ne vardır?

Üstte kişi/kaynak **özet tablosu**, altta **atama tablosu**. İkisi de arama ve sayfalıdır; kart listesi yoktur.

Özet: ad, uygun/aşırı yük, toplam saat, tarihsiz saat, hafta özeti. Hafta çipleri satır açılınca görünür.

Atama: kaynak adı, rol, saat, pencere (tarih aralığı), ilgili WBS.

Tarih boşsa WBS plan tarihleri kullanılır. O da yoksa saatler **tarihsiz kova** olarak sayılır.

## Örnek

Ahmet, `2.1 Proje planı` için 60 saat, 1–12 Eylül.

İki haftada 40+40 = 80 saatlik eşik vardır; 60 bu pencerede sığabilir. Aynı Ahmet’e aynı haftaya bir 30 saat daha yazarsanız aşırı yük chip’i ve Durum’da **Aşırı yük** çıkar. Sistem Ahmet’in diğer işini sonraki haftaya **ötelenmez**.

## Ne değildir?

- Gantt kaynak histogramı değildir
- İzin / vardiya takvimi değildir
- Maliyet hesabı değildir (**Bütçe** ayrıdır)
- OC’deki assignee alanını otomatik doldurmaz

## Sık hata

Aşırı yük görünce “sistem tarihi düzeltti” sanmak. Sadece uyarıdır. Saati veya pencereyi siz değiştirirsiniz.

## Sonraki adım

Para tarafı için **Bütçe**. İşin kapanması için yine WBS + kanıt.
