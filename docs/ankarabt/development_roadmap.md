# AnkaraBT — geliştirme yol haritası

AbtApp (`192.168.1.249`, `https://app.ankarabt.com`) üzerindeki ürün işleri. Sunucu, etki alanı ve posta notları `server/` altındadır.

İlk iş OperationCore workspace kesitleridir. Belge indeksi ve antivirüs taraması ondan sonra, birlikte ele alınır.

OperationCore maddeleri [OC workspace oluşturma planı](../odak/operationcore/OC_WORKSPACE_CREATION_PLAN.md) (26 Ekim 2026) içinden gelir. O planda kütüphane kökü, kütüphane görünümü, yazılım paketi kataloğu, paket kurulumu ve sihirbaz bitmiştir. Aşağıya yalnızca kalan kesitler yazıldı.

Durumlar: **konuşuldu**, **planlandı**, **yapılıyor**, **bitti**.

## DI-01 — Yüklenen dosyaların antivirüs taraması

| | |
|---|---|
| Durum | Konuşuldu (2026-10-27). Kod yok. |
| Sıra | OperationCore workspace kesitlerinden sonra. Belge indeksi ile birlikte bakılır. |
| İstek | Document Intelligence’a yüklenen her dosya ClamAV ile taransın. Sonuç hem yükleme bitince hem dosya listesinde görünsün. |

### Akış

Yükleme bugün iki adımdır: MngDocument kaynak kaydını açar, dosyanın kendisi DataGateway üzerinden MinIO’ya yazılır. Tarama bu ikinci adımın hemen ardından çalışır.

Tarama, yükleme isteğinin içinde bekletilmez. Dosya kaydı listeye **taranıyor** durumuyla düşer. Tarama bitince aynı satır güncellenir. Yükleme penceresi kapanmadan önce bu son durumu da gösterir; liste ile aynı alandan okur.

Sonuç kaynak kaydında (`dm_resources`) durur. DataGateway dosya kaydı taramayı yapar, Document sonucu kaynağa yazar. Liste, yükleme özeti ve indirme kararı bu alana bakar. İçerik indeksi rozetiyle aynı türden bir kolondur.

### Görünen durumlar

| Durum | Anlamı |
|---|---|
| Taranıyor | Dosya duruyor, tarama henüz bitmedi |
| Temiz | Bilinen imzaya rastlanmadı |
| Tehdit | İmza eşleşti |
| Taranamadı | Motor kapalı, zaman aşımı veya dosya okunamadı. Bu durum temiz sayılmaz |

### Tehdit

Kayıt listede kalır. İndirme, önizleme ve Collabora açılışı kapalıdır. Dosya silinmez; karantinaya alınır. Kim yüklediği ve taramanın ne bulduğu durur.

Yeni sürüm yüklenince tarama yeniden çalışır. Sistemin kendi ürettiği dokümanlar kuyruğa girmez. Taranacak olan kullanıcının yüklediği dosyadır.

### Sınırlar

ClamAV imza tabanlıdır. Bilinen zararlıları yakalar. Parolalı arşivin içini ve daha önce görülmemiş bir dosyayı görmez. Motor bellek olarak yaklaşık 1 GB ister. İmza güncellemesi için sunucunun dışarıya çıkması gerekir. AbtApp bu yükü kaldırır.

DataGateway yol haritasında aynı tarama için eski bir taslak vardır (`docs/content/MngDataGateway/support/guides/ROADMAP_MngDataGateway.md`). O taslak uygulanmamıştır. Bu madde, sonucun DI listesine ve yükleme bitişine bağlanmasını da kapsar.

## DI-02 — Belge indeksi hata veriyor

| | |
|---|---|
| Durum | Gözlendi (2026-10-27, AbtApp). Düzeltilmedi. |
| Sıra | OperationCore workspace kesitlerinden sonra. Antivirüs taraması ile birlikte bakılır. |

OpenSearch ayaktadır. İndeks yine de iki ayrı şekilde düşüyor.

