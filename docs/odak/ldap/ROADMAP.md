# LDAP entegrasyonu — Roadmap

**Ortam:** Odak POC — `192.168.20.20` (`monitrang`)  
**Hedef:** Kurumsal LDAP veya Active Directory ile MonitraNG kimlik akışını birleştirmek  
**Active Directory:** `LDAP://192.168.20.3:389/DC=odak,DC=local`  
**Son güncelleme:** 25 Mayıs 2026  
**Durum:** **Odak Faz K (K1–K5 + G1) tamamlandı** — geliştirme **duraklatıldı**  
**Güncel özet:** [DEVAM.md](./DEVAM.md) · Detay: [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)

---

## 1. Amaç

Müşterilerin mevcut dizinlerindeki kullanıcı ve gruplarla MonitraNG’ye giriş ve yetkilendirme yapabilmesi:

- Ayrı parola yönetimi minimumda (tercihen kurumsal hesap)
- Domain (tenant) bazında izolasyon korunur
- Mevcut **MngKeeper + Keycloak realm** modeli mümkün olduğunca korunur
- Mongo `@users`, DataGateway `persons`, menü izinleri LDAP gruplarıyla uyumlu hale gelir

---

## 2. Mevcut durum (baseline)

```
Kullanıcı → Mng.Ui / MngDomainUI
         → MngKeeper POST /api/auth/token (username, password, domain)
         → Keycloak realm = domain adı
         → JWT (domain_name, domain_id, user_groups, isAdmin, …)
         → Mongo mng_{domain}/@users (Keeper ile senkron)
```

| Bileşen | LDAP ile ilişki |
|---------|------------------|
| **Keycloak** | Her domain = ayrı realm; kullanıcılar şu an realm içinde **yerel** veya pipeline ile oluşturulan admin |
| **MngKeeper** | Kullanıcı CRUD Keycloak + Mongo; federated kullanıcı için ek senkron kuralları gerekir |
| **Mng.Ui** | `domain@username` veya domain seçimi + şifre; LDAP’a özel UI yok |
| **MngReactor** | Mimari dokümanda LDAP/OpenLDAP modülü geçiyor; **repo’da LDAP kodu yok** |
| **Odak sunucu** | Keycloak: `http://192.168.20.20:8080/keycloak/` |

**Referans:** [DOMAIN_OLUSTURMA.md](../domain/DOMAIN_OLUSTURMA.md), [MngKeeper Architecture Guide](../../content/MngKeeper/support/architecture/ARCHITECTURE_GUIDE.md)

---

## 3. Mimari karar

**Seçilen yol:** Keycloak User Federation (AD → Keycloak) + **MngKeeper** KC→Mongo (endpoint, login) + **MngScheduler** periyodik sync ([SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md)).

