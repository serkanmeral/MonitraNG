# Active Directory Kurulum Kaydı

Tarih: 19 Ekim 2026  
İlgili plan: [roadmap.md](roadmap.md)

Durum: `DC01` ayakta ve `ankarabt.local` ormanının ilk domain controller’ı. İstemci DNS’i, OU, kullanıcı ve gruplar henüz uygulanmadı.

## 1. Alınan kararlar

| Konu | Karar |
|---|---|
| Etki alanı | `ankarabt.local` |
| NetBIOS adı | `ANKARABT` |
| Domain controller adı | `DC01` |
| Domain controller adresi | `192.168.1.254/24` |
| Ağ geçidi | `192.168.1.1` |
| DC üzerindeki DNS | `127.0.0.1` |
| Hyper-V sunucusu | Etki alanına alınmayacak. DNS’i `192.168.1.1` kalacak. |
| İstemci DNS’i | Birinci `192.168.1.254`, ikinci `192.168.1.1`. Modem DHCP kaydını IT değiştirecek. |
| Yönetim hesabı | Günlük iş için `Administrator` kullanılmayacak. Ayrı bir IT hesabı `Domain Admins` grubuna alınacak. |

Tek fiziksel sunucu üzerinde tek domain controller vardır. Sanal makine kapalıyken yeni oturum açma ve ad çözümleme durur. Bu sınır pilot kapsamında kabul edilmiştir.

## 2. Ortam

Kontrol, `192.168.1.54` adresli yönetim bilgisayarından yapıldı. Bu bilgisayar etki alanında değildir ve DNS’i `192.168.1.1` olmaya devam etmektedir.

| Makine | Adres | Gözlem |
|---|---|---|
| Yönetim bilgisayarı | `192.168.1.54` | Wi-Fi. Etki alanında değil. |
| Hyper-V sunucusu | `192.168.1.250` | Ethernet. Uzak Masaüstü (3389) ve WinRM (5985) açık. |
| Hyper-V sunucusu, eski adres | `192.168.1.63` | Wi-Fi adaptörü kontrol sırasında hâlâ yanıt veriyordu. IT bu adresin kalkıp kalkmadığını netleştirmeli. |
| DC01 | `192.168.1.254` | Hyper-V sanal makinesi. MAC `00-15-5d-bb-9e-00`. |

Hyper-V sunucusu Ethernet’e alınınca sanal makine aynı ağa çıktı. Bundan önce `192.168.1.254` bu ağda görünmüyordu. Uzak Masaüstü, DC01 üzerinde kapalıdır. WinRM açıktır. Bu bilgisayardan kimlik bilgisi olmadan yönetim oturumu açılmadı.

## 3. Yapılan işler

1. Hyper-V sunucusuna ve sanal makineye ağ erişimi doğrulandı.
2. Sanal makineye sabit `192.168.1.254` adresi ve `127.0.0.1` DNS’i verildi. Adres önceden tanımlı olduğu için yeniden eklenmedi.
3. Sunucu adı `DC01` olacak şekilde yeni orman kuruldu.
4. Active Directory Domain Services, DNS ve Global Catalog ilk domain controller olarak devreye alındı.
5. Kurulum bu bilgisayardan sorgulanarak doğrulandı.

DSRM parolası kurulum sırasında belirlendi. Parola bu dosyada tutulmaz. IT parolayı parola kasasında saklar.

## 4. Doğrulama

19 Ekim 2026 kontrolünde `192.168.1.254` için sonuçlar:

| Kontrol | Sonuç |
|---|---|
| Ping | Yanıt verdi |
| DNS (53) | Açık |
| Kerberos (88) | Açık |
| LDAP (389) | Açık |
| LDAPS (636) | Açık |
| SMB (445) | Açık |
| WinRM (5985) | Açık |
| Uzak Masaüstü (3389) | Kapalı |
| `ankarabt.local` | DNS kaydı var |
| `_ldap._tcp.dc._msdcs.ankarabt.local` | `dc01.ankarabt.local:389` |
| `dc01.ankarabt.local` | `192.168.1.254` |
| `www.google.com`, DC DNS üzerinden | Çözüldü |

