# AnkaraBT — son durum

## Son Çalışılan Konu

PDKS mobil uygulamasının herkese açık Android yayını. Play Console hesabı olmadığı için bu iş durduruldu. Karar `docs/ankarabt/dev/pdks/pdks_plani.md` bölüm 14.8 ve 14.9’dadır.

## Tamamlanan İşler

- Kapı ekranı canlıda: kod, karşılama şeridi, saat ve iş özeti vitrini.
- Vitrin dört kart, üç grafik ve çalışma alanı tablosudur. Yenileme süresi Saatler formundaki `panoYenilemeSaniye` alanındadır. Varsayılan 60 saniyedir. Sınır 15–600.
- Merkez lokasyonunun panosu `kapi-is-ozeti` dir.
- Şifre tutması, devam grubu `Abt Calisan` ve karşılama cümleleri çalışır. Cümle telefonda da görünür.
- MBL-01 kabuk, MBL-02 telefon anahtarı ve İK onayı, MBL-03 modül listesi, MBL-04 geçiş ve MBL-05 ön kamera Expo Go ile denendi.
- Kabul edilen giriş veya çıkıştan üç saniye sonra uygulama ana sayfaya döner.
- Aynı gün kabul edilmiş bir giriş ve ondan sonraki bir çıkış günü kapatır. Yeni okutma satır yazmaz.
- Geçiş karesi doksan gün sonra silinir. Silme MngExtensions içindeki süpürgededir. MinIO yaşam kuralı ve MngScheduler kullanılmaz.
- Güncelleme yolu kilitlendi. Yeni sürüm Play ve App Store’dan iner. Anahtar, onay ve sunucu adresi durur.

## Devam Eden İşler

- Yayın (MBL-07) bir Play Console hesabına kadar durur.
- Bildirim (MBL-06) şirket kararına bırakıldı. Geçiş buna bağlı değildir.
- Tarayıcıdaki şifreli geçiş duruyor.
- Geçiş satırını `abt_admin` yazıyor. Ayrı teknik kullanıcı seçilmedi.
- Oturum, otuz gün dolmadan düşebiliyor. Düşünce şifre bir kez sorulur. Anahtar ve onay durur. Silme kaydı yoktur.

## Sonraki Adımlar

1. Play Console hesabı bulununca Android herkese açık yayına dön. Şirket hesabında paket incelemeye girer. 13 Kasım 2023’ten sonra açılmış kişisel hesapta önce 12 kişi, 14 gün kapalı test gerekir.
2. İlk paket Android 16’yı (API 36) hedefler. Kurulum kimliği `com.monitrang.mobile` ilk yüklemede kilitlenir.
3. Mağaza sayfası, herkese açık gizlilik metni ve incelemeci için ayrı bir deneme hesabı hazırlanır.
4. iPhone yayını Android hesabından sonra ayrıca konuşulur.

## Önemli Notlar

- Node 22 var. Expo genel komutu yok. `npx expo` yeter. Java, Android Studio ve Android SDK yok. iOS bu bilgisayarda derlenmez. Mağaza paketi bulut derlemesi ister.
- Kurulum kimliği `com.monitrang.mobile`. Ekran adı MonitraNG. Paket adı `monitrang`.
- Gizli anahtar telefondan çıkmaz. Açık anahtar ve onay Keeper’daki personel kaydındadır. IMEI ve telefon numarası kimlik değildir.
- Yüz tanıma ve parmak izi yoktur. Kare kimlik doğrulamaz. İK, personel kartından ister. Kapalıysa ön kamera açılmaz.
- Uygulama anahtarı değiştiremez. Kaldırma ve yeniden üretim İK ekranındadır. Personel kendi telefonunu onaylayamaz.
- Sunucu adresi varsayılan `https://mng.ankarabt.com` dir. Yalnızca `https` saklanır. QR adresinin host’u bu ayarla aynı olmalıdır.
- Telefonda bakmak için Expo Go yeter. Expo Go kurulu uygulama değildir.
- Lokasyon `merkez`, kapı `merkez-giris`. Kapı adresi `https://mng.ankarabt.com/kapi`.
- Otuz saniye içindeki ikinci okutma yok sayılır ve yine de `pdks_gecisler` satırı yazar. Sonuç `ignored` olur. Gün kapanmaz.

## Son Güncelleme

17 Kasım 2026, akşam. Mobil dilimler MBL-05’e kadar bitti. Güncelleme yolu yazıldı. Android herkese açık yayın, Play Console hesabı bulunana kadar durduruldu.

## Nerede Kalmıştık

Uygulama Expo Go ile kapıda denendi. Herkese açık Android yayının şartları konuşuldu. Hesap hazır olunca mağaza paketine dönülecek. Bildirim ve iPhone yayını bu hesabı beklemiyor. Bildirim şirket kararına, iPhone yayını ayrı bir konuşmaya bırakıldı.
