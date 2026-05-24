# K1 — Keycloak + Active Directory (manuel kurulum rehberi)

**Ortam:** Odak — `192.168.20.20`  
**Active Directory:** `ldap://192.168.20.3:389` · Base DN: `DC=odak,DC=local`  
**Hedef realm:** `odak` (MonitraNG domain ile aynı ad)  
**Durum:** Uygulama sırası — bu dosyayı doldurarak ilerleyin  
**Son güncelleme:** 22 Mayıs 2026

> K2 (MngKeeper → Mongo) ve UI kısıtları (K5) **K1 doğrulandıktan sonra** başlar.  
> Alan matrisi: [USER_SOURCES.md](./USER_SOURCES.md)

---

## 0. Ön kontrol listesi

| # | Kontrol | Sonuç | Not |
|---|---------|--------|-----|
| 0.1 | `192.168.20.20:8080` Keycloak Admin açılıyor | ⬜ | http://192.168.20.20:8080/keycloak/admin/ |
| 0.2 | `192.168.20.3:389` erişim (sunucudan veya PC’nizden) | ⬜ | `Test-NetConnection 192.168.20.3 -Port 389` |
| 0.3 | Realm `odak` mevcut | ⬜ | Domain Creation ile oluşturulmuş olmalı |
| 0.4 | AD **bind** hesabı (salt okunur arama) | ⬜ | IT’den: Bind DN + parola |
| 0.5 | Users OU / Groups OU (veya tüm domain) | ⬜ | Örn. `OU=Users,DC=odak,DC=local` |
| 0.6 | Pilot test kullanıcısı (AD’de parola bilinen) | ⬜ | Kullanıcı adı: ______________ |

**Keycloak Admin girişi:** `admin` + altyapı parolası — [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md).

---

## 1. Realm seçimi

1. Admin Console → sol üst realm → **`odak`** (master değil).
2. **Realm settings → General:** Realm adı `odak` olduğunu doğrulayın.

---

## 2. LDAP User Federation (Active Directory)

**Yol:** `odak` realm → **User federation** → **Add provider** → **ldap**

### 2.1 Connection / Ana ayarlar

| Alan | Önerilen değer | Sizin ortamınız |
|------|----------------|-----------------|
| UI display name | `Odak AD` | |
| Vendor | **Active Directory** | |
| Connection URL | `ldap://192.168.20.3:389` | |
| Bind type | simple | |
| Bind DN | Servis hesabı DN | |
| Bind credentials | (parola) | |
| Use truststore SPI | Only for LDAPS (dev’de genelde `false`) | |

**Test connection** ve **Test authentication** → Success olmalı.

### 2.2 LDAP arama kapsamı

| Alan | Tipik AD değeri | Sizin değeriniz |
|------|-----------------|-----------------|
| Users DN | `OU=Users,DC=odak,DC=local` veya `DC=odak,DC=local` | |
| Username LDAP attribute | `sAMAccountName` | |
| RDN LDAP attribute | `cn` | |
| UUID LDAP attribute | `objectGUID` | |
| User object classes | `person, organizationalPerson, user` | |
| Search scope | Subtree | |

### 2.3 Sync / import (ilk kurulum)

| Ayar | Değer | Not |
|------|--------|-----|
| Import users | ON | |
| Sync registrations | ON | |
| Batch size | 1000 (varsayılan) | |
| Periodic full sync | İsteğe bağlı OFF (ilk aşamada manuel) | |
| Periodic changed users sync | İsteğe bağlı OFF | |

**Kaydet.**

### 2.4 İlk manuel sync (kullanıcılar)

1. User federation → `Odak AD` → **Action** (veya provider detay)  
2. **Sync all users** (veya “Trigger full sync”)  
3. **Users** menüsünde federated kullanıcılar görünmeli (`Federation link` = `Odak AD`).

| Metrik | Değer |
|--------|--------|
| Sync sonrası kullanıcı sayısı (yaklaşık) | |
| Örnek kullanıcı `username` | |

---

## 3. Grup eşlemesi (LDAP → Keycloak groups)

AD security group’larının Keycloak **Groups** içinde görünmesi gerekir (JWT `user_groups` ve `directoryPrivileges` için grup **adlarını** not edin).

### 3.1 Group LDAP mapper

**Yol:** User federation `Odak AD` → **Mappers** → **Create** → **group-ldap-mapper**

| Alan | Önerilen | Sizin değeriniz |
|------|----------|-----------------|
| Name | `groups` | |
| Groups DN | `OU=Groups,DC=odak,DC=local` veya grupların bulunduğu OU | |
| Group object classes | `group` | |
| Membership LDAP attribute | `member` | |
| Membership attribute type | DN | |
| Mode | **LDAP_ONLY** veya READ_ONLY (AD kaynak) | |
| User roles retrieve strategy | LOAD_GROUPS_BY_MEMBER_ATTRIBUTE | |
| Member-of attribute | (boş veya `memberOf` — vendor AD’ye göre) | |
| Groups path | `/` (realm root groups) | |

