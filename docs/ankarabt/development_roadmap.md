# AnkaraBT — geliştirme yol haritası

AbtApp (`192.168.1.249`, `https://app.ankarabt.com`) üzerindeki ürün işleri. Sunucu, etki alanı ve posta notları `server/` altındadır.

Belge indeksi ve antivirüs taraması bitti. OperationCore’da kalan kesitler duruyor.

OperationCore maddeleri [OC workspace oluşturma planı](../odak/operationcore/OC_WORKSPACE_CREATION_PLAN.md) (26 Ekim 2026) içinden gelir. O planda kütüphane kökü, kütüphane görünümü, yazılım paketi kataloğu, paket kurulumu ve sihirbaz bitmiştir. Aşağıya yalnızca kalan kesitler yazıldı.

Durumlar: **konuşuldu**, **planlandı**, **yapılıyor**, **bitti**.

Toplantı modülünde konuşulan işler bitti. Takvim dosyası ve kabul-ret bu işe girmedi. Antivirüs taraması, belge indeksi hatası ve yazılım paketinin toplanması bitti. Yazılım paketinin özet ve sprint panosu pakete girdi. Help Desk paketi kuruldu. Bağı olmayan çalışma alanının kütüphane klasörü artık alanın adını taşır. Kullanıcı eğitim seti yazıldı ve indekslendi. Envanter çekirdeği abt alanına kuruldu. OperationCore’da sıradaki kod işi OC-03 görsel akıştır. Envanterin kullanılabilirlik dilimi OC-04 içindedir.

## DI-01 — Yüklenen dosyaların antivirüs taraması

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Belge indeksi hatası ile aynı turda kapandı. |
| İstek | Document Intelligence’a yüklenen her dosya ClamAV ile taransın. Sonuç hem yükleme bitince hem dosya listesinde görünsün. |

### Akış

Yükleme bugün iki adımdır: MngDocument kaynak kaydını açar, dosyanın kendisi DataGateway üzerinden MinIO’ya yazılır. Tarama bu ikinci adımın hemen ardından çalışır.

Tarama, yükleme isteğinin içinde bekletilmez. Dosya kaydı listeye **taranıyor** durumuyla düşer. Tarama bitince aynı satır güncellenir. Yükleme penceresi kapanmadan önce bu son durumu da gösterir; liste ile aynı alandan okur.

Sonuç kaynak kaydında (`dm_resources`) durur. MngDocument dosyayı indirir, ClamAV ile tarar ve sonucu kaynağa yazar. Liste, yükleme özeti ve indirme kararı bu alana bakar. İçerik indeksi rozetiyle aynı türden bir kolondur.

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
| Durum | Bitti (2026-11-01). |
| Sıra | Antivirüs taraması ile aynı turda kapandı. |

OpenSearch ayaktadır. Eksik dosyada indeks artık ham `404` ile kırılmaz. Toplu indekste bu sonuç «Dosya okunamadı» olur. Metni olmayan dosya ayrı kalır: «Belgeden metin çıkmadı».

27 Ekim’de görülen iki kayıt veri olarak durabilir. `6e430842-d6a6-43b5-8883-e6125a24e362` bir `.txt` yolunu gösteriyordu; dosya MinIO’da yoktu. `fdf11a45-e5b4-4521-a247-0296d84a3960` için metin çıkmamıştı. Kod bu dosyaları geri getirmez.

İş kaydı indeks tetiklemez. İndeks Belge Zekâsının kendi kaydıdır.

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

## OC-04 — Envanter

| | |
|---|---|
| Durum | Çekirdek abt’de (2026-11-02). Kullanılabilirlik dilimi planlandı. Katalog paketi yok. |
| Sıra | Görsel akıştan (OC-03) ayrı. İlk dilim kural, form ve rapor ayarıdır. |

Adı **Envanter**. Stok, depo, demirbaş ve zimmet bu adın altındadır. Operasyon Merkezi ağacında klasör bu adı taşır. Altında Zimmet Depo ve Personel Zimmet durur. Dinamik form menüsü bugün Zimmet Yönetimi’dir; aynı ad oraya da yazılır.

