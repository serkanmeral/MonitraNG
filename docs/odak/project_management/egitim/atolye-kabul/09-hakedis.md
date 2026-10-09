# Hakediş

**Hakediş**, sözleşmede ayrılmış bir pay için parasal taleptir. “Bu dilim kabul edildi, bedelini istiyoruz” kaydıdır.

Soldaki menüde **Kontrol → Hakediş**. Bütçenin hemen yanındadır.

Üç kayıt birbirine karışmasın:

- **Sözleşme başlığı** projenin para çerçevesidir. Taban, para birimi ve ceza tavanı burada durur. Proje kartına yazılmaz. Projede bir tanedir.
- **Dilim** sözleşmenin bir parçasıdır. Yüzde pay ya da birim fiyat olabilir. Cetvel bu satırların listesidir.
- **Dönem** o dilimin tek bir talebidir. Taslak, sunuldu, kabul veya ödendi olur.

Bütçe ayrı sekmedir. Orası plan ve gerçekleşen notudur. Hakediş o notu besleyebilir; onun yerine geçmez. Fatura, muhasebe fişi, vergi ve banka hareketi bu ekranda yoktur.

## Bu sayfadaki örnek

Sayılar **Örnek PMO** projesindedir. Projeyi açın, **Hakediş** sekmesine gelin. Aşağıdaki satırlar ekrandakiyle aynıdır. Atölye kabulü setinde hakediş yoktur; o proje bu sayfanın örneği değildir.

Sözleşme: taban **1.000.000,00 TRY**, ceza tavanı **%35**.

| Dilim | Pay | Dönem |
|---|---|---|
| Kick-off | %10, tek sefer | Ödendi. Kabul 100.000, kesinti 5.000, net 95.000. |
| Proje planı | %20, tek sefer | Sunuldu. 200.000. Henüz kabul değil. |
| Yürütme | %40, 4 taksit, 3 ayda bir | 1/4 sunuldu (100.000). 2/4, 3/4, 4/4 taslak. |
| Kapanış | %30, tek sefer | Sunuldu. 300.000. Kapı açık olduğu için kabul olmaz. |
| Ek danışmanlık | Birim fiyat 25.000 | Taslak, adet 2. Yüzdeye girmez. |

Yüzde dilimleri 10 + 20 + 40 + 30 = 100 eder. Ek danışmanlık bu toplama girmez.

## Üst şerit

Sekmenin en üstünde yedi etiket vardır. Hepsi sözleşme başlığından ve dönemlerden hesaplanır. Elle yazılmaz.

| Etiket | Örnek PMO | Ne sayar |
|---|---|---|
| Taban | 1.000.000,00 TRY | Sözleşme başlığındaki ana para. |
| Ceza tavanı | %35 · 350.000,00 TRY | Taban × tavan yüzdesi. Kesintiler bu tavanı aşamaz. |
| Kabul edilen | 95.000,00 TRY | Durumu **Kabul** veya **Ödendi** olan dönemlerin neti. Sunuldu sayılmaz. |
| Ödenen | 95.000,00 TRY | Yalnız **Ödendi** olanların neti. |
| Yüzde dilimlerinden kalan | 905.000,00 TRY | Taban eksi, yüzde dilimlerinde kabul veya ödenmiş net. Birim fiyat buna girmez. |
| Kesilen ceza | 5.000,00 TRY | Kabul ve ödendi dönemlerde gerçekten düşülen kesinti. |
| Ayrıca tahsil | 0,00 TRY | Dönemden düşülemeyen ceza bakiyesi. Tahsilat ekranı yoktur; yalnız bakiye görünür. |

Kick-off ödendiği için kabul edilen ve ödenen aynıdır: 95.000. Proje planı, yürütmenin ilk taksidi ve kapanış **sunuldu**dur. Satırlarında net görünür; üst şeride girmez. Sunuldu, “henüz kasaya yazılmadı” demektir.