**Kaydet** → federation üzerinden **Sync LDAP groups to Keycloak** (veya eşdeğer action).

### 3.2 Grup üyeliği mapper (kullanıcı ↔ grup)

Bazı kurulumlarda ek **role-ldap-mapper** / **group-membership** gerekir; AD vendor şablonu çoğu zaman `memberOf` ile üyeliği çözer. Sync sonrası:

1. **Groups** → örnek AD grubu listede mi?  
2. **Users** → pilot kullanıcı → **Groups** sekmesi → üyelikler doğru mu?

**Not edin (P0 / `directoryPrivileges` için):**

| Keycloak grup adı (CN) | Amaç (admin/manager?) |
|------------------------|------------------------|
| | |
| | |

---

## 4. Login testi (Keycloak + MngKeeper)

### 4.1 Keycloak Account Console (isteğe bağlı)

Realm `odak` → kullanıcı ile doğrudan KC login testi.

### 4.2 MngKeeper token (asıl doğrulama)

MonitraNG girişi realm = domain adı kullanır.

```http
POST http://192.168.20.20:5001/api/auth/token
Content-Type: application/json

{
  "username": "<sAMAccountName>",
  "password": "<AD parolası>",
  "domain": "odak"
}
```

| Sonuç | Beklenen |
|--------|----------|
| HTTP 200 + `accessToken` | ✅ K1.6 tamam |
| 401 | Bind/sync/login attribute kontrolü |
| Kullanıcı KC’de yok | K1.4 sync tekrar |

**UI testi:** http://192.168.20.20:3000 veya yerel `npm run dev` + `GATEWAY_URL=http://192.168.20.20:5040` — aynı kullanıcı ile giriş.

---

## 5. Bilinçli olarak yapılmayan (plan)

- Keycloak’ta grup **rename** / birleştirme yok ([IMPLEMENTATION_PLAN §5.5](./IMPLEMENTATION_PLAN.md)).
- `title` / `department` / `phone` için AD attribute mapper **zorunlu değil** (v1 uygulama alanı — [USER_SOURCES.md](./USER_SOURCES.md)).
- MngKeeper Mongo sync (K2) bu adımda **yok**.

---

## 6. Sorun giderme

### 6.1 `Could not sync users: UnknownError` (Odak — teşhis 22 Mayıs)

**Belirti:** UI `UnknownError`; Admin API `400` + `"errorMessage":"UnknownError"`.

**Tespitler (Admin API ile):**

| # | Bulgu | Etki |
|---|--------|------|
| 1 | LDAP provider **`master`** realm’de; **`odak`** realm’de federation **yok** | MonitraNG `domain: odak` ile giriş bu LDAP’ı **kullanmaz** |
| 2 | Provider `usernameLDAPAttribute` = `cn` (AD için yanlış) | Sync tutarsızlığı |
| 3 | **username** mapper `ldap.attribute` = `cn` (provider `sAMAccountName` iken) | **50 kullanıcı failed** veya UnknownError |
| 4 | **group-ldap-mapper** (Groups DN = tüm domain) | Mapper varken bazen yalnızca **UnknownError** (LDAP’a hiç çıkmadan abort) |

**Çözüm sırası (doğrulandı):**

1. Realm = **`odak`** (master değil) — federation’ı burada kurun veya taşıyın.
2. Vendor **Active Directory** → **Username LDAP attribute** = `sAMAccountName`.
3. **Mappers** → `username` mapper → LDAP attribute = **`sAMAccountName`** (cn değil).
4. İlk sync: önce **yalnızca kullanıcı** (group mapper olmadan veya devre dışı) → **Sync all users**.
5. Grup mapper: `groups.dn` = grupların OU’su (tüm `DC=...` değil); `Membership user LDAP attribute` = **`sAMAccountName`**; sorun olursa **Ignore missing groups** = ON.
6. Ayrı: **Sync LDAP groups to Keycloak** (kullanıcı sync’ten sonra).

**Not:** Keycloak 23’te **Referral** alanı Admin UI’da yok.

### 6.2 Test connection OK, **Sync all users** hata veriyor (genel)

**Neden:** Test connection/authentication çoğu zaman **bind hesabının kendisi** ile oturum açar; sync ise **Users DN** altındaki tüm kullanıcıları **listeleme** izni ister. Bind hesabında farklı (daha kısıtlı) hak olabilir.

