# OC workspace oluşturma planı

**Tarih:** 26 Ekim 2026  
**Durum:** İlk beş madde kodda. Sıradaki madde iş kaydı belge bağıdır.  
**Kapsam:** Workspace kütüphanesi, iş kaydı belgeleri, oluşturma sihirbazı, workspace paketleri, görsel durum akışı, alt iş.

Bu plan bir şablon marketi değildir. Kütüphane, Belge Zekâsında workspace’e ait klasördür. Paket, proje iş paketinin workspace karşılığıdır.

## Kilitli kararlar

### Kütüphane

- Workspace bir projeye bağlıysa ayrı kök açılmaz. Projenin klasörü kullanılır: `Dökümanlar / Projeler / {proje kodu}`. Kimlik projenin `diFolderId` alanındadır.
- Bağ, projenin `workspaceId` veya `meetingWorkspaceId` alanının bu workspace’i göstermesidir. Toplantı workspace’i de aynı klasörü görür.
- Bağı olmayan workspace için `Dökümanlar / Operasyon / {workspace kodu}` açılır. Kod `WS-0004` biçimindedir. Görünen ad klasör adı olmaz. Ad değişince klasör taşınmaz. Kimlik `op_workspaces.diFolderId` alanındadır.
- Bağ sonradan kurulursa görünüm proje klasörüne döner. Daha önce açılmış Operasyon klasörü silinmez.
- Bağ kalkarsa belgeler projede kalır. Workspace için Operasyon klasörü o anda açılır.
- Paylaşılan klasörün yetkisi projenindir. Workspace grupları bu klasörün üzerine yazılmaz.
- Operasyon klasörü oluşurken workspace’in görme ve düzenleme grupları Belge Zekâsı yetkisine yazılır. Grup listesi boşsa `Operasyon` klasörünün yetkisi miras kalır. Sonraki grup değişikliği bu klasörü yeniden yazmaz.
- Kök, sihirbaz kaydından sonra, paket kurulumu kökü boş bulursa ve kütüphane ilk kez açılırsa kurulur. `Dökümanlar` yoksa klasör açılmaz, workspace kaydı bozulmaz. `Operasyon` yoksa `Dökümanlar` altında açılır. Kayıtlı klasör silinmişse aynı kodla bulunur veya yeniden açılır.
- Operasyon Merkezi gezinme klasörü ayrıdır. O klasör workspace’i ağaçta gruplar. Belge klasörü değildir.
- İlk sürümde Wiki, Kararlar gibi hazır alt klasör açılmaz. Yalnızca kök vardır. Proje klasörünün mevcut alt klasörleri durur.
- Paketin kütüphane alt klasörleri bu kökün altına yazılır. Aynı ad atlanır.
- Aynı klasör workspace içindeki Kütüphane görünümünden ve Belge Zekâsı ağacından açılır.
- Kütüphane sekmesi, projedeki kütüphane ekranının aynısıdır. Kök, çözülen klasör kimliğidir. Kullanıcı bu kökün üstüne çıkamaz. Ekran, klasörün projeye mi workspace’e mi ait olduğunu bilmez.
- Klasör ve belge oluşturma, taşıma ve silme Belge Zekâsının yetkisindedir. Kök açılamazsa sekme boş durum gösterir. Workspace sayfası hata vermez. İş kaydına belge bağlamak bu sekmenin işi değildir.

### İş kaydı belgeleri

- Ekler sekmesi kütüphane belgesi seçer veya yeni belgeyi kütüphane ağacına yazar.
- Hedef, kütüphane kökü veya altındaki bir klasördür. Ağacın dışına yazılmaz.
- Seçim kopya üretmez. İş kaydı belge kimliğini tutar.
- İşten düşürmek bağı siler. Belge kütüphanede kalır.
- Kütüphaneden silinen belge, işte kopuk bağ olarak görünür.
- Yorum ekleri dosya olarak kalır. Kütüphane kuralına girmez.
- Daha önce yüklenmiş iş ekleri durur ve açılır. Yeni belgeler kütüphaneye gider.
- Ekler sekmesi iki liste gösterir. Eski dosya ekleri ayrı durur. Yeni bağ, kütüphane belgesinin kimliğidir.
- Kök, çözülen kütüphane klasörüdür. Kök sonradan proje klasörüne dönerse, Operasyon klasörüne bağlanmış belgeler işte durur ve kopuk sayılmaz. Yeni seçim ve yeni belge yalnızca güncel kökün altına gider.

