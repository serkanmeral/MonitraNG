# Bütçe

**Bütçe**, iş paketi zarfıdır. Planlanan tutar ve gerçekleşen tutar burada durur. Fatura, muhasebe fişi ve kur dönüşümü yoktur.

Soldaki menüde **Kontrol → Bütçe**. Hakedişin hemen yanındadır. Hakediş, kabul edilmiş payın talebidir. Bütçe, plan ve gerçekleşen notudur. Hakediş bir satırı besleyebilir. Onun yerine geçmez. Ayrıntı [Hakediş](./09-hakedis.md) sayfasındadır.

## İki tablo

Üstte **Paketler**, altta **Kalemler**. İkisi de aramalı ve sayfalıdır. Paket burada WBS kalemidir. Raftaki iş paketi değildir.

Üst şerit: “n aşım · n paket · n kalem”.

Para birimleri karışıksa “Para birimleri karışık; üst toplam çeviri değildir” yazar. TRY ile USD toplanmaz.

Paket satırı, bir WBS kaleminin plan, gerçekleşen, kalan ve aşım özetidir. Satır açılınca o kalemdeki bütçe satırları görünür.

Kalem satırı: ad, kategori, para birimi, plan, gerçekleşen, kalan, not, WBS.

Kalan, plan eksi gerçekleşendir. Gerçekleşen planı geçerse kalan eksi görünür, **Aşım** çipi yanar ve Durum’da **Bütçe aşımı** çıkar. İş kapanmaz.

## Kalem penceresi

**Kalem ekle** dört karttan başlar.

| Kategori | Ne işe yarar | Ad örneği |
|---|---|---|
| İşçilik | Kendi ekibinizin emeği. Kaynak saatinden tutar çıkmaz. | Planlama işçilik |
| Malzeme | Malzeme, ekipman, sarf. Fatura bu satır değildir. | Saha kablolama |
| Taşeron | Dışarıya verilen iş. Taşeron faturası kütüphanededir. | Kazı taşeronu |
| Diğer | İkram, lisans, yol. Üçüne sığmayan. | Kick-off ikram |

| Alan | Ne işe yarar |
|---|---|
| Ad | Kalemin adı. Aynı WBS, aynı kategori ve aynı ad ikinci kez yazılamaz. |
| WBS | Uyarının yapıştığı kalem. |
| Para birimi | Üç harf. Kur çevrilmez. Boşsa TRY sayılır. Hakedişe bağlayacaksanız sözleşme başlığı ile aynı olmalıdır. |
| Plan | Zarf. |
| Gerçekleşen | Şimdiye kadar yazdığınız tutar. Faturadan kendiliğinden gelmez. Hakediş dilimi bu satıra bağlıysa gerçekleşen, kabul ve ödendi netlerinin toplamıyla ezilir. |
| Not | Serbest açıklama. |

Pencere kalanı canlı gösterir. Aşımda “Aşım — Durum’da sayılır” yazar. Zarf içindeyse “Zarf içinde” yazar.

Silmek kalemi siler. WBS ve işler durur.

## Örnek

Atölye kabulüne yazmayın. Durum listesi bütçe aşımı göstermesin. Kendi zarfınız için:

Kalem: `Saha kablolama`  
Kategori: Malzeme  
WBS: 1.1 Kontrol listesi  
Plan: 80.000 TRY  
Gerçekleşen: 95.000 TRY  

Kalan eksi 15.000 görünür. Aşım çipi yanar. Durum’da **Bütçe aşımı** çıkar. Bu bir muhasebe fişi değildir. Tedarikçi faturasını kütüphaneye yükleyip kanıt diye bağlayabilirsiniz. Bütçe satırı yine elle güncellenir.

**Örnek PMO** projesinde iki kalem hazırdır. Onlara dokunmayın:

| Kalem | Plan | Gerçekleşen |
|---|---|---|
| Saha işçilik | 80.000 TRY | 32.000 TRY |
| Vitrin ve mobilya | 400.000 TRY | 125.000 TRY |

Hakediş dilimini bu satırlara bağlarsanız gerçekleşen, dilimlerin kabul netiyle değişir. Eğitimde bağ **Yok** kalsın.

## Adımlar

1. Atölye kabulünde **Bütçe** sekmesini açın. “Henüz bütçe kalemi yok” doğrudur.
2. **Örnek PMO** açın. İki kalemin plan ve gerçekleşenini yukarıdaki tabloyla doğrulayın.
3. Bir kalemi düzenleyip gerçekleşeni değiştirmeyin. **Vazgeç** deyin.
4. Kendi projenizde malzeme kartıyla 80.000 / 95.000 örneğini kurun. Aşım çipini görün. Sonra kalemi silin veya gerçekleşeni zarfın içine çekin.

## Ne değildir?

- Fatura uygulaması değildir.
- Otomatik kur çevirmez. USD ve TRY ayrı kalemler olabilir.
- Kaynak saatinden maliyet üretmez.
- Hakediş cetveli değildir. Cetvel ayrı sekmededir.

Sıradaki: [Okundu](./16-okundu.md)
