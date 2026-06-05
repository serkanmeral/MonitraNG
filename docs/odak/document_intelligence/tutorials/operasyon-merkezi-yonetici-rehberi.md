# Operasyon Merkezi — Yönetici Rehberi

Bu rehber, Operasyon Merkezi'nde **çalışma alanı (workspace)** oluşturup uçtan uca yapılandırmak isteyen yöneticiler içindir. Bir çalışma alanını sıfırdan kurmanın doğru sırasını, her sekmenin ne işe yaradığını ve hangi alanların doldurulduğunu adım adım anlatır.

> **Erişim:** Yapılandırma ekranı **yönetim grubu** üyelerine açıktır. Menüden **Tanımlamalar → Workspace tanımlaması** ile ulaşırsınız. Platform yöneticileri (`admins`) tüm tanımlamalara erişebilir.

---

## 1. Önerilen kurulum sırası

Bileşenler birbirine bağlıdır; aşağıdaki sırayı izlemek en sağlıklısıdır:

1. **Sistem tanımlaması** — global katalog (durum, öncelik, tip, alan havuzu) oluşturun.
2. **Çalışma alanı oluştur** ve **Genel** ayarları yapın.
3. **Değerler** — kullanılacak tip, durum, öncelik ve alanları seçin/etkinleştirin.
4. **Etiketler** — workspace etiket kataloğunu (`op_tags`) tanımlayın (formlarda **Etiketler** alanı için).
5. **Durum akışı** — en az bir akış ve geçişleri tanımlayın; birini varsayılan yapın. Kapanış geçişlerinde **zorunlu alanlar** eklemeyi unutmayın.
6. **Formlar** — oluşturma/düzenleme formunu ve alan yerleşimini tasarlayın (kapanışta istenen alanlar forma da eklenmeli).
7. **Board'lar** — liste/Kanban panolarını akış ve forma bağlayın.
8. **Panolar (dashboard)** — isteğe bağlı özet pano widget'ları; board'a bağlayın.
9. **Politikalar**, **Kurallar**, **SLA** — katalog hazır olduktan sonra ekleyin.
10. **Zamanlanmış işler** — board ve tip tanımlı olunca kurun.

---

## 2. Çalışma alanı oluşturma

1. Üst karttaki **Workspace** seçicisinin yanındaki **Yeni workspace** düğmesine tıklayın.
2. Açılan **Yeni workspace oluştur** penceresinde alanları doldurun:

| Alan | Zorunlu | Açıklama |
| --- | --- | --- |
| **Workspace adı** | Evet | Görünen ad |
| **Workspace tipi** | Hayır | Ekip / Help desk / Operasyonel / Proje |
| **İş anahtarı prefix** | Hayır | Örn. `MNG`, `HD`, `SOC` |
| **Açıklama** | Hayır | Serbest metin |

3. **Oluştur**'a tıklayın. Çalışma alanı kodu (`key`) otomatik atanır; ekran **Genel** sekmesine geçer.

> İkon, renk, erişim grupları gibi ayrıntılar oluşturma penceresinde değil, **Genel** sekmesindedir.

---

## 3. Sekmeler ve görevleri

Yapılandırma **11 ana sekmeden** oluşur. **Değerler** sekmesinin altında 4 alt sekme bulunur.

| Sekme | Amaç |
| --- | --- |
| **Genel** | Kimlik, anahtar formatı, erişim grupları |
| **Değerler** | Tip, durum, öncelik ve alan kataloğu seçimi |
| **Etiketler** | Workspace etiket kataloğu (`labels` / `op_tags`) |
| **Durum akışı** | Geçiş tanımları (iş akışı) |
| **Formlar** | Oluşturma/düzenleme formu ve alan davranışları |
| **Board'lar** | Liste ve Kanban panoları |
| **Panolar** | Özet dashboard widget'ları ve board bağlantısı |
| **Politikalar** | Alan görünürlük/zorunluluk/varsayılan kuralları |
| **Kurallar** | Olay anında doğrulama ve otomasyon |
| **Zamanlanmış işler** | Tekrarlayan otomatik iş oluşturma |
| **SLA** | Yanıt/çözüm süre hedefleri |

Ek menü: **Tanımlamalar → Zamanlanmış job'lar** (platform geneli zamanlayıcı işleri).

---

## 4. Genel

Çalışma alanının kimliğini ve iş anahtarı kurallarını belirler.

| Alan | Açıklama |
| --- | --- |
| **Workspace kodu** | Otomatik atanır (salt okunur) |
| **Workspace adı / Açıklama** | Görünen bilgiler |
| **Workspace tipi** | Ekip / Help desk / Operasyonel / Proje |
| **İş anahtarı prefix** | Örn. `MNG`, `HD` |
| **İş anahtarı formatı** | Örn. `{prefix}-{seq:D4}` → `MNG-0001` |
| **Sıra başlangıcı** | İlk numara |

