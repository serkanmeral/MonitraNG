# Hak bantları ne işe yarar

**Hak bantları**, yıllık iznin kaç gün yazılacağını tutar. Kıdem aralığı günü belirler. Yaş tabanı, genç ve ileri yaşta bu günün belli bir sayının altına düşmesini engeller.

Menü: **Personel İzin → Tanımlar → Hak bantları**.

Her satır bir kuraldır. Dört satır vardır. Yeni satır eklenebilir. Kapalı satır hesaba girmez.

| Kod | Tip | Aralık | Gün |
|---|---|---|---|
| kidem_1_5 | Kıdem | 1–5 yıl, ikisi de dahil | 14 |
| kidem_6_14 | Kıdem | 6–14 yıl, ikisi de dahil | 20 |
| kidem_15 | Kıdem | 15 yıl ve üzeri | 26 |
| yas_tabani | Yaş tabanı | 18 yaşından küçük veya 50 yaş ve üzeri | En az 20 |

Kıdem, işe girişten hakkın yazıldığı güne kadar tamamlanan yıldır. 5 yıl 11 ay, 5 yıldır. 6. yıldönümünde 6 olur. 5 yıl 14 gündür. 6 yıl 20 gündür. 14 yıl hâlâ 20 gündür. 15. yıldönümünde 26 olur.

Bir yılı doldurmayan kişiye hakediş yazılmaz. Bant satırı bunu değiştirmez.

Doğum tarihi boşsa yaş tabanı uygulanmaz. Hak, yalnız kıdem bandındaki gündür.

## Pencere

Satırı açınca önce **Bant** bloğu gelir: kod, tip, durum. Tip seçilince ikinci blok açılır.

Kıdem seçilince **Kıdem** bloğu gelir: alt yıl, üst yıl, gün. Yaş alanları görünmez.

Yaş tabanı seçilince **Yaş tabanı** bloğu gelir: alt yaş, üst yaş, minimum gün. Yıl ve gün alanları görünmez.

## Bant

| Alan | Anlamı |
|---|---|
| Kod | Kaydın iç kodu. Düzenlemede değişmez. `kidem_1_5` gibi durur. |
| Tip | **Kıdem**, hakediş gününü yazar. **Yaş tabanı**, bu günün minimum günün altına düşmesini engeller. |
| Durum | **Açık** satır hesaba girer. **Kapalı** satır yok sayılır. Silmek yerine kapatmak, eski hareketleri bozmaz. |

## Kıdem

Alt yıl ve üst yıl dahildir. Üst yıl boşsa bant üstten açıktır. `kidem_15` böyledir: 15 ve sonrası 26 gündür.

Hesap, kişinin tamamlanan yılına uyan ilk açık kıdem satırını alır. Aralıklar birbirinin üstüne binmemelidir. 5 ile 6 arasında boşluk da bırakılmamalıdır. Boşlukta kalan yılın kıdem günü 0 sayılır.

| Tamamlanan yıl | Gün |
|---|---|
| 1, 2, 3, 4, 5 | 14 |
| 6, 7, 8, 9, 10, 11, 12, 13, 14 | 20 |
| 15 ve sonrası | 26 |

## Yaş tabanı

Yaş da hakkın yazıldığı günde tamamlanan yıldır. İşe girişteki yaş değildir.

| Alan | AnkaraBT | Anlamı |
|---|---|---|
| Alt yaş | 18 | Bu yaştan küçük olan, minimum günün altına düşemez. |
| Üst yaş | 50 | Bu yaş ve üzeri, minimum günün altına düşemez. 49 yaş tabana girmez. 50 girer. |
| Minimum gün | 20 | Bant günü bundan küçükse hak 20’ye çıkar. Bant 20 veya 26 ise ek gün yazılmaz. |

Tek yaş tabanı satırı okunur. İkinci satır eklemek hesabı iki tabana bölmez.

Örnek: kıdem 2 yıl, bant 14, yaş 50. Hak 20 olur. Fark, bant farkı satırı olarak yazılır. Kıdem 16 yıl, bant 26, yaş 52. Hak 26 kalır. Taban ek gün yazmaz.

## Hak ne zaman yazılır

Yıldönümünde dönem açılır. O günkü bant ve yaş tabanı, hakediş olarak tam yazılır. Aynı kişi, aynı dönem ve aynı hareket ikinci kez yazılmaz.

Yılın içinde bant veya yaş tabanı yükselirse yalnız fark yazılır. Eski hakediş silinmez. Düşüş eski satırı silmez. Aynı kişi, aynı dönem ve aynı hedef gün ikinci kez yazılmaz. Hedef gün, ulaşılan haktır: `20` veya `26`.

Açılış satırı bu toplama girmez. Dosyadan gelen hakediş girer. Bu yüzden aktarımda, kıdemi bir yıl ve üzeri olan kişiye hem hakediş hem açılış yazıldı. Sabah işi hakedişi görünce aynı yılı bir daha eklemez.

Çıkış tarihi dolu olan veya durumu ayrıldı olan kişiye yeni hakediş ve bant farkı yazılmaz.

29 Şubat’ta işe giren kişide, artık olmayan yılda yıldönümü bir sonraki güne kayabilir.

## Ne değildir?

- Ayar kaydı değildir. Dönem, devir ve gün karşılığı **Ayarlar** sekmesindedir.
- İzin türü değildir. Yıllık izin dışındaki türler bu banttan gün almaz.
- Bakiye değildir. Bakiye, hakediş, devir, bant farkı, bloke ve kullanım satırlarının toplamıdır.
- Dosyadaki birikmiş kalan değildir. Kalan, açılış satırındadır. Bant, bundan sonraki hakkı yazar.