Elle indeks, `6e430842-d6a6-43b5-8883-e6125a24e362` için DataGateway’den `404 FILE_NOT_FOUND` aldı. Kaynak bir `.txt` yolunu gösteriyor; dosya MinIO’da yok: `/data/users/dm_resources/d6fb63c9-205d-4983-bda7-c3f835d444b8/fb8901b1-db18-4cf8-8613-087520ac32d8.txt`. Metin çıkarma bu indirmede kırılıyor.

Aynı gün otomatik indeks, `fdf11a45-e5b4-4521-a247-0296d84a3960` için «Belgeden metin çıkmadı» dedi. Bu, dosyanın hiç bulunamamasından ayrı bir sonuçtur.

İş kaydı indeks tetiklemez. İndeks Belge Zekâsının kendi kaydıdır. Bu yüzden workspace kesiti bitmeden bu hataya girilmez; antivirüs ile aynı turda ele alınır.

## OC-01 — İş kaydındaki dosya ekini kapatmak

| | |
|---|---|
| Durum | Bitti (2026-10-27). |
| Sıra | Alt işten ve görsel akıştan önce. |

Ekler sekmesi kütüphane belgesi seçer veya yeni belgeyi kütüphane köküne ya da alt klasöre yazar. Belgeler Belge Zekâsında kalır; iş kaydı `libraryDocumentIds` ile belge kimliğini tutar. Bağı düşürmek belgeyi silmez. Kütüphaneden silinen belge işte kopuk bağ olarak görünür. İndeks, Belge Zekâsının kendi kaydıdır. İş kaydı indeks tetiklemez.

İş kaydına doğrudan dosya yükleyen liste kapalıdır. Formdaki dosya alanı yükleme kabul etmez. Sunucu `attachments` alanını reddeder. Daha önce iş kaydına yüklenmiş dosyalar bu listede görünmez. Yorum ekleri dosya olarak kalır.

Kök sonradan proje klasörüne dönerse, Operasyon klasörüne bağlı belgeler işte durur ve kopuk sayılmaz. Yeni seçim ve yeni belge yalnızca güncel kökün altına gider.

## OC-02 — Alt iş

| | |
|---|---|
| Durum | Bitti (2026-10-28). |
| Sıra | Dosya eki kapandıktan sonra. Yazılım paketindeki üst iş ve iş tiplerine dayanır. |

Ayrı bir Epic kaydı yoktur. Üst iş normal bir iş kaydıdır. Alt işler `parentItemId` ile bağlanır. Derinlik ikidir; alt işin altına yeni iş girmez.

Yazılım paketinde `type.epic` üst iştir ve altına `type.work` alır. `type.work` alt iş almaz. İş, üst işe bağlanmadan da açılabilir. İzin paketinde alt iş yoktur. Hangi tipin üst olabileceği pakette `childTypeKeys` ile yazılır.

Alt iş aynı workspace’te kalır, kendi kodunu alır ve kendi tipinin akışını kullanır. Üstün durumu altları değiştirmez. Altlar kapanınca üst kendiliğinden kapanmaz. Üst iş, altında iş varken silinmez. Döngü kurulamaz. Üst iş sprinte girmez.

İş profilinde Alt işler bölümü listeyi ve durumu gösterir, yeni alt iş açar. Alt işi olmayan iş aynı bölümden bir üst işe taşınır; bağı değiştirir veya kaldırır. Sprint ve durum bu taşımada değişmez. Pano alt işi normal kart olarak gösterir. Üst işin kartında alt iş sayısı durur. Liste tipi sütun olarak taşımaz; üst iş süzgeci bu dilimde yoktur.

## OC-03 — Görsel durum akışı

| | |
|---|---|
| Durum | Planlandı (2026-10-26). Kod yok. Sıradaki iş. |
| Sıra | Alt iş bitti. Paketin yazdığı geçiş listesini düzenler. |

