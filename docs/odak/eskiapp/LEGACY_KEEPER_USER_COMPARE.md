# Legacy Kalite vs MngKeeper — Kullanıcı karşılaştırma

**Son güncelleme:** 2 Temmuz 2026  
**Durum:** İlk analiz tamamlandı · sonraki iş planlaması için referans  
**İlgili:** [README.md](./README.md) · [../ldap/USER_SOURCES.md](../ldap/USER_SOURCES.md) · [../proddeploy/README.md](../proddeploy/README.md)

---

## 1. Amaç

Odak’ın eski **Kalite** uygulaması (`kalite.users`, ~111 kayıt) ile MonitraNG **MngKeeper** kullanıcı havuzunu (prod, domain `odak`, ~122 kayıt) karşılaştırmak:

- Hangi kişiler **her iki sistemde** var?
- Kim **yalnızca legacy**’de veya **yalnızca Keeper**’da?
- Geçiş / yetkilendirme / AD sync planlaması için karar zeminı oluşturmak.

**Bilinçli kapsam dışı:** E-posta ile eşleştirme (kurumsal politikası — e-posta güvenilir eşleştirme anahtarı değil).

---

## 2. Veri kaynakları

| Kaynak | Konum | Tablo / API | Okuma |
|--------|--------|-------------|--------|
| **Legacy Kalite** | `192.168.20.30` | MySQL `kalite.users` | SSH + `kalite_ro` (SELECT only) |
| **MngKeeper (prod)** | `192.168.20.8:5040` | `GET /keeper/api/User` (sayfalı) | Bearer token (`odak_admin`) |

### Legacy `users` alanları (kullanılan)

| Alan | Açıklama |
|------|----------|
| `id` | Birincil anahtar |
| `username` | Giriş kullanıcı adı |
| `name`, `surname` | Ad / soyad (bazen ikinci ad soyad alanında) |
| `email` | Rapor **üretiminde kullanılmaz**; referans amaçlı export edilir |
| `status` | `1` = aktif, `0` = pasif |

### Keeper alanları (kullanılan)

| Alan | Açıklama |
|------|----------|
| `userId` | Mongo `@users.__dataId` |
| `username` | Keycloak / AD `sAMAccountName` veya local |
| `firstName`, `lastName` | Ad / soyad |
| `isActive` | Aktiflik |
| `provisioningSource` | `Local` veya `Directory` (AD sync) |

---

## 3. Eşleştirme süreci

### 3.1 Akış

```
① Legacy: SSH → mysql JSON export (kalite.users)
② Keeper: prod token → GET /keeper/api/User (tüm sayfalar)
③ Script: normalize + eşleştir (sıralı kurallar)
④ Çıktı: JSON + CSV + MD (reports/ + LATEST.md)
```

### 3.2 Eşleştirme kuralları (öncelik sırası)

| Sıra | Kod | Kural |
|------|-----|--------|
| 1 | `username` | Normalize edilmiş kullanıcı adı (küçük harf, trim) |
| 2 | `name_exact` | Tam ad metni — `name + surname` vs `firstName + lastName` (Türkçe karakter toleranslı ASCII fold) |
| 3 | `name_first_last` | İlk ad kelimesi + son soyad kelimesi (evlilik / ikinci ad toleransı) |

**Çoklu aday:** Aynı anahtara birden fazla Keeper kaydı düşerse — aktif + `sAMAccountName` benzeri kısa username tercih edilir; hâlâ belirsizse `ambiguous` kategorisi.

**E-posta:** Kullanılmaz.

### 3.3 Rapor kategorileri

| Kategori | Anlam |
|----------|--------|
| **matched** | Legacy ↔ Keeper eşleşmesi |
| **legacy_only** | Yalnızca Kalite DB’de |
| **keeper_only** | Yalnızca Keeper’da (AD duplicate / yeni personel / servis hesabı olabilir) |
| **ambiguous** | Birden fazla Keeper adayı; manuel inceleme |

---

## 4. Script ve komutlar

**Script:** [`scripts/tests/MngKeeper/users/compare-legacy-kalite-users.ps1`](../../../scripts/tests/MngKeeper/users/compare-legacy-kalite-users.ps1)

**Önkoşullar:**

- `Posh-SSH` modülü
- Legacy sunucuya SSH (`192.168.20.30`, kullanıcı `odak`)
- Prod Keeper token: `docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1` (veya `$env:TEMP\operationcore_dg_token_prod.txt`)

