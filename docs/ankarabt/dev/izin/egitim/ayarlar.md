# Ayarlar ne işe yarar

**Ayarlar**, personel izninin kurum kuralıdır. Tek satırdır. İkinci bir ayar kaydı açılmaz. Liste bu satırın özetidir. Asıl alanlar satırı açınca gelir.

Menü: **Personel İzin → Tanımlar → Ayarlar**.

Bu satır üç şeyi tutar:

- Dönemin nasıl açılacağı, devrin ve hak aşımının nasıl duracağı.
- Bir izin gününün kaça sayılacağı.
- Talebi kimin açacağı ve çıkış tarihinin yeni hakkı kesip kesmeyeceği.

Listenin üstündeki dört düğme de bu sayfadadır. Onlar ayar alanı değildir. Düğme işlemi hemen çalıştırmaz. Önce bir pencere açılır.

AnkaraBT’de duran değerler aşağıdadır. Bir alanı değiştirmek, geçmiş hareket satırlarını silmez. Yeni hesap bundan sonraki işte yeni değeri okur. Bazı anahtarlar kayda yazılır; bugünkü hesap henüz o anahtarı okumaz. O ayrım her alanın altında yazılıdır.

## Dönem

Dönem, yıllık iznin bakiyesinin hangi aralıkta tutulacağıdır. Hareket satırındaki dönem başlangıcı bu aralığın ilk günüdür.

| Değer | Anlamı |
|---|---|
| İşe giriş yıldönümü | Dönem, kişinin işe girdiği ay ve günde açılır. 9 Aralık 2024’te giren kişinin bu dönem başı 9 Aralık 2025’tir. Herkesin dönemi aynı günde açılmaz. |
| Takvim yılı | Dönem 1 Ocak’ta açılır. Kurumdaki herkes aynı aralığı kullanır. |

AnkaraBT değeri **İşe giriş yıldönümü**dir.

Bugünkü dönem açılışı ve bant farkı bu yıldönümünü kullanır. Takvim yılı seçeneği kayda yazılır. Hesap henüz o seçeneği okumaz. Takvim yılına çekmeden önce hesabın da o kurala bağlanması gerekir.

29 Şubat’ta işe giren kişide, artık olmayan yılda yıldönümü bir sonraki güne kayabilir.

Bir yılı doldurmayan kişiye hakediş yazılmaz. Devir satırı yine yazılır.

## Devir

Devir, bir dönem kapanırken kalan bakiyenin yeni döneme taşınmasıdır. Kalan eksi de olabilir. Eksi bakiye de yeni döneme geçer.

| Değer | Anlamı |
|---|---|
| Sınırsız | Kalanın tamamı yeni döneme yazılır. Üst sınır yoktur. |
| Tavanlı | Taşınacak gün bir tavanla sınırlanır. Tavanlı seçilince iki alan daha açılır. |

| Alan | Anlamı |
|---|---|
| Devir tavan günü | Yeni döneme en çok kaç gün taşınacağı. Bunun üstü taşınmaz. |
| Devir süre günü | Taşınan günün yeni dönemde kaç gün içinde kullanılması gerektiği. Süre dolunca kalan düşer. |

AnkaraBT değeri **Sınırsız**dır. Tavan ve süre boştur. Bu iki alan yalnız devir **Tavanlı** iken görünür.

Bugünkü açılış, önceki dönemin bakiyeye etki eden günlerini olduğu gibi devir yazar. Tavan ve süre kayda yazılır. Hesap henüz onları uygulamaz.

## Hak aşımı

Hak aşımı, yıllık izin bakiyesi yetmediğinde talebin ne olacağıdır. Bakiye, dönemdeki bakiyeye etki eden günlerin toplamıdır. Açık bloke bu toplamın içindedir.

| Değer | Anlamı |
|---|---|
| Serbest | Talep bakiyenin üstüne çıkabilir. Bakiye eksiye düşer. Talep bu yüzden durmaz. |
| Uyarı | Talep açılır. Bakiye eksiye düşeceği kişiye gösterilir. Talep durmaz. |
| Kes | Bakiye yetmiyorsa talep açılmaz. |

AnkaraBT değeri **Serbest**tir.

Bugünkü talep, bakiyenin eksiye düşmesine izin verir. Uyarı ve kes kayda yazılır. Hesap henüz bu anahtarla talebi durdurmaz.

Mazeret, rapor, ücretsiz, doğum ve babalık yıllık bakiyeden düşmez. Hak aşımı onları kesmez.

