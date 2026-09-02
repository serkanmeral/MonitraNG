# DLP — Planlama (origin sınıflandırma)

**Durum:** Dilim 0 bitti; Dilim 1 motor sahada; Outlook eklentisi kuruldu, Active teyidi Office lisansına park  
**Son güncelleme:** 2 Eylül 2026 (park: IT Office aktivasyonu)  
**Ortam:** Odak / geliştirme PC (Classic Outlook mevcut)  
**İlişkili:** [README.md](./README.md) · [DEVAM.md](./DEVAM.md) · [POLICY.md](./POLICY.md) · [LAB.md](./LAB.md) · DI etiketleri (`dm_tags`) · [document_intelligence](../document_intelligence/DEVAM.md) · [ldap/GROUP_SOURCES.md](../ldap/GROUP_SOURCES.md) · MngLogsAgent (`MngLogs/`) · SIEM ([siem](../siem/README.md), [monitoring](../monitoring/README.md))

---

## 1. Vizyon

MonitraNG DLP’si klasik “her dosyayı tara, kalıp yakala” ile başlamaz. Belge **Document Intelligence (DI)** içinde doğar veya oraya yüklenir; **sınıflandırma etiketi kaynakta** yapışır; etiket **dosyanın metasına** yazılır; kural bu etikete bağlanır; sahadaki **MngLogsAgent** işlemi denetler.

Bu, Microsoft Purview’daki *sensitivity label* modeline yakındır ve ürünün elindeki DI + Keeper + agent üçlüsüne oturur.

**Tek cümle ilke:** Sınıflandırma ve kural merkezi; kesici uçtadır. Uç bazen agent, bazen Outlook eklentisi, bazen DI sunucusu, ileride posta transport veya ağ geçididir.

---

## 2. Kilit kararlar (31 Ağu 2026)

| # | Konu | Karar |
|---|------|--------|
| K1 | Sınıflandırma | Mevcut `dm_tags` kataloğu; `kind = classification`. Belgede **tek birincil sınıf**. Organizasyonel etiketler ayrı kalır. |
| K2 | Kimlik | Kural **Keeper grubuna** bağlanır. AD senkronu kolaylaştırıcıdır, kaynak değildir. Keeper’da AD’de olmayan gruplar da vardır. |
| K3 | İlk kanal | **E-posta** (Classic Outlook). USB test makinesine takmak zor; USB sonraki faz. |
| K4 | Agent rolü | `MngLogsAgent` **politika motoru**. Outlook eklentisi yalnızca sensör/aktuatör. USB ve onaysız süreç aynı motoru kullanır. |
| K5 | Etki kademesi | Önce **audit**, sonra **warn** (gerekçe), sonra **block**. İlk günden kesme yok. |
| K6 | Damgasız dosya | İlk sürüm: `unclassified` = **allow + audit**. Aksi halde her rastgele ek kilitlenir. |
| K7 | Servis yerleşimi | Yeni DLP servisi yok. **Sınıf+damga = MngDocument**; **kural+yayın = MngLogCollector** (`GET /api/v1/policy/dlp`); **motor = MngLogsAgent**; kimlik = Keeper. UI: kural SIEM ailesi, sınıf seçimi DI. Detay: [POLICY.md](./POLICY.md) §1. |
| K8 | Kural çatışması | Sıralı liste, **ilk eşleşen kazanır** (`priority` küçük = önce). |
| K9 | Evaluate yeri | Outlook gönderim anında **yalnızca localhost agent**. Collector/Keeper yok. |
| K10 | Local API auth | PIN değil; `%ProgramData%\MngLogs\Agent\dlp-local.key` + `X-MngLogs-DlpKey`. |
| K11 | Outlook eklentisi | Dilim 1 hedefi Classic **COM/VSTO**. Office.js / Yeni Outlook sonra. |
| K12 | Dilim 1 kesme | `enforcementMode: auditOnly` — kural `block` olsa bile `allowSend: true`, olay `wouldBlock`. |