**Erişim ve yetki grupları** (Keycloak grup adları, tam eşleşme):

- **Görüntüleme grupları** — kimler workspace'i ve işleri görebilir.
- **Düzenleme grupları** — kimler iş oluşturup düzenleyebilir.
- **Yönetim grupları** — kimler tanımları değiştirebilir.

> Boş bırakılırsa çalışma alanı, yetkili tüm kullanıcılara açık olur. Değişiklikten sonra **Kaydet**'e basın.

**Örnek (MonitraNG Geri Bildirim):** yalnızca `MonitraNG Users` grubu görüntüleme ve düzenleme; `admins` yönetim.

---

## 5. Değerler (katalog)

Bu çalışma alanında kullanılacak global katalog değerlerini seçtiğiniz yerdir. Formlar ve politikalar yalnızca burada seçilenleri listeler.

| Alt sekme | Yapılan |
| --- | --- |
| **İş tipleri** | Global katalogdan tip seçimi + çalışma alanına özel tip ekleme |
| **Durumlar** | Global durum kataloğundan seçim |
| **Öncelikler** | Global öncelik kataloğundan seçim |
| **Alanlar** | Global alan havuzundan seçim + çalışma alanına özel alan ekleme |

**Tipik akış (her alt sekme için):**

1. **Global katalogdan seçim** bölümünde kullanmak istediklerinizi işaretleyin.
2. İlgili **... seçimini kaydet** düğmesine basın.
3. Gerekirse **Workspace tipi/alanı ekle** ile bu çalışma alanına özel kayıt oluşturun.

> **Önemli:** Hiç durum seçmezseniz akış ve politika ekranlarında durum listeleri boş görünür. Önce **Sistem tanımlaması** altında global kayıtların var olduğundan emin olun.

---

## 6. Etiketler

Formlardaki **Etiketler** (`labels`) alanı workspace'e özel `op_tags` kataloğunu kullanır.

- Etiket **adı**, **renk** ve isteğe bağlı **açıklama** tanımlayın.
- Örnek etiketler: MonitraNG, UI / Arayüz, API / Veri, Prod, Yetki.

---

## 7. Durum akışı

İşlerin durumlar arasında nasıl ilerleyeceğini tanımlar.

1. **Yeni akış** ile bir akış oluşturun.
2. Akış alanları: **Akış adı**, **Açıklama**, **Başlangıç state**, **Workspace varsayılan akışı**, **Aktif**, **Sıra**.
3. **Geçiş ekle** ile her geçiş için:
   - **transitionKey** ve **Etiket (UI)**
   - **Kaynak state** → **Hedef state**
   - **Zorunlu alanlar** — geçiş penceresinde kullanıcıdan istenecek alanlar (ör. `resolutionSummary`)
   - **İzinli gruplar**
4. **Kaydet**. En az bir akışı **varsayılan** işaretlemeyi unutmayın.

> **İpucu:** Yalnızca `op_rules` ile doğrulama koyup forma ve geçişe alan eklemezseniz kullanıcı hatayı görür ama alanı dolduramaz. Hem **zorunlu alanlar** hem **validation kuralı** birlikte kullanın.

---

## 8. Formlar

İş oluşturma/düzenleme ve profil **Detaylar** sekmesinin kaynağıdır.

1. **Yeni form** ile başlayın. Düzenleme penceresinin 3 sekmesi vardır:

| Sekme | İçerik |
| --- | --- |
| **Genel** | Form adı, açıklama, üst metin, açılış genişliği, **varsayılan tip/akış/durum/öncelik**, "Workspace varsayılan formu" |
| **Yerleşim** | Bölümler ve alanların 12 sütunluk ızgarada düzeni |
| **Alan politikaları** | Her alan için **Görünür / Salt okunur / Zorunlu / Maskeli / Varsayılan** |

2. **Önizleme** ile formu kaydetmeden görebilirsiniz.

> Bir formu **varsayılan** yaparsanız, board'larda yeni iş açılışında otomatik kullanılır. Kapanışta istenen pool alanları (ör. çözüm özeti) forma eklenmeli — aksi halde profil ve geçiş penceresi alanı göstermez.

---

## 9. Board'lar

Kullanıcıların işleri gördüğü liste ve Kanban panolarını oluşturur.

1. **Yeni board** → sihirbaz açılır.
2. **Adım 1 — Temel bilgiler:** **Board adı**, **Görünüm tipi** (Liste/Kanban), **Durum akışı**.
3. **Adım 2 — Yapılandırma:**
   - **Liste:** gösterilecek durumlar (**Akıştan doldur**), **tablo sütunları** (sıra, biçim, sıralanabilir, filtrelenebilir), **hesaplanan sütun**, **varsayılan sıralama**.
   - **Kanban:** **Akıştan kolonları oluştur** ile durum kolonlarını üretin.
