# Organizasyon şeması — yol haritası

Son güncelleme: 16 Kasım 2026.

Durum: Şema ofiste kullanılıyor (16 Kasım 2026). Yayın yapıldı. Unvan ve departmanın sahibi şemadır. Çalışanlar aynı kutudadır. Active Directory’ye yazılmaz.

Ortam AbtAppServer’dır. Erişim [server/access.md](../../server/access.md) içindedir. Adres `192.168.1.249`, uygulama `https://app.ankarabt.com`. İlk veri `abt` alanındadır. Şema alan verisidir. Başka alan açılırsa aynı kayıt o alanda durur.

## Ne bu iş

Görsel bir ağaçtan mevki çizilir. Kutu kişidir diye değil, mevkidir. Kişi koltuğa oturur. Yerel kullanıcı ve dizin kullanıcısı aynı koltuğa oturabilir.

Yayın, kartları günceller. Sürüklemek yayın değildir. İzin personel kartındaki yönetici kişisine bakar. Yayın o alanı şemadan doldurur ve bu bağı bozmaz. Onay geçişinde mevki seçilmişse karar anında koltuktaki kişi bakar. Seçilmemiş talep grupta kalır.

Active Directory’ye unvan, departman veya yönetici yazılmaz. Bu karar 16 Kasım 2026’da kilitlendi.

## Kilitli kararlar

1. Düğüm mevkidir. Kişi düğüm değildir. Kişi değişince mevki ve ona bağlı onay durur.
2. Departman ayrı kutu değildir. Kutunun üzerindeki etikettir.
3. Her mevkinin tek üstü vardır. Kökün üstü yoktur. Döngü ve ikinci ebeveyn kabul edilmez.
4. Bir kullanıcı tek mevkide durur. Ya oturandır ya da çalışandır. Koltuk boş kalabilir. Çalışan ayrı kutu değildir.
5. Tuval üstten alta ağaçtır. Kutu başka kutunun altına bırakılır. Serbest ok çizilmez.
6. Tuval, alarm akış laboratuvarındaki yüzeyin ağaç halidir. Keşif ekranındaki ağaç ve graf yerleşimi taşınmaz. Onlar üç katmanlı ve salt okunurdur.
7. Yayın, oturanın unvanını mevki adından, departmanını etiketten yazar. Çalışanın unvanı durur. Departmanı bu koltuğun etiketidir. Yöneticisi oturandır.
8. Yayın, dizin kullanıcısında ad, e-posta ve gruba dokunmaz. Unvan ve departman yalnızca MonitraNG kartına yazılır.
9. Şema bu iki alanın sahibi olduktan sonra dizin senkronu, AD dolu gelse bile unvan ve departmanı ezmez. Sahiplik yalnızca yayınla yazılır. Elle unvan düzenlemesi sahipliği açmaz ve açık sahipliği silmez.
10. Yönetici, Keeper kullanıcısında ayrı bir alan değildir. İzin personel kartındaki kişi alanına yazılır. Üst koltuk boşsa yönetici boş kalır. İzindeki yedek, `Yonetim` grubu, durur.
11. Onay hedefi mevki olunca karar anında koltukta kim varsa o bakar. Koltuk boşsa üst mevki, o da boşsa bugünkü grup yedeği devreye girer.
12. Active Directory’ye unvan, departman veya yönetici yazılmaz. Şema MonitraNG’de kalır.

## Bu yol haritasında olmayanlar

| İş | Ne zaman |
|---|---|
| Ayrı departman ağacı | Yok. Departman etiket kalır |
| Bir kişinin iki koltuğu | Yok |
| Serbest çizim, ikinci ebeveyn, döngü | Tuval kabul etmez |
| AD’ye geri yazma | Yok. 16 Kasım 2026’da kapatıldı |
| Şemadan grup veya OU üretmek | Yok |
| İzin dönem hesabı, PDKS | Bu işe girmez |

## Kayıt

Mevki, alan verisidir. Dataset `org_mevkiler`.

| Alan | Anlamı |
|---|---|
| Ad | Mevki adı. Yayında unvan olur |
| Üst | Tek mevki. Kökte boş |
| Departman | Etiket. Yayında departman olur. Boş kalabilir |
| Oturan | Keeper kullanıcısı. Boş kalabilir. Kullanıcı tek satırda durur |
| Çalışanlar | Aynı kutudaki kişiler. Ayrı mevki değildir. Oturan bu listede duramaz |
| Sıra | Kardeşler arasındaki diziliş |

