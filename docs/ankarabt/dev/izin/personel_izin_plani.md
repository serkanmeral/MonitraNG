# Personel izin yönetimi — plan

Son güncelleme: 4 Kasım 2026.

Durum: ilk değerler kilitlendi. Hesap, dataset üzerindeki ön tanımlı sorgudadır. Onay zinciri ve dosyadan kart ile geçmiş bu turda yok.

Bağlam ve sunucu erişimi: [bulgular.md](./bulgular.md). Üzerinde çalışılacak sunucu AbtAppServer (`192.168.1.249`).

## Ne bu iş

Personelin izin hakkı ve izin talebi. Hak bir hareket defteridir. Talep, Operation Core çalışma alanıdır. Kart, sözlük ve ayar dinamik formdadır. Gün sayısı, bant, devir, çakışma ve bakiye ön tanımlı sorgudadır. MngOperations izin hesabı taşımaz. Sorguyu geçişe ve satır yazmaya bağlayan genel yetenek yeter.

Tam özlük bu işin ön koşulu değildir. Ücret, aile, adres ve sözleşme kartta yoktur. İş akışı motoru bu hesabı çalıştırmaz. Geçiş, istek bitmeden sorgunun sonucunu kullanır.

## Bu turun dışında

| İş | Ne zaman |
|---|---|
| Onay zinciri | Çalışma alanı kurulurken çizilir. Sonradan çalışma alanı ayarından değiştirilir. Üzerinde iş bulunan durum silinmez |
| Personel kartı ve bu yılın izin geçmişi | Dosyadan birlikte elle işlenir. Toplu aktarım ekranı yoktur. Keeper bağı da bu iştedir |
| Görsel durum akışı | Bu iş onu beklemez. Akış tanım listesinden kurulur |

## Ayar kaydı

Tek satır. İK düzenler.

| Ayar | İlk değer |
|---|---|
| Dönem | İşe giriş yıldönümü. Takvim yılına çekilebilir |
| Devir | Sınırsız. Tavan günü ve süre alanları durur, bu değerde kullanılmaz |
| Hak aşımı | Serbest. Bakiye eksiye düşebilir. Uyarı veya tam kesmeye çekilebilir |
| Hak yazımı | Yıl içinde de. Yaş veya kıdem bandı yükselince fark satırı yazılır |
| Dönem açılışı | Kendiliğinden. İK düğmesi de aynı sırayı çalıştırır |
| Gün karşılığı | Hafta içi 1, Cumartesi 0,5, Pazar 0. Her gün 0, 0,5 veya 1 olabilir |
| Talep | Personel kendi talebini, yönetici ekibini, İK herkesin talebini açar |
| Yönetici boşsa | `Yonetim` |
| Çıkış tarihi | Doluysa yeni talep ve yeni hakediş kesilir |

## Personel kartı

Dinamik form. Keeper kullanıcısı login kaydıdır. Kıdem bu karttadır.

Sicil tekildir. Ad, soyad, işe giriş, doğum tarihi, cinsiyet (`kadın` veya `erkek`, boş kalabilir), birim, yönetici, Keeper kişisi, çıkış tarihi, durum (çalışıyor veya ayrıldı).

Yönetici, Keeper kişisidir. Boşsa onay `Yonetim` grubuna düşer. İlk doldurma dosya işindedir.

## İzin türü

| Tür | Kim açar | Süre | Bakiye | Belge | Yarım gün |
|---|---|---|---|---|---|
| Yıllık izin | Herkes | İş günü | Düşer | Hayır | Evet |
| Mazeret | Herkes | Nedenin tavanı | Düşmez | Hayır | Hayır |
| Rapor | Herkes | İş günü, tam gün | Düşmez | Evet | Hayır |
| Ücretsiz | Herkes | İş günü | Düşmez | Hayır | Evet |
| Doğum | Kadın | 16 veya 18 takvim haftası | Düşmez | Evet | Hayır |
| Babalık | Erkek | En çok 5 iş günü | Düşmez | Hayır | Hayır |

Tür kaydında kod, ad, bakiyeden düşer, belge zorunlu, yarım gün, kim açar (`herkes`, `kadın`, `erkek`), süre tipi (`isGunu` veya `takvimHaftasi`), takvim günü, çoğul takvim günü, gün tavanı, sıra ve durum vardır. Babalık tavanı türdedir. Mazeret tavanı nedendedir. Kapalı tür yeni talepte görünmez. Cinsiyet boşsa yalnız `herkes` türleri görünür.

Mazeret nedeni ayrı formdur. Ad ve gün tavanı tutar. İlk satırlar evlilik 3, ölüm 3. Ölümde yakınlık alanı yoktur. Yeni neden bu formdan eklenir. Talep tavanın altında kalabilir. Üstüne çıkarsa durur.