Ayrıntı: [stok, depo, demirbaş ve zimmet](stok_depo_demirbas_zimmet_plani.md). Kurulum katalog paketi değildir. Ayrı bir zimmet servisi yoktur. Kapanış, çalışma alanının geçişindedir.

Abt alanına şunlar indi: beş veri seti, mevcut tedarikçi bağı, iki çalışma alanı, üç kapanış kuralı, üç rapor, üç belge, ikinci çalıştırmada çiftlemeyen demo. Ürün grubu kategoridir. Marka katalogda metindir. Yeni bir tür ağacı açılmaz.

Çekirdek emanet takibidir. Stok muhasebesi ve taşınır defteri bu dilimin dışında kalır.

### Kullanılabilirlik

Günlük kullanımdan önce. Sıra sabittir.

1. Teslim, demirbaşın depo ve rafını temizler. İade, depoyu ve rafı yeniden yazar. Zimmetli satır depoda görünmez.
2. Demirbaş formunda durum, kişi, depo ve raf kilitlidir. Bu dört alan yalnız geçişten değişir.
3. Teslim ve iade belirli gruplara bağlanır. Grupların adı bu dilim uygulanırken yazılır.
4. Demirbaş envanteri ürün grubunu ve markayı gösterir, ikisini de süzer. Grup katalogdaki bağdan okunur. “Bütün laptoplar” ve “bütün Dell’ler” bu listeden sorulur.

### Sonra

Kullanılabilirlik kapandıktan sonra, bu sırayla:

- Üretici seri numarası demirbaşa yazılır. Grup bayrağı, seri listesi boşsa üretilmiş numaraya izin verir.
- Garanti bitişi, katalogdaki ay ile giriş tarihinden hesaplanır.
- Demirbaş satırında zimmet geçmişi durur. İade, son iş kodunu silince hikâye kaybolmaz.
- Hurda ve kayıp bir geçişle işlenir. Formdan işaretlenmez.
- Satınalma çalışma alanı depo girişine bağlanır.

Miktar bakiyesi (bir satırda adet) ayrı bir stok kartıdır. Bu gövdeye girmez. Geçiş bitince kendiliğinden tutanak ve ürün görseli bu sıranın dışındadır.

İzin paketi ayrıca konuşulur. Yazılım paketi (`software` `1.3.4`) katalogdadır. Kurulum bugün kütüphane alt klasörü olan paketi kurmaz. Envanter klasör veya hazır belge taşırsa o adım da açılır.

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
| Durum | Bitti (2026-11-01). |
| Sıra | Sprint diliminin kapanışı. Yeni ekran açmaz. |

Yazılım paketi `1.4.0` durumları, geçişleri, tipleri, formu, profili ve panoyu kurar. Paket dosyası durum renklerini, profil bölümlerini, Vade, Başlangıç ve İzleyenler etiketlerini, öncelik kataloğunu, formun varsayılan önceliğini, liste sütunlarını ve kanban kartındaki alanları taşır. Kurulum, sprint, takvim ve Gantt görünümünü yalnızca anahtar boşsa açar.

Paket bu parçaları veri olarak taşır. Kurulum, aynı adlı önceliği ve havuz alanını ikinci kez açmaz; alanda varsa onu seçer. Formun varsayılan önceliği paketten gelir. Liste sütunları ve kart alanları paket kaydındadır. Kurulum kodu yazılım kelimelerini tanımaz.

Sprint motorun kaydıdır. Paket yalnızca «bu alan sprint kullanır» der. İzin, stok ve demirbaş paketleri bunu demez; sprint sekmesi onlarda açılmaz. Deneme işleri ve sprint kayıtları pakete girmez. Her kurulum boş alan açar.

## OC-07 — Takvim görünümü

| | |
|---|---|
| Durum | İlk dilim bitti (2026-10-28). |
| Sıra | Görsel akıştan önce açıldı. İlk dilim kapandı. |

Takvim, çalışma alanının sekmesidir. Panonun üçüncü görünümü değildir. Sekme `settings.views.calendar` açıksa görünür. Kişinin ajandası değildir. Toplantı, izin veya dış takvim bağlanmaz.