### Sihirbazın sordukları

1. Ad
2. Tip: ekip, servis masası, operasyon, proje
3. İş öneki
4. Operasyon Merkezi klasörü. Boşsa kök
5. Görme ve düzenleme grupları

Sistem kayıttan sonra workspace kodunu üretir, iş kodu biçimini `{önek}-{sıra:4 hane}` ve sırayı 1 kabul eder, kütüphane kökünü açar.

- Üç adımdır: kimlik, yer ve erişim, boş workspace veya paket. Paketin önerdiği tip ve önek, ilk adımın yerine geçmez.
- «Proje» tipi, Projeler kaydına bağ değildir. Sihirbaz proje sormaz. Yeni workspace bağsız açılır.
- Kayıt son onayda yazılır. Vazgeçmek kayıt bırakmaz. İş öneki zorunludur.
- Görme grupları boşsa domaindeki herkes workspace’i görür. Düzenleme grupları boşsa herkes iş kaydı açar ve değiştirir. Sahip ve yönetici grupları sorulmaz.
- Sihirbazı manager veya admin açar.
- Paket kurulumu workspace kaydından sonra çalışır. Yarıda kalırsa workspace durur, kurulum kaydı yazılmaz.
- Paket seçilince her panonun adı ayrı bir alanda durur. Alan paketin adıyla dolu gelir. Boş bırakılırsa paketin adı yazılır. Ad en fazla 80 karakterdir. Paket anahtarı değişmez.
- Kullanıcının verdiği pano adı yükseltmede yerinde kalır. Damga katalog adındadır. Satırdaki ad farklıysa parça yönetici değiştirmiş sayılır.

### Gelişmiş tanım

Sihirbaz bunları sormaz. Tanım ekranında veya pakette durur.

- Açıklama, kod biçimi, sıra başlangıcı
- Sahip ve yönetici grupları
- İş tipleri, durumlar, öncelikler, alanlar
- Etiketler, durum akışı, formlar, panolar, gösterge tabloları
- Alan politikaları, kurallar, zamanlanmış işler, otomasyonlar, SLA, posta
- Kütüphane alt klasörleri ve hazır belgeler

### Paket

- Ön tanımlı paket ürün kataloğundadır. Domain yöneticisi paket yazmaz. Yönetici kurar veya kurulu workspace’i yeni sürüme yükseltir.
- Tanım, proje iş paketindeki gibi gömülü bir JSON belgesidir: kod, ad, sürüm, açıklama, yayıncı.
- Yeni paket yeni bir kod ve `1.0.0` sürümü açar. Aynı kod katalogda bir kez durur. Katalog yalnızca son sürümü taşır.
- Paket grup kimliği, workspace adı ve Operasyon Merkezi klasörü taşımaz. Tip ve iş öneki öneridir. Asıl değerleri sihirbaz sorar.
- Paket gelişmiş tanımı ve kütüphane alt klasörlerini doldurur.
- Sihirbazın son adımı boş workspace veya pakettir.
- Kurulum kaydı, workspace’in hangi paketin hangi sürümünü aldığını tutar.
- Aynı sürüm yeniden kurulunca var olan parçalar atlanır.
- Yeni sürüm kendiliğinden kurulmaz. Arayüz kaydı güncel değil diye gösterir. Yönetici önizlemeyi onaylayınca yükseltme çalışır.
- Eşleşme görünen ada göre değil, paket kodu ve parçanın sabit anahtarına göredir.
- Parça yoksa oluşturulur. Paketten geldiği gibi duruyorsa yeni sürüme çekilir. Yönetici değiştirmişse yerinde kalır. Yeni sürüm bir parçayı çıkarmışsa ve o parça üzerinde iş varsa silinmez.
- Ad, önek, gruplar ve Operasyon Merkezi klasörü yükseltmede değişmez.
- Aynı paket birden fazla workspace’e kurulur. Her workspace’in kendi kurulum kaydı vardır. Aynı workspace’te ikinci bir kurulum kaydı açılmaz.
- İlk katalog dört pakettir: izin, yazılım geliştirme, stok ve depo, demirbaş ve zimmet.
- Stok ile depo aynı pakettir. Depo, hareketin yeridir.
- Demirbaş ile zimmet aynı pakettir. Zimmet, demirbaşın kişiye verilmesidir.
- İzin, stok ve depo, demirbaş ve zimmet gövdeleri sonra konuşulur. Başlangıç gövdesi yazılım paketidir.

