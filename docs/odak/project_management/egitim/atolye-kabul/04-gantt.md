# Gantt

**Gantt**, planı takvimde gösterir. Görev bir çubuktur. Kilometre taşı bir elmastır. Çubuğun yeri, kalemin plan başlangıç ve bitiş tarihidir.

Soldaki menüde **Plan → Gantt**.

Grafik kendi kendine ilerlemez. Yüzde, WBS kalemindeki ilerlemeden gelir. Tarihi çubuğu sürükleyerek değiştirmeyin. Yanlış gündeyse tarihi **WBS** kaleminden düzeltin. Gantt o tarihi çizer.

## Ekranda ne vardır?

| Parça | Ne işe yarar |
|---|---|
| Gün | Takvimi gün gün gösterir. Kısa işlerde bunu kullanın. Atölye kabulü üç haftadır; gün ölçeği satırları seçer. |
| Hafta | Aynı çubukları hafta hafta toplar. Uzun projede günler sığmayınca buna geçin. |
| Tarih yok | Plan tarihi boş kalemler burada toplanır. Çubukları yoktur. Tarihi WBS’ten yazınca çubuğa dönerler. |
| FS | Bitiş–başlangıç oku. “Önceleyen bitmeden izleyen başlamasın.” Oku bu sekmede görürsünüz. Oku yazmak **Bağımlılıklar (FS)** sekmesindedir. |
| Gerçekleşen | İşin gerçekten başladığı ve bittiği aralık. Plan çubuğunun yanında durur. Planı değiştirmez. |
| WBS kalemi | Yeni kırılım kalemi açar. Tür ve tarih sorusu WBS ile aynıdır. Kalem burada da, WBS sekmesinde de aynı kayıttır. |

Özet kalemin kendi çubuğu yoktur. Altındakilerin tarihini toplar ve grup olarak durur. Altı boşsa grup da tarihsiz kalır.

İki kalem arasında FS varsa ve izleyenin plan başlangıcı, önceleyenin bitişinden artı bekleme gününden önceyse, bağımlılık satırında **Plan tarihleri FS ile uyuşmuyor** uyarısı çıkar. Gantt tarihi kendisi düzeltmez. Siz WBS tarihini veya bekleme gününü değiştirirsiniz.

## Örnekte ne görürsünüz?

Kırılımı henüz yazmadıysanız grafik boştur. [İş kırılımı](./05-is-kirilimi.md) sayfasını bitirince geri dönün. O zaman şunları görürsünüz:

- **1 Saha hazırlığı** grup olarak durur. Özet kalemin kendi çubuğu yoktur.
- **1.1 Kontrol listesi** 6 Ekim’den 17 Ekim’e uzanan bir çubuktur.
- **1.2 Saha kabul** 24 Ekim’de bir elmastır. Süresi yoktur. O gün teslim anıdır.

Bu sette FS oku yoktur. 1.1 bitmeden 1.2 olmasın derseniz oku [Bağımlılıklar](./10-bagimlilik.md) sayfasında kurarsınız. Atölye yürüyüşünde oku kurmuyoruz. Elmas ve çubuk yeter.

## Adımlar

1. **Gantt** sekmesini açın. Boşsa iş kırılımı sayfasına geçin.
2. Kırılımı kurduktan sonra buraya dönün.
3. **Gün** ölçeğinde 1.1 çubuğunun 6–17 Ekim, 1.2 elmasının 24 Ekim olduğunu doğrulayın.
4. Çubuk yanlış gündeyse **WBS** kalemini açıp plan tarihini düzeltin. Gantt’a geri gelin.
5. Yeni kalem gerekiyorsa **WBS kalemi** burada da durur. Bu sette üçüncü kalem eklemeyin.

## Ne değildir?

- İş kuyruğu değildir. Kimin üzerinde açık iş olduğu Operasyon Merkezi’ndedir.
- Bağımlılık oku bu sayfada yazılmaz. Ok, **Bağımlılıklar (FS)** sekmesindedir. Gantt onu çizer.
- Kaynak histogramı değildir. Saat **Kaynak** sekmesindedir.
- Grafik yüzdeyi kendisi üretmez.

Sıradaki: [İş kırılımı](./05-is-kirilimi.md)