Ay görünümü, içinde bulunulan ayı açar. Hafta pazartesi başlar. Günün işi, vadesi o güne düşen kayıttır: no, başlık, durum, atanan. Vadesi olmayan kayıt günlere yazılmaz; ayın altında Tarihsiz listesinde durur. Nokta, durumun tema rengidir.

Sprint bandı, sprint görünümü açıksa sprintin kendi başlangıç ve bitişindedir. Bant, tarihsiz işi güne koymaz. Takvim başlangıç tarihini okumaz. Sürükleyerek tarihi değiştirmek, hafta görünümü ve başlangıç-bitiş aralığı bu dilimde yoktur.

## OC-08 — Gantt

| | |
|---|---|
| Durum | İlk dilim bitti (2026-10-28). |
| Sıra | Takvimle birlikte açıldı. İlk dilim kapandı. |

Gantt, çalışma alanının sekmesidir. Panonun dördüncü görünümü değildir. Sekme `settings.views.gantt` açıksa görünür.

Çubuğun boyu takvim günüdür. Başlangıç ve vade birlikte aralığı kurar. Biri varsa çubuk bir gündür. İkisi de yoksa kayıt Tarihsizdedir. İş puanı boyu belirlemez. Çubuğun rengi durumun tema rengidir. Profildeki geçiş düğmesi, gidilecek durumun rengini kullanır. Bağımlılık oku yoktur. Çubuğu sürüklemek tarihi yazmaz.

Satır iş kaydıdır. Üst iş grup satırı değildir; ayrı satırda durur. Sprint bandı, sprint görünümü açıksa takvimdekiyle aynıdır.

## OC-09 — Sprint raporu

| | |
|---|---|
| Durum | Konuşuldu (2026-10-28). |
| Sıra | Yazılım paketi toplandı. Belge üreticisine ve çalışma alanı kütüphanesine dayanır. |

Rapor, motorun ürettiği sabit bir PDF değildir. Yazılım paketi bir belge şablonu gösterir. Üretim, sprintin adını, amacını, tarihini, durumunu ve iş tablosunu şablona basar. Tabloda no, başlık, durum, atanan, öncelik ve iş puanı durur. Reddedilen iş satırda kalır. Hız ve burndown hesaplanmaz.

Dosya, çalışma alanının kütüphane klasörüne yeni bir belge olarak yazılır. Önceki raporun üstüne yazılmaz. Aktif sprintten de alınabilir. Üretilen belge sistem belgesidir. Antivirüs kuyruğuna girmez.

Kurulum, kütüphane alt klasörü olan paketi bugün kurmaz. Şablon bu yüzden belge üreticisinin şablonudur. Paket yalnızca şablonun anahtarını taşır. Çıktı kütüphaneye sonradan düşer.

## OC-10 — Yazılım paketi özet panosu

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Yazılım paketi toplandı. Help Desk paketinden önce kapandı. |

İki pano vardır. İkisi de kanban değildir. `op_dashboards` kaydıdır. Genel pano tasarımcısındaki personel panosu bu işe girmez.

**Yazılım özeti** varsayılandır. Sprint bilmez. Üstte dört durum kartı durur: Yeni, Geliştirmede, İncelemede, Testte. Yanında açık işlerin puan toplamı vardır; aynı kart puanı yazılmamış açık işi de sayar. Ortada iki grafik vardır: açık puan durumlara bölünür, Yeni işler önceliğe bölünür. Altta iki liste vardır: incelemedeki işler ve kişiye atanmış açık işler. Satırda no, başlık, durum, atanan ve iş puanı durur.

**Sprint** ikinci panodur. Varsayılan değildir. Sprint görünümü kapalı alanda kurulmaz. Üç kart vardır: sprintteki iş, sprintte açık puan, sprintte kapanan puan. Bir grafik sprintteki işi duruma böler. Liste sprintin işlerini aynı satırla gösterir. Aktif sprint yoksa pano boş kalır. Hız ve burndown yoktur.

Grafik yazısı açık ve koyu temayı şablondaki grafiklerden okur. Açık iş sorgusu kapanmış kaydı saymaz. Yerel alanda pano **Yazılım geliştirme** çalışma alanına kuruldu.

## OC-11 — Özet panoyu yazılım paketine koymak

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | OC-10 ile aynı turda kapandı. |