**Admin / manager grupları:** Müşteri LDAP’ına müdahale yok; listeler **`mng_keeper.domains`** dokümanında (`settings.directoryPrivileges`) — [IMPLEMENTATION_PLAN §5.5](./IMPLEMENTATION_PLAN.md#55-ayrıcalık-grupları--keeper-içi-eşleme-karar-yol-2).

Detaylı iş sırası: **[IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)**.

### Seçenek A — Keycloak User Federation (birincil yol) ✅

LDAP/AD, **domain realm’ine** Keycloak “User federation” (LDAP provider) olarak bağlanır.

| Artı | Eksi |
|------|------|
| Standart OIDC/JWT akışı değişmez | Realm başına LDAP bağlantı ayarı |
| MngKeeper token endpoint’i aynı kalabilir | Grup eşlemesi (LDAP → Keycloak group) dikkat ister |
| Keycloak Admin UI ile POC hızlı | İlk girişte `@users` senkronu ayrıca tanımlanmalı |

**Akış (hedef):**

```
LDAP/AD → Keycloak (federation) → MngKeeper token → JWT → Uygulamalar
                ↓ (event veya periyodik / login hook)
         Mongo @users + grup üyelikleri
```

### Seçenek B — MngKeeper doğrudan LDAP bind

Keeper login öncesi LDAP’ta doğrulama; başarılıysa Keycloak’ta kullanıcı oluştur/güncelle veya “shadow user” senkronu.

| Artı | Eksi |
|------|------|
| İnce ayar Keeper’da | Keycloak ile çift kaynak riski |
| Federation’dan bağımsız POC | Daha fazla özel kod ve test |

### Seçenek C — MngReactor LDAP modülü (tamamlayıcı)

Dizin **arama**, toplu import veya operasyonel senaryolar (monitoring / asset ile ilişkili kimlik) için Reactor üzerinden LDAP sorgusu.

| Artı | Eksi |
|------|------|
| Mimari plânla uyumlu | Auth akışından ayrı faz; güvenlik sınırları net olmalı |

**Önerilen sıra:** Önce **A**, gerekirse **C**; **B** yalnızca federation’ın müşteride mümkün olmadığı durumda.

---

## 4. Kapsam dışı (şimdilik)

- SCIM tam otomatik yaşam döngüsü (create/disable) — sonraki iterasyon
- Çoklu LDAP forest / trust karmaşık topolojiler — ayrı müşteri projesi
- MngDomainUI üzerinden LDAP sihirbazı — Faz 4 sonrası değerlendirme
- Parola sıfırlama LDAP üzerinden (AD self-service) — müşteri politikasına bağlı

---

## 5. Faz planı

### Faz K (Odak — öncelikli) — Onaylanan iş paketi

| Kod | İş | Sahip |
|-----|-----|--------|
| **K1** | Keycloak: AD federation + **manuel** LDAP sync (bir kerelik / gerektiğinde) | ✅ Ops |
| **K2** | MngKeeper: `POST` endpoint — KC sync (opsiyonel tetik) + **KC → Mongo** (kullanıcı, grup, üyelik) | ✅ |
| **K3** | **MngScheduler** — domain listesi + Keeper sync POST (Keeper’da Quartz yok) | ✅ Deploy |
| **K4** | MngKeeper: **Login** sonrası kullanıcı tutarlılık kontrolü + gerekirse tek kullanıcı sync | ✅ |
| **K5** | **Local vs Directory:** `DirectoryUserFieldSets`, `fieldPolicies`, UI + API guard ([USER_SOURCES.md](./USER_SOURCES.md)) | ✅ |
| **G1** | Grup Local / Directory + domain manuel sync UI | ✅ |
| **K2–K3** | **Tek aktif tam sync:** manuel istek sürerken job atlanır; job sürerken manuel → **409** uyarı | ✅ |

> Mevcut `POST /api/sync/users|groups|all` yalnızca Keeper → DataGateway içindir; LDAP işi için **yeni** endpoint gerekir.  
> Eşzamanlılık detayı: [IMPLEMENTATION_PLAN.md §5.4](./IMPLEMENTATION_PLAN.md#54-eşzamanlılık--tek-aktif-tam-sync-zorunlu).

---

### Faz 0 — Keşif ve gereksinimler (1–2 hafta)

**Çıktılar:** Gereksinim dokümanı (`REQUIREMENTS.md`, bu klasörde açılacak), müşteri LDAP özeti

| # | Görev | Sorumlu | Durum |
|---|--------|---------|--------|
| 0.1 | Müşteri dizin tipi: AD mi, OpenLDAP mi, TLS zorunluluğu | Ürün / ops | ⬜ |
| 0.2 | Bind DN, base DN, user/group objectClass, filtre örnekleri | Müşteri IT | ⬜ |
| 0.3 | Giriş kimliği: `sAMAccountName`, `uid`, `userPrincipalName`? | Ürün | ⬜ |
| 0.4 | Grup eşlemesi: AD security group → MonitraNG `user_groups` / menü izinleri | Ürün | ⬜ |
| 0.5 | Domain başına ayrı LDAP mı, tek LDAP + farklı base DN mi? | Mimari | ⬜ |
| 0.6 | Offline kullanıcı / yerel admin fallback (break-glass) | Güvenlik | ⬜ |

**Karar kaydı:** Bu faz sonunda Seçenek A/B/C onayı ve Odak lab LDAP tipi.

---

### Faz 1 — Lab ortamı (Odak veya docker-compose) (1 hafta)

**Çıktılar:** Test LDAP, Keycloak federation POC notları (`POC_KEYCLOAK_LDAP.md`)

| # | Görev | Durum |
|---|--------|--------|
| 1.1 | OpenLDAP veya Samba AD test container (mng_common yanında veya ayrı compose) | ⬜ |
| 1.2 | Test kullanıcıları ve grupları (ör. `ldap-user`, `ldap-admins`) | ⬜ |
| 1.3 | Odak’ta test realm (`odak-ldap-test`) — üretim `odak` realm’ine dokunmadan | ⬜ |
| 1.4 | `ldapsearch` / Keycloak “Test connection” doğrulama | ⬜ |
| 1.5 | TLS: StartTLS / LDAPS sertifika stratejisi (dev’de gevşek, prod sıkı) | ⬜ |

---

### Faz 2 — Keycloak User Federation (2–3 hafta)

**Çıktılar:** Realm şablonu, federation ayar checklist’i, başarılı login + JWT claim doğrulaması

| # | Görev | Durum |
|---|--------|--------|
| 2.1 | Realm başına LDAP provider yapılandırması (connection, bind, search scope) | ⬜ |
| 2.2 | Username attribute ↔ login form (`domain` + kullanıcı adı) uyumu | ⬜ |
| 2.3 | Group mapper: LDAP grup → Keycloak group → JWT `user_groups` | ⬜ |
| 2.4 | Mevcut custom claim mapper’lar (`domain_id`, `isAdmin`, …) federated user’da test | ⬜ |
| 2.5 | İlk giriş / periyodik import stratejisi (Keycloak sync vs manual) | ⬜ |
| 2.6 | Domain Creation Pipeline: yeni domain’de LDAP federation opsiyonel bayrak | ⬜ |
| 2.7 | Dokümantasyon: Keycloak Admin adımları (ekran görüntülü kısa rehber) | ⬜ |

**Doğrulama:**

```text
ldap-user + parola → MngKeeper /api/auth/token → 200 + accessToken
JWT içinde domain_name, user_groups dolu
```

---

### Faz 3 — MngKeeper ve veri katmanı senkronu (2–3 hafta)

**Çıktılar:** Federated kullanıcıların Mongo `@users` ve DataGateway ile tutarlılığı

| # | Görev | Durum |
|---|--------|--------|
| 3.1 | Login veya token sonrası `@users` upsert (Keycloak `sub` ↔ `keycloakUserId`) | ⬜ |
| 3.2 | Grup üyeliklerinin Keeper grupları / menü permissions ile hizası | ⬜ |
| 3.3 | `isAdmin` / `is_manager`: LDAP grubundan mı, Keeper attribute’tan mı? | ⬜ |
| 3.4 | Kullanıcı güncelleme: LDAP’ta değişen ad/e-posta → senkron periyodu | ⬜ |
| 3.5 | Devre dışı LDAP hesabı → Keycloak disable → MonitraNG erişim kapanması | ⬜ |
| 3.6 | Mevcut yerel kullanıcılarla çakışma (aynı username) politikası | ⬜ |

---

### Faz 4 — UI ve kullanıcı deneyimi (1–2 hafta)

**Çıktılar:** Mng.Ui login metinleri, hata mesajları, ops dokümantasyonu

| # | Görev | Durum |
|---|--------|--------|
| 4.1 | Login sayfası: “Kurumsal hesabınızla giriş” / domain seçimi metinleri (TR) | ⬜ |
| 4.2 | LDAP/KC hata kodları → anlamlı Türkçe mesajlar | ⬜ |
| 4.3 | Profil sayfası: “Kaynak: LDAP” göstergesi (opsiyonel) | ⬜ |
| 4.4 | MngDomainUI: domain oluştururken “LDAP federation etkin” seçeneği (ileri faz) | ⬜ |
| 4.5 | `docs/odak/ui` veya ldap altında operatör runbook | ⬜ |

---

### Faz 5 — MngReactor LDAP modülü (opsiyonel, 2+ hafta)

**Önkoşul:** Faz 2–3 tamam; iş gereksinimi net

| # | Görev | Durum |
|---|--------|--------|
| 5.1 | Gereksinim: hangi senaryolar Keeper federation ile yetmiyor? | ⬜ |
| 5.2 | `ILdapService` / arama API tasarımı (read-only bind) | ⬜ |
| 5.3 | Gateway route `/reactor/api/v1/ldap/*` ve yetkilendirme | ⬜ |
| 5.4 | Rate limit, audit log, hassas alan maskeleme | ⬜ |

---

### Faz 6 — Odak doğrulama ve müşteri rollout (1 hafta + müşteri penceresi)

| # | Görev | Durum |
|---|--------|--------|
| 6.1 | Odak `odak` realm’de müşteri LDAP’ına read-only bağlantı testi | ⬜ |
| 6.2 | Pilot kullanıcı grubu (5–10 kişi) UAT | ⬜ |
| 6.3 | Rollback: federation kapatınca yerel admin ile giriş | ⬜ |
| 6.4 | `MNG_COMMON_ODAK` / müşteri erişim dokümanına LDAP notları | ⬜ |
| 6.5 | Production checklist (sertifika, firewall, bind parola rotasyonu) | ⬜ |

---

## 6. Domain başına yapılandırma (taslak)

Her MonitraNG **domain** = Keycloak **realm**. LDAP entegrasyonu realm düzeyinde:

| Alan | Örnek | Not |
|------|--------|-----|
| `connectionUrl` | `ldaps://dc.customer.local:636` | Müşteriye özel |
| `usersDn` | `OU=Users,DC=customer,DC=local` | |
| `bindDn` | servis hesabı | Vault / env; repoda parola yok |
| `usernameLDAPAttribute` | `sAMAccountName` veya `uid` | Login ile eşleşmeli |
| `groupsDn` | `OU=Groups,DC=...` | Menü izinleri için kritik |
| Federation enabled | `true/false` | Domain meta veya Keeper config |

**Mongo domain meta** genişletmesi (ileride): `ldapSettings` alt nesnesi — Faz 0.5 kararına bağlı.

---

## 7. Güvenlik ve operasyon

| Konu | Beklenti |
|------|----------|
| Bind hesabı | Salt okunur + arama; mümkünse ayrı OU |
| Ağ | Odak/sunucudan müşteri LDAP portuna erişim (VPN / firewall kuralı) |
| Sertifika | LDAPS veya StartTLS; self-signed için truststore |
| Gizliler | `.env` / Docker secrets; **git’e commit yok** |
| Denetim | Başarısız bind/login logları (Keycloak + Keeper) |
| Break-glass | En az bir yerel `admin` realm kullanıcısı federation’dan bağımsız |

---

## 8. Test planı (özet)

| Senaryo | Beklenen |
|---------|----------|
| Geçerli LDAP kullanıcı + parola | Token 200, UI giriş |
| Yanlış parola | 401, Türkçe mesaj |
| LDAP kapalı / timeout | Anlamlı hata, servis çökmez |
| Kullanıcı LDAP’ta disable | Sonraki giriş reddedilir |
| Grup üyeliği değişti | JWT / menü izinleri güncellenir (senkron politikasına göre) |
| Yerel admin | Federation açıkken de giriş yapabilir |
| Yeni domain + LDAP şablonu | Pipeline veya manuel checklist tamamlanır |

---

## 9. Açık sorular

1. Müşteride **tek AD** mi var; MonitraNG domain’leri AD’de **OU** ile mi ayrılacak?
2. Login formatı: `DOMAIN\user`, `user@domain.com`, yalnızca `user` + UI domain seçimi?
3. **Guest / harici** kullanıcılar LDAP dışında kalacak mı?
4. Multi-factor: Keycloak MFA mı, AD zorunlu MFA mı?
5. MngReactor LDAP modülü **hangi ürün özelliği** için şart? (Faz 5’e girdi koşulu)
6. İlk müşteri pilotu: Odak sunucusundan müşteri LDAP’ına **network** erişimi var mı?

---

## 10. İlerleme özeti

| Faz | Ad | Durum |
|-----|-----|--------|
| **P0** | `directoryPrivileges` + `IPrivilegeGroupResolver` | ✅ |
| **K1** | Keycloak manuel AD sync (odak realm) | ✅ |
| **K2** | Keeper endpoint KC→Mongo + coordinator | ✅ |
| **K3** | MngScheduler periyodik orchestration | ✅ |
| **K4** | Login kullanıcı sync | ✅ |
| **K5** | Kullanıcı kaynağı + UI/API kısıtları | ✅ |
| **G1** | Grup kaynağı + domain sync UI | ✅ |
| 0 | Keşif (bind DN, grup OU) | ⬜ (genel ürün) |
| 1–6 | ROADMAP genel fazlar | K sonrası |

**LDAP Odak POC duraklatıldı.** Özet: [DEVAM.md](./DEVAM.md) · Opsiyonel backlog: §4.

---

## 11. Alt dokümanlar

| Dosya | Ne zaman |
|-------|----------|
| **[DEVAM.md](./DEVAM.md)** | Her geliştirme oturumu başında |
| `REQUIREMENTS.md` | Faz 0 bitince |
| `POC_KEYCLOAK_LDAP.md` | Faz 1–2 bitince |
| `RUNBOOK_OPERATIONS.md` | Faz 6 öncesi |

---

## 12. İlgili linkler

- [README.md](./README.md) — bu klasör indeksi
- [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)
- [../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) — Keycloak admin URL
- [../../content/prd.md](../../content/prd.md) — Keycloak multitenant PRD
