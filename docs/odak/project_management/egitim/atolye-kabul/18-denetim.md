# Denetim

**Denetim paketi**, müşteri veya denetçi için kanıt belgelerini bir araya getirir. ZIP indirme, e-posta ve sihirbaz yoktur. Belgeler silinmez. Paket, hangi kayıtların bir arada olduğunu tutar.

Soldaki menüde **Kontrol → Denetim**.

Kütüphanenin yerine geçmez. Paydaş portalı değildir. Paydaş, “kim hangi belgeye bakacak” kaydıdır. Denetim, “bu set resmi kanıt setidir” kaydıdır.

## Liste

Liste boşsa “Henüz denetim paketi yok” yazar. Filtre ve sayfalama vardır. **Paket ekle** pencereyi açar.

| Kolon | Ne işe yarar |
|---|---|
| Ad | Paketin adı. |
| Tür | Denetim, müşteri veya iç. |
| Durum | Taslak, hazır, teslim veya geri çekildi. |
| Kanıt | Seçilen belge sayısı. Yoksa eksik sayılır. |
| Son tarih | Geçerse geciken paket olur. |
| Alıcı | Bilgi alanıdır. Sistem e-posta atmaz. |

Eksik kanıt, açık paket ve gecikme Durum’a yansır. Kapatma kilidi değildir.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Ad | Örnek: `ISO saha denetimi Ekim`. |
| Tür | **Denetim**: dış denetçi veya ISO seti. **Müşteri**: müşteriye gösterilecek set. **İç**: kurum içi kontrol. |
| Kanıtlar | **Kütüphaneden seç**. Birden fazla belge eklenir. Kimlik yapıştırılmaz. Çipin çarpısı belgeyi paketten çıkarır. Belgenin kendisi durur. |
| Son tarih | Boşsa gecikme sayılmaz. |
| Alıcı | Denetçinin adı. Bilgi. Posta gitmez. |
| WBS | Boşsa paket proje düzeyidir. |
| Durum | **Taslak**: toplanıyor. **Hazır**: set tamam, henüz teslim işareti yok. **Teslim**: en az bir belge gerekir. **Geri çekildi**: not zorunlu. |
| Not | Geri çekmekte zorunlu. |

**Teslim et**, durumu teslime çeker. Belge yoksa olmaz.

Silmek paketi siler. Kanıt belgelerinin kendisi silinmez.

## Örnek

Paket: `ISO saha denetimi Ekim`  
Tür: Denetim  
Kanıtlar: şartname özeti, yangın butonu tutanağı, güvenlik talimatı  
Alıcı: dış denetçinin adı

Hazır olunca **Teslim et**. Denetçiye dosyaları kütüphaneden veya kendi yolunuzla gösterirsiniz. Bu sekme “hangi set resmi” kaydıdır. Dosyayı postalamaz.

Kanıt seçilmeden kayıt açılırsa Durum’da **Eksik denetim paketi** çıkar. Son tarih geçerse **Geciken denetim paketi**. Teslim edilmemiş paket **Açık denetim paketi** olur.

## Adımlar

1. Kanıtları önce kütüphanede toplayın.
2. **Denetim → Paket ekle**. Ad ve türü yazın. Üç belgeyi seçin. Alıcıya ad yazın.
3. Durumu hazır bırakıp kaydedin. Teslim için en az bir belgenin durduğunu görün.
4. **Teslim et** deyin.
5. Denemeyi silin. Belgelerin kütüphanede kaldığını doğrulayın.

## Ne değildir?

- Kütüphane yerine geçmez.
- Otomatik ZIP üretmez.
- Paydaş kaydı değildir.
- E-posta göndermez.

Sıradaki: [Toplantı](./19-toplanti.md)