Yüzde dilimlerinin toplamı 100’ü aşarsa sarı uyarı çıkar: “Yüzde dilimleri toplamı … 100’ü aşıyor.” Kayıt silinmez. Uyarı, cetveli düzeltmeniz içindir.

## Sözleşme başlığı

Kartın adı **Sözleşme başlığı**. Üç kutu ve **Kaydet** vardır. Kaydetmeden üst şerit değişmez.

| Alan | Ne işe yarar |
|---|---|
| Taban | Yüzde dilimlerinin bölündüğü ana para. Örnekte 1000000 yazınca dilim payları 100.000, 200.000, 400.000 ve 300.000 olur. Birim fiyatlı iş bu tabandan düşülmez. |
| Para birimi | Üç harf. Örnekte TRY. Dilimi bir bütçe satırına bağlarsanız o satırın para birimi bununla aynı olmalıdır. Satırda para birimi boşsa TRY sayılır. |
| Tavan % | Cezanın tabana göre üst sınırı. Varsayılan 35’tir. 35, “en fazla tabanın yüzde 35’i kesilir” demektir. Örnekte tavan tutarı 350.000’dir. |

Taban 0 iken yüzde dilimlerinin tutarı da 0’dır. Önce başlığı kaydedin, sonra cetvele bakın.

## Cetvel

**Cetvel**, dilimlerin listesidir. Sağda **Dilim ekle** vardır.

Tabloda üç kolon vardır: **Ad**, **Pay**, sağda düğmeler.

Bir satıra tıklayınca o dilim seçilir ve alttaki **Dönemler** o dilimin taleplerini gösterir. Seçili satır renklenir. Satır seçilmeden dönem listesi “Dönemleri görmek için cetvelden bir satır seçin” der.

**Pay** kolonu türüne göre değişir:

- Yüzde, tek sefer: `10% · 100.000,00 TRY · Tek sefer`
- Yüzde, taksit: `40% · 400.000,00 TRY · 4 taksit · 3 ay`
- Birim fiyat: `Birim 25.000,00 TRY`

Yüzde tutarı taban × yüzde / 100’dür. Bu tutar dilimin tavanıdır. O dilimde kabul ve ödenen netler bu tavanı aşamaz.

Satırın sağında:

| Düğme | Ne işe yarar |
|---|---|
| Planı yaz | Yalnız dilim bir bütçe satırına bağlıysa görünür. Örnek PMO’da görünmez; hiçbir dilim bütçeye bağlı değildir. |
| Düzenle | Dilim penceresini açar. |
| Çöp kutusu | Dilimi siler. Bütün dönemleri taslaksa silinir; taslaklar da birlikte gider. Sunulmuş, kabul veya ödenmiş dönemi olan dilim silinmez. |

## Dilim penceresi

**Dilim ekle** veya **Düzenle** bu pencereyi açar. Ad boşsa **Kaydet** kapalıdır.

