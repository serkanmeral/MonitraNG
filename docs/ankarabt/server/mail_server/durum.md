# MAIL01 yapılan işler ve kalanlar

Son güncelleme: 20 Ekim 2026.

## Kurulum

Mailcow, `/opt/mailcow-dockerized` altına kuruludur. Kaynak `github.com/mailcow/mailcow-dockerized`, dal `master`, revizyon `81f6f7b`. Docker 29.9.1, Compose v5.6.0.

`mailcow.conf` değerleri:

- `MAILCOW_HOSTNAME=mail.ankarabt.com`
- `TZ=Europe/Istanbul`
- `SKIP_CLAMD=n`
- IPv6 kapalı

İlk imaj çekiminde `ghcr.io` zaman aşımı olmuş, imajlar tek tek yeniden çekilmiştir. Yığın `docker compose up -d` ile ayaktadır. İç ağdan 80, 443, 25, 587 ve 993 kapıları açıktır.

## Posta alanı ve pilot kutu

20 Ekim 2026’da `ankarabt.com` eklenmiştir. DKIM seçicisi `dkim` olarak oluşmuştur. Açık anahtarın dosya yeri bu notta doğrulanmamıştır. İkinci bir DKIM ekleme denemesi `dkim_domain_or_sel_invalid` dönmüştür. Yeni seçici üretilmez.

`serkan.meral@ankarabt.com` kutusu vardır. `smeral@ankarabt.com` takma adı bu kutuya işaret eder. Takma ad, MX sağlayıcıda kaldığı sürece canlı postayı etkilemez. Eski kutu henüz IMAP ile kopyalanmamıştır. Sağlayıcının adı bilinmiyor.

## Giriş ekranı

Kullanıcı logoyu panelden değiştirmiştir. Logo dosyasına dokunulmaz.

Şablonlar `/opt/mailcow-dockerized/data/web/templates` altındadır. `update.sh` bunları geri yazabilir. Özgün halleri `/opt/mailcow-dockerized/data/web/templates-login-backup/` içindedir.

Yapılan görünür değişiklikler:

- Kullanıcı girişinden “parolamı unuttum” kaldırıldı.
- FIDO2, IAM ve diğer alternatif giriş düğmeleri kaldırıldı.
- “Login as admin” ve “Login as domain admin” duruyor. Kullanıcı bunların kalmasını istedi.
- `/reset-password.php` hemen `/` adresine 302 döner.

Son doğrulama: `/` 200, admin ve domain admin bağlantıları duruyor, unuttum ve FIDO2 düğmesi yok.

## Active Directory

DC01, `192.168.1.254`, Windows Server 2025 Standard. Etki alanı `ankarabt.local`, NetBIOS `ANKARABT`.

LDAPS için makine sertifikası üretilmiştir. Schannel, sertifika yalnızca kişisel depodayken onu sunmuyordu. Sertifika güvenilen kök deposuna da konduktan ve NTDS yeniden başlatıldıktan sonra 636 sertifika vermeye başladı.

| | |
|---|---|
| Konu | `CN=dc01.ankarabt.local` |
| Parmak izi | `BEF42AFA8D9A89F7328D70F6F87882DE46B62615` |
| Bitiş | 20 Ekim 2031 |
| SAN | `dc01.ankarabt.local`, `ankarabt.local` |
| Sağlayıcı | Microsoft RSA SChannel Cryptographic Provider |
| Anahtar tipi | Exchange |

Ormana `ankarabt.com` UPN soneki eklenmiştir. `serkan.meral` için UPN ve `mail` özniteliği `serkan.meral@ankarabt.com` yapılmıştır. Hesap `CN=Users,DC=ankarabt,DC=local` altındadır ve etkindir.

Mailcow kimlik sağlayıcısı LDAP’dir. PHP kabından bağ denemesi başarılıdır. Arama bir kayıt döner. Ayarlar:

- Otomatik kutu açma kapalı (`login_provisioning=0`)
- Toplu kullanıcı alma kapalı (`import_users=0`)
- Periyodik eşitleme kapalı (`periodic_sync=0`)
- SSL doğrulaması açık (`ignore_ssl_error=0`)

20 Ekim öğleden sonra `serkan.meral@ankarabt.com` satırında `authsource` `ldap` yapıldı. Yerel parola özeti duruyor. Dovecot kimlik önbelleği temizlendi. SOGo, IMAP ve SMTP erişimi açıktı.

Aynı gün AD parolasıyla giriş denendi. Mailcow auth ucu `401` döndü. LDAP araması kaydı buldu: `CN=Serkan Meral,CN=Users,DC=ankarabt,DC=local`. Hesap etkin (`userAccountControl=512`). `pwdLastSet=0` ve hesaplanan bayrak `8388608`: parola sonraki oturumda değiştirilecek. Kullanıcı bağ denemesi `Invalid credentials` (`49`) verdi. Mailcow bu parola değişimini yapamaz. `authsource` geri alınmadı.

MAIL01’in işletim sistemi DNS’i modemdedir (`192.168.1.1`). `ankarabt.local` adları sunucunun kendisinde çözülmez. PHP kabı, `docker-compose.override.yml` içindeki `extra_hosts` ile DC’yi görür. Sunucu DNS’ini DC’ye çevirmek ayrı bir iştir. İstemci DNS’inin 20 Ekim akşamı durumu DC klasöründeki `durum.md` dosyasındadır.

## Bilinçli olarak yapılmayanlar

- MX, SPF, DKIM ve DMARC yayını değişmedi.
- Eski sağlayıcıdan IMAP kopyası ve yönlendirme yapılmadı.
- AD parolasıyla giriş, “sonraki oturumda parola değiştirilsin” kapanmadan tamamlanmadı. Kimlik kaynağı `ldap` olarak duruyor.
- Bu bilgisayar ve Hyper-V sunucusu etki alanına alınmadı.
- İstemci IPv6 DNS’i hâlâ modem (`fe80::1`). Ayrıntı DC durum dosyasındadır.
- Yönetim paneli MFA açılmadı.
- Genel güvenilir TLS sertifikası yok.
- Yedek ve geri yükleme denemesi yapılmadı.

## Taşıma sırası

Canlı posta kesilmeden şu sıra korunur:

1. Sağlayıcı adı ve kutu listesi.
2. Eski kutunun IMAP kopyası. Kaynak silinmez.
3. Yeni kutudan dışarı gönder-al denemesi.
4. Eski adresten `serkan.meral@ankarabt.com` adresine yönlendirme.
5. Son IMAP kopyası.
6. Outlook’un yeni kutuya alınması.
7. En son MX. Eski MX değeri geri dönüş için saklanır.

Outlook, Office ile gelen klasik Outlook’tur. Hesap elle IMAP eklenir. Sihirbazın Exchange veya Microsoft 365 seçmesine izin verilmez. Gelen posta IMAP ve TLS, giden posta 587 ve STARTTLS. “Yeni Outlook” kapalı kalır. Kullanıcı adı, adresin tamamıdır.