4. **Gelişmiş ayarlar (isteğe bağlı):** varsayılan kayıt formu, profil ekranı, iş tipi, öncelik, durum; **görüntüleme/düzenleme grupları**; **bağlı özet pano**.

> Doğrulama: board adı zorunludur, kolonlar için bir **durum akışı** seçilmelidir ve en az bir kolon tanımlanmalıdır.

---

## 10. Panolar (dashboard)

Çalışma alanı hub'ında **Özet | Pano** görünümü için widget tabanlı özet panolar.

1. **Yeni pano** oluşturun (ad, varsayılan pano işareti).
2. Widget ekleyin: **özet kart**, **grafik**, **liste** vb.
3. Sorgu/scope: workspace, durum, board, atanan kişi filtreleri.
4. **Board'a bağla** — kullanıcı board seçince pano sekmesi açılır.

---

## 11. Politikalar (alan davranışı)

Alanların formlarda nasıl görüneceğini ve davranacağını belirler.

- Politika türleri: **Görünürlük**, **Salt okunur**, **Varsayılan değer**.
- Üç adımlı kurulum: **Alan seçin → Politika türü → Ne zaman (Her zaman / Koşullu)**.
- Koşullu politikalarda **Şart ekle** ile alan + karşılaştırma + değer tanımlanır (koşullar VE ile birleşir).
- **Politikayı kaydet**.

> **Politikalar ≠ Kurallar:** Politikalar formdaki görünümü; kurallar olay anındaki doğrulama/otomasyonu yönetir.

---

## 12. Kurallar (otomasyon ve doğrulama)

İş oluşturulurken, güncellenirken veya durum değişirken otomatik çalışan mantık.

1. **Yeni kural** → sihirbaz:

| Adım | İçerik |
| --- | --- |
| **Tanım** | Kural adı, açıklama, **kural türü** (Doğrulama / Varsayılan değer / Otomasyon), **ne zaman çalışsın**, öncelik sırası, açık/kapalı |
| **Kapsam** | İş tipi, board, geçerli durum, geçiş anahtarı (`transitionKey`), önceki/hedef durum |
| **Koşul** | Her zaman uygula veya yalnızca şartlar sağlanınca (`op: and` + `items`) |
| **Etki** | Doğrulama: hata mesajı · Varsayılan: alan/atanan · Otomasyon: izleyici, bildirim, e-posta, aktivite |

2. **Kuralı kaydet**.

**Örnek:** `WorkItemTransition` + `complete_from_review` → `resolutionSummary` boş ise hata.

---

## 13. SLA politikaları

Yanıt ve çözüm süre hedeflerini tanımlar.

1. **Yeni SLA politikası** açın.
2. Alanlar: **Politika adı**, **Açıklama**, **İş tipi** (boş = tüm işler), **Öncelik**, **Yanıt hedefi (dakika)**, **Çözüm hedefi (dakika)**, **Politika önceliği**, **Aktif**.
3. **Kaydet**. Hedefler iş profilinde **SLA** panelinde ve listede **SLA durumu** sütununda görünür.

---

## 14. Zamanlanmış işler

Tekrarlayan işleri (ör. periyodik kontroller) otomatik oluşturur.

1. **Yeni zamanlama** → 4 adım:

| Adım | İçerik |
| --- | --- |
| **Tanım** | Zamanlama adı, iç not, açık/kapalı |
| **Ne zaman** | Her X dakika/saat, her gün belirli saatte, haftanın seçili günleri veya **gelişmiş (cron)** + saat dilimi |
| **Hedef** | Board ve iş tipi |
| **Şablon** | İş başlığı, açıklama, **atanan** (zorunlu), öncelik |

2. **Zamanlamayı kaydet**. Sağ panelde **canlı önizleme** ile bir sonraki çalışmaları görebilirsiniz.

---

## 15. İpuçları

- **Önce global katalog:** Tip/durum/öncelik/alan kayıtları **Sistem tanımlaması** altında yoksa, çalışma alanı seçim ekranları boş kalır.
- **Varsayılan akış ve form:** Board ve yeni iş deneyiminin sorunsuz olması için en az bir akışı ve bir formu **varsayılan** işaretleyin.
- **Kapanış alanları:** Form layout + geçiş `requiredFields` + validation kuralı üçlüsünü birlikte planlayın.
- **Politika mı, kural mı?** Görünüm/zorunluluk → **Politikalar**; olay anında kontrol/otomasyon → **Kurallar**.
- **Erişim grupları:** Çalışma alanı ve board düzeyinde görüntüleme/düzenleme gruplarını ayrı ayrı verebilirsiniz.
- **Kayıtlar DataGateway üzerinden** saklanır; tanım değişiklikleri anında geçerli olur.

---

*Bu rehber Operasyon Merkezi yönetim ekranlarının güncel davranışını (Haziran 2026) temel alır.*