Ağaç kuralı kayıt yazılırken durur. Tuval aynı kuralı ikinci kez çizemez.

Keeper kartında `title` ve `department` durur. Yönetici alanı yoktur. Dizin kullanıcısında unvan ve departman uygulama tarafından düzenlenebilir. Dizin senkronu, şemanın sahip olmadığı kartta bu iki alanı doluysa yazar. `orgOwnsProfile` açık kartta unvan ve departman farkı senkron tetiklemez ve yazılmaz. İlk dizin kullanıcısı açılırken bayrak kapalıdır; AD unvanı o anda kopyalanır.

İzin personel kartındaki `yoneticiId` Keeper kişisidir. Boşsa onay `Yonetim` grubuna düşer. Yayında oturanın yöneticisi üst koltuğun oturanıdır. Çalışanın yöneticisi kendi koltuğunun oturanıdır. Oturan boşsa çalışanın yöneticisi boşalır.

## Tuval

Sayfa Mng.Ui içindedir. Yerleşim üstten alta ağaçtır. Kaydırma, sığdırma ve küçük harita durur.

Kök varsa Kök mevki düğmesi görünmez. Yeni mevki, seçili kutunun altına açılır. Yer tutucu adlar (Kök mevki, Yeni mevki) kutuda yazılmaz; başlık departman olur. Gerçek mevki adı başlıkta kalır.

Oturan mavi, çalışan turkuazdır. Kutuda en fazla üç çalışan görünür. Fazlası **ve N kişi** satırındadır. Satır, salt okunur liste açar. Liste ada göre sıralanır, adla aranır ve 20’şer sayfalanır. Sağ panel açılmaz. Kişi penceresi açılışta Ad Soyad sütununa göre A’dan Z’ye gelir.

Kullanıcı Keeper listesinden koltuğa oturtulur veya çalışanlara eklenir. Bırakılan kutu yeni üstün çocuğu olur. Kayıt bunu reddederse tuval eski üstte kalır.

Taslak, yayınlanmamış ağaçtır. Yayın düğmesi kartları günceller.

## Yayın

Tek işlem. Sürükleme ara kayıt yazabilir. Kart güncellemesi yalnızca yayındadır.

| Kart | Yazılan | Yazılmayan |
|---|---|---|
| Oturan, yerel veya dizin | Unvan, departman | Ad, e-posta, grup. Dizin kullanıcısında hesap durumu da durur |
| Çalışan | Departman. Yönetici, oturandır | Unvan. Ad, e-posta, grup |
| İzin personel kartı | Yönetici | Sicil, işe giriş, bakiye |

Oturanın yöneticisi üst mevkinin oturan kişisidir. Üst yoksa veya üstün koltuğu boşsa izin kartındaki yönetici boşaltılır. `Yonetim` yedeği aynen kalır. 16 Kasım 2026’da abt şeması yayınlandı.

Personel kartı olmayan kullanıcı yayınlanır. Unvan ve departman yazılır. İzin satırı açılmaz.

Aynı kullanıcı iki koltuğa konmuşsa yayın durur. Ağaç kuralı bunu satır yazılırken de keser.

## Onay

ORG-03, izin yöneticisini kişi alanında bırakır. Şema o kişiyi günceller. Onayla ve reddet düğmesi `Yonetim` grubundadır.

ORG-04, akış geçişine mevki ekler. İzin ve zimmet aynı düzenleyiciyi kullanır. Geçişte mevki seçilmişse karar anında oturan kullanıcı çözülür. Koltuk boşsa üst mevkiye çıkılır. Zincir boşsa geçişteki grup durur. Talep alanı adlandırılmış ama boşsa talep grupta kalır. Yönetici ve platform yöneticisi bu kontrolden önce geçer.

## Dilimler

| Dilim | İş | Durum |
|---|---|---|
| ORG-01 | `org_mevkiler` ve ağaç kuralı | Kuruldu (2026-11-16). Deneme satırları silindi |
| ORG-02 | Ağaç tuvali | Açıldı (2026-11-16) |
| ORG-03 | Yayın: unvan, departman, izin yöneticisi | Açıldı (2026-11-16). Deneme kartları eski haline alındı |
| ORG-04 | Şema unvan ve departmanın sahibi olur. Onay hedefi mevki olur | Açıldı (2026-11-16). Keeper, Operations ve arayüz yenilendi |
| ORG-05 | AD’ye unvan, departman, yönetici yazmak | Yok. 16 Kasım 2026’da kapatıldı |