Tuval, alarm akış laboratuvarındaki kabuktur: palet, düğüm, ok, taslak, yayın. Düğüm durumdur. Ok geçiştir. Geçişte etiket, zorunlu alanlar ve uygulayabilecek grup durur. Kural, otomasyon, posta ve SLA ayrı düğüm olmaz; seçilen okun yanına yazılır. Eşik, toplama ve hata ayıklama düğümleri gelmez. Bu tuval, otomasyon merkezindeki genel iş akışı motoru değildir.

Paket akışı liste olarak taşır. Tuval bu listenin editörüdür. Sihirbaz çizmez. İş kaydı, tipinde akış varsa onu kullanır; yoksa workspace’in varsayılan akışını kullanır.

Tuval seçilen akışın taslağını düzenler. Taslak akış kaydında durur. Sayfadan çıkmak taslağı silmez. Vazgeçmek yayımlanmış listeye döner. Akışta tek başlangıç düğümü vardır. Başlangıcı olmayan taslak yayınlanmaz. Tuvalden açılan yeni durum ad ve kategori sorar. Kategori açık, devam veya kapalıdır.

Yayın, üzerinde iş bulunan bir durumu akıştan çıkarmışsa o işler durumlarında kalır. O durumdan çıkan geçişler kalkar. Düğümün yerini taslak saklar. İş kayıtları bu yeri okumaz.

## OC-04 — Diğer paket gövdeleri

| | |
|---|---|
| Durum | Konuşulacak. Gövde yok. |
| Sıra | Uygulama sırasının dışında. |

İlk katalog dört pakettir: izin, yazılım geliştirme, stok ve depo, demirbaş ve zimmet. Yazılım paketi (`software` `1.3.4`) katalogdadır. Diğer üç gövde sonra konuşulur.

Stok ile depo aynı pakettir. Depo, hareketin yeridir. Demirbaş ile zimmet aynı pakettir. Zimmet, demirbaşın kişiye verilmesidir.

Kurulum bugün kütüphane alt klasörü olan paketi kurmaz. Bu gövdeler klasör veya hazır belge taşırsa kurulumun o adımı da açılır.

## OC-05 — Sprint ve proje bağlantısı

| | |
|---|---|
| Durum | Sprint dilimi bitti (2026-10-28). Proje bağlantısı sonraya bırakıldı. |
| Sıra | Uygulama sırasının dışında. |

Sprint kaydı çalışma alanına aittir. Bir alanda tek aktif sprint vardır. Kapanış işin durumunu değiştirmez. Bitmeyen iş yerinde kalır. Üst iş sprinte girmez. Liste, üyeliği ve pano süzgeci bu dilimdedir. Hız ve burndown yoktur.

Sprint, takvim ve Gantt sekmeleri çalışma alanı ayarındadır (`settings.views`). Anahtar yoksa sekme kapalıdır. Yazılım paketi kurulumda yalnızca boş anahtarı açar; kullanıcının kapattığı anahtarın üstüne yazmaz.

Proje ile workspace arasındaki iletişim bu sıranın dışındadır. Sihirbaz proje sormaz. «Proje» tipi, Projeler kaydına bağ değildir. Yeni workspace bağsız açılır.

## OC-06 — Yazılım sürecini pakete toplamak

| | |
|---|---|
| Durum | Konuşuldu (2026-10-28). |
| Sıra | Sprint diliminin kapanışı. Alt işten ve görsel akıştan önce yapılabilir. Yeni ekran açmaz. |

Yazılım paketi durumları, geçişleri, tipleri, formu, profili ve panoyu kurar. `1.3.4` paket dosyası durum renklerini, profil bölümlerini ve Vade, Başlangıç, İzleyenler etiketlerini taşır. Kurulum, sprint, takvim ve Gantt görünümünü yalnızca anahtar boşsa açar. Öncelik kataloğu, formun varsayılan önceliği, liste sütunları ve kanban kartındaki alanlar hâlâ kurulum kodundadır. Yeni bir alana aynı paket kurulunca liste ve kart eksik kalır.

