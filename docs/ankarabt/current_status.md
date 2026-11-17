# AnkaraBT — son durum

## Son Çalışılan Konu

Mng.Ui karşılama mesajı. Giriş yapmış kullanıcı, tarihi gelmiş mesajı uygulama kabuğunda modal olarak görür. PDKS kapı cümleleri bu işe girmedi.

## Tamamlanan İşler

- Dataset `ui_karsilama_mesajlari`, form `ui-karsilama-mesajlari-form` ve AnkaraBT menüsü **Karşılama mesajları** AbtAppServer üzerindeki abt alanına kuruldu.
- Kayıtta başlık, HTML, görsel, başlangıç, bitiş, sıra ve birden fazla grup vardır. Grup boşsa mesaj herkese gider.
- Aynı güne denk gelen mesajlar sıra numarasına göre ardışık açılır. Numara yoksa başlangıç günü, o da yoksa oluşturulma zamanı sırayı belirler.
- Modal her başarılı girişte açılır. Sayfa yenilemesi yeni giriş sayılmaz.
- HTML ekrana basılmadan önce ayıklanır. Görsel alanından yalnız resim basılır.
- Yazma `admins`, `managers`, `Yonetim` ve `idari isler` gruplarındadır. Okuma her giriş yapmış kullanıcıya açıktır.
- İlk mesaj yazıldı. Başlık MonitraNG'ye hoş geldiniz. Aralık 17 Kasım 2026 ile 31 Mart 2027'dir. Hedef herkestir. Yol Operasyon Merkezi, Help Desk ve Monitra Yardım olarak durur.
- Kurulum `docs/ankarabt/dev/ui/scripts/setup-abt-karsilama.py` ile yinelenir.

## Devam Eden İşler

- Ofisteki yayınlanmış Mng.Ui imajı bu kabuğu henüz taşımaz. Modal, ofis ağ geçidine bağlı yerel arayüzde denendi.
- PDKS Android yayını bir Play Console hesabına kadar durur. Bildirim şirket kararına bırakıldı.

## Sonraki Adımlar

1. Karşılama kabuğunu ofisteki Mng.Ui imajına almak.
2. Play Console hesabı bulununca Android yayınına dönmek.

## Önemli Notlar

- Tanım dataset ve dinamik formdadır. Modal kabuktadır.
- Bitiş, başlangıçtan önceyse mesaj açılmaz. HTML ve görselin ikisi de boşsa mesaj açılmaz.
- Grup alanı grup adını saklar. Karşılaştırma girişteki grup adıyla yapılır.
- Kapı şeridindeki karşılama cümleleri `pdks_ayarlar` satırında durur. Bu modülle karıştırılmaz.

## Son Güncelleme

17 Kasım 2026, akşam. Karşılama mesajı kuruldu, ilk mesaj yazıldı ve yerel girişte görüldü.

## Nerede Kalmıştık

Karşılama kaydı ve modal bitti. Sıradaki yayın adımı, aynı kabuğu ofisteki Mng.Ui imajına almaktır. PDKS mağaza yayını hesap bekler.
