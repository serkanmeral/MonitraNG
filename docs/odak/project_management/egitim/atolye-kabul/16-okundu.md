# Okundu

**Okundu**, resmi bir belge için kişi bazında “okudum, anladım” kaydıdır. Sınav, eğitim sistemi ve nitelikli e-imza yoktur. Belge revizyonu değişince yeni kayıt açılır. Eski satır yeni revizyonu kapsamaz.

Soldaki menüde **Kontrol → Okundu**.

Üstteki cümle: portal ve e-posta yoktur. Belgeyi kütüphaneden seçersiniz. Kişi adını yazarsınız. **Okudum** damgasını siz basarsınız. Dış kişi Monitra’ya girmez.

## Liste

Liste boşsa “Henüz okundu kaydı yok” yazar. Arama ve **Tümü** süzgeci vardır. Üst şerit “n bekleyen · n gecikmiş” der.

| Kolon | Ne işe yarar |
|---|---|
| Belge | Kütüphaneden seçilen sayfa veya dosya. Kimlik elle yazılmaz. |
| Revizyon | Örneğin `v3`. Belge seçilince dolabilir. Revizyon değişince yeni satır gerekir. Aynı belge, aynı revizyon ve aynı kişi ikinci kez yazılamaz. |
| Kişi | İç çalışan veya kurum dışı ad. Giriş hesabı değildir. |
| Son tarih | Damga için son gün. Geçerse ve durum bekliyorsa **Geciken okundu** olur. |
| WBS | Boşsa kayıt proje düzeyidir. Durum teslimata yapışmaz. Bağlarsanız o kalemin satırında görünür. |
| Durum | Bekliyor, okundu veya feragat. |

**Okudum**, bekleyen satırı okundu yapar. Damgayı dış kişi basmaz.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Belge | **Kütüphaneden seç**. Seçilmeden “Henüz belge seçilmedi” yazar. |
| Revizyon | `v3` gibi. İpucu: belge seçince dolabilir. Yeni revizyon yeni satır ister. |
| Kişi | Örnek yer tutucu: Ayşe veya müşteri saha müdürü. |
| Son tarih | Boşsa gecikme sayılmaz. Dolu ve geçmişse kayıt “son tarih geçmiş” uyarısı verir. |
| WBS | İsteğe bağlı. |
| Durum | **Bekliyor**: henüz damgalanmadı. **Okundu**: damga basıldı. **Feragat**: okumayacak veya kapsam dışı. Feragatte not zorunludur. |
| Not | Feragatte zorunlu. Diğerlerinde isteğe bağlı. |

Silmek kaydı siler. Belge ve WBS durur.

## Örnek

Güvenlik talimatı v3 yayınlandı. İki kayıt, son tarih cuma.

| Kişi | Revizyon | Ne olur |
|---|---|---|
| Ayşe | v3 | **Okudum** basılır. Durum okundu olur. |
| Mehmet | v3 | Cuma geçer, damga yoktur. Durum’da **Geciken okundu** çıkar. |

Talimat v4 olursa Ayşe’nin v3 satırı yetmez. v4 için yeni satır açılır.

Atölye kabulündeki kontrol listesi sayfasına da aynı şekilde kayıt açabilirsiniz. Yürüyüşü bozmamak için bu setin Durum listesine okundu satırı eklemeyin. Denemeyi kendi kopyanızda yapın veya kaydı silin.

## Adımlar

1. Kütüphane’de belge yoksa önce sayfayı yazın. Kayıt, olmayan belgeyi seçemez.
2. **Okundu → Kayıt ekle**.
3. Kontrol listesini seçin. Revizyon `v1`. Kişi `Ayşe`. Son tarih bu haftanın cuma günü.
4. Durumu bekliyor bırakıp kaydedin.
5. Satırda **Okudum** deyin.
6. Aynı belge ve `v1` ile Ayşe’yi ikinci kez kaydetmeyin. Kayıt reddedilir.
7. Deneme satırını silin.

## Ne değildir?

- Eğitim sistemi veya sınav değildir.
- Belgenin onay döngüsü değildir. O, belgenin kendi durumudur.
- Kapı değildir. Kapı kriterine “ilgili okundu tamam” diye elle tik atabilirsiniz. Okundu sekmesi kapıyı geçirmez.

Sıradaki: [Yükümlülük](./17-yukumluluk.md)