Doğum tek taleptir. Çoğul değilse 16 hafta (112 takvim günü), çoğulsa 18 hafta (126 takvim günü). Bitiş başlangıçtan yazılır. İş günü tablosu kullanılmaz. Cumartesi ve Pazar iznin içindedir. Kullanım raporuna takvim günü yazılır.

## Hak bantları

Kıdem, işe girişten dönemin açıldığı güne kadardır. Bir yılı doldurmayan kişiye hakediş yazılmaz.

| Kıdem | Gün |
|---|---|
| 1 yıldan 5 yıla kadar, 5 dahil | 14 |
| 5 yıldan fazla, 15 yıldan az | 20 |
| 15 yıl ve üzeri | 26 |

Yaş tabanı ayrı satırdır. 18 yaşından küçük veya 50 yaşından büyük personelde gün 20’den az olamaz. Bant zaten 20 veya 26 ise taban ek gün yazmaz. Yıl içinde bant veya taban yükselirse aradaki fark bant farkı satırı olarak yazılır. Düşüş eski satırı silmez.

Kıdem satırında alt ve üst yıl dahildir. Kayıtlar 1–5, 6–14 ve 15 ve üzeridir. `$dateDiff` tamamlanan yılı verir. 5 yıl 14 gündür. 6. yıldönümünde 20 olur.

## Resmi tatil

İki dataset. Gün hesabı yalnız gerçekleşen günü okur.

Tatil kalıbı tekrar eden tanımdır. Tür `milli` veya `dini`. Tekrar `sabit_gun` veya `yillik`. Sabit günde ay ve gün doludur. Süre, tam gün sayısıdır. Arife yok veya yarım gündür. Yarım gün, başlangıçtan önceki güne 0,5 yazar ve öğleden sonrayı tatil sayar.

Milli kalıplar: 1 Ocak, 23 Nisan, 1 Mayıs, 19 Mayıs, 15 Temmuz, 30 Ağustos, 29 Ekim. 28 Ekim, Cumhuriyet Bayramı’nın arife yarım günüdür. Dini kalıplarda ay ve gün yoktur. Ramazan üç tam gün, Kurban dört tam gündür. İkisinin arifesi yarım gündür.

Tatil günü tek tarihtir. Tür `milli`, `dini` veya `idari`. Gün karşılığı 1 veya 0,5. Aynı tarih ikinci kez yazılmaz. İdari günün kalıbı yoktur. Uzatma, yanındaki boş günlere yazılır. Milli veya dini gün yerinde kalır.

Yılı aç, sabit milli kalıpları o yılın tarihlerine yazar. Aynı yıl tekrar açılırsa duran satırın üstüne yazmaz. Dini bayramda İK ilk tam günü girer. Süre ve arife oradan açılır. Otomatik takvim kaynağı yoktur.

## Gün hesabı

Başlangıç ve bitiş aralığa dahildir. O tarihte tatil günü varsa haftanın karşılığı değil, tatil satırı geçerlidir.

| Gün | Düşen |
|---|---|
| Hafta içi | 1 |
| Cumartesi | 0,5 |
| Pazar | 0 |
| Tam tatil | 0 |
| Arife | 0,5. Tam gün izin sabahı düşer |

Yarım gün talep tek gündür. Yıllık izin ve ücretsizde açılır. Öğleden önce, günün karşılığı ile 0,5 içinden küçük olanı düşer. Hafta içi sabah 0,5, Cumartesi ve arife sabahı 0,5 olur. Öğleden sonra, karşılık 0,5 veya daha küçükse 0, değilse 0,5 düşer. Mazeret, rapor, doğum ve babalıkta yarım gün yoktur.

Doğumda bu tablo kullanılmaz. Süre takvim haftasıdır.

## Hareket defteri

Satır güncellenmez. Yeni olay yeni satır yazar. Gün işaretlidir. Artı hak yazar, eksi gün düşer. Yarım gün 0,5 olur.

| Satır | Ne zaman | Bakiye |
|---|---|---|
| Hakediş | Yıldönümünde | Artı. O günkü bant ve yaş tabanı |
| Bant farkı | Yıl içinde bant yükselince | Artı. Yalnız fark |
| Devir | Yeni dönem açılırken | Önceki kalan, eksi de olsa |
| Bloke | Yıllık talep düşünce | Eksi |
| Kullanım | Yıllık talebin son onayı | Bloke kadar iade artısı ve kullanım eksisi. Net düşüş aynı kalır |
| İade | Red veya iptal | Bloke ya da kullanım kadar artı |
| Açılış | Dosyadan | Bu yılın kullanılmış ve kalan günü |

Mazeret, rapor, ücretsiz, doğum ve babalık da satır yazar. Bakiyeye etki, yazıldığı andaki tür ayarıdır. Bu türlerde etkisizdir. Dönem kullanımında görünür, yıllık bakiyeyi değiştirmez.