Yazılım paketi `1.5.0` iki panoyu taşır. Kurulum `op_dashboards` yazar. Aynı adlı pano varsa ikincisini açmaz ve duran panonun üzerine yazmaz. Yeni kurulan alan Yazılım özeti ile açılır. Sprint panosu, sprint görünümü açıksa kurulur. Kurulu bir alan bu sürümü açık yükseltmeyle alır. Yerel **Yazılım geliştirme** alanı bu sürümle açıldı.

## OC-12 — Help Desk çalışma alanı

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Yazılım panosu pakete girdikten sonra kapandı. |

Help Desk ayrı bir pakettir: `helpdesk` `1.0.0`. İzin, stok ve demirbaş gövdeleri bu iş değildir. Sprint ve iş puanı yoktur. Aynı paket iki kuyruk açar. Ayrım çalışma alanındadır.

Tipler Talep, Hata ve Değişikliktir. Yeni kayıt Talep olarak açılır. Durumlar Yeni, İşlemde, Beklemede, Kapatıldı ve Reddedildi. Geçişler Üstlen, Beklet, Sürdür, Kapat, Reddet ve Yeniden açtır. Bekletme, kapatma ve reddetme yorum ister. Üstlenme ve yeniden açma atanan kişi ister. Öncelikler yazılım paketiyle aynıdır: Acil, Yüksek, Normal, Düşük. Varsayılan Normal. Alanlar başlık, açıklama, tip, öncelik, talep eden, atanan, izleyenler ve vadedir. Konu listesi pakette yoktur.

Pano adı Kuyruktur. Varsayılan özet Yardım özetidir. Dört kart Yeni, İşlemde, Beklemede ve bana atanan açık işi sayar. İki grafik açık kayıtları önceliğe ve tipe böler. İki liste yeni kuyruğu ve bana atanan açık işi gösterir. Bana atanan kart çalışma alanına kilitli değildir; kişinin alandaki bütün açık işini sayar.

Yerelde iki alan açıldı: **Monitra Yardım** (`MYD`) ve **IT Help Desk** (`IT`). Görme ve düzenleme managers ve admins gruplarındadır. Üç çalışma alanı kütüphanesinde Yonetim ve Abt Calisan tam yetkilidir.

## OC-13 — Operasyon klasörünün adı

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Kütüphane kökü açıldıktan sonra. Görünen adı düzeltir. |

Bağı olmayan çalışma alanı `Dökümanlar / Operasyon` altında durur. Klasörün görünen adı çalışma alanının adıdır. Kod (`WS-0004`) klasör adı değildir. Bağ `op_workspaces.diFolderId` alanındadır. Çocuklar, dosyalar ve yetkiler bu ad değişince yerinden oynamaz.

26 Ekim planında görünen adın klasör adı olmayacağı yazıyordu. O kural bu maddeyle değişti. Alanın adı sonradan değişirse klasör kendiliğinden yeniden adlandırılmaz. Klasör hâlâ kod adındaysa, kütüphane ilk açıldığında alanın adına çekilir. Kullanıcının kendi verdiği ad yerinde kalır.

Projeye bağlı çalışma alanı ayrı Operasyon klasörü açmaz. Projenin klasörünü kullanır: `Dökümanlar / Projeler / {proje kodu}`.

Yerelde üç klasör ada çekildi: Yazılım geliştirme, Monitra Yardım, IT Help Desk. Bağı olmayan boş klasörler silindi. İçinde dosya olan ve çalışma alanına bağlı olmayan `WS-0003` duruyor.

## MT-01 — Toplantı günü ve seri

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Toplantı modülünün tabanı. |

Seri bir kalıptır. Günler `mt_meetings` satırıdır. Günün kendi tutanağı, kalıptan ayrı gündemi, davetli dışında bir katılım durumu, katılım notu, farklı katılımcı listesi, görevlendirmesi veya değişmiş saati varsa seri kaydı o günü yeniden yazmaz.

Toplantı penceresi sekmelerden oluşur: olay, katılımcılar, gündem, tutanak, ekler, görevlendirmeler, davet. Sayfa takvimle açılır. Liste, arama, özet, rapor ve ayarlar onun yanındadır.

