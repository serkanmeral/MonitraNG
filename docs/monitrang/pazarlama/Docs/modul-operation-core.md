# Operasyon Merkezi — Modül özellik envanteri

**Kod:** `operation-core` · **Durum:** Canlı (genişletme devam ediyor)  
**UI:** `/apps/operation-core` · **Backend:** MngOperations

**Referanslar:** [OC Faz 1 spec](../../odak/operationcore/operationcore_phase1.md) · [MngOperations MVP](../../odak/operationcore/mngoperations/MVP_CHECKLIST.md) · [Referans teklif — Üretim Operasyonu (iç)](../../odak/commercial/Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md)

> **Bu dosyanın amacı (şu an):** OC’nin **müşteri perspektifi**, ürün kimliği, temel kavramları, yetenekleri ve gerçek hayat örneklerini netleştirmek. Tam özellik envanteri zamanla genişletilecek. Broşür metinleri **henüz doldurulmayacak** — bkz. [§Broşür (ertelendi)](#broşür-ertelendi).

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Teklifte tanımlı, geliştirilmedi

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Operasyon Merkezi (OC)**, kurumun operasyonel ve iş süreçlerini **workspace** ve **WorkItem** modeli üzerinde toplayan; durum akışı, form, atama, bildirim ve denetim izi ile e-posta–Excel dışındaki işleri platformda yürüten modüldür.

### 1.2 İsim ve alternatif dil

| Bağlam | Önerilen ifade |
|--------|----------------|
| Platform / modül adı | **Operasyon Merkezi (OC)** |
| Broşür, landing, genel kitle | «Operasyon Merkezi — **kurumsal süreç ve iş yönetimi**» |
| IT / teknik kitle | «Operasyon Merkezi — ticket, olay ve runbook süreçleri» |
| Üretim / kalite / tedarik | «Operasyon Merkezi — **iş emri ve süreç takibi**» |
| Yönetim / IK / onay süreçleri | «**Süreç yönetimi** ve görev merkezi» (OC alt başlığı ile) |

**Operasyon Merkezi** adı; NOC/SOC/operasyon odası çağrışımıyla **canlı operasyonları** vurgular. Aynı modül, pazarlama ve satış dilinde **süreç yönetimi**, **iş akışı yönetimi** veya **görev & kayıt merkezi** olarak da anlatılabilir — özellikle IT dışı departmanlarda «operasyon merkezi» ifadesi yabancı gelebilir; bu durumda **«MonitraNG Operasyon Merkezi ile süreçlerinizi merkezileştirin»** formülü kullanılır.

> OC bir **genel amaçlı süreç platformudur**; referans müşteri tekliflerinde yalnızca **Üretim workspace** dikey dilimi ayrı paket olarak geçebilir (§9). IT helpdesk, onay, bakım vb. aynı motorla kurulabilir — ayrı teklif kalemi zorunlu değildir.

### 1.3 OC ne değildir?

| Beklenti | OC gerçeği |
|----------|------------|
| Sabit kodlu «helpdesk yazılımı» | **Metadata + runtime kurallar** — süreç workspace bazında yapılandırılır |
| Tam BPMN / iBPM suite (Camunda, Signavio…) | **Hafif süreç motoru** — state flow, form, kural, bildirim; ağır çizelgeleme/MES değil |
| Proje portföy yönetimi (MS Project, Jira epik hiyerarşisi) | **WorkItem odaklı** — kanban/board, timeline; derin PPM Faz 1 dışı |
| E-posta istemcisi / sohbet | **Kayıt + süreç** — iletişim Notifier kanallarına bağlanır |
| Excel’in yerine geçen tablo | **Form + durum + iz** — raporlama ayrı modül (Raporlama) |

---

## 2. Müşteri perspektifi

> **Hedef kitle:** Satış, broşür, landing ve «MonitraNG nedir?» anlatımı. Teknik terimler (RuntimeContext, rule engine…) burada **kullanılmaz**; bkz. §4–§5.

### 2.1 Tek paragraf (broşür / sunum)

**Operasyon Merkezi**, kurumunuzdaki operasyonel ve iş süreçlerini e-posta ve Excel’den çıkarıp tek platformda toplar. Her talep, görev, onay veya iş emri numaralı bir **kayıt** olarak açılır; sorumlu atanır, aşamalar izlenir ve tüm geçmiş **denetlenebilir** kalır. IT desteğinden üretim emrine, kalite kaydından yönetim onayına — aynı altyapı, sizin süreçlerinize göre kurulur. MonitraNG’nin belge, izleme ve otomasyon modülleriyle birlikte çalışarak «olay oldu» ile «iş tamamlandı» arasındaki boşluğu kapatır.

### 2.2 Günlük deneyim — müşteri ne görür?

| Adım | Müşteri dili |
|------|----------------|
| 1 | **İş açılır** — arıza, talep, onay, iş emri, kalite kaydı… |
| 2 | **Form doldurulur** — öncelik, açıklama, ilgili kişi (sürece göre) |
| 3 | **Kuyruk / panoda görünür** — kimde, hangi aşamada |
| 4 | **Durum ilerler** — örn. Yeni → Atandı → İşlemde → Kapalı |
| 5 | **Geçmiş kalır** — yorumlar, sistem notları, zaman çizelgesi |
| 6 | **Gerekirse haber gelir** — e-posta, uygulama içi, Telegram |

**Özet:** Operasyon Merkezi = kurumsal **iş ve süreç defteri** + **görev panosu** + **denetlenebilir geçmiş**.

Aynı modül farklı departmanlarda farklı isimle anlatılabilir: operasyon ekibi «Operasyon Merkezi», IK/finans «**süreç ve görev yönetimi**» — ürün aynıdır.

### 2.3 MonitraNG içindeki yeri

Müşteri OC’yi **ayrı bir ada** gibi görmemeli:

| Bağlantı | Müşteri cümlesi |
|----------|-----------------|
| **Döküman Zekası** | İş kaydına belge eklenir; gerektiğinde belgeden inceleme kaydı açılır |
| **Monitoring** | Alarm veya sensör, ilgili iş emrinde görünür veya kayda not düşer |
| **Zamanlama** | Periyodik kontroller otomatik iş olarak açılabilir |
| **Workflow** | Karmaşık çok adımlı otomasyon; OC **insanın gördüğü kayıt merkezi** |
| **Raporlama** | Özet panolar OC’de; kurumsal raporlar Raporlama modülünde |

**Kısa formül:** Diğer modüller **olay** üretir; Operasyon Merkezi o olayın **insan tarafından yürütüldüğü** yerdir.

### 2.4 Müşteriye net sınırlar

| Beklenti | Gerçek |
|----------|--------|
| Hazır «kutudan çıkar IT yazılımı» | Süreciniz **kurulumda tanımlanır**; esnek, hazır paket değil |
| Tam fabrika / MES çizelgeleme | İş emri ve takip evet; ağır üretim planlama ayrı sınıf |
| Sohbet veya e-postanın yerine geçer | **Kayıt + süreç** merkezi; iletişim bildirimlerle desteklenir |
| Tüm raporlar burada | Operasyon panoları OC’de; kurumsal rapor **Raporlama**’da |

### 2.5 Yapılandırılabilir davranış — pazarlama derinliği

Zamanlanmış işler, kurallar, bildirim politikaları ve workspace otomasyonları **ürünün parçasıdır**; global dokümanda **sonuç diliyle** geçmeli, admin ekranı adı adım **ertelenir**.

| Konu | Müşteriye anlatılır mı? | Nasıl anlatılır (fayda) | Detay envanteri |
|------|-------------------------|-------------------------|-----------------|
| **Zamanlanmış işler** | ✅ Evet | «Her Pazartesi 09:00 bakım kontrol listesi otomatik açılır» | §6.3 · §5.6 |
| **Kurallar** | ✅ Evet (örneklerle) | «Eksik açıklama ile kapatılamaz», «Kritik öncelikte yönetici alanını doldur» | §6.3 · §5.2 |
| **Bildirim politikaları** | ✅ Evet | «Atanınca sorumluya mail; kapanınca talep sahibine bildirim» | §6.3 · §5.3 |
| **Otomatik işler (workspace otomasyonu)** | 🔶 Kısmen | «Belirli geçişte alan dolsun / bildirim gitsin» — **ileri** satış konusu | §5.6 *(genişletilecek)* |
| **Admin: tanım ekranları** | ⏸️ Hayır (broşür) | «MonitraNG ekibi / süreç sahibi kurar» yeterli | İç teknik doküman |
| **Workflow vs OC kuralı** | 🔶 İhtiyaç halinde | «Tek kayıt içi kurallar OC; çok sistemli zincir Workflow» | §8 |

**Karar (v0.2):** Global pazarlama dokümanında **§2.5 tablosu + §6.3 örnek cümleler** yeterli; ayrı «Admin kurulum rehberi» veya ekran envanteri **broşür netleşince** §5 altında genişletilir.

---

## 3. Amaç ve çözdüğü problem

### 3.1 Sorun (Problem)

Kurumlar operasyonel işleri çoğunlukla **dağınık kanallarda** yürütür:

- «Şu konuyu mail at» — kim ne yaptı, hangi sürüm geçerli, SLA tutuldu mu belirsiz
- Excel / WhatsApp / paylaşımlı klasör — **tek doğruluk kaynağı yok**
- Alarm veya belge olayı oluşur; **insan müdahalesi** ayrı sistemde takip edilmez
- Onay ve kalite adımları **denetlenebilir iz** bırakmaz
- Departmanlar farklı araç kullanır; platform modülleri (Monitoring, DI, SIEM) **operasyonel karar** katmanına bağlanamaz

### 3.2 Amaç

OC’nin amacı:

1. **Operasyonel ve iş süreçlerini** tek platformda **kayıt altına almak**
2. **Durum, atama, form ve timeline** ile süreci görünür kılmak
3. Diğer MonitraNG modüllerinden gelen olayları **WorkItem’a dönüştürmek** veya süreci tetiklemek
4. Kurallar ve bildirimlerle **tekrarlanabilir, denetlenebilir** davranış sağlamak
5. Workspace modeliyle **aynı tenant’ta farklı süreç domain’lerini** (IT, üretim, onay, geri bildirim…) yan yana çalıştırmak

### 3.3 Çözüm (özet)

Operasyon Merkezi, **MngOperations** backend’i ve **DataGateway** metadata’sı üzerinde çalışır. Her **workspace** bir süreç alanını tanımlar (hangi iş tipleri, durumlar, formlar, board’lar geçerli). Kullanıcı ve sistem **WorkItem** oluşturur; backend **RuntimeContext** ile hangi alanın görünür/readonly olduğuna, hangi geçişlerin mümkün olduğuna karar verir. Her anlamlı adım **timeline**’a işlenir; gerekirse **Notifier** ile e-posta/Telegram/in-app bildirim gider.

**Tasarım ilkesi:** *Backend decides, UI renders* — iş kuralları UI’da değil, metadata + rule engine’de.

---

## 4. Temel kavramlar

| Kavram | Kısa tanım | Örnek |
|--------|------------|-------|
| **Workspace** | Süreç domain’i; tip/durum/form/board konfigürasyonunun kapsayıcısı | `IT Destek`, `Üretim`, `Onaylar`, `Geri Bildirim` |
| **WorkItem** | Süreçteki tekil kayıt — görev, olay, emir, talep, onay | `TSK-00042`, `URE-00108` |
| **Work item tipi** | Kaydın sınıfı ve form şablonu | Incident, Service Request, Üretim emri, NCR |
| **State (durum)** | Yaşam döngüsü konumu | Yeni → Atandı → İşlemde → Kapalı |
| **Transition (geçiş)** | Durum değişimi; kurallar ve timeline tetikler | `assign`, `resolve`, `close` |
| **Board / kuyruk** | WorkItem listesi veya kanban görünümü | «Açık olaylar», «Kalite kuyruğu» |
| **Form / alan** | Yapılandırılabilir veri girişi | Öncelik, atanan, müşteri, sensör ref. |
| **Timeline** | Değişmez denetim izi — geçiş, yorum, sistem olayı | «14:32 Alarm notu eklendi» |
| **Kural (rule)** | Default (zenginleştir) veya validation (reddet) | Boş açıklama ile kapatılamaz |
| **RuntimeContext** | UI’ya sunulan tek doğruluk — izin, görünürlük, aksiyonlar | Profil sayfasındaki «Çöz» butonu |

**Kimlik üretimi:** WorkItem anahtarları workspace prefix ile üretilir (`TSK-00001`, `URE-00022`).

**Tip kategorileri (semantik gruplama):** `incident`, `service_request`, `problem`, `change`, `task`, `operational` — raporlama ve UI gruplama için; state kategorisi (`open` / `in_progress` / `closed`) ayrı eksendir.

---

## 5. Öne çıkan yetenekler

### 5.1 Süreç ve kayıt yönetimi

| Yetenek | Durum | Not |
|---------|-------|-----|
| Workspace tanımı (tip, durum, alan, board) | ✅ | DG `op_*` dataset’leri + admin UI |
| WorkItem oluşturma / güncelleme | ✅ | Form runtime |
| Durum geçişi (state flow) | ✅ | Transition key + pipeline |
| Kanban / liste board | ✅ | Board runtime + predefined query |
| WorkItem profil + timeline | ✅ | Denetim izi |
| Yorum ekleme | ✅ | Timeline activity |
| Workspace bazlı anahtar (`PREFIX-SEQ`) | ✅ | |
| Çoklu workspace (aynı tenant) | ✅ | IT Destek + Üretim + Geri Bildirim paralel |
| Workspace ağacı (parent/child) | 🔲 | Model hazır; Faz 1 tek seviye |

### 5.2 Form, yetki ve kurallar

| Yetenek | Durum | Not |
|---------|-------|-----|
| Dinamik form alanları (lookup, person, tarih…) | ✅ | Field behavior backend’de — bkz. [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md) §4.2 |
| Katmanlı alan politikası (workspace → board → state) | ✅ | Most restrictive wins |
| Validation / default kuralları | ✅ | Rule engine |
| Geçiş öncesi/sonrası kural pipeline | ✅ | |
| SLA hesabı (temel) | 🔶 | Foundation var; working-hours escalation 🔲 |
| Onay akışları (OC approvals) | 🔶 | UI + admin; derin entegrasyon Workflow ile 🔲 |

### 5.3 Bildirim ve olaylar

| Yetenek | Durum | Not |
|---------|-------|-----|
| In-app bildirim | ✅ | `op_notifications` |
| E-posta (Notifier üzerinden) | ✅ | Template key + politika |
| Telegram | 🔶 | Platform Notifier ile; OC politikaları genişletilebilir |
| RabbitMQ `oc.events` publish | ✅ | Workflow / tüketici 🔲 |
| `from-origin` (Scheduler / dış modül tetik) | ✅ | Zamanlanmış WorkItem |

### 5.4 Görünürlük ve raporlama yüzeyi

| Yetenek | Durum | Not |
|---------|-------|-----|
| Dashboard runtime | ✅ | Workspace dashboard — bkz. [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md) |
| Predefined query çalıştırma | ✅ | Board/liste verisi |
| OC içi rapor modülü | ❌ | **Raporlama** modülü ayrı |
| Export (Excel/PDF) | 🔲 | Raporlama / DI ile |

### 5.5 Platform entegrasyonları (özet)

| Yetenek | Durum | Not |
|---------|-------|-----|
| DI — kalem belgesi / deep link | ✅ | WorkItem profilinde |
| DI — otomatik WorkItem tetik (AI/kural) | 🔲 | **F-FILE-TRIGGER / D-DOC-TRIGGER** |
| Monitoring — emir kartında metrik | 📋 | Üretim workspace referans senaryosu |
| Monitoring — alarm → emre not | 📋 | Referans paket — standart |
| SIEM — olay → WorkItem | 🔲 | SIEM modülü köprüsü |
| Workflow — adım / orkestrasyon | 🔲 | MngWorkflow plan |
| Scheduler — periyodik WorkItem | ✅ | OC admin scheduled-jobs |

### 5.6 Yapılandırma katmanı — müşteri dili ↔ teknik (özet)

Broşürde **fayda**; envanterde **yetenek**; kurulumda **admin adımları**.

| Müşteri duyar | Teknik karşılık | Durum |
|---------------|-----------------|-------|
| «Her hafta aynı kontrol açılsın» | Scheduler → `from-origin` WorkItem | ✅ |
| «Kapatmadan önce çözüm yazılsın» | Validation rule (geçiş) | ✅ |
| «Atanınca mail gitsin» | Bildirim politikası + Notifier | ✅ |
| «Alan otomatik dolsun» | Default rule | ✅ |
| «Alarm olunca kayda not düşsün» | Modül entegrasyonu / otomasyon | 🔶–📋 |
| «Geçişte başka sistemde API çağır» | Workflow veya workspace otomasyonu | 🔲 |

---

## 6. Gerçek hayat örnekleri

Örnekler **sektörden bağımsız** süreç tiplerini gösterir; aynı OC motoru farklı workspace’lerle kurulur.

### 6.1 Günlük senaryolar (kısa)

| # | Senaryo | Workspace / tip | OC’nin rolü |
|---|---------|-----------------|-------------|
| 1 | Çalışan «yazıcı çalışmıyor» yazar | IT Destek / Incident | Kayıt, atama, SLA, çözüm timeline |
| 2 | Fırın sıcaklığı eşiği aşıldı | Üretim / Üretim emri | Monitoring metrik görünür; alarm emre not düşer |
| 3 | Tedarikçi sözleşmesi PDF yüklendi, AI «gizli» etiketledi | — → OC | DI kuralı inceleme WorkItem açar *(plan)* |
| 4 | Ay sonu kontrol listesi her Pazartesi 09:00 | Bakım / Task | Scheduler `from-origin` ile WorkItem |
| 5 | Müşteri şikâyeti e-postadan geldi | Müşteri Hizmetleri / Talep | Form, atama, kapanış onayı |
| 6 | Yeni çalışan onboarding checklist | IK / Task | Adım adım durum; sorumlu atama |
| 7 | SIEM’de kritik güvenlik olayı | SOC / Operational | Olay kaydı, analist atama, not zinciri |
| 8 | Capex harcama onayı 50.000 TL üzeri | Onaylar / Change | Form + onay geçişi; timeline denetim |
| 9 | Kalite mühendisi NCR açar | Üretim / NCR | Ayrı kuyruk; kök neden alanları |
| 10 | MonitraNG kullanıcısı hata bildirir | Geri Bildirim / Task | Prod seed workspace |

### 6.2 Sektörel tablo

| Sektör | Örnek workspace | Tipik WorkItem | Platform köprüsü |
|--------|-----------------|----------------|------------------|
| **Üretim / kompozit** | Üretim, Kalite | Üretim emri, NCR, bakım iş emri | Monitoring sensör, DI CoC |
| **Bankacılık** | Operasyon, Uyum | Olay kaydı, değişiklik talebi, inceleme | SIEM, Raporlama |
| **Lojistik** | Depo operasyonu | Sevkiyat istisnası, hasar kaydı | Monitoring (soğuk zincir), DI irsaliye |
| **Savunma / proje** | Proje operasyonu | Aksiyon maddesi, teslimat onayı | DI resmi yazı, Workflow |
| **Kamu / belediye** | Vatandaş talebi | Başvuru, şikâyet | Dış portal → HTTP flow → WF → OC *(plan)* |
| **Sağlık (operasyonel IT)** | Klinik sistem destek | Incident, erişim talebi | Monitoring altyapı |
| **Enerji / tesis** | Bakım | İş emri, planlı duruş | Monitoring asset, Scheduler |

### 6.3 «Süreç yönetimi» ve otomasyon — anlatım cümleleri

Broşür veya satış görüşmesinde:

- **«Onay süreçlerinizi tek yerde toplayın»** — Onaylar workspace, geçiş, timeline
- **«İş emirlerinizi Excel’den çıkarın»** — Üretim / bakım workspace, board, atama
- **«Olaydan aksiyona tek zincir»** — Monitoring / belge / güvenlik olayı → kayıt → kapanış
- **«Denetimde hazır olun»** — Kim, ne zaman, ne yaptı — zaman çizelgesi
- **«Periyodik işler unutulmasın»** — Zamanlanmış görevler otomatik açılır *(§2.5)*
- **«Kurallar süreci gevşetmesin»** — Eksik bilgi ile ilerlenemez *(§2.5)*
- **«Doğru kişi haberdar olsun»** — Atama, eskalasyon, kapanış bildirimleri *(§2.5)*

---

## 7. Kimler kullanır?

| Rol | Tipik kullanım |
|-----|----------------|
| **Operasyon / NOC / SOC** | Olay triage, runbook adımları, vardiya devri |
| **IT / helpdesk** | Incident, talep, problem, change |
| **Üretim / kalite** | Emir takibi, NCR, hat durumu |
| **Bakım / tesis** | İş emri, planlı bakım checklist |
| **Yönetim / uyum** | Onay kuyruğu, inceleme kaydı |
| **Süreç sahibi / admin** | Workspace tanımı, akış, kural, board |
| **Platform entegrasyonu** | Scheduler, Workflow, DI/Monitoring olayları |

---

## 8. Platform bağlantıları

| Modül | OC ile ilişki | Örnek |
|-------|---------------|-------|
| **Döküman Zekası** | WorkItem’a belge bağlama; olaydan WorkItem tetik *(plan)* | CoC PDF profil panelinde; AI etiket → inceleme kaydı |
| **Monitoring** | Metrik görünürlük; alarm → not / tetik | Üretim emri kartında sensör şeridi |
| **SIEM** | Güvenlik olayı → operasyon kaydı | SOC workspace triage |
| **Raporlama** | OC verisi rapor kaynağı | Açık incident sayısı, SLA ihlali |
| **Workflow** | Çok adımlı orkestrasyon; OC adım hedefi | Onay zinciri + dış HTTP |
| **Scheduler (omurga)** | Periyodik WorkItem oluşturma | Haftalık kontrol görevi |
| **Notifier (omurga)** | E-posta, Telegram, in-app | Atama bildirimi |
| **Keeper + DG (omurga)** | Kimlik, metadata, veri | `op_*` dataset’leri |

**Tetik yönleri** (modül haritası ile uyumlu):

```text
Zamanlama (Scheduler)     ──► OC WorkItem oluştur
Modül olayı (DI alarm…)   ──► OC WorkItem / geçiş  [kısmen plan]
Workflow adımı            ──► OC kayıt güncelle   [plan]
Dış HTTP flow             ──► Workflow → OC       [plan]
```

---

## 9. Referans teklif eşlemesi — Üretim Operasyonu (iç kullanım)

Referans müşteri tekliflerinde OC **genel platform** olarak değil, **Üretim Operasyonu** paketi olarak anlatılabilir:

| Teklif maddesi | OC karşılığı |
|----------------|--------------|
| Üretim workspace | 1 workspace kurulumu |
| Dinamik süreç | Tip, durum, form, board |
| Üretim emri yaşam döngüsü | State flow örneği |
| İsteğe bağlı NCR kuyruğu | İkinci tip / board |
| Monitoring köprüsü | Profil embed + alarm notu |
| Wow seti (canlı şerit, vardiya özeti…) | UI + Notifier |
| O8 opsiyon | Süreç tetiki / NCR taslak |
| **Kapsam dışı** | Tam MES; IT helpdesk teklif dışı açıkça belirtilmiş |

> Ürün perspektifinde OC **horizontal** modüldür; üretim workspace = **referans dikey implementasyon** örneğidir.

---

## 10. Teknik referans (iç kullanım)

| Alan | Konum |
|------|--------|
| Backend | `MngOperations/` |
| Metadata | DG `op_*` — [datasets README](../../odak/operationcore/datasets/README.md) |
| UI | `Mng.Ui/pages/apps/operation-core/` |
| Referans workspace’ler | IT Destek, Geri Bildirim, OC Demo — [reference/](../../odak/operationcore/reference/) |
| API | [API_SURFACE.md](../../odak/operationcore/mngoperations/API_SURFACE.md) |

---

## Broşür (ertelendi)

Landing / broşür metinleri özellik envanteri genişleyene kadar **doldurulmayacak**. Taslak: [platform-tanitimi.md § Operasyon Merkezi](./platform-tanitimi.md)

---

## Görseller (bekleyen)

| Dosya | Açıklama |
|-------|----------|
| `../Files/oc-ekran-board.png` | Board / kanban |
| `../Files/oc-ekran-profil-timeline.png` | WorkItem profil + timeline |
| `../Files/oc-ekran-workspace-admin.png` | Workspace tanımları |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · Ürün kimliği v0.2 (§2 müşteri perspektifi)*
