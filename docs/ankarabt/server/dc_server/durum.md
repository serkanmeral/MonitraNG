# DC01 yapılan işler ve kalanlar

Son güncelleme: 20 Ekim 2026.

19 Ekim kaydı [ad-kurulum.md](ad-kurulum.md) dosyasındadır. Bu dosya ondan sonra yapılanları yazar.

## Denetleyici

`DC01` ilk orman denetleyicisidir. Active Directory Domain Services, DNS ve Global Catalog çalışır. `ankarabt.local` SOA kaydı vardır. `_ldap._tcp.dc._msdcs.ankarabt.local`, `dc01.ankarabt.local:389` adresine gider. DC DNS’i `www.google.com` adını çözer.

Tek denetleyici sanal makinededir. Makine kapalıyken yeni oturum ve bu DNS üzerinden ad çözümleme durur. Bu sınır pilot için kabul edilmiştir.

## LDAPS

636 kapısı baştan açıktı fakat sertifika sunmuyordu. Schannel, kişisel depodaki sertifikayı zincir güvenilir olmadığı için varsayılan sunucu sertifikası saymıyordu. Olay günlüğü 1220 ve 36886 bunu yazdı. `8009030e`, güvenlik paketinde kimlik bilgisi olmadığı anlamına gelir.

Çalışan sertifika `certreq` ile üretildi, makinenin güvenilen kök deposuna eklendi ve NTDS yeniden başlatıldı. Bundan sonra hem DC’nin kendisi hem `192.168.1.54` üzerindeki bilgisayar TLS el sıkışmasını tamamladı.

| | |
|---|---|
| Konu | `CN=dc01.ankarabt.local` |
| Parmak izi | `BEF42AFA8D9A89F7328D70F6F87882DE46B62615` |
| Bitiş | 20 Ekim 2031 |
| Genişletilmiş kullanım | Yalnızca Server Authentication |
| Sağlayıcı | Microsoft RSA SChannel Cryptographic Provider |
| Anahtar tipi | Exchange |
| Depolar | Yerel bilgisayar kişisel deposu ve güvenilen kök deposu |

`New-SelfSignedCertificate` ile üretilen CNG sertifikaları ve istemci kimliği de taşıyan sertifikalar LDAPS tarafından seçilmedi. `-KeySpec KeyExchange` akıllı kart penceresi açabiliyor. Yazılım sağlayıcısı açıkça verilmeden o komut çalıştırılmaz. `Restart-Service NTDS -Force` sırasında DNS’in geç kalkması normaldir. Komut satırı dönünce DNS yeniden cevap verir.

Sertifikanın ortak kısmı Mail01 üzerindedir: `/opt/mailcow-dockerized/data/assets/ssl/dc01-ldaps.crt`.

## Kullanıcılar

Ormana `ankarabt.com` UPN soneki eklenmiştir. Posta ile aynı ada girilecek hesaplarda UPN `ad.soyad@ankarabt.com` olur. `ad-kurulum.md` içindeki `ankarabt.local` UPN cümlesi bu karar ile değişmiştir.

| Hesap | Yer | UPN | Posta özelliği |
|---|---|---|---|
| `serkan.meral` | `CN=Users,DC=ankarabt,DC=local` | `serkan.meral@ankarabt.com` | `serkan.meral@ankarabt.com` |
| `svc-mailcow` | `CN=Users,DC=ankarabt,DC=local` | `svc-mailcow@ankarabt.local` | Yok |

`serkan.meral` etkindir. Parolasını kullanıcı belirlemiştir. `svc-mailcow` parolası süresizdir ve hesap parolayı kendisi değiştiremez. Mailcow bu hesapla LDAPS bağını 20 Ekim 2026’da doğrulamıştır.

OU tasarımı hazırdır, dizine girilmemiştir. Kök OU `AnkaraBT` olur. Altında `Kullanicilar`, `Gruplar`, `Bilgisayarlar`. Kullanıcı OU’ları: `Yonetim`, `IT`, `Yazilim`, `Satis`, `Idari`. Gruplar: `GG-Yonetim`, `GG-IT`, `GG-Yazilim`, `GG-Satis`, `GG-Idari`. OU adları ASCII tutulur.

Arayüz, DC konsolunda **Sunucu Yöneticisi → Araçlar → Active Directory Kullanıcıları ve Bilgisayarları** (`dsa.msc`).

## İstemci DNS, 20 Ekim akşamı

Modem DHCP havuzu `192.168.1.15`–`192.168.1.249`. Bitiş `249` doğrudur; `.250`–`.254` sunucuları havuzun dışındadır. Wi-Fi ve Ethernet aynı alt ağdadır. Deneme bilgisayarının Wi-Fi’de olması sunuculara erişimi değiştirmez.

IT, DHCP birincil DNS alanına `192.168.1.254` yazdı. Uygula ve yeniden başlatmanın ilk kontrolünde kira hâlâ yalnız `192.168.1.1` getirdi. 20 Ekim yaklaşık 17:30’da IPv4 DHCP DNS `192.168.1.254` oldu. Ağ geçidi `192.168.1.1` kaldı.

IPv6 DNS `fe80::1` duruyor ve Windows onu öne alıyor. `nslookup ankarabt.local` sunucusu `fe80::1`, cevap `192.168.1.1`. Aynı sorgu `-Server 192.168.1.254` ile `192.168.1.254` döner. `dc01.ankarabt.local` için de böyledir. Bu halde etki alanına katılma denemesi yapılmaz.

`serkan.meral` parolası sonraki oturumda değiştirilecek şekilde duruyor. Posta girişi bu bayrak kapanınca yeniden denenir.

## Ertelenenler

- Modem IPv6 DNS’inin kapatılması. IPv4 DNS `192.168.1.254` oldu.
- Hyper-V sunucusunun Wi-Fi adresi `192.168.1.63` kontrol sırasında hâlâ cevap veriyordu. Hangi adaptörün kalacağını IT netleştirir.
- Ters bölge `192.168.1.0/24` oluşturulduğu doğrulanmadı. Yoksa:

```powershell
Add-DnsServerPrimaryZone -NetworkId "192.168.1.0/24" -ReplicationScope Forest
Register-DnsClient
```

- Test bilgisayarının etki alanına alınması. `SERKAN-MAINPC` çalışma grubunda, 20 Ekim akşamı adresi `192.168.1.30`. IPv6 DNS kapanmadan katılmaz. Oturum yerel yöneticisi değildir.
- Grup İlkesi, LAPS, güvenlik duvarı, BitLocker ve sistem durumu yedeği.

MAIL01’in kendi DNS’i hâlâ modemdedir. Posta kabı, DC adını `extra_hosts` ile görür. Bu ayrıntı mail klasöründeki `durum.md` dosyasındadır.
