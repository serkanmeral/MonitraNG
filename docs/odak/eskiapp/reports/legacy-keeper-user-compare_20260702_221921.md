# Legacy Kalite vs MngKeeper — Kullanici Karsilastirma Raporu

**Olusturulma:** 2026-07-02 22:19:21  
**Legacy kaynak:** `192.168.20.30` · MySQL `kalite.users`  
**Keeper kaynak:** `http://192.168.20.8:5040` · prod domain `odak`

> E-posta eslestirmesi **yapilmadi** (kullanici talebi). Eslestirme: username, tam ad, ad+soyad (ilk/son kelime).

---

## Ozet

| Metrik | Deger |
|--------|------:|
| Legacy Kalite kullanicisi | 111 |
| Prod Keeper kullanicisi | 122 |
| **Toplam eslesen** | **14** |
| — username ile | 13 |
| — tam ad ile | 1 |
| — ad + soyad (ilk/son kelime) ile | 0 |
| Sadece Legacy'de | 97 (aktif: 14, pasif: 83) |
| Sadece Keeper'da | 109 (aktif: 62; muhtemel AD duplicate: 57) |
| Belirsiz (coklu aday) | 0 |

### Eslestirme kurallari

1. **username** — normalize edilmis kullanici adi (birincil)
2. **name_exact** — ad+soyad tam metin (Turkce karakter toleransli)
3. **name_first_last** — ilk ad + son soyad kelimesi (or. `Murat` + `Kucuk`; ikinci ad / evlilik soyadi farklarini tolere eder)
4. Coklu adayda: aktif + sAMAccountName benzeri username tercih edilir

---

## Eslesen kullanicilar (14)

| Eslestirme | Legacy kullanici | Legacy ad | L. durum | Keeper kullanici | Keeper ad | K. durum | Kaynak |
|------------|------------------|-----------|----------|------------------|-----------|----------|--------|
| username | ikuru | İnci Doğru Kuru | Aktif | ikuru | İnci Kuru | Aktif | Directory |
| username | okaragul | Osman Karagül | Aktif | okaragul | Osman Karagül | Aktif | Directory |
| username | mkucuk | Murat Küçük | Aktif | mkucuk | Murat Küçük | Aktif | Directory |
| username | eyenicelik | Esra Yeniçelik | Aktif | eyenicelik | Esra Yeniçelik | Pasif | Directory |
| username | ahusan | Anıl Hüsan | Aktif | ahusan | Anıl Hüşan | Aktif | Directory |
| username | agezer | Ahmet Emin Gezer | Aktif | agezer | Ahmet Emin Gezer | Aktif | Directory |
| username | mcolak | Miray  Çolak | Aktif | mcolak | Miray Çolak | Aktif | Directory |
| username | mcolak | MİRAY NUR ÇOLAK | Pasif | mcolak | Miray Çolak | Aktif | Directory |
| username | enalbat | Nurşah Elif Nalbat | Aktif | enalbat | Elif Nalbat | Aktif | Directory |
| name_exact | cngulten | Cansu Nur Gülten | Aktif | cgulten | Cansu Nur Gülten | Aktif | Directory |
| username | mboztepe | Merve Boztepe | Aktif | mboztepe | Merve Boztepe | Aktif | Directory |
| username | haydin | Hanife Özcan Aydın | Aktif | haydin | Hanife Aydın | Aktif | Directory |
| username | egayret | Eren Gayret | Aktif | egayret | Eren Gayret | Aktif | Directory |
| username | akutluca | Ayhan Kutluca | Aktif | akutluca | Ayhan Kutluca | Aktif | Directory |

---

## Sadece Legacy Kalite'de (97)

Keeper'da karsiligi bulunamayan hesaplar. Pasif kayitlar buyuk olasilikla ayrilmis personel.

### Aktif (14)