| Alan | Ne zaman görünür | Ne işe yarar |
|---|---|---|
| Ad | Her zaman | Cetveldeki isim. Örnek: Kick-off, Yürütme. |
| Tür | Her zaman | **Yüzde** veya **Birim fiyat**. Yüzde, tabandan pay alır. Birim fiyat, adet × fiyat işidir ve yüzde toplamına girmez. |
| Yüzde | Tür yüzde iken | Bu dilimin tabandaki payı. 0 ile 100 arası. Örnek: 10, tabanın yüzde 10’u. |
| Ödeme | Tür yüzde iken | **Tek sefer** bir dönem açar. **Taksit** birden çok dönem açar. |
| Taksit adedi | Ödeme taksit iken | Kaç dönem açılacağı. 1 ile 60 arası. Örnekte yürütme 4’tür. |
| Aralık (ay) | Ödeme taksit iken | Dönemler arası ay. 1 ile 24. Örnekte 3’tür; tarihler üç ay arayla durur. |
| Birim fiyat | Tür birim fiyat iken | Bir adetin bedeli. Örnekte 25000. Bu türde ödeme her zaman tek seferdir, yüzde 0’dır. |
| İlk dönem tarihi | Her zaman | İlk dönemin tarihi. Taksitte sonrakiler bu tarihe aralık eklenerek yazılır. Yürütmede ilk tarih 1 Ekim 2026’dır; sonrakiler 1 Ocak, 1 Nisan ve 1 Temmuz 2027. |
| Kapı | Her zaman | İsteğe bağlı. Kapı, ödemeyi açmaz. Bağlı kapının durumu **Geçti** değilse dönem **Kabul** olamaz. Taslak ve sunuldu serbesttir. Feragat kabulü açmaz. Kapı yoksa kabul serbesttir. Listede **Yok** seçilebilir. |
| İş kırılımı | Her zaman | Dilimin hangi WBS kalemine yapıştığı. Parayı hesaplamaz. Bulmak içindir. **Yok** bırakılabilir. |
| Bütçe satırı | Her zaman | İsteğe bağlı bağ. Aşağıda ayrı anlatılır. Örnek PMO’da **Yok** kalsın. |
| Not | Her zaman | Serbest açıklama. Cetvel tablosunda görünmez; düzenlerken okunur. |

Kaydedince eksik dönemler taslak olarak açılır. Tek sefer ve birim fiyat bir dönem açar. Taksit, taksit adedi kadar dönem açar. Adları `Yürütme 1/4` biçimindedir. Sonradan taksit sayısını artırırsanız yalnız eksik sıra numaraları eklenir. Var olan dönemlerin adı ve tarihi yeniden yazılmaz.

**İptal** pencereyi kapatır, kayıt yapmaz.

## Dönemler

Seçili dilimin talepleri burada durur. Sağda **Dönem ekle** vardır. Dilim seçili değilse düğme kapalıdır.

| Kolon | Ne işe yarar |
|---|---|
| Dönem | Sıra numarası ve ad. Örnek: `1. Kick-off`. |
| Durum | Taslak, Sunuldu, Kabul veya Ödendi. |
| Net | Bu dönemin hesaplanan tutarı. Üst şeride her net girmez; yalnız kabul ve ödendi girer. |

**Düzenle** her satırda vardır. Çöp kutusu yalnız **taslak** satırda vardır. Sunulmuş dönem silinmez; gerekirse düzenleyip yeniden taslağa alınır.

**Dönem ekle**, cetvelin açtığı dönemlerin üstüne yeni bir taslak ekler. Yeni dönem her zaman taslak açılır. Oluştururken durum seçilmez.

## Dönem penceresi

Ad (dönem etiketi) boşsa **Kaydet** kapalıdır.

| Alan | Ne işe yarar |
|---|---|
| Dönem | Listenin görünen adı. Örnek: `Yürütme 1/4`. |
| Sıra | Aynı dilimde dönem numarası. İki dönem aynı sırayı alamaz. En az 1. |
| Tarih | Bu talebin vadesi. |
| Talep edilen | İstenen tutar. Bilgi alanıdır. Net hesabına girmez. |
| Kabul edilen | Üzerinde anlaşılan tutar. Net buradan hesaplanır. |
| Kesinti | Bu dönemden düşülmek istenen ceza. |
| Fiyat farkı | Elle yazılan düzeltme. Artı tutarı büyütür, eksi küçültür. Otomatik endeks veya formül yoktur. |
| Adet | Yalnız seçili dilim **birim fiyat** ise görünür. Kaç birim işlendiğini yazar. Tutar kendiliğinden çarpılmaz. Talep ve kabul tutarını siz yazarsınız. |
| Durum | Yalnız düzenlerken görünür. Listede yalnız yapılabilecek geçişler vardır. |
| Kütüphaneden seç | Kanıt belgesi. Proje kütüphanesinden seçilir. Seçilenler çip olarak durur; çarpı kanıtı kaldırır. |
| Not | Serbest açıklama. Örnekte kick-off notu “Kesinti 5.000.” yazar. |

