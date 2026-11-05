# İzin türleri ne işe yarar

**İzin türleri**, bir talebin hangi kurallarla açılacağını tutar. Hak bantları yalnız yıllık iznin kaç gün yazılacağını belirler. Bu sayfa türün kendisini tanımlar.

Menü: **Personel İzin → Tanımlar → İzin türleri**.

Her satır bir türdür. Yeni tür eklenebilir. Kapalı tür talep listesinde görünmez. Kapalı türle yeni talep de açılmaz.

## Açık türler

| Sıra | Ad | Kim | Süre | Tek talep | Dönem | Bakiye | Belge | Yarım gün |
|---|---|---|---|---|---|---|---|---|
| 1 | Yıllık izin | Herkes | İş günü | Yok | Yok | Düşer | Hayır | Evet |
| 2 | Evlilik | Herkes | İş günü | 3 | Yok | Düşmez | Hayır | Hayır |
| 3 | Ölüm | Herkes | İş günü | 3 | Yok | Düşmez | Hayır | Hayır |
| 4 | Rapor | Herkes | İş günü | Yok | Yok | Düşmez | Evet | Hayır |
| 5 | Ücretsiz | Herkes | İş günü | Yok | 30 | Düşmez | Hayır | Evet |
| 6 | Doğum | Kadın | Takvim haftası | 112 veya 126 | Yok | Düşmez | Evet | Hayır |
| 7 | Babalık | Erkek | İş günü | 5 | Yok | Düşmez | Hayır | Hayır |

Evlilik ve ölüm kendi türüdür. Talep neden sormaz. Ölümde yakınlık alanı yoktur.

Eski **Mazeret** türü kapalıdır. Eski mazeret nedenleri de kapalıdır. Yeni talep bu kayıtlardan açılmaz.

Ücretsizin 30 günü başlangıç değeridir. Yasal tavan değildir. Tür kaydından değiştirilir.

## Pencere

Satırı açınca üç blok gelir.

**Kimlik:** kod, ad, sıra, durum, kim açar.

**Süre:** süre tipi. İş gününde gün tavanı görünür, takvim günleri gizlenir. Takvim haftasında tek ve çoğul takvim günü görünür, gün tavanı gizlenir. Dönem limiti her iki tipte de durur.

**Kural:** bakiyeden düşer, belge zorunlu, yarım gün.

Bu ayrım yerel arayüzdedir. Canlı arayüz imajı yenilenene kadar üretim penceresi bütün alanları bir arada gösterebilir. Alan adları ve seçenekler her iki yerde de Türkçedir.

## Kimlik

| Alan | Anlamı |
|---|---|
| Kod | Kaydın iç kodu. Düzenlemede değişmez. |
| Ad | Talepte ve listede görünen ad. |
| Sıra | Listedeki sıra. Küçük sayı üstte durur. |
| Durum | **Açık** talepte görünür. **Kapalı** görünmez. Silmek yerine kapatmak eski talebi bozmaz. |
| Kim açar | **Herkes**, **Kadın** veya **Erkek**. Cinsiyeti boş olan kişi yalnız Herkes türlerini görür. Uymayan talep durur. |

Cinsiyet personel kartındadır. Kadın, doğumu ve herkes türlerini görür. Erkek, babalığı ve herkes türlerini görür.

## Süre

**İş günü**, ayarlardaki hafta karşılığını kullanır. Hafta içi 1, Cumartesi 0,5, Pazar 0. Aynı tarihte tatil günü varsa onun karşılığı haftanın yerine geçer.

**Takvim haftası** bu tabloyu kullanmaz. Hafta sonu da süreye dahildir. Doğum böyledir.

| Alan | Anlamı |
|---|---|
| Gün tavanı | Tek talebin üstüdür. Talep daha kısa olabilir. Boşsa tek talep sınırı yoktur. Evlilik 3, ölüm 3, babalık 5. |
| Dönem limiti | Aynı kişinin, aynı yıldönümü döneminde, aynı türden kullanabildiği en çok gündür. Boşsa dönem limiti yoktur. |
| Takvim günü | Tek doğum. Başlangıç ve bitiş dahildir. Boşsa 112 sayılır. |
| Çoğul takvim günü | Çoğul doğum. Başlangıç ve bitiş dahildir. Boşsa 126 sayılır. |

Gün tavanı ile dönem limiti ayrıdır. Evlilikte bir talep 3 günü geçemez. Yılda kaç evlilik olacağı sayılmaz. Ücretsizde tek talebin tavanı yoktur. Dönemde toplam 30 günü geçemez.

Dönem limiti açık talebi de sayar. Onaylı kullanım sayılır. Red ve iptal günü geri verir. Limit değişince eski satır silinmez. Sonraki talep yeni sayıyı okur.

Yıllık iznin dönem limiti boştur. Onun sınırı hakediş defteridir. Bakiye eksiye düşebilir.

Doğumun bitiş tarihi, tek doğumda başlangıca 111 gün, çoğulda 125 gün eklenerek yazılır. 112 ve 126, başlangıç ve bitiş dahil bu aralığa denk gelir. Takvim günü alanını değiştirirseniz düşen gün değişir. Otomatik bitiş tarihi bu sayıyı okumaz.

## Kural

| Alan | Anlamı |
|---|---|
| Bakiyeden düşer | Açıksa talep bakiyeden düşer. Yalnız yıllık izinde açıktır. Diğer türler satır yazar, bakiyeyi değiştirmez. Sonradan kutuyu değiştirmek eski satırları düzeltmez. |
| Belge zorunlu | Açıksa belge alanı dolmadan talep açılmaz. Belgenin içeriği denetlenmez. Alanın dolu olması yeter. Rapor ve doğumda açıktır. |
| Yarım gün | Açıksa tek günde öğleden önce veya sonra seçilebilir. Yalnız yıllık izin ve ücretsizde açıktır. Diğer türde yarım gün seçilirse talep durur. |

## Ne değildir?

- Hak bandı değildir. Yıllık iznin 14, 20 ve 26 günü **Hak bantları** sayfasındadır.
- Bakiye değildir. Bakiye, hakediş, devir, bant farkı, bloke ve kullanım satırlarının toplamıdır.
- Tatil takvimi değildir. İş gününün kaça sayılacağı **Ayarlar** ve **Tatil günleri** sayfalarındadır.
- Mazeret nedeni listesi değildir. Evlilik ve ölüm artık türdür. Eski neden satırları kapalıdır.