JSON ve HTTP sözleşmesi: **[POLICY.md](./POLICY.md)**. Lab: **[LAB.md](./LAB.md)**.

Bu tablo değişmedikçe implementasyon spekülatif kabul edilmez.

---

## 3. Terminoloji

| Terim | Anlam |
|-------|--------|
| **Organizasyonel etiket** | Arama / filtre (`proje-x`). DLP kuralı bağlanmaz. |
| **Sınıflandırma** | DLP anlamı (`dahili`, `gizli`, `kisisel-veri`). Hassasiyet sırası var. |
| **Damga (persist)** | Sınıflandırmanın DOCX/XLSX/PDF (ve isteğe bağlı NTFS ADS) içine yazılması. |
| **Kanal / action** | `email.send`, `usb.copy`, `browser.upload`, `unsanctioned.appRead`, … |
| **Onaylı süreç** | Outlook, Word, Explorer, kurumsal Teams — sınıflı dosyayı okuyabilir (kurala göre). |
| **Onaysız süreç** | WhatsApp, kişisel tarayıcı yüklemesi, Telegram — içerik incelenmez; dosya sürece kapatılır. |
| **Kesici (enforcer)** | Kararı uygulayan uç: agent, Outlook eklentisi, DI API, ileride transport/proxy. |
| **İç / dış alıcı** | Kural sözlüğündeki kurum e-posta domain listesine göre. MX kaydı değil, adres domain’i. |

**Kapsam dışı (bilinçli, ilk dilimler):** tarayıcı webmail içeriği, WhatsApp mesaj/alıcı, kişisel Gmail gövdesi, ekran görüntüsü, yazıcı, TLS inspection, mobil MDM.

---

## 4. Mimari

```text
DI (üretim / yükleme)
  → sınıflandırma (dm_tags.kind = classification)
  → damga (dosya metası)
        │
        ▼
Merkezi politika (sunucu UI)
  sınıflandırma + action + Keeper grubu + hedef/bağlam → audit|warn|block
        │  derlenmiş politika (policy.json kanalı)
        ▼
MngLogsAgent (motor)
  kimlik önbelleği: Windows kullanıcı → Keeper user + groupIds
        │
        ├── Outlook eklentisi     action: email.send      (alıcı bilinir)
        ├── (sonra) dosya+süreç   browser / WhatsApp      (alıcı bilinmez)
        ├── (sonra) USB           action: usb.copy
        └── (sonra) ağ kesicisi   aynı kural, başka uç
        │
        ▼
dlp.* olayı → SIEM
```

DI klasör yetkisi (“kim indirebilir”) ile DLP (“indirdikten sonra USB’ye / dış mail’e gidebilir mi”) **farklı kontrollerdir**. İkisi birlikte gerekir.

### 4.1 Dört katman

| Katman | Nerede | İş |
|--------|--------|-----|
| Sınıflandırma | DI + `dm_tags` | Dosyanın hassasiyeti |
| Kalıcılık | Dosya metası | Etiket DI’dan çıktıktan sonra da yaşasın |
| Politika | Sunucu DLP config | Kim, hangi kanal, hangi etki |
| Uygulama | Agent + kesiciler | Olay anında izin / uyarı / engel + kayıt |

---

## 5. Sınıflandırma modeli

Mevcut `dm_tags` alanları: `name`, `color`, `description`, `isActive`.

Sınıflandırma için eklenmesi önerilenler:

- `kind`: `organizational` \| `classification` (yoksa `organizational` — mevcut etiketler kırılmaz)
- `sensitivity`: sayı (karşılaştırma; birden fazla ekte **en yüksek kazanır**)
- `persistToFile`: sınıflandırmada varsayılan `true`

Belge kaydı:

- Organizasyonel `tags[]` aynen kalır
- **Birincil sınıflandırma tek alan** (`classificationTagId` veya eşdeğeri)

Kural motoru yalnızca birincil sınıfa bakar. `gizli` + `dahili` bir arada olmaz; tek sınıf, gerekirse en yüksek.

---

## 6. Damga (dosya metası)