**Tam koşu (canlı veri):**

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG
.\scripts\tests\MngKeeper\users\compare-legacy-kalite-users.ps1
```

**Cache ile hızlı yeniden üretim** (aynı gün içinde veri değişmediyse):

```powershell
.\scripts\tests\MngKeeper\users\compare-legacy-kalite-users.ps1 -SkipLegacyFetch -SkipKeeperFetch
```

**Parametreler (opsiyonel):**

| Parametre | Varsayılan | Not |
|-----------|------------|-----|
| `-KeeperBaseUrl` | `http://192.168.20.8:5040` | Prod |
| `-LegacyServer` | `192.168.20.30` | Legacy Kalite |
| `-OutputDir` | `docs/odak/eskiapp/reports` | Çıktı klasörü |

---

## 5. Çıktı dosyaları

| Dosya | Açıklama |
|-------|----------|
| `reports/legacy-keeper-user-compare_YYYYMMDD_HHmmss.json` | Tam makine okunur rapor |
| `reports/legacy-keeper-user-compare_YYYYMMDD_HHmmss.csv` | Excel / filtre |
| `reports/legacy-keeper-user-compare_YYYYMMDD_HHmmss.md` | Zaman damgalı MD |
| **`reports/legacy-keeper-user-compare_LATEST.md`** | **Her koşuda güncellenen son rapor** |
| `reports/legacy-users-cache.json` | Legacy ham export |
| `reports/keeper-users-cache.json` | Keeper ham export |

---

## 6. Sonuç özeti (2 Temmuz 2026 — prod)

**Koşu:** `2026-07-02 22:19:21` · Legacy `111` · Keeper `122`

| Metrik | Değer |
|--------|------:|
| **Toplam eşleşen** | **14** |
| — username ile | 13 |
| — tam ad ile | 1 |
| — ad+soyad (ilk/son kelime) ile | 0 |
| Sadece Legacy’de | 97 (aktif: **14**, pasif: 83) |
| Sadece Keeper’da | 109 (aktif: 62; muhtemel AD duplicate: **57**) |
| Belirsiz | 0 |

Tam tablolar: **[reports/legacy-keeper-user-compare_LATEST.md](./reports/legacy-keeper-user-compare_LATEST.md)**

---

## 7. Eşleşen kullanıcılar (14)

| Eşleştirme | Legacy kullanıcı | Legacy ad | L. | Keeper kullanıcı | Keeper ad | K. | Kaynak |
|------------|------------------|-----------|-----|------------------|-----------|-----|--------|
| username | ikuru | İnci Doğru Kuru | Aktif | ikuru | İnci Kuru | Aktif | Directory |
| username | okaragul | Osman Karagül | Aktif | okaragul | Osman Karagül | Aktif | Directory |
| username | mkucuk | Murat Küçük | Aktif | mkucuk | Murat Küçük | Aktif | Directory |
| username | eyenicelik | Esra Yeniçelik | Aktif | eyenicelik | Esra Yeniçelik | **Pasif** | Directory |
| username | ahusan | Anıl Hüsan | Aktif | ahusan | Anıl Hüşan | Aktif | Directory |
| username | agezer | Ahmet Emin Gezer | Aktif | agezer | Ahmet Emin Gezer | Aktif | Directory |
| username | mcolak | Miray Çolak | Aktif | mcolak | Miray Çolak | Aktif | Directory |
| username | mcolak | MİRAY NUR ÇOLAK | Pasif | mcolak | Miray Çolak | Aktif | Directory |
| username | enalbat | Nurşah Elif Nalbat | Aktif | enalbat | Elif Nalbat | Aktif | Directory |
| **name_exact** | **cngulten** | Cansu Nur Gülten | Aktif | **cgulten** | Cansu Nur Gülten | Aktif | Directory |
| username | mboztepe | Merve Boztepe | Aktif | mboztepe | Merve Boztepe | Aktif | Directory |
| username | haydin | Hanife Özcan Aydın | Aktif | haydin | Hanife Aydın | Aktif | Directory |
| username | egayret | Eren Gayret | Aktif | egayret | Eren Gayret | Aktif | Directory |
| username | akutluca | Ayhan Kutluca | Aktif | akutluca | Ayhan Kutluca | Aktif | Directory |

**Notlar:**

- `mcolak`: Legacy’de iki kayıt (aktif + pasif), aynı Keeper hesabına bağlandı.
- `cngulten` → `cgulten`: Tek **ad-soyad** eşleşmesi; username farklı.
- `eyenicelik`: Legacy aktif, Keeper pasif — durum uyumsuzluğu.

---

## 8. Kritik listeler

### 8.1 Sadece Legacy’de — aktif (14) · öncelikli inceleme

Keeper’da karşılığı bulunamayan **aktif** legacy hesaplar:

