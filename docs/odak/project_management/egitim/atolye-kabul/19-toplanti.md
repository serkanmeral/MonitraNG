# Toplantı

**Toplantı**, projenin olay takvimidir. Sıra şöyledir: olay, gündem, tutanak, aksiyon. Tutanak, kütüphanede **Toplantı notları** klasörüne sayfa olur. Outlook, Teams, davetiye dosyası ve tutanaktan kendiliğinden madde çıkarma yoktur.

Soldaki menüde **Taraflar → Toplantı**.

## Üç görünüm

| Görünüm | Ne işe yarar |
|---|---|
| Takvim | Ay, hafta ve gün. Hafta pazartesi başlar. Görünen aralıktaki olaylar, seri ve anlık karışıktır. Ay ve gün adları uygulama dilini izler. Tarih kutusunun dili tarayıcının dilidir. İkisi ayrıdır. |
| Liste | İki sicil. Üstte **Haftalık seriler**. Satır bir seridir. Tıklayınca o serinin örnekleri sayfalı açılır. Altta **Anlık toplantılar**: serisiz veya seriden kopmuş olanlar. Arama ve tarih penceresi vardır. Pencere varsayılan olarak geriyi ve ileriyi kapsar. |
| Tutanaklar | Sonucun sicilidir. Dosya deposu değildir. **Kayıtlı**: tutanak sayfası olanlar. **Eksik**: yapılmış veya geçmiş, iptal edilmemiş, tutanağı olmayanlar. Satır, düzenlemeyi **Tutanak** sekmesinde açar. **Tutanak yaz**, eksik kuyruğuna gider. |

Takvim boşsa “Henüz toplantı yok. Takvimden bir aralık seçin veya ekleyin” yazar.

İki ekleme düğmesi vardır. **Tek toplantı** bir olay açar. **Haftalık seri** aynı gün ve saatte tekrarlayan şablon açar.

## Olay penceresi

Sekmeler: **Olay**, **Gündem**, **Tutanak**, **Aksiyonlar**. Aksiyonlar, kayıt olduktan sonra dolar. Gündem toplantıdan önce, tutanak sonra yazılır. **Kaydet** bütün sekmeleri birden basar.

| Alan | Ne işe yarar |
|---|---|
| Ad | Toplantının adı. |
| Başlangıç ve bitiş | Tek olayın saati. |
| Konum | Oda veya yer. |
| Bağlantı | Adres metni. Sistem kimseyi çağırmaz. |
| Katılımcılar | Adlar. Giriş hesabı açılmaz. |
| Durum | **Planlandı**, **Yapıldı**, **İptal**. **Yapıldı** işareti tutanak yazmaz. |
| Gündem | Metin yazılırsa **Toplantı notları** klasörüne “ad — Gündem” sayfası olur. Ya da kütüphaneden seçilir. Kimlik yapıştırılmaz. |
| Tutanak | Aynı klasöre “ad — Tutanak” sayfası. Seri, gündem metnini yeni haftaya kopyalayabilir. Tutanak sayfasını kopyalamaz. Her haftanın tutanağı ayrıdır. |
| Not | Serbest not. |

### Haftalık seri

| Alan | Ne işe yarar |
|---|---|
| Gün | Pazartesiden pazara. |
| Saat | Başlangıç saati. |
| Süre (dk) | Örnek: 60. |
| İlk toplantı | Serinin ilk günü. |
| Bitiş tarihi | Seri bu günden sonra örnek üretmez. |
| En fazla 26 örnek | Daha uzağı bu kayıt basmaz. Yapılmış ve iptal örnekler korunur. |

**Yalnız bu**, bir haftanın saatini kaydırırsa o örneği seriden koparır. Diğer haftalar durur. **Bu örneği iptal et**, o haftayı iptal eder. Seri durur.

Seriyi silmek, gelecekteki planlı örnekleri iptal eder. Yapılmış toplantılar ve tutanaklar kalır.

Toplantıyı silmek, toplantıyı ve aksiyonlarını siler. Tutanak belgesi ve OC işleri silinmez.

## Aksiyon

| Alan | Ne işe yarar |
|---|---|
| Aksiyon | Yapılacak cümle. |
| Sorumlu | Ad. |
| Son tarih | Geçerse **Geciken toplantı aksiyonu**. |
| İş kaydı | OC işi. Boşsa **İşsiz toplantı aksiyonu**. |
| Durum | Açık, işleniyor, bitti, feragat. Feragatte not zorunlu. |
| Bitti | Aksiyonu bitti yapar. OC’deki işi kapatmaz. |

Açık aksiyon Durum’da **Açık toplantı aksiyonu** olur. Aksiyonu silmek OC işini silmez.

## Örnek

Seri: her pazartesi 09:00–10:00, ad `Haftalık kabul`, bitiş 16 Aralık 2026.

1. **Haftalık seri** deyin. Günü pazartesi, saati 09:00, süreyi 60, bitişi 16 Aralık yapın.
2. Gündem sekmesine `Tezgâh yerleşimi ve eksikler` yazın. Kaydedin.
3. Takvimde pazartesi günlerini görün. En fazla 26 örnek vardır.
4. Bir pazartesiyi açın. **Tutanak** sekmesine dört satır yazın. Kaydedin.
5. **Tutanaklar → Kayıtlı** altında bu satırı görün.
6. **Aksiyon ekle**: `Eksik topraklama ölçümü`, sorumlu `Ayşe`, son tarih bu cuma. İş bağlamayın.
7. Durum’da işsiz aksiyonun çıktığını görün.
8. Bir sonraki pazartesiyi **Bu örneği iptal et** ile iptal edin. Diğer pazartesiler durur.
9. Deneme serisini silin. Yapılmış tutanak sayfası kütüphanede kalır.

Atölye yürüyüşünün Durum listesini sade tutmak için denemeyi bitirince aksiyonu da silin.

## Ne değildir?

- Dış takvim veya davetiye değildir.
- Kararlar sekmesi değildir. Resmi karar orada yazılır.
- Tutanak dosya deposu değildir. Sayfa kütüphanededir. Sicil burada durur.
- Tutanaktan kendiliğinden aksiyon çıkarmaz.
- **Bitti**, OC işini kapatmaz.

Sıradaki: [Paydaş](./20-paydas.md)