Mongo’daki `tags` indirme anında kaybolur. Agent ve Outlook eki **dosyanın içindeki** damgayı okur.

| Yöntem | Rol | Zayıf nokta |
|--------|-----|-------------|
| Office custom property / PDF XMP | **Birincil** — dosyayla taşınır | Kullanıcı Farklı kaydet / damga silme |
| NTFS ADS | Windows’ta hızlı okuma | FAT32 USB, zip, e-posta, macOS’ta silinir |
| Hash / parmak izi | Sonraki faz — damga silinirse tanıma | Save As yeni hash |

**Kurallar:**

- Üretim ve indirmede damga yazılır.
- Sınıflandırma değişince DI kopyası **yeniden damgalanır**; diskte kalan eski indirme eski damgayı taşır (bilinçli borç).
- İlk kapsam: DI’nın ürettiği ve indirdiği **DOCX / XLSX / PDF**. TXT, rastgele zip, ekran görüntüsü damgalanmaz.

---

## 7. Kimlik

```text
Windows oturumu  ODAK\ali
      →  sAMAccountName / UPN eşlemesi
Keeper kullanıcı
      →  üyelikler = AD’den gelenler ∪ yalnızca Keeper’da olanlar
      →  kural groupIds[] (ad değil, id)
```

Agent **Windows TokenGroups’a güvenmez**; orada Keeper-only grup yoktur.

- Politika derlenirken grup **id**
- UI’da ad
- Online: çözüm + cache; offline: son cache, olayda `identitySource: cache`
- Aynı isimde AD ve yerel grup olabilirse bağ **id** ile

---

## 8. Politika modeli

Kural cümlesi:

> **Eğer** sınıflandırma ∈ X **ve** action ∈ Y **ve** kullanıcı bu Keeper gruplarında **değilse** (**ve** hedef/bağlam Z) → **etki**

Örnek (ilk e-posta dilimi):

| Sınıflandırma | Action | Hedef | Grup | Etki |
|---------------|--------|--------|------|------|
| `gizli` | `email.send` | dış domain | `Finans-Yoneticiler` değil | block |
| `gizli` | `email.send` | iç domain | herkes | audit |
| `kisisel-veri` | `email.send` | dış | `IK` değil | warn |
| `dahili` | `email.send` | dış | herkes | audit |

Üç kavram ayrı sözlük / ayrı kural alanı:

1. **Kanal politikası** — e-posta, USB, yazdırma, bulut, onaysız süreç
2. **Cihaz kontrolü** — USB allowlist (VID/PID/serial) — DLP değil, yanında durur
3. **Kimlik** — Keeper grupları

Hedef ve ağ bölgesi (sonraki faz) kuralı bölmez; koşul ekler (`internalEmailDomain`, `networkZone`, `processList`).

Agent’a **belge kataloğu gitmez**. Gider: sınıflandırma şeması, derlenmiş kurallar, sözlükler (iç domain, süreç listesi, USB), kimlik önbelleği veya çözüm ucu.

---

## 9. Kanal matrisi

| Action | Kesici | Alıcı / hedef bilinir mi? | İlk dilim |
|--------|--------|---------------------------|-----------|
| `email.send` | Outlook Classic eklentisi + agent | Evet (To/Cc/Bcc domain) | **Evet** (audit→warn→block) |
| `email.share` (DI) | MngDocument sunucu | Evet | Evet (sunucu, agent yok) |
| `browser.upload` | Dosya + `chrome`/`msedge` | Hayır | Sonra (önce audit olayı) |
| `unsanctioned.appRead` | Dosya + süreç listesi (WhatsApp, Telegram, …) | Hayır | Sonra |
| `usb.copy` | Agent (+ ileride filter driver) | Cihaz | Sonra |
| `network.exfil.*` | Proxy / CASB / mail gateway | Kısmen (SNI/IP) | Sonra; ayrı kesici |
| OWA / kurumsal webmail | Posta sunucusu transport | Evet | Entegrasyon fazı |
| Mobil WhatsApp | MDM/MAM | — | Ürün dışı (cümlede yazılır) |