| # | Kontrol | Ne yapın |
|---|---------|----------|
| 1 | **Hata metni** | UI’daki kırmızı uyarıyı veya Events’i kopyalayın (tam metin) |
| 2 | **Keycloak log** | Sunucuda: `docker logs keycloak --tail 100` — `LDAP`, `sync`, `Referral`, `OperationNotSupported` arayın |
| 3 | **Users DN** | Çok dar OU mu? Geçici deneme: `DC=odak,DC=local` (tüm domain; yavaş olabilir ama teşhis için iyi) |
| 4 | **Pagination** | **Enable pagination** (LDAP searching bölümü): ON; hata sürerse OFF dene |
| 4b | **Referral** | Keycloak **23 Admin UI’da yok** (varsayılan JNDI genelde ignore). Eski dokümanlardaki “Referral=ignore” bu sürümde aranmamalı |
| 5 | **Bind hesabı izni** | IT: Users OU’da **List contents / Read all properties** (en azından pilot OU) |
| 6 | **Custom user LDAP filter** | Boş veya AD için: `(&(objectCategory=person)(objectClass=user))` — fazla kısıtlayıcı filtre varsa kaldırın |
| 7 | **Edit mode** | `READ_ONLY` (AD kaynak) — `WRITABLE` sync’te ek hata üretebilir |
| 8 | **UUID LDAP attribute** | Vendor **Active Directory** ise `objectGUID` kalmalı; elle değiştirildiyse varsayılana dönün |
| 9 | **Import users** | ON olmalı |
| 10 | **Çift kullanıcı** | Aynı `username` yerel KC kullanıcısı varsa sync tek kayıtta fail edebilir — log’da `exists` benzeri |

**Sık log satırları → anlam:**

| Log / mesaj | Çözüm |
|-------------|--------|
| `ReferralException` | KC 23’te UI yok; Users DN / bind yetkisi; gerekirse IT ile ldapsearch |
| `Insufficient access rights` / `error code 50` | Bind DN yetkisi / Users DN |
| `failed to parse uuid` / `objectGUID` | AD vendor şablonuna dön, UUID = `objectGUID` |
| `OperationNotSupported` + pagination | Pagination aç/kapa |
| `javax.naming.NameNotFoundException` | Users DN yanlış |

**ldapsearch (sunucudan, bind ile kullanıcı listesi):**

```bash
ldapsearch -x -H ldap://192.168.20.3:389 -D "<BindDN>" -W \
  -b "DC=odak,DC=local" \
  "(&(objectCategory=person)(objectClass=user))" \
  sAMAccountName dn -LLL | head -30
```

Liste geliyorsa sorun büyük ihtimalle Keycloak provider ayarı (referral/pagination/filter). Liste gelmiyorsa bind/OU yetkisi.

**Geçici dar kapsam (ilk başarı için):** Tek bir OU + filtre:

- Users DN: `OU=TestUsers,DC=odak,DC=local` (örnek)
- Custom filter: `(sAMAccountName=pilot_kullanici)` — yalnızca 1 kullanıcı import; çalışınca kapsamı genişletin.

### 6.2 Diğer

| Belirti | Olası neden | Kontrol |
|---------|-------------|---------|
| Test connection fail | Firewall / bind DN | 0.2, bind hesabı |
| Kullanıcı sync 0 (hata yok) | Users DN yanlış | §6.1 ldapsearch |
| Login 401, KC’de user var | Username attribute ≠ login | `sAMAccountName` |
| Gruplar boş | Groups DN / mapper mode | §3.1 |
| Login 401, user yok | Sync yapılmadı | §2.4 |

**Sunucudan LDAP testi (SSH `odak@192.168.20.20`):**

```bash
# ldap-utils yüklüyse
ldapsearch -x -H ldap://192.168.20.3:389 -D "<BindDN>" -W -b "DC=odak,DC=local" "(sAMAccountName=<pilot>)" dn mail memberOf
```

---

## 7. K1 tamamlandı — checklist

| Kod | Tamam |
|-----|--------|
| K1.1 | LDAP provider (Active Directory) — **odak** realm |
| K1.2 | Connection + bind + Users DN |
| K1.3 | Group mapper + grup sync |
| K1.4 | Sync all users (+ groups) |
| K1.5 | Pilot user + grup üyeliği KC’de görünür |
| K1.6 | `POST /api/auth/token` 200 — ⬜ doğrulanacak |

**API doğrulama (22 Mayıs 2026):** odak realm ~56 kullanıcı, ~68 grup; örnek gruplar: `MonitraNG Users`, `Yonetim Users`, `admins`, `managers`, `Domain Admins`, …

**Sonraki adım (geliştirme):** P0 → K2 → … — [DEVAM.md](./DEVAM.md)

---

## 8. Kayıt defteri

| Alan | Değer |
|------|--------|
| Realm | `odak` |
| Federation provider ID | `aad91a31-5a8f-49b6-8dec-808586379810` |
| Sync sonrası kullanıcı sayısı | ~56 |
| Sync sonrası grup sayısı | ~68 |
| Pilot kullanıcı | (doldurun) |
| Token test (K1.6) | ⬜ |
| Notlar | Login sorununda `username` mapper = `sAMAccountName` kontrolü (provider hâlâ `cn` görülebilir) |

**MonitraNG / iş grupları (P0 `directoryPrivileges` için aday):**  
`admins`, `managers`, `MonitraNG Users`, `Yonetim Users`, `Depo Users`, `Erp Users`, `IK Users`, `Kalite Users`, `Planlama Users`, `Satin Alma Users`, `Talasli Users`, `Tasarim Users`