| Legacy kullanici | Ad soyad |
|------------------|----------|
| admin | Admin |
| gokutan | Gökhan Okutan |
| ccandemir | Cihan Candemir |
| hbilsel | Hasan Bilsel |
| akaradas | Ahmet Karadaş |
| agulyazi | Ali İhsan Gülyazı |
| sgulal | Sabriye Gülal |
| oozger | Ömer Özger |
| bgokgoz | Büşra Nur Gökgöz |
| ksaydin | Kürşad Serdar Aydın |
| mdemirkazik | Mustafa Demirkazik |
| saydin | Sadi  Aydın |
| adguzel | Arhan Doruk  Güzel |
| gkarakus | Gülce Karakuş |

### Pasif (83)

<details>
<summary>Pasif legacy kullanicilar (83 kayit)</summary>

| Legacy kullanici | Ad soyad |
|------------------|----------|
| ntatlakoglu | Nazan Tatlakoğlu |
| eyazdiran | Ercüment Yazdıran |
| sbuyukgullu | Samet Büyükgüllü |
| aaltintas | Abdullah Altıntaş |
| tkarabulut | Temam Karabulut |
| bcetin | Burak Çetin |
| megioglu | Mevlüt Eğioğlu |
| ckaratas | Ceren İlkim Karataş |
| oagoren | Oğuzhan Ağören |
| bsahbaz | Buse Şahbaz |
| eer | Emre Er |
| ecurebal | Erkan Cürebal |
| eegioglu | Ecevit Eğioğlu |
| osimsek | Oğulcan Şimşek |
| uyuce | Ufuk Yüce |
| rakoz | Recep Aköz |
| sdelibas | Samet Delibaş |
| ygulyazi | Yemliha Gülyazı |
| osener | Özgür Sener |
| rucan | Resul Uçan |
| hkar | Hüseyin Kar |
| bkirac | Burak Kıraç |
| kozturk | Kaan Öztürk |
| eyıldız | EMRULLAH YILDIZ |
| bcaner | Bahri Caner |
| acaliskan | Alperen Çalışkan |
| akose | Alihan Köse |
| bmenc | Berkay Menç |
| gyildiz | Gamze Yıldız |
| eoner | Emir Can Öner |
| fyel | Fırat Yel |
| bomurlu | Büşra Ömürlü |
| hsarikaya | Halit Sarikaya |
| mckucuk | Mertcan Küçük |
| bkucuk | Bilal Küçük |
| ebakir | Eren Bakır |
| bcandemir | Barış Candemir |
| akislalioglu | Ahmet Kışlalıoğlu |
| mak | Muhammed Ak |
| bkumru | Barış Kumru |
| asortac | Ahmet Samil Ortac |
| auzun | Adem Uzun |
| fkoca | Ferdi Koca |
| ekoc | Emre Melih Koç |
| icinli | İsmail Cinli |
| bboyraz | Burkay Boyraz |
| iuzun | İlhan Uzun |
| okarakadioglu | Osman Karakadıoğlu |
| bkirikkayaoglu | Bayram Kırıkkayaoğlu |
| averim | Ayşegül Verim |
| mdemir | Murat Demir |
| tkekec | Tayfun Kekeç |
| edemiroglu | Emin Demiroğlu |
| mtarcan | Murat Tarcan |
| eoguz | Engin Berk Oğuz |
| hsendil | Halil Şendil |
| etopcu | Emre Topçu |
| ihisitemiz | İsmail Hakkı İşitemiz |
| gkarakurt | Gizem Karakurt |
| gakcay | Günseli Akçay |
| gatay | Gökhan Atay |
| okocturk | Onur Koçtürk |
| aozcan | Aslıhan Özcan Öcalan |
| scakmak | Sadık Çakmak |
| atiras | Ahmet Eren Tıraş |
| lerarslan | Levent Erarslan |
| mcevik | Mehmet Çevik |
| yyalcinalp | Yiğit Yalçınalp |
| myerlitas | Muhammet Yerlitaş |
| agulcu | Abdülsamet Gülcü |
| dakkaya | Devrim Akkaya |
| aaydugdu | Ahmet Aydoğdu |
| aerel | Abdullah Erel |
| syildiz | Serkan Yıldız |
| svarol | Serkan Varol |
| bgundogdu | Bora Ümit Gündoğdu |
| agunes | Aylin Güneş |
| syolal | Soner Yolal |
| egencosmanoglu | Enes  Gençosmanoğlu |
| btaban | Burak Taban |
| zsanlı | Zeki Şanlı |
| bozben | Burak Özben |
| uatakli | Umut  Ataklı |

