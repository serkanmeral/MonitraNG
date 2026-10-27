# Faz 1 – Mail Sunucusu

Ana plan: [roadmap.md](roadmap.md)  
Kimlik altyapısı: [ad-kurulum.md](ad-kurulum.md)

Durum: mailcow iç ağda ayakta. Adres `https://192.168.1.253`. MX değişmedi. Mevcut posta sağlayıcıda çalışmaya devam ediyor.

## 1. Amaç

Çalışan postayı kesmeden MAIL01 üzerine almak. Yeni kutu adı `ad.soyad@ankarabt.com` olur. Eski adres bir süre daha çalışır ve yeni kutuya yönlenir. Eski iletiler yeni kutuya kopyalanır.

Kimlik kaynağı `ankarabt.local` olacak. Outlook hedefi IMAP/SMTP’dir.

## 2. Kapsam

| Dahil | Dahil değil |
|---|---|
| MAIL01, Debian, Docker Compose, mailcow | Exchange, MAPI |
| Postfix, Dovecot, SOGo | Outlook takvim ve kişi eşitlemesi |
| Rspamd, ClamAV | İkinci mail sunucusu, yüksek erişilebilirlik |
| İç ağda IMAP, SMTP ve webmail | Doğrudan internete açık sunucu |
| Kota, alias, grup adresi | Kurumsal DLP |
| TLS, yönetim paneli MFA | |
| AD/LDAP kimlik doğrulama denemesi | |
| Eski kutuların kopyalanması ve yönlendirme | Eski sağlayıcıdaki kutunun silinmesi |
| Yedekleme ve örnek geri yükleme | |

Mevcut sağlayıcının MX kaydı, yeni sunucu gönder-al denemesini geçene kadar değişmez. Kullanıcı bu süre boyunca eski kutusunda çalışmaya devam eder.

## 3. Yerleşim

| Bileşen | Değer |
|---|---|
| Sanal makine | MAIL01, `mail01`, `192.168.1.253` |
| SSH | `mailadmin`, sudo yetkili |
| Hyper-V sunucusu | `192.168.1.250` |
| İşletim sistemi | Debian |
| Ürün | mailcow: dockerized |
| DNS | DC01, `192.168.1.254` |
| Etki alanı | `ankarabt.local` |

MAIL01, DC01 ile aynı Hyper-V sunucusundadır. Hyper-V sunucusunun kendisi etki alanına alınmaz.

Posta kutusu verisi ilk aşamada MAIL01 diskindedir. NAS hazır olunca yedek hedefi NAS olur. NAS, mail kurulumunun ön koşulu değildir. Kalıcı yedek kopyası NAS dışında da durmalıdır.

## 4. Kurulumdan önce kapanacak kararlar

Bunlar netleşmeden VM açılabilir, posta alanı ve dış kapı açılamaz.

| Karar | Neden gerekli | Şu an |
|---|---|---|
| Yeni posta alanı | Yeni kutuların adresi | `ankarabt.com`, biçim `ad.soyad@ankarabt.com` |
| Eski adres | Yönlendirme ve kopyalama kaynağı | `smeral@ankarabt.com` → `serkan.meral@ankarabt.com`. Aynı alan adı |
| Pilot sınırı | Dışarıdan gelen posta yeni kutuya düşmeli | Taşıma var. Eski MX, deneme bitene kadar durur |
| MAIL01 adresi | Sabit IP, güvenlik duvarı ve MX | `192.168.1.253`, Ubuntu 26.04.1 |
| Giriş modeli | Active Directory parolası mı, ayrı posta parolası mı | UPN `ad.soyad@ankarabt.com`. Taşıma anında yerel parola, sonra yalnızca AD |
| Sağlayıcı | IMAP kopyası ve yönlendirme bunun panelinden yapılır | Adı yazılacak |

İç ad `mail.ankarabt.local` olur ve DC01 DNS’inde MAIL01 adresine çözülür.

## 5. Adres eşlemesi

Örnek kişi:

| | Adres |
|---|---|
| Eski, sağlayıcıda çalışan | `smeral@ankarabt.com` |
| Yeni kutu | `serkan.meral@ankarabt.com` |
| Yeni sunucudaki takma ad | `smeral@ankarabt.com` → `serkan.meral@ankarabt.com` |