## Hak yazımı

Hak yazımı, kıdem veya yaş bandı yılın ortasında yükselince yeni günün ne zaman yazılacağıdır.

| Değer | Anlamı |
|---|---|
| Yıl içinde de | Bant yükseldiği gün aradaki fark yazılır. Eski hakediş silinmez. Düşüş eski satırı silmez. |
| Yalnız dönem başı | Fark, bir sonraki dönem açılışına kadar yazılmaz. Dönem başındaki hakediş o günkü bandı yazar. |

AnkaraBT değeri **Yıl içinde de**dir.

Bant farkı düğmesi ve sabah işi, açık dönemde bugünkü hak ile yazılmış hakediş ve bant farkının toplamını karşılaştırır. Fark artıysa yeni satır yazar. Aynı kişi, aynı dönem ve aynı hedef bant ikinci kez yazılmaz. Açılış satırı bu toplama girmez.

Yalnız dönem başı seçeneği kayda yazılır. Sabah işi henüz bu anahtara bakmaz. Her gün, kıdemi en az bir yıl olan kişide fark arar.

## Kendiliğinden açılış

Açıkken dönem, kişinin yıldönümü sabahında kendiliğinden açılır. Sıra şöyledir:

1. Önceki dönemin bakiyesi devir olarak yazılır.
2. Kıdem en az bir yılsa, o günkü bant ve yaş tabanı hakediş olarak yazılır.
3. Özet satırı yenilenir.

Aynı kişi, aynı dönem ve aynı hareket ikinci kez yazılmaz. Pencereyi yeniden çalıştırmak duran satırı bozmaz.

Kapalıyken bu sabah işinin durması beklenir. Dönemi İK, **Dönemi aç** penceresinden çalıştırır. Gönderilen tarih bugündür. Yıldönümü bugün olmayan kişiye satır yazmaz.

AnkaraBT değeri **açık**tır.

Sabah işi kurulu ve her gün 07:00’de çalışır. İş henüz bu anahtarı okumaz. Anahtarı kapatmak, kurulu sabah işini bugün durdurmaz.

## Gün karşılığı

Bir izin günü, haftanın hangi gününe denk geldiyse o kadar düşer. Değer 0, 0,5 veya 1 olur.

| Gün | AnkaraBT | Anlamı |
|---|---|---|
| Pazartesi – Cuma | 1 | Tam iş günü. Bir tam gün izin 1 düşer. |
| Cumartesi | 0,5 | Yarım iş günü. Cumartesiye denk gelen tam gün izin 0,5 düşer. |
| Pazar | 0 | İzin gününden sayılmaz. Pazara denk gelen gün bakiyeden düşmez. |

Aynı tarihte bir tatil günü varsa haftanın karşılığı değil, tatil satırı geçerlidir. Tam tatil 0 düşer. Arife 0,5 düşer ve öğleden sonrası tatil sayılır.

Yarım gün talep tek gündür. Yalnız yıllık izin ve ücretsizde açılır.

| Yarım | Düşen |
|---|---|
| Öğleden önce | Günün karşılığı ile 0,5 içinden küçük olanı. Hafta içi sabah 0,5’tir. Cumartesi ve arife sabahı da 0,5’tir. |
| Öğleden sonra | Karşılık 1 ise 0,5. Karşılık 0,5 veya 0 ise öğleden sonra 0 düşer. |

Doğum izni bu tabloyu kullanmaz. Tekil doğum 112, çoğul doğum 126 takvim günüdür. Başlangıç ve bitiş aralığa dahildir. Cumartesi ve Pazar da izin süresinin içindedir.

## Talep

Talebi kim açabilir. Üç anahtar birbirinden bağımsızdır. Kapalı olan yol sayılmaz.

| Alan | AnkaraBT | Anlamı |
|---|---|---|
| Personel kendi talebini açar | Açık | Kişi, kartındaki Keeper kişisi kendisiyse kendi talebini açar. Keeper bağı boşsa bu yol işlemez. |
| Yönetici ekibini açar | Açık | Kartta yönetici olarak yazılmış Keeper kişisi, o personelin talebini açar. |
| İK herkesi açar | Açık | **İK grubu** alanındaki grubun üyesi, herkesin talebini açar. Grup boşsa bu yol kapalıdır. |

Karşılaştırma grup kimliğiyle değil, grubun adıyla yapılır. AnkaraBT’de İK grubu **IK**dır. Üyenin grup adları arasında `IK` varsa yol açıktır. Ad değişirse hem grup hem bu alan aynı ada gelmelidir.