### ORG-01 — Mevki kaydı

Kayıt açılır, üst bağlanır, kullanıcı oturtulur. Kök tektir. Döngü, ikinci ebeveyn ve aynı kullanıcının ikinci koltuğu reddedilir. Tuval yoktur. Doğrulama satır yazısında biter.

Veri kümesi `org_mevkiler`. Ağaç kuralı şemadaki `tree` bloğundadır. Üst alanı `ust`, oturan alanı `oturan`. Boş üst kök sayılır. Kök satırı `__treeRoot` işaretini taşır ve bu işaret tekildir. Alt mevkisi duran satır silinmez. Kurulum `scripts/setup-abt-org.py`. 16 Kasım 2026’da abt alanında çalıştı. Ağaç bloğu geri geldi. Üstsüz ikinci kök, döngü ve altı duran kaydın silinmesi reddedildi. Deneme satırları silindi.

### ORG-02 — Tuval

Ağaç çizilir. Kutu taşınır, alt mevki açılır, koltuk dolar veya boşalır. Serbest ok yoktur. Kayıt reddederse kutu eski yerinde kalır. Yayın düğmesi bu dilimde kart yazmaz. Taslak durur.

Sayfa `Mng.Ui/pages/apps/organizasyon`. Menü `scripts/setup-abt-org-menu.py` ile AnkaraBT altına yazıldı. 16 Kasım 2026’da kök, alt mevki, taşıma ve kendi altına bağlama denendi. Altı duran mevki silinmedi. Deneme satırları silindi.

### ORG-03 — Yayın

Yayın, oturan her kullanıcıya unvan ve departman yazar. İzin kartı varsa yöneticiyi üst koltuktan yazar. Dizin kullanıcısının kimlik alanlarına dokunmaz. 16 Kasım 2026’da abt alanında denendi. Sonraki yayın `ownProfileByOrg` gönderir; sahipliği ORG-04 keser.

16 Kasım 2026’da abt alanında denendi. Dizin kullanıcısının unvanı mevkiden yazıldı, adı ve grupları durdu. Yerel kullanıcının unvanı ve departmanı yazıldı. İzin kartı olmayan kişi atlandı. Üstü boş kökün yöneticisi boş kaldı. Deneme mevkileri silindi. Kartlar eski unvanına alındı.

### ORG-04 — Sahiplik ve onay mevkisi

Şemanın yayınladığı kartta unvan ve departmanın sahibi şemadır. Keeper kullanıcısında `orgOwnsProfile` durur. Yayın `ownProfileByOrg` gönderir ve bayrağı açar. Bayrak bir daha bu dilimde kapanmaz. Sonraki dizin senkronu unvan ve departmanı değiştirmez. Yalnız bu iki alan yüzünden senkron da planlanmaz. Ad, e-posta, grup ve hesap durumu dizinde kalır. İlk oluşturma bayraksızdır; AD unvanı o anda kopyalanır.

İzin ve zimmet, akış düzenleyicisinde onaylayan mevki seçer. Kayıt `permissions.mevkiId` olur. İsteğe bağlı talep alanı `permissions.mevkiField` adını taşır. Seçim kişi kopyası değildir. Kişi koltuktan inince yeni oturan bakar. Boş koltuk üst mevkiye, o da boşsa geçişteki gruba çıkar. Mevki seçilmemiş geçiş ve alanı boş talep bugünkü grup yolunda kalır.

İzin talebi formunda `izin_onayMevki` durur. Onayla ve reddet bu alanı okur. Alan boşsa grup `Yonetim` kalır. 16 Kasım 2026’da abt alanına yazıldı. Zimmet aynı alanı akış düzenleyicisinden seçer; duraklatılmış zimmet akışına otomatik alan yazılmadı.

16 Kasım 2026’da AbtAppServer üzerindeki Keeper, Operations ve arayüz imajları yenilendi. Üç konteyner sağlıklı kalktı.

### ORG-05 — AD’ye yazma

16 Kasım 2026’da kapatıldı. Unvan ve departman MonitraNG kartında kalır. Ad, e-posta, grup ve hesap durumu dizinde kalır. Yönetici izin personel kartındadır.
