# Tatil kalıpları ne işe yarar

**Tatil kalıpları**, her yıl tekrar eden tatilin tanımıdır. Hesap bu listeyi okumaz. Okuduğu liste **Tatil günleri**dir. Kalıp, o günleri açmak için durur.

Menü: **Personel İzin → Tanımlar → Tatil kalıpları**.

Dokuz açık satır vardır. Kod düzenlemede değişmez. Kapalı kalıp yeni güne açılmaz.

| Ad | Tür | Tekrar | Tam gün | Arife |
|---|---|---|---|---|
| Yılbaşı | Milli | 1 Ocak | 1 | Yok |
| Ulusal Egemenlik ve Çocuk Bayramı | Milli | 23 Nisan | 1 | Yok |
| Emek ve Dayanışma Günü | Milli | 1 Mayıs | 1 | Yok |
| Atatürk’ü Anma ve Gençlik ve Spor Bayramı | Milli | 19 Mayıs | 1 | Yok |
| Demokrasi ve Milli Birlik Günü | Milli | 15 Temmuz | 1 | Yok |
| Zafer Bayramı | Milli | 30 Ağustos | 1 | Yok |
| Cumhuriyet Bayramı | Milli | 29 Ekim | 1 | Yarım. 28 Ekim |
| Ramazan Bayramı | Dini | Tarihi boş | 3 | Yarım |
| Kurban Bayramı | Dini | Tarihi boş | 4 | Yarım |

## Pencere

Üç blok vardır.

**Kimlik:** kod, ad, durum.

**Tarih:** tür ve tekrar. Tekrar **Sabit tarih** ise ay ve gün görünür. **Tarihi sonra girilir** seçilirse ay ve gün gizlenir.

**Süre:** tam gün sayısı ve arife.

Bu ayrım yerel arayüzdedir. Canlı arayüz imajı yenilenene kadar üretim penceresi bütün alanları bir arada gösterebilir.

## Alanlar

| Alan | Anlamı |
|---|---|
| Kod | Kaydın iç kodu. Düzenlemede değişmez. |
| Ad | Yıl açılınca tatil gününün açıklamasına yazılır. |
| Durum | Kapalı kalıp yeni güne açılmaz. |
| Tür | Milli ya da dini. |
| Tekrar | Sabit tarihte ay ve gün doludur. Dini bayramda tarih boştur. |
| Ay ve gün | İlk tam gün. 29 Ekim için ay 10, gün 29. |
| Tam gün sayısı | İlk tam günden başlayarak kaç gün. Ramazan 3, Kurban 4. |
| Arife | Yarım gün, ilk tam günden önceki güne 0,5 yazar. Öğleden sonrası tatildir. |

## Yılı açmak

Ayarlar sayfasındaki **Tatil yılını aç** düğmesi, sabit tarihli milli kalıpları o yılın günlerine yazar. Her sabah 07:00’de aynı iş çalışır. Aynı tarih duruyorsa üstüne yazılmaz.

Dini bayram bu işe girmez. Ramazan ve Kurban’ın günleri **Tatil günleri** sayfasından elle yazılır. Kalıptaki 3 ve 4, ilk gün girilince kendiliğinden açılmaz.

Tam güne bu iş bugün **1** yazar. Arifeye 0,5 yazar. 1, o günü tam iş günü gibi izinden düşürür. Plandaki tam tatil 0’dır. Bu fark duruyor.

## Ne değildir?

- Gerçekleşen gün değildir. Hesap kalıbı okumaz.
- İdari tatil değildir. Köprü günü **Tatil günleri** sayfasına elle yazılır.
- İzin türü değildir. Tür, talebin kuralıdır. Kalıp, takvimin kuralıdır.
