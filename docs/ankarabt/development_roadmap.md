# AnkaraBT — geliştirme yol haritası

AbtApp (`192.168.1.249`, `https://app.ankarabt.com`) üzerindeki ürün işleri. Sunucu, etki alanı ve posta notları `server/` altındadır.

OperationCore maddeleri [OC workspace oluşturma planı](../odak/operationcore/OC_WORKSPACE_CREATION_PLAN.md) (26 Ekim 2026) içinden gelir. O planda kütüphane kökü, kütüphane görünümü, yazılım paketi kataloğu, paket kurulumu ve sihirbaz bitmiştir. Aşağıya yalnızca kalan kesitler yazıldı.

Durumlar: **konuşuldu**, **planlandı**, **yapılıyor**, **bitti**.

## DI-01 — Yüklenen dosyaların antivirüs taraması

| | |
|---|---|
| Durum | Konuşuldu (2026-10-27). Kod yok. |
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

## OC-01 — İş kaydındaki dosya ekini kapatmak

| | |
|---|---|
| Durum | Kalan kesit (2026-10-26). Belge bağı kodda. |
| Sıra | Alt işten ve görsel akıştan önce. |

Ekler sekmesi kütüphane belgesi seçer veya yeni belgeyi kütüphane köküne yazar. Belgeler Belge Zekâsında kalır; iş kaydı belge kimliğini tutar. İndeks, Belge Zekâsının kendi kaydıyla çalışır. İş kaydı ayrıca indeks tetiklemez.

Kalan iş, iş kaydına doğrudan dosya yükleyen listeyi kapatmaktır. Sekmede yalnızca kütüphane bağı durur. Daha önce iş kaydına yüklenmiş dosyalar yeni yol değildir. Yorum ekleri dosya olarak kalır ve bu kurala girmez.

Kök sonradan proje klasörüne dönerse, Operasyon klasörüne bağlı belgeler işte durur ve kopuk sayılmaz. Yeni seçim ve yeni belge yalnızca güncel kökün altına gider.

## OC-02 — Alt iş

| | |
|---|---|
| Durum | Planlandı (2026-10-26). Kod yok. |
| Sıra | Dosya eki kapandıktan sonra. Yazılım paketindeki üst iş ve iş tiplerine dayanır. |

Ayrı bir Epic kaydı yoktur. Üst iş normal bir iş kaydıdır. Alt işler `parentItemId` ile bağlanır. Derinlik ikidir; alt işin altına yeni iş girmez.

Yazılım paketinde `type.epic` üst iştir ve altına `type.work` alır. `type.work` alt iş almaz. İş, üst işe bağlanmadan da açılabilir. İzin paketinde alt iş yoktur. Hangi tipin üst olabileceği pakette yazılır.

Alt iş aynı workspace’te kalır, kendi kodunu alır ve kendi tipinin akışını kullanır. Üstün durumu altları değiştirmez. Altlar kapanınca üst kendiliğinden kapanmaz. Üst iş, altında iş varken silinmez. Döngü kurulamaz.

İş kartında Alt işler bölümü listeyi gösterir ve yeni alt iş açar. Pano alt işi normal kart olarak gösterir. Sprint bu bağın üstüne sonra oturur.

## OC-03 — Görsel durum akışı

| | |
|---|---|
| Durum | Planlandı (2026-10-26). Kod yok. |
| Sıra | Alt işten sonra. Paketin yazdığı geçiş listesini düzenler. |

Tuval, alarm akış laboratuvarındaki kabuktur: palet, düğüm, ok, taslak, yayın. Düğüm durumdur. Ok geçiştir. Geçişte etiket, zorunlu alanlar ve uygulayabilecek grup durur. Kural, otomasyon, posta ve SLA ayrı düğüm olmaz; seçilen okun yanına yazılır. Eşik, toplama ve hata ayıklama düğümleri gelmez. Bu tuval, otomasyon merkezindeki genel iş akışı motoru değildir.

Paket akışı liste olarak taşır. Tuval bu listenin editörüdür. Sihirbaz çizmez. İş kaydı, tipinde akış varsa onu kullanır; yoksa workspace’in varsayılan akışını kullanır.

Tuval seçilen akışın taslağını düzenler. Taslak akış kaydında durur. Sayfadan çıkmak taslağı silmez. Vazgeçmek yayımlanmış listeye döner. Akışta tek başlangıç düğümü vardır. Başlangıcı olmayan taslak yayınlanmaz. Tuvalden açılan yeni durum ad ve kategori sorar. Kategori açık, devam veya kapalıdır.

Yayın, üzerinde iş bulunan bir durumu akıştan çıkarmışsa o işler durumlarında kalır. O durumdan çıkan geçişler kalkar. Düğümün yerini taslak saklar. İş kayıtları bu yeri okumaz.

## OC-04 — Diğer paket gövdeleri

| | |
|---|---|
| Durum | Konuşulacak. Gövde yok. |
| Sıra | Uygulama sırasının dışında. |

İlk katalog dört pakettir: izin, yazılım geliştirme, stok ve depo, demirbaş ve zimmet. Yazılım paketi (`software` `1.0.0`) katalogdadır. Diğer üç gövde sonra konuşulur.

Stok ile depo aynı pakettir. Depo, hareketin yeridir. Demirbaş ile zimmet aynı pakettir. Zimmet, demirbaşın kişiye verilmesidir.

Kurulum bugün kütüphane alt klasörü olan paketi kurmaz. Bu gövdeler klasör veya hazır belge taşırsa kurulumun o adımı da açılır.

## OC-05 — Sprint ve proje bağlantısı

| | |
|---|---|
| Durum | Sonraya bırakıldı (2026-10-26). |
| Sıra | Uygulama sırasının dışında. |

Sprint, alt iş bağının üstüne sonra oturur. Proje ile workspace arasındaki iletişim de bu sıranın dışındadır. Sihirbaz proje sormaz. «Proje» tipi, Projeler kaydına bağ değildir. Yeni workspace bağsız açılır.