</details>

---

## Sadece Keeper'da (109)

Legacy'de karsiligi yok veya username/ad eslesmedi. `AD duplicate` = username bosluk iceriyor (CN kaydi); ayni kisi icin sAMAccountName kaydi ayrica olabilir.

### Aktif — muhtemel gercek kullanici / servis (62)

| Keeper kullanici | Ad soyad | Kaynak | Not |
|------------------|----------|--------|-----|
| administrator |  | Directory | AD duplicate? |
| pc-002$ |  | Directory | AD duplicate? |
| pc-003$ |  | Directory | AD duplicate? |
| pc-004$ |  | Directory | AD duplicate? |
| pc-005$ |  | Directory | AD duplicate? |
| pc-006$ |  | Directory | AD duplicate? |
| pc-007$ |  | Directory | AD duplicate? |
| pc-008$ |  | Directory | AD duplicate? |
| pc-009$ |  | Directory | AD duplicate? |
| pc-010$ |  | Directory | AD duplicate? |
| pc-011$ |  | Directory | AD duplicate? |
| pc-012$ |  | Directory | AD duplicate? |
| pc-013$ |  | Directory | AD duplicate? |
| pc-talasli$ |  | Directory | AD duplicate? |
| planlama-test | planlama | Directory |  |
| safetica_dlp$ |  | Directory | AD duplicate? |
| salma-test | salma | Directory |  |
| serkan.meral | Serkan Meral | Local |  |
| talasli | talasli | Directory |  |
| tasarim-test | tasarim | Directory |  |
| temiz-oda | temiz-oda | Directory |  |
| terminal$ |  | Directory | AD duplicate? |
| test.user1 | Test User1 | Local |  |
| test.user2 | Test User2 | Local |  |
| test.user3 | Test User3 | Local |  |
| test.user4 | Test User4 | Local |  |
| test.user5 | Test User5 | Local |  |
| wac$ |  | Directory | AD duplicate? |
| pc-001$ |  | Directory | AD duplicate? |
| odak_admin | Admin odak | Local |  |
| o.ozcan | Ömer Özcan | Directory |  |
| montaj | montaj | Directory |  |
| ayildiz | Sıtkı Aytaç Yıldız | Directory |  |
| bc-001$ |  | Directory | AD duplicate? |
| ckoc | Canan Koç | Directory |  |
| cnc | cnc | Directory | AD duplicate? |
| dba01$ |  | Directory | AD duplicate? |
| dummy | Dummy | Directory | AD duplicate? |
| dummy2 | dummy2 | Directory | AD duplicate? |
| ec-001$ |  | Directory | AD duplicate? |
| ec-002$ |  | Directory | AD duplicate? |
| erp-destek | erp-destek | Directory | AD duplicate? |
| erp-test | erp | Directory | AD duplicate? |
| erp-user$ |  | Directory | AD duplicate? |
| exchange$ |  | Directory | AD duplicate? |
| win-o4fuo01qbhb$ |  | Directory | AD duplicate? |
| fkosger | Fatih Kosger | Directory |  |
| guest |  | Directory |  |
| haktas | Hazal Aktaş | Directory |  |
| ik-pc-001$ |  | Directory | AD duplicate? |
| k.kesim | kumas kesim | Directory |  |
| kalite-test | kalite | Directory |  |
| kbardakci | Kağan Bardakçı | Directory |  |
| ksengul | Kubilay Şengül | Directory |  |
| ldap-user | ldap-user | Directory |  |
| licensing$ |  | Directory | AD duplicate? |
| m.altay | Mustafa Altay | Directory |  |
| mc-001$ |  | Directory | AD duplicate? |
| mc-002$ |  | Directory | AD duplicate? |
| monitra | monitra | Directory |  |
| fs$ |  | Directory | AD duplicate? |
| yonetim-test | yonetim | Directory |  |