İki adres de `ankarabt.com` üzerindedir. Eski MX kaydı, yeni kutu gönder-al denemesini geçene kadar sağlayıcıda kalır. MX en son adımda değişir ve eski değer geri dönüş için saklanır. Değişimden sonra eski adres, yeni sunucuda takma ad olarak durur.

## 6. Taşıma sırası

Posta bu sıra bozulursa kesilir. Eski sağlayıcı, yeni kutu dışarıdan posta alıncaya kadar asıl gelen kutusudur.

1. Sağlayıcıdaki kutu listesi çıkarılır: adres, takma ad, kota, yönlendirme.
2. MAIL01 kurulur. `ankarabt.com` tanımlanır. TLS, SPF, DKIM ve DMARC hazırlanır.
3. `serkan.meral@ankarabt.com` açılır. Kullanıcı eski kutusunda çalışmaya devam eder.
4. Eski kutu, IMAP ile yeni kutuya kopyalanır. Eski kutu silinmez. Kopya, geçiş gününe kadar tekrarlanır.
5. Yeni kutudan dışarıya bir iletinin gittiği ve dışarıdan gelen bir iletinin düştüğü denenir.
6. Eski adresten yeni adrese yönlendirme açılır. Bundan sonra yeni postalar yeni kutuya gelir. Eski kutu durur.
7. Son IMAP kopyası alınır. Yönlendirmeden önce düşmüş iletiler de yeni kutuya geçer.
8. Kullanıcının Outlook’u yeni kutuya alınır. Eski hesap, kopya doğrulanana kadar silinmez.
9. MX yeni sunucuya çevrilir. Geri dönüş için eski MX değeri saklanır. `smeral@ankarabt.com` yeni sunucuda takma ad olarak kalır.
10. Eski kutu bir süre daha sağlayıcıda tutulur. Yeni düzen oturunca kapatılır.

Active Directory bağlantısı bu taşımadan sonradır. Taşıma gününde kullanıcı, mailcow parolasıyla girer.

## 6.1 Active Directory ve Outlook

Gönder-al ve AD parolasıyla giriş bu kurulumda çalışır. Takvim, kişi ve Exchange özellikleri kapsam dışıdır.

Sorunsuz giriş için AD’deki oturum adı ile posta adresi aynı olmalıdır. `ankarabt.local` alanına `ankarabt.com` UPN soneki eklenir. Kullanıcının UPN’i `serkan.meral@ankarabt.com` yapılır. Posta özniteliği de bu adres olur. Outlook’ta kullanıcı adı bu adresin tamamıdır.

İstemci, Microsoft Office ile gelen klasik Outlook’tur. Hesap elle IMAP olarak eklenir. Sihirbazın Exchange veya Microsoft 365 tahmini kullanılmaz. Gelen posta IMAP ve TLS, giden posta 587 ve STARTTLS olur. Outlook içindeki “yeni Outlook” düğmesi açılmaz.

LDAP bağlantısı LDAPS (636) üzerindedir. MAIL01, DC01 sertifikasına güvenir. Kimlik doğrulama yalnızca AD’ye bakar. AD’de kapatılan hesap postaya da giremez. Posta kutusu mailcow’da durur. AD hesabı kutuyu kendiliğinden açmaz. Dağıtım listesi mailcow takma adıdır. AD grubu listeyi kendiliğinden oluşturmaz.

İlk deneme `serkan.meral` hesabıyla, MX değişmeden yapılır. Webmail ve Outlook bu hesapla açılmadan diğer kullanıcılara geçilmez.

## 7. Kurulum sırası

1. Sağlayıcı adı ve MAIL01 IP adresi.
2. MAIL01 oluşturulur. Disk ve bellek mailcow gereksinimine göre ayrılır.
3. DC01’de `mail.ankarabt.local` kaydı açılır.
4. Debian, Docker ve mailcow kurulur. Yönetim paneli yalnızca iç ağdan açılır.
5. Posta alanı, TLS, kota, spam ve antivirüs tanımlanır.
6. Bölüm 6’daki taşıma uygulanır.
7. AD kullanıcıları oluştuktan sonra LDAP denenir.
8. Yedek alınır ve örnek bir ileti geri yüklenir.

Güvenlik duvarında, deneme başlayınca SMTP, IMAP ve webmail için kontrollü dış erişim gerekir. Yönetim paneli dışarı açılmaz.

## 8. İş listesi

### 8.1 Sanal makine ve DNS

