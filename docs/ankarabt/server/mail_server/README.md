# MAIL01 devir notu

Son güncelleme: 20 Ekim 2026.

Bu klasör, AnkaraBT posta sunucusunda kalınan yerdir. Yeni bir Cursor oturumu buradan devam eder. Ayrıntılı erişim bilgileri [erisim.md](erisim.md) içindedir. Yapılan iş ve bilinçli olarak yapılmayanlar [durum.md](durum.md) içindedir.

Taşıma planının kopyası [mail-roadmap.md](mail-roadmap.md) dosyasındadır. Kurulumdan sonraki durum [durum.md](durum.md) içindedir. İkisi çelişirse durum dosyası geçerlidir. Plan dosyasındaki `roadmap.md` ve `ad-kurulum.md` bağlantıları Faz 1 klasörüne aittir.

## Şu anki durum

Mailcow iç ağda ayaktadır. Adres `https://192.168.1.253`. Posta alanı `ankarabt.com` tanımlıdır. Pilot kutu `serkan.meral@ankarabt.com` açılmıştır. Takma ad `smeral@ankarabt.com` bu kutuya gider. Eski sağlayıcıdaki posta durur. MX değişmemiştir.

20 Ekim öğleden sonra kutunun kimlik kaynağı `ldap` yapıldı. Yerel kutu parolası duruyor. AD parolasıyla giriş denendi ve reddedildi. Hesapta “sonraki oturumda parola değiştirilsin” açık (`pwdLastSet=0`). Mailcow bu değişikliği kendisi yapamıyor. Ayrıntı [durum.md](durum.md) içindedir.

## Sıradaki iş

DC’de `serkan.meral` için “sonraki oturumda parola değiştirilsin” kapatılır ve mevcut parola kalıcı olur. Ardından tek deneme: `https://192.168.1.253`, kullanıcı `serkan.meral@ankarabt.com`, parola AD parolası. Başarısız olursa `authsource` tekrar `mailcow` yapılır. Yerel parola, başarılı girişten önce silinmez.

MX, SPF, DKIM yayını ve DMARC bu denemeden sonra da değişmez. Eski sağlayıcıdan IMAP kopyası ve yönlendirme, sağlayıcı adı öğrenilmeden yapılmaz. Bu bilgisayarın etki alanına alınması ayrı iştir; kaldığı yer [dc durum dosyası](../dc_server/durum.md) içindedir.

## Dokunulmayacaklar

- Canlı MX kaydı ve eski sağlayıcıdaki kutu.
- `smeral@ankarabt.com` takma adı.
- Kullanıcının yüklediği giriş logosu.
- Hyper-V sunucusu `192.168.1.250`. Etki alanına alınmaz.
- `serkan.meral` kutusunun yerel parolası. `ldap` kaynağı geri alınmadan silinmez.