Yazılım paketi, kod `software`, sürüm `1.0.0`. Önerilen tip ekiptir. Önerilen önek `YZG` olur.

- İki iş tipi vardır. `type.epic` üst iştir ve altına `type.work` alır. `type.work` alt iş almaz. İş, üst işe bağlanmadan da açılabilir.
- Durumlar: Yeni (açık, başlangıç), Geliştirmede (devam), İncelemede (devam), Tamamlandı (kapalı).
- Tek akış `flow.default`. Geçişler: başlat, incelemeye gönder, geri gönder, tamamla, yeniden aç.
- İki tip de bu akışı kullanır. Pano, form ve profil bu akışa bağlıdır. Formda başlık, açıklama, tip, atanan ve öncelik vardır.
- Öncelik listesi boştur. Domain kataloğu kullanılır. Kütüphane alt klasörü ve hazır belge yoktur. Paket iş kaydı açmaz.

Zorunlu gövde. Biri eksikse paket kataloğa girmez:

- En az bir iş tipi
- Durumlar: bir başlangıç durumu ve en az bir geçişin vardığı durum
- Varsayılan durum akışı: başlangıç durumu ve geçişler
- Varsayılan pano, oluşturma formu ve profil
- Her zorunlu türden tek varsayılan

Boş bırakılabilir: öncelikler, ek alanlar, etiketler, gösterge tabloları, alan politikaları, kurallar, zamanlanmış işler, otomasyonlar, SLA, posta, kütüphane alt klasörleri, hazır belgeler, açıklama. Öncelik listesi boşsa domain kataloğu kullanılır.

Oluşturma kuralları:

- Her parçanın değişmeyen anahtarı vardır. Sürüm artsa da anahtar durur. Görünen ad değişebilir.
- Tip akış anahtarına, geçiş durum anahtarlarına, form, pano ve profil tip, akış ve geçiş anahtarlarına bakar. Bağ çözülemiyorsa paket kataloğa girmez.
- Durum ve öncelik domain kataloğundadır ve adları tekildir. Aynı ad varsa yeniden kullanılır. Yükseltme bu adları değiştirmez. İş tipi, akış, form, pano ve profil workspace’e aittir.
- İş tipi adı da domainde tekildir. Aynı ad bu workspace’te yoksa kurulum bir kez `{ad} · {workspace kodu}` yazar. Eşleşme yine paket anahtarındandır. Durum kayıtları workspace’ler arasında paylaşılır.
- Akış pakette bir listedir. Görsel tuval bu listenin sonraki editörüdür.
- Kütüphane klasörleri kütüphane köküne göreli yazılır.

### Görsel akış

- Tuval, alarm akış laboratuvarındaki kabuktur: palet, düğüm, ok, taslak, yayın.
- Düğüm durumdur. Ok geçiştir. Geçişte etiket, zorunlu alanlar ve uygulayabilecek grup durur.
- Gelişmiş palet okun yan etkisidir: kural, otomasyon, posta, SLA.
- Eşik, toplama ve hata ayıklama düğümleri gelmez.
- Paket akışı liste olarak taşır. Tuval bu listenin editörüdür. Sihirbaz çizmez.
- İş kaydı, tipinde akış varsa onu kullanır. Tipte akış yoksa workspace’in varsayılan akışını kullanır.
- Tuval, seçilen akışın taslağını düzenler. Taslak akış kaydında durur. Sayfadan çıkmak taslağı silmez. Vazgeçmek yayımlanmış listeye döner.
- Akışta tek başlangıç düğümü vardır. Başlangıcı olmayan taslak yayınlanmaz.
- Tuvalden açılan yeni durum ad ve kategori sorar. Kategori açık, devam veya kapalıdır.
- Yayın, üzerinde iş bulunan bir durumu akıştan çıkarmışsa o işler durumlarında kalır. O durumdan çıkan geçişler kalkar.
- Düğümün yerini taslak saklar. İş kayıtları bu yeri okumaz.
- Kural, otomasyon, posta ve SLA ayrı düğüm olmaz. Seçilen okun yanına yazılır.
- Bu tuval, otomasyon merkezindeki genel iş akışı motoru değildir.