## MT-02 — Toplantı konusu

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Arama ve özete zemin. |

Konu düz bir listedir: ad, kısa kod, renk, arşiv. Ağaç yoktur. Konu Ayarlar’da tanımlanır. Toplantı ve seri onu listeden seçer. Seri kaydı, konusu ayrıca seçilmemiş günlere konuyu kopyalar. Günün konusu seriden ayrıldıysa yalnız o alan korunur; başlık ve saat yine kalıptan gelir. İptal gri kalır. Diğer günler konunun rengini takvimde kullanır.

## MT-03 — Ekler

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Gündem ve tutanaktan ayrı. |

Ek, o güne bağlanan bir kütüphane belgesidir. Dosya kopyalanmaz. Kütüphaneden seçilir, taranarak yüklenir veya boş bir sayfa olarak açılır. Gündem ve tutanak ek sayılmaz. Seri kaydı ekleri değiştirmez. Toplantı silinince bağ silinir; belge kütüphanede kalır.

## MT-04 — Arama

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Konu ve eklerden sonra. |

İki kip vardır. Toplantı araması kaydın kendi alanlarına bakar. Üstte başlık ve tarih durur. Gelişmiş satırlar birlikte aranır: başlık, yer, konu, durum, düzenleyen, katılımcı, kurum dışı kişi, katılım durumu, seri, tutanak, gündem belgesi, ek, görevlendirme. Katılımcı birden fazla kişi alır. Şunlardan biri, hepsi ve hiçbiri ayrıdır.

İçerik araması en az üç karakter ister. Gündem metnine, tutanağa ve indekslenmiş eklere bakar. Sonuçta rol ve kısa parça görünür. İndeksi dolmamış ek bu listede çıkmaz.

## MT-05 — Katılım notu ve kurum dışı kişiler

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Aramadan sonra. |

Not, katılımcının o güne ait satırındadır. Belge ve tutanak değildir. İsteğe bağlıdır. Notu olan günün katılım listesini seri kaydı silip baştan yazmaz.

Kurum dışı kişiler Ayarlar’da tanımlanır. Giriş hesabı değildir. Toplantı ve seri onları listeden seçer. Bir toplantıda veya seride kayıtlı kişi silinmez.

## MT-06 — Konuya göre özet

| | |
|---|---|
| Durum | Bitti (2026-11-01). Varsayılan pano bu şekliyle yeterli. |
| Sıra | MT-05 bitti. |

Özet, takvimden sonraki görünümüdür. Üstte dört sayı durur: yaklaşan gün, tutanağı eksik gün, açık görev, gecikmiş görev. Yaklaşan, başlangıcı şu andan sonra olan ve 14 günü aşmayan gündür. Tutanağı eksik, bitişi geçmiş, son 30 günün içinde kalan ve tutanak belgesi olmayan gündür. Gecikmiş, termini İstanbul takviminde bugünden önce olan açık görevdir. Bugün terminli olan gecikmiş sayılmaz.

İki grafik vardır. İlki önümüzdeki 14 günü gün gün, konunun rengiyle böler. Sütun, o günün yaklaşan kayıtlarını listede öne alır. İkincisi açık görevleri konuya göre, gecikmemiş ve gecikmiş olarak ayırır. Çubuk o konunun görev listesine iner. Sayı kartı ilgili sütuna iner.

Altta konular aynı üç listeyle durur. Konusu olmayanlar Konusuz başlığındadır. Üçü de boş olan konu gizlenir. İptal edilmiş gün hiçbir yere girmez. Kişisel pano ve sürüklenebilir kutu yoktur.

## MT-07 — Konu raporu

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | MT-06 bitti. |

Rapor, özetin ardından ayrı bir görünümdür. Bir konu ve bir tarih aralığı seçilir. Aralık varsayılan olarak içinde bulunulan İstanbul ayıdır. İptal edilmiş günler girmez. Konusu olmayanlar Konusuz başlığındadır.

Her satırda tarih, başlık, durum, tutanak ve görevler durur. Açık görev önce gelir. Tamamlanan görev de listelenir. Başlık toplantıyı açar. Tutanak belgesi varsa önizlenir. İstenirse aynı liste kütüphanede boş bir sayfaya yazılır. Sayfa, tutanakların gövdesini taşımaz; günün adını, durumunu, tutanak adını ve görevleri yazar. Yeni bir belge motoru açmaz.