**WhatsApp / tarayıcı Gmail:** içerik taranmaz. Sınıflı dosya onaysız sürece kapatılır (izolasyon). “Kime gitti” olayı üretilmez.

**Ağ dağıtım kısıtı:** aynı politika ailesi (**data in motion**); yeni etiket sistemi değil. İlk üründe yok.

---

## 10. E-posta dilimi (ilk kesici)

```text
Outlook (kullanıcı oturumu)
  ItemSend → ekler + To/Cc/Bcc
        → localhost agent API
MngLogsAgent
  Keeper grupları + damga (eklerin max sensitivity) + iç/dış domain
        → Allow | Warn | Block
Eklenti gönderir / gerekçe sorar / iptal eder
Agent dlp.email.* → SIEM
```

İlk bakılanlar: ek sınıflandırması, alıcı iç/dış, gönderen grupları, etki.  
İlk bakılmayanlar: gövde tarama, TCKN regex, şifreli ZIP içi, BCC oyunu.

**Yeni Outlook (Monarch)** COM eklentisini taşımaz. Geliştirme PC’sinde Classic Outlook (Office 16) yüklü — lab buna göre.

MngNotifier SMTP (Gmail / Odak SMTP / Mailu) **uygulama bildirimi**dir; DLP kuyusu değildir. Sınıflı test eki gerçek SMTP’ye verilmez.

---

## 11. Geliştirme lab’ı (bu PC)

Amaç: internete sızdırmadan iç/dış alıcı ve ek+sınıflandırma kararını görmek.

| Katman | Ne | Outlook gerekir mi? |
|--------|-----|---------------------|
| A | Agent/sunucu **simülasyon**: dosya + alıcı + kullanıcı → karar | Hayır |
| B | **smtp4dev** (veya Mailpit) — SMTP kuyusu `127.0.0.1:2525` | Hayır |
| C | Outlook **lab hesabı** `tester@dlp.internal` → SMTP localhost | Evet |

İç domain sözlüğü lab’da örn. `dlp.internal`, `odak.local`.  
`ali@dlp.internal` = iç; `dis@gmail.com` = dış; ikisi de smtp4dev’de birikir, Google’a gitmez.

Asıl M365 hesabı From olarak kullanılmaz (posta Microsoft bulutuna gider).  
Odak SMTP / Gmail / `mail.monitrang.com` DLP testinde kullanılmaz.

---

## 12. Sunucu DLP config arayüzleri

Kesmez; politikayı yazar, yayınlar, izler.

| # | Ekran | İş | İlk dalga |
|---|--------|-----|-----------|
| 1 | Sınıflandırma kataloğu | `dm_tags` `kind=classification`, sensitivity, persist | **Evet** |
| 2 | Kural listesi / editör | sınıf + action + Keeper grubu + hedef → etki; öncelik; açık/kapalı | **Evet** |
| 3 | Kanallar / action kataloğu | `email.send` vb.; çoğu kapalı veya audit | Evet (ince) |
| 4 | Sözlükler | İç e-posta domain, onaylı/onaysız süreç; sonra USB, ağ bölgesi | Domain + süreç |
| 5 | Damga ve yayın | Dosya tipleri, unclassified davranışı, derlenmiş politika sürümü, yayınla/geri al | **Evet** |
| 6 | Kimlik önizleme | Windows kullanıcı → Keeper grupları (destek) | İkinci |
| 7 | Simülasyon | Katman A’nın sunucu yüzü | **Evet** |
| 8 | Olay / istisna kuyruğu | `dlp.*` özeti; warn gerekçesi; SIEM’e de gider | İnce kuyruk |
| 9 | Kapsam cümlesi | UI’da “ne kapalı”: webmail içerik yok, ağ proxy yok | Metin |

DI belge ekranında sınıf seçimi DLP config’i **tekrar etmez**; belge UI’sı kataloğa bağlanır.

Günlük sıra: katalog → sözlük → kural → yayın → simülasyon → olaylar.