Paket bu parçaları veri olarak taşır. Kurulum, aynı adlı önceliği ve havuz alanını ikinci kez açmaz; alanda varsa onu seçer. Formun varsayılan önceliği paketten gelir. Liste sütunları ve kart alanları paket kaydındadır. Kurulum kodu yazılım kelimelerini tanımaz.

Sprint motorun kaydıdır. Paket yalnızca «bu alan sprint kullanır» der. İzin, stok ve demirbaş paketleri bunu demez; sprint sekmesi onlarda açılmaz. Deneme işleri ve sprint kayıtları pakete girmez. Her kurulum boş alan açar.

## OC-07 — Takvim görünümü

| | |
|---|---|
| Durum | İlk dilim bitti (2026-10-28). |
| Sıra | Görsel akıştan önce açıldı. Belge indeksi ve antivirüsten önce durur. |

Takvim, çalışma alanının sekmesidir. Panonun üçüncü görünümü değildir. Sekme `settings.views.calendar` açıksa görünür. Kişinin ajandası değildir. Toplantı, izin veya dış takvim bağlanmaz.

Ay görünümü, içinde bulunulan ayı açar. Hafta pazartesi başlar. Günün işi, vadesi o güne düşen kayıttır: no, başlık, durum, atanan. Vadesi olmayan kayıt günlere yazılmaz; ayın altında Tarihsiz listesinde durur. Nokta, durumun tema rengidir.

Sprint bandı, sprint görünümü açıksa sprintin kendi başlangıç ve bitişindedir. Bant, tarihsiz işi güne koymaz. Takvim başlangıç tarihini okumaz. Sürükleyerek tarihi değiştirmek, hafta görünümü ve başlangıç-bitiş aralığı bu dilimde yoktur.

## OC-08 — Gantt

| | |
|---|---|
| Durum | İlk dilim bitti (2026-10-28). |
| Sıra | Takvimle birlikte açıldı. Belge indeksi ve antivirüsten önce durur. |

Gantt, çalışma alanının sekmesidir. Panonun dördüncü görünümü değildir. Sekme `settings.views.gantt` açıksa görünür.

Çubuğun boyu takvim günüdür. Başlangıç ve vade birlikte aralığı kurar. Biri varsa çubuk bir gündür. İkisi de yoksa kayıt Tarihsizdedir. İş puanı boyu belirlemez. Çubuğun rengi durumun tema rengidir. Profildeki geçiş düğmesi, gidilecek durumun rengini kullanır. Bağımlılık oku yoktur. Çubuğu sürüklemek tarihi yazmaz.

Satır iş kaydıdır. Üst iş grup satırı değildir; ayrı satırda durur. Sprint bandı, sprint görünümü açıksa takvimdekiyle aynıdır.

## OC-09 — Sprint raporu

| | |
|---|---|
| Durum | Konuşuldu (2026-10-28). |
| Sıra | Paket toplandıktan sonra. Belge üreticisine ve çalışma alanı kütüphanesine dayanır. |

Rapor, motorun ürettiği sabit bir PDF değildir. Yazılım paketi bir belge şablonu gösterir. Üretim, sprintin adını, amacını, tarihini, durumunu ve iş tablosunu şablona basar. Tabloda no, başlık, durum, atanan, öncelik ve iş puanı durur. Reddedilen iş satırda kalır. Hız ve burndown hesaplanmaz.

Dosya, çalışma alanının kütüphane klasörüne yeni bir belge olarak yazılır. Önceki raporun üstüne yazılmaz. Aktif sprintten de alınabilir. Üretilen belge sistem belgesidir. Antivirüs kuyruğuna girmez.

Kurulum, kütüphane alt klasörü olan paketi bugün kurmaz. Şablon bu yüzden belge üreticisinin şablonudur. Paket yalnızca şablonun anahtarını taşır. Çıktı kütüphaneye sonradan düşer.