Pencerenin üstündeki cümle şunu hatırlatır: sunmak için en az bir kanıt gerekir. Kabul, bağlı kapı geçmeden olmaz.

### Net nasıl çıkar

```
ödenecek = kabul edilen + fiyat farkı
kesilen  = kesinti, ödenecek tutar ve kalan ceza tavanı içinden en küçüğü
net      = ödenecek − kesilen
ayrıca tahsil = kesinti − kesilen
```

Talep edilen bu hesaba girmez. Bir dönemde talep 200.000, kabul 0 ise net 0’dır.

Kick-off örneği: kabul 100.000, fiyat farkı 0, kesinti 5.000. Tavan 350.000 olduğu için 5.000’in tamamı düşülür. Net 95.000. Ayrıca tahsil 0.

Ek danışmanlık örneği: adet 2, birim fiyat 25.000, talep 50.000, kabul 0, durum taslak. Net 0 görünür. Adet × fiyat, net değildir. Kabul tutarını yazıp durumu ilerletmeden üst şerit değişmez.

### Durum sırası

Atlanamaz. Taslaktan doğrudan ödendi seçilemez.

1. **Taslak.** Henüz sunulmadı. Kanıt şart değil. Silinebilir.
2. **Sunuldu.** En az bir kanıt gerekir. Üst şeride girmez. Yeniden taslağa alınabilir. Ayrı bir “iade” durumu yoktur; iade, sunuldu → taslak geçişidir.
3. **Kabul.** Kanıt durur. Bağlı kapı varsa durumu **Geçti** olmalıdır. Feragat yetmez. Kapı yoksa kabul serbesttir. Bu andan sonra net, üstteki kabul edilene ve ceza tavanına yazılır.
4. **Ödendi.** Kabulün üstüne bir adım. Ayrıca bir banka kaydı açılmaz. “Bedel ödendi diye işaretlendi” demektir.

Kapanış dilimi buna örnektir. Dönem sunulmuştur, kanıtı teslimat listesidir, kabul tutarı 300.000 yazılmıştır. Bağlı kapı **Kapanış** hâlâ açıktır. Durumu kabule çekip kaydederseniz ekran kabul etmez: bağlı kapı geçmeden dönem kabul edilemez.

Kick-off’un kapısı **Kapsam onayı** geçtiği için kabul, ardından ödendi olabildi.

Yüzde diliminde bir sınır daha vardır. O dilimin kabul ve ödenen netleri, dilimin pay tutarını aşamaz. Kick-off payı 100.000’dir. Net 95.000 olduğu için sınırın içindedir. Kabulü 110.000 yapmaya kalkarsanız kayıt olmaz.

Taslak ve sunuldu, ceza tavanını önizler; tavanı tüketmez. Tavanı yalnız kabul ve ödendi tüketir. Sıra, dilimin cetveldeki sırası, sonra dönem numarasıdır.

## Ceza tavanı, ikinci örnek

Bu hesap Örnek PMO’da yoktur. Tavanın nasıl dolduğunu görmek içindir.

Taban 1.000.000, tavan %35, yani 350.000.

Bir dönemde kabul 100.000, kesinti 400.000 olsun. Dönemin kendisinden en fazla 100.000 düşülür. Kesilen 100.000, net 0. Geri kalan 300.000 **ayrıca tahsil** olur. Bu bakiye için ayrı ekran yoktur.

Tavan dolunca sonraki dönemlerde kesinti uygulanamaz. Yazılan kesintinin tamamı ayrıca tahsil olarak durur.

Fiyat farkı da elle yazılır. Kabul 100.000 ve fiyat farkı +8.000 ise, kesinti 0 iken net 108.000’dir. Ekran endeks hesaplamaz.

## Bütçe satırı

Dilim penceresindeki **Bütçe satırı**, bu dilimin parasını bir bütçe kalemine yapıştırır. Boş bırakılırsa bütçe sekmesi hiç değişmez.

