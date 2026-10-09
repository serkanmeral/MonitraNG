# Yükümlülük

**Yükümlülük**, şartname maddesi, son tarih, iş ve kanıt zinciridir. Maddeleri siz yazarsınız. Belgeden kendiliğinden madde çıkarılmaz. Sözleşme motoru yoktur.

Soldaki menüde **Kontrol → Yükümlülük**.

Üç kayıt yan yana durabilir. Yükümlülük sözleşmeden gelen “yapılacak”tır. Karar “nasıl sapacağız”dır. Kapı “bu noktadan geçmeden kapatma”dır.

## Liste

Liste boşsa “Henüz yükümlülük yok” yazar. Filtre ve sayfalama vardır. **Kayıt ekle** pencereyi açar.

| Kolon | Ne işe yarar |
|---|---|
| Madde | Madde numarası. Örnek: `ŞRT-12.3`. |
| Yükümlülük | Yapılacak işin cümlesi. |
| Kaynak belge | Şartname veya sözleşmenin geçtiği kütüphane belgesi. |
| İş | OC iş kaydı. Boşsa Durum’da **İşsiz yükümlülük** olur. |
| Kanıt | Karşılandı demek için gereken belge. |
| Son tarih | Geçerse **Geciken yükümlülük** olur. |
| Durum | Açık, işleniyor, karşılandı veya feragat. |

Bu satırlar kapatma kilidi değildir. Uyarıdır.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Madde | Numara. Siz yazarsınız. |
| Yükümlülük | Cümle. Örnek: `Yangın butonu 30 m’de bir`. |
| Kaynak belge | **Kütüphaneden seç**. Şartname özeti. Seçilmezse “Kaynak belge seçilmedi” yazar. |
| Kanıt | Yine kütüphaneden. **Karşılandı** için zorunludur. Yoksa “Kanıt belge seçilmedi” yazar. |
| İş kaydı | Bu projenin workspace’indeki iş. Arama anahtar veya başlıkla yapılır. Boşsa “İş kaydı seçilmedi” yazar. **İşi kaldır** bağı çözer, OC işini silmez. |
| Son tarih | Boşsa gecikme sayılmaz. Geçmişse “son tarih geçmiş” uyarısı verir. |
| WBS | Boşsa kayıt proje düzeyidir. |
| Durum | **Açık**: henüz karşılanmadı. **İşleniyor**: iş başladı, kanıt bekleniyor. **Karşılandı**: kanıt bağlı, kapatıldı. **Feragat**: kapsam dışı, not zorunlu. |
| Not | Feragatte zorunlu. |

Kimlik yapıştırılmaz. Belge kütüphaneden, iş iş seçiciden gelir.

Silmek yükümlülüğü siler. Belge, iş ve WBS durur.

## Örnek

| Alan | Değer |
|---|---|
| Madde | ŞRT-12.3 |
| Yükümlülük | Yangın butonu 30 m’de bir |
| Kaynak | Kütüphanedeki şartname özeti |
| İş | Teslimat kalemine bağlı iş. Atölye kabulünde iş yoktur. Bu örneği işi olan bir projede kurun. |
| Kanıt | Yangın butonu tutanağı |
| Son tarih | 15 Ekim |
| Durum | Açık. Kanıt bağlanınca karşılandı. |

İş bağlanmazsa Durum’da **İşsiz yükümlülük**. Kanıt yokken karşılandı seçilemez. Son tarih geçerse **Geciken yükümlülük**.

## Adımlar

1. Kaynak belgeyi önce kütüphaneye koyun.
2. **Yükümlülük → Kayıt ekle**. Madde ve cümleyi yazın. Kaynak belgeyi seçin.
3. Durumu açık bırakın. Kanıtı boş bırakın. Kaydedin.
4. Durum sekmesinde açık yükümlülüğün göründüğünü doğrulayın.
5. Kanıtı seçip durumu **Karşılandı** yapın. Kanıtsız karşılandı kaydı olmaz.
6. Denemeyi silin. Atölye kabulünün uyarı listesine bu satırı bırakmayın.

## Ne değildir?

- Maddeleri PDF’den çıkarmaz.
- Fatura veya ödeme takibi değildir. O, hakediştir.
- Silmek belgeyi ve OC işini silmez.
- Kapı değildir. Kapı kriterine maddeyi elle yazabilirsiniz. Yükümlülük kapıyı geçirmez.

Sıradaki: [Denetim](./18-denetim.md)