---

## 13. Faz planı

Kod yazılmadan önce bu sıra speke bağlanır. Faz adları değişebilir; bağımlılık değişmez.

### Dilim 0 — Sınıflandırma gerçeği

- `dm_tags.kind`, sensitivity, persist — **kod**
- Belgede birincil `classificationTagId` — **kod**
- Üretim / indirmede DOCX/XLSX/PPTX/PDF damgası — **kod** ([POLICY.md](./POLICY.md) §7)
- DI paylaşımında (`email.share`) sunucu tarafı kural kancası → **Dilim 1/2’ye alındı** (politika motoru yokken audit kancası bağlanamaz)

Canlı DG şema PATCH ayrı adım (script hazır).

### Dilim 1 — Politika + e-posta audit

- Kural nesnesi + sözlük (iç domain)
- Derlenmiş politika → agent (`policy.json` kanalı)
- Kimlik: Windows → Keeper grup cache
- Outlook eklentisi: ItemSend → agent; **yalnızca audit** (gönderim kesilmez)
- Simülasyon UI veya agent local evaluate
- `dlp.email.*` SIEM
- Geliştirme PC lab (smtp4dev + lab hesabı)

### Dilim 2 — Soft enforce (e-posta)

- Warn (gerekçe) ve block
- Unclassified allow+audit doğrulanmış olsun
- DI `email.share` block (sunucu)

### Dilim 3 — Onaysız süreç (tarayıcı / WhatsApp)

- Süreç listesi sözlüğü
- Sınıflı dosyanın `chrome`/`msedge`/`WhatsApp.exe` tarafından okunması → önce audit, sonra block
- Alıcı yok; olay: kullanıcı + süreç + sınıf + etki

### Dilim 4 — USB / cihaz kontrolü

- Allowlist, kopya audit, sonra kesme (filter driver ayrı teknik karar)

### Dilim 5 — Zenginleşme (DLP standardı)

- Hash / parmak izi, gövde tarama, pano, yazıcı, ağ kesicisi, OWA transport, istisna onay akışı, mobil MDM notu (entegrasyon)

---

## 14. Mevcut yapı (baseline)

| Bileşen | DLP’ye katkısı | Durum |
|---------|----------------|--------|
| `dm_tags` + belge `tags` | Org etiket + `kind=classification` + `classificationTagId` | Dilim 0 kod |
| DI üretim / indirme | Damga yazılır (Office custom.xml / PDF yorum) | Dilim 0 kod |
| Keeper grupları (Local ∪ Directory) | Kural kimliği | Var; AD sync + yerel grup |
| MngLogsAgent `policy.json` | Politika dağıtım kanalı | Var (metrik/eventlog); DLP yok |
| `WindowsLoggedOnUsers` | İnteraktif oturum | Var |
| Outlook Classic (dev PC) | İlk kesici lab | Yüklü |
| MngNotifier / Mailu / Odak SMTP | Bildirim; DLP değil | Ayır |

---

## 15. Açık konular (kilitlenmedi)

Kilitlenenler (K7–K12): [POLICY.md](./POLICY.md).

Hâlâ açık:

1. ~~Damga şeması~~ — kilit: [POLICY.md](./POLICY.md) §7
2. Linux agent: ilk dilim Windows; Linux kapsam dışı (cümle net, kod yok)
3. Fail-open vs fail-closed agent-down — Dilim 1 fail-open; Dilim 2 bayrağı
4. Ortak motor kütüphanesi adı (`DlpEngine` nerede derlenir: Agent.Core vs küçük shared proje)

---

## 16. Bilinçli vaat (ürün cümlesi)

**İlk teslim:** Classic Outlook masaüstü + DI sunucu paylaşımı; origin sınıflandırma; Keeper grup kuralı; audit-first.

**Yok:** tarayıcı webmail içeriği, WhatsApp alıcı/içerik, ağ proxy DLP, USB kesme, mobil.

Bu cümle config UI’da ve satışta aynı kalır.
