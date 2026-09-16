# Yükümlülük

**Yükümlülük**, şartname maddesi + son tarih + iş + kanıt zinciridir. Otomatik madde çıkarımı ve sözleşme motoru yoktur. Maddeleri siz yazarsınız.

## Ekranda ne vardır?

Kayıt tablosu filtreli ve sayfalıdır. **Yükümlülük ekle** modalında kaynak ve kanıt **kütüphaneden seçilir**; iş **OC iş seçicisinden** (arama + sayfa) bağlanır. Kimlik yapıştırılmaz.

Madde no, yükümlülük metni, kaynak belge, bağlı iş, kanıt belge, son tarih, durum (açık / işleniyor / karşılandı / feragat).

**Karşılandı** için kanıt gerekir. Feragat not ister. İş bağsız ve gecikmiş kayıtlar Durum’a yansır; kapatma kilidi değildir.

## Örnek

Madde: `ŞRT-12.3 Yangın butonu 30 m’de bir`  
Kaynak: Kütüphane’deki şartname özeti  
İş: `SEEDPMO-0009` (3.2 Teslimatlar)  
Kanıt: yangın butonu tutanak  
Son tarih: 15 Ekim  
Durum: Açık → kanıt bağlanınca Karşılandı

İş bağlanmazsa Durum’da **İş bağsız**. Kanıt yoksa açık yükümlülük kalır.

## Karar ve kapı ile fark

Yükümlülük sözleşmeden gelen “yapılacak”. Karar “nasıl sapacağız”. Kapı “bu noktadan geçmeden kapatma”. Üçü aynı teslimatta yan yana durabilir.

## Ne değildir?

- NLP ile maddeleri PDF’den çıkarmaz
- Fatura/ödeme takibi değildir
- Silmek belgeyi ve OC işini silmez

## Sonraki adım

Kanıt dosyası **Kütüphane**, iş **WBS / OC**. Durum’daki yükümlülük bayrakları buradan gelir.