| Legacy kullanıcı | Ad soyad |
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
| saydin | Sadi Aydın |
| adguzel | Arhan Doruk Güzel |
| gkarakus | Gülce Karakuş |

Pasif 83 kayıt → [LATEST rapor § Pasif legacy](./reports/legacy-keeper-user-compare_LATEST.md).

### 8.2 Sadece Keeper’da — dikkat edilecekler

- **~57 muhtemel AD duplicate:** username boşluk içerir (`ahmet emin gezer`) veya makine hesabı (`pc-001$`, `krbtgt` vb.) — eşleşen kişinin CN kaydı; `sAMAccountName` kaydı ayrı satırda zaten **matched** olabilir.
- **Yeni / legacy’de yok örnekler:** `fkosger`, `haktas`, `kbardakci`, `ksengul`, `ayildiz`, `ckoc`, `o.ozcan` (legacy `oozger` farklı username).
- **Local / break-glass:** `odak_admin`, `serkan.meral`, `test.user1`…

Detay tablolar → [LATEST rapor § Sadece Keeper](./reports/legacy-keeper-user-compare_LATEST.md).

---

## 9. Bulgular (özet)

1. **Düşük kesişim:** 111 legacy vs 122 Keeper → yalnızca **14 bire bir eşleşme** (çoğu AD username ile).
2. **Legacy arşiv ağırlıklı:** 97 legacy-only’nin **83’ü pasif** — ayrılmış personel; migrasyon dışı bırakılabilir.
3. **Aktif legacy-only (14):** AD sync / Keeper kapsamı veya username farkı (ör. `oozger` vs `o.ozcan`) — **manuel AD kontrolü** gerekir.
4. **Keeper şişkinliği:** AD federation CN-tabanlı ikinci kayıtlar raporu şişiriyor; Keycloak LDAP mapper (`sAMAccountName`) gözden geçirilmeli ([POC_KEYCLOAK_LDAP.md](../ldap/POC_KEYCLOAK_LDAP.md)).
5. **Ad-soyad eşleştirmesi sınırlı:** Tam ad yalnızca 1 ek eşleşme (`cngulten`/`cgulten`); ilk/son kelime kuralı bu koşuda 0 ek sonuç verdi.

---

## 10. Planlanan / açık sonraki adımlar

| # | Konu | Durum | Öneri |
|---|------|--------|-------|
| 1 | Aktif legacy-only (14 kişi) | ⏳ Açık | AD’de var mı; Keeper sync OU kapsamı genişletilmeli mi? |
| 2 | Keeper AD duplicate (~57) | ⏳ Açık | Keycloak mapper düzeltmesi; CN kayıtlarını rapor dışı filtre |
| 3 | Pasif legacy (83) | Karar bekliyor | Migrasyon / eşleme dışı arşiv |
| 4 | Eşleşen 14 kişi | ⏳ Açık | MonitraNG oturum doğrulama; isteğe bağlı `legacyUserId` mapping |
| 5 | Legacy `admin` | Bilinçli ayrı | Keeper `odak_admin` — bire bir eşleme gerekmez |
| 6 | Rapor yenileme | Periyodik | AD sync veya personel değişiminden sonra script yeniden koş |

**Karar bekleyen sorular:**

- Aktif legacy-only personel Keeper’a **eklenecek mi**, yoksa legacy okuma-only kalacak mı?
- Eşleşme tablosu uygulama verisine (`@users` custom alan veya ayrı dataset) **yazılacak mı**?
- AD duplicate temizliği **IT / Keycloak** tarafında mı yoksa rapor filtresi yeterli mi?

---

## 11. İlgili dokümanlar

| Doküman | İçerik |
|---------|--------|
| [LEGACY_KALITE_OVERVIEW.md](../siparis/LEGACY_KALITE_OVERVIEW.md) | Eski uygulama menü / veri modeli |
| [USER_SOURCES.md](../ldap/USER_SOURCES.md) | Keeper Local vs Directory |
| [POC_KEYCLOAK_LDAP.md](../ldap/POC_KEYCLOAK_LDAP.md) | AD sync, username mapper |
| [SERVER_ACCESS.md](./SERVER_ACCESS.md) | Legacy sunucu SSH / DB |

---

*Bu belge, kullanıcı karşılaştırma oturumu (2 Temmuz 2026) sonucunda oluşturulmuştur. Güncel ham rapor her script koşusunda `reports/legacy-keeper-user-compare_LATEST.md` dosyasına yazılır; özet rakamlar değişirse §6–§8 bu belgeden veya LATEST’ten senkronize edilmelidir.*
