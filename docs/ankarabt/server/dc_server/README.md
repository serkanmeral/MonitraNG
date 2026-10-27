# DC01 devir notu

Son güncelleme: 20 Ekim 2026.

Bu klasör, AnkaraBT etki alanı denetleyicisinde kalınan yerdir. Yeni bir Cursor oturumu buradan devam eder. Erişim bilgileri [erisim.md](erisim.md) içindedir. 20 Ekim’den sonraki durum [durum.md](durum.md) içindedir.

19 Ekim kurulum kaydının kopyası [ad-kurulum.md](ad-kurulum.md) dosyasındadır. O kayıt, LDAPS sertifikasını, `ankarabt.com` oturum sonekini ve `serkan.meral` kullanıcısını henüz bilmez. Çelişirse `durum.md` geçerlidir.

## Şu anki durum

`DC01`, `ankarabt.local` ormanının ilk ve tek denetleyicisidir. Adres `192.168.1.254`. DNS, Kerberos, LDAP ve LDAPS cevap verir. Windows Server 2025 Standard.

`serkan.meral` kullanıcısı `CN=Users` altındadır. Görünen ad `Serkan Meral`. Oturum adı ve posta özelliği `serkan.meral@ankarabt.com`. Hesap etkin. `pwdLastSet=0`: sonraki oturumda parola değişecek. Bu yüzden Mailcow LDAP girişi 20 Ekim öğleden sonra reddedildi. Posta tarafı [mail durum dosyası](../mail_server/durum.md) içindedir.

20 Ekim akşamı modem, IPv4 DHCP DNS olarak `192.168.1.254` dağıtıyor. Deneme bilgisayarı `SERKAN-MAINPC` çalışma grubunda, adres `192.168.1.30` (öğlene kadar `192.168.1.54` idi). IPv6 DNS hâlâ `fe80::1`. Windows sorguları modeme soruyor; `ankarabt.local` modemin cevabıyla `192.168.1.1` oluyor. Doğrudan `192.168.1.254` sorulunca aynı ad `192.168.1.254` çıkıyor. OU ve departman grupları henüz açılmamıştır.

## Sıradaki iş

1. Modemin IPv6 sekmesinde DNS kapatılır. `fe80::1` istemciye gitmez.
2. `SERKAN-MAINPC` üzerinde kira yenilenir. `nslookup ankarabt.local` sunucusu `192.168.1.254` olur, cevap da `192.168.1.254` olur.
3. Yerel yönetici hesabıyla bir deneme bilgisayarı `ankarabt.local` etki alanına alınır. `SERKAN-MAINPC\serkan.meral` yerel yönetici değildir. Hyper-V sunucusu alınmaz.
4. `serkan.meral` üzerinde “sonraki oturumda parola değiştirilsin” kapatılır. Ardından posta girişi mail klasöründeki sırayla denenir.

Ters DNS bölgesi, OU ve gruplar, ardından ayrı bir IT yöneticisi bu katılmadan sonradır. Günlük iş `Administrator` ile yapılmaz.

## Dokunulmayacaklar

- Hyper-V sunucusu `192.168.1.250`. Etki alanına alınmaz. DNS’i `192.168.1.1` kalır.
- Çalışan denetleyicinin Hyper-V anlık görüntüsü. Yedek, sistem durumu yedeğidir.
- LDAPS sertifikası `BEF42AFA8D9A89F7328D70F6F87882DE46B62615`. Yerine CNG ile yeni sertifika üretilirse 636 yeniden susar.
- Modem IPv4 DNS’i artık `192.168.1.254`. IPv6 DNS `fe80::1` kapanmadan etki alanına katılma denenmez.