### Alt iş

- Ayrı bir Epic kaydı yoktur. Üst iş normal bir iş kaydıdır. Alt işler `parentItemId` ile bağlanır.
- Derinlik ikidir. Alt işin altına yeni iş girmez.
- Hangi tipin üst olabileceği ve hangi tipin altına girebileceği pakette yazılır. İzin paketinde alt iş yoktur. Yazılım paketinde üst iş ve iş tipleri vardır.
- Alt iş aynı workspace’te kalır ve kendi kodunu alır. Kendi tipinin akışını kullanır.
- Üstün durumu altları değiştirmez. Altlar kapanınca üst kendiliğinden kapanmaz.
- Üst iş, altında iş varken silinmez. Döngü kurulamaz.
- İş kartında Alt işler bölümü listeyi gösterir ve yeni alt iş açar. Pano alt işi normal kart olarak gösterir.
- Sprint bu bağın üstüne sonra oturur.

## Uygulama sırası

Konuşma sırası kolaydan zora idi. Kod, bağımlılık sırasıyla gider. Üst madde bitmeden alt madde başlamaz.

| Sıra | Madde | Neden bu sırada |
|------|--------|-----------------|
| 1 | Kütüphane kökü | Sihirbaz, paket kurulumu, kütüphane sekmesi ve iş belgesi aynı klasör kimliğini kullanır |
| 2 | Kütüphane görünümü | Kökün açıldığını workspace içinden gösterir |
| 3 | Paket kataloğu ve yazılım paketi | Kurulum ve sihirbazın son adımı bu tanımı okur |
| 4 | Paket kurulumu | Workspace kaydına tip, akış, form, pano ve profil yazar |
| 5 | Sihirbaz | Kökü açar ve seçilen paketi kurar |
| 6 | İş kaydı belge bağı | Çözülen kökün içinden seçer ve yazar |
| 7 | Alt iş | Yazılım paketindeki üst iş ve iş tiplerine dayanır |
| 8 | Görsel durum akışı | Paketin yazdığı geçiş listesini düzenler |

İzin, stok ve depo, demirbaş ve zimmet gövdeleri bu sıranın dışındadır. Sprint ve proje ile workspace iletişimi de öyledir.

## Yapılanlar

26 Ekim 2026. İlk beş madde bitti. Operasyon Merkezi klasör ağacı bundan önce vardı; sihirbazın yer adımı onu seçer.

1. Kütüphane kökü. Bağlı projede projenin klasörü kullanılır. Bağ yoksa `Dökümanlar / Operasyon / {workspace kodu}` açılır. `Dökümanlar` yoksa klasör açılmaz ve workspace kaydı durur.
2. Kütüphane görünümü. Workspace içindeki sekme bu kökü açar. Kullanıcı kökün üstüne çıkamaz.
3. Paket kataloğu. Yazılım paketi `software` `1.0.0` gömülüdür. Katalogda tek pakettir. Gövde eksikse servis ayağa kalkmaz.
4. Paket kurulumu. Yönetici veya admin kurar. Kayıt, bütün parçalar yazıldıktan sonra workspace üzerine işlenir. Aynı sürüm var olan parçaları atlar. Yeni sürüm açık yükseltme ister. Kütüphane alt klasörü olan paket bu adımda kurulmaz.
5. Sihirbaz. Üç adım: kimlik, yer ve erişim, boş workspace veya paket. Kayıt son onayda yazılır. Kök ondan sonra çözülür. Paket ondan sonra kurulur. Pano adı son adımda değiştirilebilir.

`mngoperations` bu kurulum ve pano adı davranışıyla yayındadır. Sihirbaz arayüzü yerel geliştirme sunucusundadır.

Sıradaki madde iş kaydı belge bağıdır. Alt iş ve görsel durum akışı ondan sonradır.
