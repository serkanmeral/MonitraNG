# Kütüphane

**Kütüphane**, bu projeye ait resmi kayıtların klasörüdür. Sayfa, belge ve dosya burada durur. Proje sayfasından çıkmadan açılır.

Soldaki menüde **Teslimat → Kütüphane**.

Üstteki cümle şunu söyler: klasör içeriği karar veya kanıt kaydına kendiliğinden bağlanmaz. Bağ, ayrıca **Bağla** ile kurulur.

## Ekranda ne vardır?

Kök, **Proje klasörü**’dür. İçinde arama kutusu vardır: **Bu klasörde ara**. Eşleşme yoksa “Bu aramada kayıt yok” yazar. Klasör boşsa “Bu klasör boş” yazar.

Tablonun kolonları:

| Kolon | Ne işe yarar |
|---|---|
| Ad | Sayfanın, dosyanın veya klasörün adı. Satıra tıklayınca kayıt açılır. |
| Tür | Klasör, sayfa, dosya veya çizim. |
| Etiketler | Varsa etiket. Yoksa çizgi durur. |
| Durum | Yayında, taslak gibi belge durumu. Bu, proje kartındaki taslak değildir. |
| Güncelleme | Son yazıldığı an. |

**Document Intelligence'da aç**, aynı klasörü belge modülünün tam ekranında açar. Kayıt iki yerde ayrı kopya değildir. Kök klasör bulunamazsa ekran, Dökümanlar alanının önce kurulması gerektiğini söyler. Kurulu bir projede bu çıkmaz.

Sağdaki düğmeler:

| Düğme | Ne işe yarar |
|---|---|
| Dosya yükle | Bilgisayardan bir dosya koyar. Koymak, onu göreve kanıt yapmaz. |
| Yeni | Menü açar. Altında üç grup vardır. |
| Yeni → Yeni klasör | Bu klasörün altında klasör açar. |
| Yeni → Yeni sayfa | Metin sayfası açar. Atölye kabulünün kontrol listesi budur. |
| Yeni → Yeni çizim | Boş bir draw.io tuvali açar. Çizmek, süreci resmi yapmaz. Resmi kayıt [Süreç](./21-surec.md) sayfasındadır. |
| Yeni → şablondan | Antetli belge üretir. Bu sette kullanmayın. Antet Belge Tasarımcısı’ndadır. |
| Bağla | Seçili belgeyi bir işe kanıt, bir işe plan veya bir karara resmi kayıt diye bağlar. |

### Bağla penceresi

Üç bağ vardır. Üçü de klasöre bırakmanın yerine geçmez.

| Bağ | Ne işe yarar |
|---|---|
| Kanıt | Belge, seçilen WBS kaleminin OC işine kanıt olur. Durum’daki **Kanıt yok** bayrağı kalkar. Yaprak kalemde iş kaydı yoksa “Önce iş bağlayın” der. |
| Plan | Belge, işe plan veya kaynak diye bağlanır. Durum’daki **Plan yok** bayrağı kalkar. Yine iş kaydı gerekir. |
| Karar | Belge, bir karar kaydının resmi belgesi olur. Klasördeki dosya tek başına karar değildir. Karar kaydı yoksa “Karar kaydı yok” der. **Yeni karar oluştur** ile başlık yazıp kayıt açabilirsiniz. |

Bu sette 1.1’e iş bağlamıyoruz. **Bağla** demeyin. Sayfa klasörde durur. Göreve kanıt olmaz. Durum sayfasındaki **Kanıt yok** bunu söyler.

Wiki, Kararlar, Yüklemeler ve Toplantı notları proje açılınca duran varsayılan klasörlerdir. Paket raftan gelmez. Karar yazınca metin **Kararlar** klasörüne sayfa olur. Tutanak, **Toplantı notları** klasörüne yazılır.

## Örnekte ne görürsünüz?

Proje klasörünün içinde, sizin eklediğiniz sayfa: **Kontrol listesi**.

Sayfanın metni kısadır:

```text
Tezgâh yerleşimi tamam.
Enerji ve topraklama ölçüldü.
Koruyucu ekipman yerinde.
Eksik madde varsa kabul yapılmaz.
```

Sonraki sayfada karar yazılınca **Kararlar** klasöründe bir sayfa daha oluşur. Onu siz Yeni sayfa ile açmazsınız. Karar kaydı açar.

## Adımlar

1. **Kütüphane** sekmesini açın. Kök, bu projenin klasörüdür.
2. **Yeni → Yeni sayfa** ile `Kontrol listesi` sayfasını yazın. Metin yukarıdaki dört satırdır.
3. Sayfayı kaydedin. Liste satırında adı, türü ve güncelleme zamanı görünür.
4. **Bağla** demeyin.
5. **Document Intelligence'da aç** ile aynı klasörün tam ekranda durduğunu görün. Sonra proje sayfasına dönün.

## Ne değildir?

- Belge Tasarımcısı değildir. Antet ve şablon orada üretilir. Kabul listesi bir sayfadır.
- Klasöre bırakmak karar kaydı açmaz. Karar, **Kararlar** sekmesindedir.
- Sayfayı silmek WBS kalemini silmez.
- Dosya yüklemek kanıt sayılmaz. Kanıt, işe **Bağla → Kanıt** ile yapışır.

Sıradaki: [Karar ve kapı](./07-karar-ve-kapi.md)