## 5. Bilinçli olarak ertelenen işler

- Modem DHCP DNS kaydı henüz değiştirilmedi. IT hazırlayacak.
- Hyper-V sunucusu etki alanına alınmadı.
- Ters DNS bölgesi (`192.168.1.0/24`) komut olarak hazırlandı, oluşturulduğu doğrulanmadı.
- OU, grup ve kullanıcılar Active Directory’ye girilmedi.
- Test bilgisayarı etki alanına alınmadı.
- Grup İlkesi, LAPS, firewall ve yedekleme henüz başlamadı.

## 6. Sıradaki adımlar

### 6.1 IT – DHCP DNS

Modemde (`192.168.1.1`) DHCP DNS sırası:

1. `192.168.1.254`
2. `192.168.1.1`

Ağ geçidi `192.168.1.1` kalır. Hyper-V sunucusunun kendi DNS’i elle `192.168.1.1` olarak bırakılır. Değişiklik yeni DHCP kirasıyla yayılır. Geri almak için DNS yeniden `192.168.1.1` yapılır.

DC kapalıyken yalnızca birinci DNS’e bakan istemciler web sitesi adlarını çözemez. İkinci DNS, birinci sunucu hiç yanıt vermezse devreye girer.

### 6.2 DC01 – ters kayıt bölgesi

DC01 konsolunda, bölge yoksa:

```powershell
Add-DnsServerPrimaryZone -NetworkId "192.168.1.0/24" -ReplicationScope Forest
Register-DnsClient
```

### 6.3 OU, grup ve kullanıcı

Arayüz: DC01 masaüstünde **Sunucu Yöneticisi → Araçlar → Active Directory Kullanıcıları ve Bilgisayarları**.

Ekleme sırası:

1. `AnkaraBT`
2. Altında `Kullanicilar`, `Gruplar`, `Bilgisayarlar`
3. `Kullanicilar` altında `Yonetim`, `IT`, `Yazilim`, `Satis`, `Idari`
4. `Gruplar` altında genel güvenlik grupları: `GG-Yonetim`, `GG-IT`, `GG-Yazilim`, `GG-Satis`, `GG-Idari`
5. Kullanıcılar kendi departman OU’suna konur ve yalnızca o grubun üyesi yapılır.

OU adları kasıtlı olarak ASCII tutulur. Oturum adı `ad.soyad`, UPN `ad.soyad@ankarabt.local` olur. İlk parolanın ilk girişte değiştirilmesi istenir.

Kişi listesi henüz yoktur. Departman adları doğrulandıktan sonra kullanıcılar girilir.

### 6.4 Etki alanına katma

DHCP DNS yayıldıktan sonra bir test bilgisayarı `ankarabt.local` etki alanına alınır. Hyper-V sunucusu bu teste dahil edilmez.

### 6.5 Plandaki sonraki dalga

Bunlar [roadmap.md](roadmap.md) bölüm 4.2 ve sonrasındadır. Kimlik yapısı ve test katılımı bitmeden başlanmaz.

- Parola, kilitlenme ve ekran kilidi politikaları
- Yönetici ile standart hesabın ayrılması
- Windows LAPS
- Windows Defender, güvenlik duvarı ve BitLocker politikaları
- DC01 sistem durumu yedeği

## 7. İşletim notları

- DC01 yedeği, çalışan domain controller’ın anlık görüntüsüyle alınmaz. Sunucu yedeği veya System State yedeği kullanılır.
- Hyper-V sunucusu, üzerindeki tek domain controller’a bağımlı olacak şekilde etki alanına alınmaz.
- DC01 Uzak Masaüstü kapalıdır. Yönetim, Hyper-V konsolundan veya WinRM açıldıktan sonra yapılır.
- Eski `192.168.1.63` adresi hâlâ yanıt veriyorsa Hyper-V sunucusunda Wi-Fi ve Ethernet birlikte çalışıyordur. IT hangi adaptörün kalacağını netleştirir.
