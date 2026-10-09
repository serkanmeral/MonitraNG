# Paketler

**Paket**, bu proje için hazır bir iş kırılımı, belge klasörü ve gerekirse ince bir Operasyon Merkezi iskeleti kurar. Uygulama mağazası değildir. İmzasız üçüncü taraf paket kurulmaz. Raftakiler birinci taraftır ve doğrulanmıştır.

Soldaki menüde **Teslimat → Paketler**.

Atölye kabulü yürüyüşünde paket kurmayın. Ağacı elle kurduk ki WBS’in ne olduğunu görün. Bu sayfa, kendi işinizde boş projeye iskelet basmak içindir.

## Ne zaman kullanılır?

Boş bir projeyi elle bölmek yerine “kabul / kalite / PMO” gibi bilinen bir iskelet istediğinizde. Aynı projeye ikinci paket eklenir. WBS numarası kaldığı yerden devam eder. Yanlış paketi **Sök** ile geri alırsınız. Dolu klasör ve paylaşılan isimler kalır.

Yeni proje penceresindeki **İş paketi** aynı rafı açar. Orada seçip kaydederseniz kurulum, proje açılır açılmaz başlar.

## Rafta ne vardır?

Aynı motor, farklı içerik.

| Paket | Ne iskeleti |
|---|---|
| PMO | Plan, tutanak, karar, durum, teslimat |
| Kalite | Prosedür, form, kayıt, revizyon |
| Kabul | Saha ve teslim kabul kırılımı |
| Teklif | Teklif hazırlığı |
| Mimari | Mimari teslimat ve diyagram klasörü |
| ECO / ECN | Mühendislik değişiklik emri ve bildirimi |
| Onboarding | Karşılama, yetkinlik, göreve hazır |

## Ekranda ne vardır?

Üç liste: **Kurulmadı**, **Kurulu**, **Sürüm geride**.

| Parça | Ne işe yarar |
|---|---|
| Birinci taraf | Paket bu ürünün kendi rafındandır. |
| Doğrulandı | İmza kontrolünden geçmiş. Doğrulanmamış paket kurulmaz. |
| Kurulu sürüm | Bu projede basılı olan sürüm. Raf yeniyse satır **Sürüm geride** listesine düşer. |
| Klasörler | Paketin kütüphanede açacağı klasör adları. |
| Türler | WBS türleri. |
| Önizle | Bu projedeki mevcut kırılıma göre ne olacağını gösterir. Henüz yazmaz. |
| Kur | İlk kurulum. |
| Eksikleri tamamla | Kurulu pakette atlanmış parçaları basar. |
| Güncelle | Sürüm gerideyse yeniler. |
| Sök | Önizlemeli geri alma. |

Önizleme yalan söylemez. Zaten duran kalem **Atla** olur. Eksik olan **Oluştur** olur. Değişmesi gereken **Güncelle** olur. Özet satır şöyledir: “Oluştur: n · atla: n · güncelle: n”.

Workspace yoksa önizleme “Workspace oluşturulacak: …” der. Workspace zaten bağlıysa “Workspace zaten bağlı veya mevcut” der.

Kurulum sırasında kalıcı bir ilerleme penceresi açılır. Yüzde çubuğu yoktur. Pencere iş bitmeden kapanmaz. Adımlar şunlardır:

| Adım | Ne basılır |
|---|---|
| WBS, workspace ve işler | Kırılım, gerekirse çalışma alanı, özet üst iş ve yaprak iş kayıtları. Ayrıntı: oluştur, atla, güncelle sayıları. |
| Proje kütüphanesi | Klasör kökü. Kök yoksa “Kütüphane kökü bulunamadı; klasörler atlandı.” |
| Klasör | Paketin klasörü. |
| Sayfa | Başlangıç sayfası. |
| Diyagram | Varsa boş draw.io. Örnek: `Onboarding akışı.drawio`. Dosya zaten varsa tekrar kur atlar. Paket örnek akış çizmez. |
| Kütüphane bağlanıyor | Klasörler projeye yapışır. |

Bitince başlık “kuruldu” olur. Olmazsa “kurulamadı”. **Kapat** ancak iş bitince anlamlıdır.

**Sök** önizlemesi ayrıdır. “Silinecek: n · korunacak: n”. Klasörler için “Boş klasör silinecek · dolu veya paylaşılan kalacak”. İşler için “Kullanılmamış iş sil · ilerleme varsa kalır”.

## Kurulum ne üretir, ne üretmez?

Üretir:

- WBS kalemleri
- Kütüphane altında paket klasörleri, başlangıç sayfaları ve varsa boş çizim
- Workspace yoksa ince OC iskeleti: durumlar, kural, pano
- Özet üst iş ve yaprak iş kayıtları

Üretmez:

- **SLA basılmaz.** Proje işi ticket kuyruğu değildir. Sekiz saat ve kırk saat sayacı helpdesk ve zimmet alanlarındadır. Proje süresi WBS ve Gantt tarihleridir. İş açıldığı anda SLA başlamaz. Sonradan politika eklenebilir. Paket bunu yapmaz.
- Kanıt bağlamaz. Yapraklara kanıt bağlamak sizin işinizdir.
- Mevcut WBS’i sessizce ezmez. Önizlemede atlanır.
- Wiki, Kararlar, Yüklemeler ve Toplantı notlarını paket malı sanmaz. Onlar proje kütüphanesinin varsayılan klasörleridir. Sök onları silmez.

Sökme şunları silmez: workspace, kural, pano. Kullanılmamış (açık, ilerlemesiz) iş kayıtları silinebilir. İlerleme varsa WBS ve iş kalır. Boş WBS kalemleri ve boş klasörler silinir. Dolu klasör ve paylaşılan isimler kalır.

Yeni işin anahtarı proje kodunu izler. Kod `ATOLYE` ise anahtar o kodla başlar.

## Örnek

Yeni bir proje açtınız, workspace boş, ad “Saha kabulü”. Atölye kabulünün kendisine bunu yapmayın.

1. **Paketler** sekmesinde **Kabul** paketini **Önizle**.
2. “Workspace oluşturulacak” satırını okuyun. Oluştur / atla / güncelle sayılarını okuyun.
3. **Kur** deyin. İlerleme listesinin bitmesini bekleyin.
4. WBS’te kabul kırılımı, Kütüphane’de klasörler, OC’de işler oluşur.
5. Yapraklara kanıt bağlamak hâlâ sizdedir.

Yanlış paketi kurduysanız sökme önizlemesine bakın. Dolu **Kararlar** klasörü silinmez.

## Ne değildir?

- Kütüphanenin yerine geçmez.
- Otomatik kanıt bağlama yapmaz.
- Diyagram editörü değildir. Tuval boştur. Düzenlemek Kütüphane veya Süreç sekmesindendir.
- OC SLA politikası basmaz.

Sıradaki: [RAID](./13-raid.md)