- [ ] MAIL01 oluşturulması
- [ ] Sabit IP ve ad
- [ ] `mail.ankarabt.local` kaydı
- [ ] İç ağdan yönetim paneline erişim, internete kapalı yönetim

### 8.2 mailcow

- [x] Docker ve mailcow kurulumu. 19 Ekim 2026, `mail.ankarabt.com`, iç ağ.
- [x] Posta etki alanının tanımlanması. `ankarabt.com`, 20 Ekim 2026. DKIM seçicisi `dkim`.
- [ ] TLS
- [ ] Kota
- [ ] Alias ve grup adresleri
- [ ] Rspamd ve ClamAV
- [ ] Yönetim paneli MFA

### 8.3 İstemci

- [ ] Pilot posta kutuları. `serkan.meral@ankarabt.com` açıldı. Takma ad `smeral@ankarabt.com`. Eski kutu henüz kopyalanmadı.
- [ ] Outlook IMAP/SMTP gönder-al
- [ ] SOGo webmail
- [ ] Hesap kapatılınca erişimin kalkması

### 8.4 Active Directory

- [ ] LDAP bağlantısının DC01’e kurulması. 20 Ekim 2026: LDAPS açıldı. Sertifika BEF42AFA, 2031’e kadar. Mailcow, svc-mailcow hesabıyla dizine bağlanıyor ve serkan.meral kaydını buluyor. Posta kutusu henüz kendi parolasını kullanıyor.
- [ ] Bir AD kullanıcısıyla webmail ve Outlook girişi
- [ ] AD’de kapatılan hesabın postayı da kapatması

LDAP denemesi, [ad-kurulum.md](ad-kurulum.md) içindeki kullanıcı adımı bitmeden yapılmaz.

### 8.5 Taşıma

- [ ] Adres eşlemesi teyit edildi: `smeral@ankarabt.com` → `serkan.meral@ankarabt.com`
- [ ] Sağlayıcı adı ve kutu listesi
- [ ] `serkan.meral@ankarabt.com` kutusunun açılması
- [ ] Eski kutunun IMAP ile kopyalanması, eski kutuya dokunulmaması
- [ ] Yeni kutunun dışarıya gönder-al denemesi
- [ ] Eski adresten `serkan.meral@ankarabt.com` adresine yönlendirme
- [ ] Son kopya
- [ ] Outlook’un yeni kutuya alınması
- [ ] MX değişimi ve eski MX değerinin saklanması

### 8.6 Dış posta

- [ ] Firewall üzerinden kontrollü SMTP, IMAP ve webmail
- [ ] `ankarabt.com` için MX
- [ ] SPF, DKIM, DMARC
- [ ] Dışarıdan gönder-al denemesi, eski MX dururken

### 8.7 Yedek

- [ ] mailcow yedeği
- [ ] Örnek kutu veya iletinin geri yüklenmesi
- [ ] Yedek kopyasının MAIL01 dışında durması

## 9. Pilot kabulü

- İç ağda Outlook ile posta gönderilip alınır.
- Webmail açılır.
- Spam ve antivirüs politikası etkindir.
- TLS ve yönetim paneli MFA açıktır.
- AD/LDAP ile en az bir kullanıcı girişi doğrulanır.
- Hesap kapatılınca posta erişimi kalkar.
- Örnek geri yükleme başarılıdır.
- `ankarabt.com` için MX, SPF, DKIM ve DMARC doğrulanır.
- Eski kutudaki iletiler yeni kutuda görülür.
- Eski adrese gelen yeni ileti, yeni kutuya düşer.
- Taşıma boyunca eski sağlayıcıdaki posta kesilmez.

## 10. Riskler

| Risk | Tedbir |
|---|---|
| Tek sunucuda DC ve mail birlikte | MAIL01 yedeği ve geri yükleme denemesi |
| Eski MX’in erken değişmesi | MX, yeni kutu gönder-al denemesini geçene kadar aynı kalır |
| Adres değişince kaybolan yazışma | Eski adres yeni kutuya yönlenir ve takma ad olarak durur |
| Kopya sırasında kutunun silinmesi | IMAP kopyası eski kutuyu silmez. Eski kutu yönlendirmeden sonra da bir süre tutulur |
| AD hazır olmadan LDAP | Kutular önce yerelde açılır, LDAP kullanıcı adımından sonra bağlanır |
| Posta diskinin dolması | Kota ve disk alarmı |
| Yönetim panelinin internete açık kalması | Panel yalnız iç ağ |