Alanlar: personel, tür, neden, hareket, gün, dönem başlangıcı, başlangıç, bitiş, yarım gün, bakiyeye etki, kaynak (sistem, talep, dosya), iş kodu, geçiş, hedef bant. Başlangıç ve bitiş çakışma sorgusu içindir. Hakediş, devir, bant farkı ve açılışta boş kalabilir. Hedef bant, bant farkında ulaşılan gündür (`20`, `26`). Aynı iş ve aynı geçiş ikinci satırı yazmaz. Aynı kişi, aynı dönem ve aynı sebeple ikinci hakediş veya ikinci devir yazılmaz. İkinci yükseliş yeni hedef bantla yazılır.

Bakiye, kişinin dönemindeki bakiyeye etki eden günlerin toplamıdır. Açık bloke bu toplamın içindedir. Toplam eksi olabilir. Okuma, hareket dataset’indeki ön tanımlı sorgudur.

## Yıldönümü açılışı

Aynı kişi ve aynı dönem için bir kez çalışır. Düğme ve kendiliğinden açılış aynı sırayı kullanır.

1. Önceki dönemin bakiyesi yeni döneme devir olarak yazılır.
2. O günkü kıdem ve yaş tabanından hakediş yazılır. Bir yıl dolmamışsa hakediş yazılmaz. Devir yine yazılır.
3. Özet bu hareketlerden yeniden hesaplanır.

Yıl içindeki bant yükselişi bu açılışın dışındadır.

## Talep

Operation Core iş kaydıdır. Onay adımları kurulurken çizilir.

Personel, izni kullanacak kişidir. Açan başka biri olabilir. Tür, kartın cinsiyetine göre süzülür. Başlangıç ve bitiş vardır. Yarım günde gün tektir ve seçim öğleden önce veya öğleden sonradır. Düşen gün elle yazılmaz. Açıklama serbesttir. Belge, kütüphane kimliğidir. Tür zorunlu diyorsa geçiş bunu bekler. Doğrudan dosya alanı açılmaz.

Yerine bakacak kişi personel kartından seçilir. Zorunlu değildir. İzni kullanan kişi ve çıkış tarihi dolu olan kişi seçilmez. Gün hesabına ve bakiyeye girmez.

Mazerette neden zorunludur. Doğumda çoğul işaretlenirse 18 hafta, değilse 16. Bitiş haftadan gelir.

Çakışma: aynı personelde açık bloke veya onaylı kullanım o günü tutar. Yeni talep durur. Red ve iptal tutmaz. Öğleden önce ile öğleden sonra çakışmaz. Türler günü paylaşır. Hak aşımının serbest olması çakışmayı açmaz.

Bloke, yıllık talep düşünce yazılır. Son onay, bloke günü kadar iade artısı ve kullanım eksisi yazar. Red ve iptal yalnız iade artısı yazar.

## Bakiye özeti

Her personel, her dönem ve her tür için bir satır. Hareket yazılınca yeniden hesaplanır. Elle düzeltilmez.

Hakediş, devir, bloke, kullanım ve bakiye. Bakiye yalnız yıllık izinde doludur. Diğer türlerde kullanım dolu, bakiye boştur.

## Rapor ve belge

| Rapor | Ne süzer |
|---|---|
| Kişi bakiyesi | Yıllık izin özeti. Personel, birim, dönem |
| Dönem kullanımı | Türlerin kullanım alanı. Personel, tür, dönem |

Onay bekleyenler çalışma alanının panosudur. Ayrı rapor değildir.

İzin formu ilk sürümde, onay geçişinde üretilir ve kütüphaneye yazılır. Gövde Belge Zekâsı şablonudur. Sonraki belgeler yeni şablonu kullanır. Üretilmiş dosya kütüphanede durur ve oradan düzenlenir. Üretim onay geçişine bağlı kalır.

## Ekranlar

| Yer | Ekran |
|---|---|
| Dinamik form | Personel kartı, ayarlar, hak bantları, izin türleri, mazeret nedenleri, tatil kalıpları, tatil günleri. Tanım [datasets/izin_automated_forms.json](datasets/izin_automated_forms.json) |
| Operasyon Merkezi | İzin talepleri. Form, pano ve onay |
| Rapor | Kişi bakiyesi, dönem kullanımı |
| Belge Zekâsı | İzin formu ve talebin belge bağı |
| Ayarlar | Dönem açılış, bant farkı, tatil yılı ve özet düğmeleri ayar formundadır. Kayıtlı aksiyonu çalıştırır |

Alt iş, sprint ve çalışma alanı takvimi bu işte yoktur. Takvim sekmesi izin tarihini göstermez.

## Hesabın yeri