### Pasif

<details>
<summary>Pasif Keeper kullanicilar</summary>

| Keeper kullanici | Ad soyad | Kaynak | Not |
|------------------|----------|--------|-----|
| ahmet emin gezer | Ahmet Emin Gezer | Directory | AD duplicate? |
| mustafa altay | Mustafa Altay | Directory | AD duplicate? |
| osman karagül | Osman Karagül | Directory | AD duplicate? |
| pc-001 |  | Directory |  |
| pc-002 |  | Directory |  |
| pc-003 |  | Directory |  |
| pc-004 |  | Directory |  |
| pc-005 |  | Directory |  |
| pc-006 |  | Directory |  |
| pc-007 |  | Directory |  |
| murat küçük | Murat Küçük | Directory | AD duplicate? |
| pc-008 |  | Directory |  |
| pc-010 |  | Directory |  |
| pc-011 |  | Directory |  |
| pc-talasli |  | Directory |  |
| safetica_dlp |  | Directory |  |
| sakgun | Sibel Akgün | Directory |  |
| serkan meral | Serkan Meral | Directory | AD duplicate? |
| sibel akgün | Sibel Akgün | Directory | AD duplicate? |
| terminal |  | Directory |  |
| wac |  | Directory |  |
| pc-009 |  | Directory |  |
| win-o4fuo01qbhb |  | Directory |  |
| miray çolak | Miray Çolak | Directory | AD duplicate? |
| mc-002 |  | Directory |  |
| anıl hüşan | Anıl Hüşan | Directory | AD duplicate? |
| ayhan kutluca | Ayhan Kutluca | Directory | AD duplicate? |
| bc-001 |  | Directory |  |
| canan koç | Canan Koç | Directory | AD duplicate? |
| cansu nur gülten | Cansu Nur Gülten | Directory | AD duplicate? |
| dba01 |  | Directory |  |
| ec-001 |  | Directory |  |
| elif nalbat | Elif Nalbat | Directory | AD duplicate? |
| eren gayret | Eren Gayret | Directory | AD duplicate? |
| merve boztepe | Merve Boztepe | Directory | AD duplicate? |
| erp-user |  | Directory | AD duplicate? |
| exchange |  | Directory |  |
| fatih kosger | Fatih Kosger | Directory | AD duplicate? |
| hanife aydın | Hanife Aydın | Directory | AD duplicate? |
| ik-pc-001 |  | Directory |  |
| i̇nci kuru | İnci Kuru | Directory | AD duplicate? |
| kağan bardakçı | Kağan Bardakçı | Directory | AD duplicate? |
| krbtgt |  | Directory |  |
| kubilay şengül | Kubilay Şengül | Directory | AD duplicate? |
| mc-001 |  | Directory |  |
| esra yeniçelik | Esra Yeniçelik | Directory | AD duplicate? |
| ömer özcan | Ömer Özcan | Directory | AD duplicate? |

</details>

---

## Degerlendirme — olasi sonraki adimlar

| # | Konu | Oneri |
|---|------|-------|
| 1 | Aktif legacy-only (14 kisi) | AD'de var mi / Keeper sync kapsami genisletilmeli mi kontrol |
| 2 | Keeper AD duplicate (~57 kayit) | Keycloak LDAP mapper (sAMAccountName); CN-tabanli ikinci kayitlar temizlenebilir |
| 3 | Pasif legacy (83 kisi) | Migrasyon disi birakilabilir; arsiv amacli tutulur |
| 4 | Eslesen 14 kisi | MonitraNG'de oturum acabilir; legacy hesap referans olarak eslenebilir |
| 5 | Legacy admin | Keeper'da ayri yonetim; bire bir eslestirme gerekmez |

*Script:* `scripts/tests/MngKeeper/users/compare-legacy-kalite-users.ps1`