Bağlayınca olanlar:

- O kalemin **gerçekleşeni**, kendisine bağlı dilimlerdeki kabul ve ödendi netlerinin toplamı olur. Sizin elle yazdığınız gerçekleşen silinip bu toplam yazılır. Kabul yoksa gerçekleşen 0 olur.
- **Planı yaz**, cetvel satırında ancak bağ varsa görünür. Basınca plan tutarı yazılır. Yüzde dilimde plan, dilimin pay tutarıdır. Birim fiyatta plan, o dilimdeki adetlerin toplamı × birim fiyattır. Başka türlü plan kendiliğinden dolmaz.
- Bağı kaldırmak, son yazılmış gerçekleşeni geri almaz. Bağlı başka bir dilim kaydedilirse gerçekleşen yine baştan hesaplanır.
- Para birimi sözleşme başlığı ile aynı olmalıdır.

Örnek PMO’da iki bütçe kalemi vardır: saha işçilik plan 80.000 gerçekleşen 32.000, vitrin ve mobilya plan 400.000 gerçekleşen 125.000. Hakediş dilimlerini bu satırlara bağlamayın. Gerçekleşenler ezilir. Eğitimde bağ **Yok** kalsın.

## Ekranda gezin

1. **Örnek PMO** projesini açın. **Kontrol → Hakediş**.
2. Üst şeritte taban 1.000.000, tavan %35 · 350.000, kabul edilen 95.000, ödenen 95.000, kalan 905.000, kesilen ceza 5.000, ayrıca tahsil 0 olduğunu doğrulayın.
3. Cetvelde **Kick-off** satırına tıklayın. Pay `10% · 100.000,00 TRY · Tek sefer` yazar. Alttaki dönem **Ödendi**, net 95.000,00 TRY.
4. Dönemi **Düzenle** ile açın. Talep 100000, kabul 100000, kesinti 5000, fiyat farkı 0. Kanıt çipi kick-off tutanağıdır. Durum listesinde ödenmiş kayıttan geri dönüş yoktur.
5. **Proje planı** satırına geçin. Dönem **Sunuldu**, net 200.000 görünür. Üstteki kabul edilen hâlâ 95.000’dir. Sunuldu üst şeride yazılmaz.
6. **Yürütme** satırına geçin. Dört dönem vardır. 1/4 sunuldu. 2/4, 3/4 ve 4/4 taslaktır; çöp kutusu yalnız onlardadır. Tarihler 1 Ekim 2026’dan başlayıp üç ay arayla gider.
7. **Kapanış** satırına geçin. Dönem sunuldu, kanıt teslimat listesi. Kapı açık olduğu için durumu kabule çekip kaydetmeyin; kayıt reddedilir. Bu sayfayı okurken örneği bozmayın.
8. **Ek danışmanlık** satırına geçin. Pay `Birim 25.000,00 TRY`. Dönem taslak, adet 2, net 0. Yüzde kalanı değişmez.

## Ne değildir?

- Fatura, e-fatura, vergi veya banka dekontu değildir. Ödendi işareti “bedel ödendi diye kaydedildi” der.
- Bütçe sekmesinin kopyası değildir. Bütçe plan ve gerçekleşen notudur. Hakediş, kabul edilmiş payın talebidir.
- Kapı geçmek ödeme açmaz. Kapı yalnız kabul adımını tutar.
- Ceza tavanının üstü otomatik tahsil edilmez. Düşülemeyen kısım ayrıca tahsil bakiyesidir.
- Birim adedi, tutarı kendiliğinden çarpmaz. Kabul tutarını dönem penceresine siz yazarsınız.

Kapının kendisi [Karar ve kapı](./07-karar-ve-kapi.md) sayfasındadır. Bütçe kaleminin her alanı [Bütçe](./15-butce.md) sayfasındadır.

Sıradaki: [Bağımlılıklar](./10-bagimlilik.md)
