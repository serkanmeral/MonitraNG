# Dashboard

**Dashboard**, projenin bakışıdır. Kart ve grafik burada durur. Satır satır tarama **Durum** sekmesindedir. Bir grafiğe tıklayınca ilgili sekmeye gidersiniz. Dashboard, o sekmenin yerine geçmez.

Soldaki menüde **Plan → Dashboard**.

Üstteki cümle: proje kompozisyonu. Grafikler ilgili sekmeye gider.

## Kartlar

| Kart | Ne işe yarar |
|---|---|
| Sağlık | **Yolunda**, **İzle** veya **Müdahale**. Uyarıların özetidir. Açık kapı ve bağsız iş varken müdahale veya izle görünebilir. Kart işi kapatmaz. |
| İlerleme | WBS yüzdesinin özeti. Kırılım yokken boştur. Özet kalem, altındakilerden toplar. |
| Sıradaki kapı | Açık kapılardan sıradaki. Atölye kabulünde **Saha kabul** burada durur. Açık kapı yoksa “Açık kapı yok” yazar. |
| Kalan gün | Plan bitişe göre “n gün kaldı”, “bitiş bugün” veya “n gün gecikti”. Plan bitiş yoksa “Plan bitiş tarihi yok” yazar. |
| Paket | “n paket kurulu”. Bu sette 0’dır. Paket kurmadık. |
| Ayrıntı için Durum | Sizi Durum listesine götürür. |

## Listeler

| Liste | Boşken ne yazar | Doluysa |
|---|---|---|
| Bugün bakılacaklar | Acil kalem yok. | Bugün bakmanız gereken uyarılar. |
| Yakın kilometre taşları | Önümüzdeki dört haftada kilometre taşı yok. | Elmaslar. Atölye kabulünde 1.2, 24 Ekim bu pencereye girer veya girmez. Bugün dört haftadan uzaksa liste boştur. Bu bir hata değildir. |
| Açık kararlar | Açık karar yok. | Durumu açık olan kararlar. Kabul edilmiş karar burada durmaz. |

## Grafikler

| Grafik | Ne işe yarar |
|---|---|
| WBS dağılımı | Kalemleri **Bitti**, **Devam**, **Geciken**, **İş bağsız**, **Başlamadı** diye böler. Atölye kabulünde 1.1 iş bağsız görünür. Veri yoksa “Grafik için veri yok” yazar. |
| Kapılar | Açık, geçti, red, feragat dağılımı. Bu sette bir açık kapı vardır. |
| Açık RAID | Risk, varsayım, sorun, dış bağımlılık. Bu sette kayıt yoksa grafik boştur. |
| Bütçe | Plan ve gerçekleşen. Satır yoksa “Bütçe satırı yok” yazar. Para birimleri karışıksa bu grafik çeviri yapmaz. Ayrıntı Bütçe sayfasındadır. |

## Örnekte ne görürsünüz?

Atölye kabulünü 01–08 sayfalarıyla kurduysanız:

- Sıradaki kapı **Saha kabul**’dür.
- WBS dağılımında 1.1 **İş bağsız** tarafındadır.
- Bütçe grafiği boştur. Bu sette kalem yazmadık.
- RAID grafiği boştur.
- **Ayrıntı için Durum** aynı açık kapıyı ve bağsız işi satır satır gösterir.

## Adımlar

1. **Dashboard** sekmesini açın.
2. Sıradaki kapının Saha kabul olduğunu doğrulayın.
3. WBS dağılımında iş bağsız dilimini bulun.
4. **Ayrıntı için Durum** ile listeye geçin. Karttaki sayının listedeki satırla aynı işi anlattığını görün.
5. Boş bütçe ve boş RAID grafiğini hata sanmayın. O sekmelerde henüz kayıt yoktur.

## Ne değildir?

- Durum listesinin kopyası değildir. Bakış buradadır. Satır Durum’dadır.
- Grafik, tarihi veya kapıyı kendisi düzeltmez.
- Portföy ekranı değildir. Birden çok projenin özeti, proje listesindeki Tümü / Aktif / Dikkat çubuğundadır. Kaynak dengeleme ve çapraz proje zamanlaması yoktur.

Sıradaki: [Paketler](./12-paketler.md)