## Yönetici boşken grup

Personel kartında yönetici boşsa onayın düşeceği gruptur. Seçici grubun adını yazar. AnkaraBT değeri **Yonetim**dir.

Bugünkü ilk onay zinciri onay ve reddi doğrudan `Yonetim` grubuna verir. Bu alan kayıttadır. Zincir henüz alanı okumaz. Buradan başka grup seçmek, kuyruktaki onayı bugün değiştirmez. Zincir, çalışma alanı ayarından ayrıca güncellenir.

## Çıkış tarihi yeni talep ve hakedişi keser

Açıkken, kartta çıkış tarihi dolu olan veya durumu ayrıldı olan kişiye yeni talep açılmaz. Yerine bakacak kişi de çıkış tarihi dolu veya ayrılmış olamaz. Yeni hakediş ve bant farkı da çıkış tarihi boş olan, çalışıyor durumundaki kişilere yazılır.

AnkaraBT değeri **açık**tır.

Bugünkü talep kontrolü ve dönem açılışı çıkış tarihine doğrudan bakar. Anahtarı kapatmak bu kesmeyi bugün kaldırmaz.

## Düğmeler

Düğmeler listedeki ayar satırının üstündedir. Tıklanınca işlem hemen çalışmaz. Bir pencere açılır. Pencerede şunlar durur:

- İşlemin ne yaptığı. Metin, kayıtlı işlem açıklamasından gelir.
- Yazma kuralı. **Dönemi aç**, **Bant farkı** ve **Tatil yılını aç** aynı anahtarlı kayıt varsa yeniden yazmaz. **Özeti yenile** aynı anahtarlı özeti yeniler. Özet, hareket satırına her yazımda kendiliğinden de çalışır.
- Gönderilecek değer. Dönemi aç ve bant farkı bugünün tarihini gönderir. Tatil yılını aç bu yılı gönderir. Özeti yenile ek parametre göndermez.
- Zamanlama. Eşleşen iş varsa adı, her gün saat kaçı, açık ya da kapalı olduğu, son çalışma zamanı, durum ve sonuç görünür. Sonuç yazılan, atlanan ve düşen sayılarıdır. İş yoksa pencerede “yalnızca buradan çalıştırılır” yazar.
- **Şimdi çalıştır.** Asıl işlem bu düğmeyledir. Sonuç aynı pencerede kalır. Hata da burada yazılır.

| Düğme | Ne yapar | Zamanlama |
|---|---|---|
| Dönemi aç | Bugün yıldönümü olan çalışanlara devir yazar. Kıdem en az bir yılsa hakediş de yazar. Bugün yıldönümü olmayan kişiye dokunmaz. | Her gün 07:00. İş adı **Izin donem acilisi**. |
| Bant farkı | Kıdemi en az bir yıl olan ve bandı yükselmiş kişide, açık döneme yalnız fark kadar artı satır yazar. Açılış satırını hak saymaz. | Her gün 07:00. İş adı **Izin bant farki**. |
| Tatil yılını aç | Sabit milli tatil kalıplarını bu yılın günlerine yazar. Aynı yıl yeniden açılırsa duran satırın üstüne yazmaz. Dini bayramın ilk tam gününü İK girer. | Her gün 07:00. İş adı **Izin tatil yili**. |
| Özeti yenile | Kişi, dönem ve tür başına hakediş, devir, bloke, kullanım ve bakiye özetini baştan yazar. | Zamanlanmış işi yoktur. Hareket yazılınca kendiliğinden de çalışır. |

Üç sabah işi açıktır. Son çalışma kaydı yoksa pencerede henüz çalışmamış yazar.

Bu pencere yerel arayüzdedir. Canlı arayüz imajı yenilenene kadar canlıdaki düğme işlemi pencereden geçmeden çalıştırır.

## Ne değildir?

- Personel kartı değildir. Ad, işe giriş, yönetici ve çıkış tarihi karttadır.
- Hak bandı değildir. 14, 20 ve 26 gün ile yaş tabanı **Hak bantları** sekmesindedir.
- İzin türü değildir. Bakiyeden düşme, belge ve yarım gün **İzin türleri** sekmesindedir.
- Tatil takvimi değildir. Kalıp ve gerçekleşen gün kendi sekmelerindedir. Gün karşılığı, tatil satırı yokken kullanılır.
- Onay kuyruğu değildir. Onay, ret ve iptal çalışma alanındadır.