## MT-08 — Açık güne Word tutanağı

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | MT-07 ile birlikte. |

Toplantı penceresi açıkken ve gün kayıtlıyken aktarım, dosyayı o günün tutanağı yapar. Gün planlandıysa yapılmışa döner. İptal edilmiş gün yapılmışa çevrilmez. Günün gündemi, katılım listesi ve görevleri silinmez. Belgeden gelen kişi listede yoksa kurum dışı ve katıldı olarak eklenir. Belgeden gelen görev, mevcut görevlerin ardına yazılır. Günün tutanağı zaten varsa değiştirmeden önce sorulur. Önceki kütüphane dosyası durur. Kayıtsız günde önce toplantının kaydedilmesi gerekir. Pencere kapalıyken araç çubuğundaki aktarım, takvimde günü olmayan eski bir tutanak için yeni bir yapılmış gün açmaya devam eder.

## MT-09 — Seri kaydının hızı

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | MT-01 kuralının üstüne. |

Seri kaydı, alanları ve katılımcı üyeliği kalıpla aynı olan düz günü yeniden yazmaz. Görev ve katılım toplu okunur. İstisna kuralları aynı kalır: tutanak, not, davetli dışı katılım, görev, ayrılmış gün, iptal ve güne özel konu korunur. Güne özel oda da yalnız o alanda korunur.

## MT-10 — Davet postası

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Seri kaydından sonra. |

Davet, açık olan güne aittir. Serinin bütün günlerine tek düğmeyle posta gitmez. Kayıt sırasında kendiliğinden gönderilmez. Toplantı penceresindeki Davet sekmesi alıcıları ve o güne ait gönderim kayıtlarını gösterir. Kayıt zamanı, türü, kime gittiğini, kime gitmediğini ve sonucu tutar. Postanın kendisi saklanmaz.

Daha önce davet gitmemişse düğme davet gönderir. Davet gitmiş bir günde başlık, saat veya yer değiştiyse güncelleme gider. Davet gitmiş gün iptal edildiyse iptal bildirimi gider. Hiç davet gitmemiş bir iptal için düğme kapalı kalır. Adresi olmayan kişi listede durur, postaya girmez. Her kişiye ayrı posta gider.

Ayarlar’daki Bildirimler sekmesi üç satır tutar: davet, güncelleme, iptal. Her birinin anahtarı ve şablonu vardır. Üçü de kapalı başlar. Şablon seçilmeden veya anahtar açılmadan gönderilemez.

## MT-11 — Oda ve çakışma

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | En son toplantı işi. |

Yer serbest metin olarak kalır. Oda, kendi takvimi olan bir yerdir: ad, isteğe bağlı kapasite, arşiv. Oda Ayarlar’da tanımlanır. Toplantı ve seri onu listeden seçer. Takvimde bir oda seçilince yalnız o odanın günleri görünür. Seri günü serinin odasını kullanır. O gün için başka oda seçilirse yalnız o günün odası ayrılır.

Aynı odada süreleri kesişen iki gün kaydı durdurmaz. Uyarı çıkar ve kayıt istenirse yine yapılır. İptal edilmiş günler uyarıya girmez. Takvim dosyası ve kabul-ret yoktur.

## EG-01 — Kullanıcı eğitim seti

| | |
|---|---|
| Durum | Bitti (2026-11-01). |
| Sıra | Ürün kodu değildir. Sayfalar kütüphanededir. |

Yer `Dökümanlar / MonitraNG / Eğitim`dir. Dört klasör vardır.

| Klasör | Sayfa |
|---|---|
| Proje | 22 |
| Kütüphane | 11 |
| Operasyon Merkezi | 13 |
| Toplantılar | 12 |

Proje sayfaları proline eğitiminden kopyalandı. Proline’daki asıllar durur. İç bağlantılar abt kopyasına çevrildi. Diğer üç set aynı yerde, Atölye kabulü örneğiyle yazıldı. 58 sayfa indekslendi. Kütüphane aramasında başlık ve içeriklerinden bulunur.