Ayrı bir izin servisi yoktur. MngOperations içinde izin adlı bir hesap yoktur. Formül, ilgili dataset’in ön tanımlı sorgusudur. Sorgu ayarı, kartı, bantı, türü, nedeni, haftanın karşılığını ve tatil gününü okur. Sonuç döner. Satır yazma sorgunun içine gömülmez.

Bakiye okuma bu sorgunun toplamıdır. Kişi, tür ve dönem parametredir. Açık bloke toplama dahildir. Liste filtresi bu sorguyu çalıştırmaz.

İki genel bağ yeter. İkisi de izin kelimesi taşımaz. Başka çalışma alanları aynı bağı kullanır.

| Bağ | Ne yapar | İzin |
|---|---|---|
| Geçiş, sorguyu çalıştırır | Dönen sayı alan olur. Dönen adet sıfır değilse geçiş durur | Düşen gün. Çakışma. Gün tavanı. Bloke, kullanım ve iade bu sayıyla yazılır |
| Sorgunun satırları dataset’e yazılır | Düğme ve zamanlanmış çağrı aynı aksiyonu çalıştırır. `atla` duran satıra dokunmaz. `yenile` üstüne yazar | Yıldönümünde devir ve hakediş. Bant farkı. Tatil yılını açma. Özet satırını yenileme |

Geçiş bağı [OC-14](../../development_roadmap.md) olarak 4 Kasım 2026’da kural penceresine alındı. Sorgu adımı koşuldan öncedir. Parametre iş kaydından okunur. Sonuç seçilen alanın üstüne yazılır. Koşul bu alanla geçişi durdurur.

Yıldönümü açılışı, bant farkı, tatil yılı ve özet satırı [DG-01](../../development_roadmap.md) olarak 4 Kasım 2026’da DataGateway’e alındı. `POST /api/v1/data/{dataset}/actions/{actionName}` kayıtlı aksiyonu çalıştırır. Dönem açılışı, bant farkı ve tatil yılı `atla` yazar. Özet `yenile` yazar. Ayar formundaki düğmeler aynı adresi çağırır.

## Sorgular

Formül bu adlandırılmış sorgulardadır. OC-14 tek satır döndürenleri geçişte çalıştırır. DG-01 satır döndürenleri hedefe yazar. Dataset ve sorgu metni [datasets/izin_datasets.json](datasets/izin_datasets.json) içindedir. Parametre yer tutucusu `:ad` biçimindedir. Boş sonuç, `bakiye` ve `cakisma` için sıfır sayılır.

| Sorgu | Bağ | Parametre | Döner |
|---|---|---|---|
| `dusen_gun` | OC-14, `izin_ayarlar` | `baslangic`, `bitis`, `yarimGun`, `turId`, `cogul` | `gun`. Türün süresi takvim haftasıysa 112 veya 126. Değilse iş günü |
| `cakisma` | OC-14, `izin_hareketleri` | `personelId`, `baslangic`, `bitis`, `yarimGun`, `isKodu` | `adet`. Net günü eksi kalan bloke ve kullanım. Karşıt yarım günler çakışmaz |
| `tavan` | OC-14, `izin_turleri` | `turId`, `nedenId`, `gun` | `tavan`, `asildi`. Nedenin tavanı varsa o, yoksa türün tavanı. Tavan boşsa aşılmadı |
| `bakiye` | Okuma, `izin_hareketleri` | `personelId`, `donemBaslangic`, `turId` | `bakiye`. Bakiyeye etki eden günlerin toplamı |
| `donem_acilis` | DG-01, atla, `izin_personel` | `tarih` | O gün yıldönümü olan kişilerin devir ve hakediş satırları. Anahtar kişi, dönem, hareket |
| `bant_farki` | DG-01, atla, `izin_personel` | `tarih` | Açık dönemde hakkı yükselen kişilerin fark satırları. Anahtar kişi, dönem, hedef bant |
| `tatil_yili` | DG-01, atla, `izin_tatil_kaliplari` | `yil` | Sabit milli kalıpların o yılki günleri ve arife yarım günü. Dini bayram gelmez |
| `ozet` | DG-01, yenile, `izin_hareketleri` | — | Personel, dönem ve tür başına hakediş, devir, bloke, kullanım, bakiye |

Geçiş `dusen_gun` sonucunu düşen gün alanına yazar. `cakisma.adet` sıfır değilse durur. `tavan.asildi` doluysa durur. Bloke, kullanım ve iade bu günle yazılır. Satırda başlangıç, bitiş ve yarım gün de durur. Tarihler gün başı UTC’dir. 29 Şubat giriş, artık olmayan yılda bir sonraki güne kayabilir. Ayar satırı yoksa `dusen_gun` boş döner.

İlk sözlük ve ayar satırları [datasets/izin_seed.json](datasets/izin_seed.json) içindedir. Personel, hareket, özet ve gerçekleşen tatil günü dosya ve düğmelerle dolar.
