# Siber Güvenlik Çözümü — Planlama

**Durum:** Taslak — görüş ve karar için  
**Son güncelleme:** 17 Nisan 2026 — Bölüm 9 (OT–IT), bölüm 10 (tespit sonrası aksiyon), bölüm 11 (Engine / Simulator planlama notları).

---

## 1. Amaç ve kapsam ayrımı

**Birincil vizyon:** Müşterinin **kendi IT / OT ortamındaki** güvenlik olaylarını ve risk sinyallerini **tespit etmek, korelasyonlamak ve uyarmak** — örneğin firewall üzerinde olağandışı trafik veya DDoS belirtileri, belirli sistemlere **yetkisiz erişim** girişimleri, şüpheli oturum veya ağ davranışı. MonitraNG zaten **izleme** (metrik, log, varlık, olay) üzerine kurulu olduğu için, güvenlik verisini aynı “görünürlük ve uyarı” çatısında sunmak doğal bir ürün genişlemesidir. Bu, klasik anlamda tam bir SIEM/SOC yerine geçmek zorunda değildir; başlangıçta **hedeflenen cihaz ve log kaynakları + net kullanım senaryoları** ile tanımlanır.

**İkincil (ayrıca yapılabilir):** MonitraNG **uygulamasının ve platformunun** güvenliği (kimlik, API, çok kiracılık, güvenli konfigürasyon). Bu, müşteri ortamı izlemesinden farklı bir iş kalemi olarak planlanmalıdır; ikisi karıştırılmamalıdır.

Bu belge öncelikle **birincil vizyonu** destekleyecek başlıkları listeler; platform güvenliği maddeleri referans olarak korunur.

---

## 2. Değer önerisi (özet)

| Boyut | Örnek mesaj |
|-------|-------------|
| **Görünürlük** | Varlıklar, kimlikler, olaylar ve zayıf noktalar tek çatı altında izlenir. |
| **Önleyici kontroller** | Erişim, şifreleme ve yapılandırma standartları ile yüzey küçültülür. |
| **Tespit ve müdahale** | Anomali ve güvenlik olayları için uyarı, kayıt ve süreç. |
| **Uyum ve denetim** | Politika, kanıt ve raporlama ile düzenleyici ve sözleşmesel beklentilere hazırlık. |

---

## 3. Müşteri ortamı: güvenlik izleme ve uyarı (birincil menü)

Aşağıdakiler, **müşterinin sistemleri** üzerinde veya müşterinin sağladığı veri kaynaklarından beslenen yetenekler için örünüz. Öncelik ve MVP, hangi kaynakların (firewall, IDS/IPS, endpoint, AD, bulut güvenlik grubu vb.) ilk fazda destekleneceğiyle birlikte seçilir.

### 3.1 Ağ sınırı ve trafik

- Firewall / güvenlik duvarı log ve olaylarının toplanması; politikaya aykırı akış, bilinen kötü IP, olağandışı hacim.
- DDoS ve yoğunluk saldırılarına işaret eden eşikler veya basit imza/kural setleri (cihazın sunduğu alanlar dahilinde).
- NetFlow / IPFIX veya eşdeğeri ile trafik özeti ve anomali (altyapı ve lisans uyumu şart).

### 3.2 Erişim ve kimlik (müşteri tarafı)

- Sunucu ve ağ cihazlarında **yetkisiz erişim** veya başarısız kimlik doğrulama patterinleri (ör. brute force, olağandışı saat/kaynak).
- Ayrıcalıklı hesap kullanımı veya politika dışı oturum sinyalleri (log kaynağı müşteride mevcut olmalı).

### 3.3 Uç nokta ve sunucu (opsiyonel fazlar)

- Agent veya agent’sız log toplama ile şüpheli süreç, yapılandırma değişikliği, kötü amaçlı yazılım izleri (entegrasyon derinliği ayrı planlanır).

### 3.4 Olay yaşam döngüsü

- Güvenlik uyarılarının önceliklendirilmesi, müşteri ekibine bildirim, basit playbook (ne zaman eskalasyon).
- İzlenebilirlik verisiyle birleşik zaman çizelgesi: “aynı anda ağ + sunucu + uygulama” (MVP’de kısıtlı da olabilir).

### 3.5 Ürün konumlandırması notu

Tam özellikli **SIEM** veya 7/24 **SOC hizmeti** bu belgenin kapsamı dışında varsayılmaz; ancak **güvenlik odaklı izleme ve uyarı** (bazen “SOC hafif” veya güvenlik monitoring modülü) ile başlanıp zamanla derinleştirilebilir. İhtiyaç halinde üçüncü parti SIEM / firewall / bulut güvenlik ürünleriyle **entegrasyon** ayrı bir karar konusudur.

---

## 4. Platform ve genel güvenlik başlıkları (referans)

Bu bölüm **MonitraNG’in kendisinin** ve genel BT güvenliğinin sıkılaştırılması içindir; müşteri IT izlemesi ile karıştırılmamalıdır. Başlıklar bir **menü** gibi düşünülmelidir.

### 4.1 Kimlik ve erişim yönetimi (IAM) — platform

- Çok faktörlü kimlik doğrulama (MFA), güçlü parola ve oturum politikaları.
- Rol tabanlı erişim (RBAC), ayrıcalıklı hesapların ayrıştırılması ve minimum yetki ilkesi.
- API ve servis hesapları için anahtar döngüsü, kapsam sınırları ve denetim izi.
- Tek oturum (SSO) ve kurumsal dizin entegrasyonu (ihtiyaca göre).

### 4.2 Ağ ve sınır güvenliği — platform

- Segmentasyon, güvenlik grupları ve yalnızca gerekli port/protokol erişimi.
- TLS sertifika yaşam döngüsü, güvenli kanal zorunluluğu (ör. MQTT/HTTP üzerinde şifreleme).
- Ters proxy, rate limiting ve kötüye kullanım azaltma (mevcut altyapı ile uyumlu tasarım).

### 4.3 Veri güvenliği

- Beklemede ve aktarımda şifreleme; hassas alanların sınıflandırılması.
- Yedekleme, geri yükleme testleri ve saklama süreleri.
- Log ve iz kayıtlarında kişisel/hassas veri minimizasyonu.

### 4.4 İzleme, olay ve müdahale — operasyon

- Güvenlikle ilgili logların toplanması, korelasyon ve saklama süresi.
- Kritik olaylar için uyarı kuralları ve eskalasyon (Monitoring / observability ile örtüşebilir).
- Olay müdahale çerçevesi: sınıflandırma, iletişim, kök neden ve iyileştirme.

### 4.5 Uygulama ve tedarik zinciri güvenliği

- OWASP odaklı tehdit modeli ve güvenli geliştirme pratikleri.
- Bağımlılık ve konteyner görüntü taraması; bilinen CVE’ler için süreç.
- Gizli bilgilerin kod dışında yönetimi (vault, ortam değişkenleri, sırlar rotasyonu).

### 4.6 Altyapı ve operasyonel güvenlik

- Patch ve sürüm yönetimi; kritik güncellemeler için SLA.
- Erişim günlükleri, yapılandırma değişikliklerinin izlenmesi.
- Felaket kurtarma ve iş sürekliliği senaryoları.

### 4.7 Uyumluluk, risk ve üçüncü taraflar

- ISO 27001, NIST, KVKK / GDPR benzeri çerçevelere göre kontrol listeleri ve gap analizi (kapsam ayrıca tanımlanır).
- Alt yüklenici ve API sağlayıcı riskleri; veri işleme sözleşmeleri.

### 4.8 Güvenlik testleri ve doğrulama

- Düzenli güvenlik taramaları, penetrasyon testi ve düzeltme takibi.
- Güvenli konfigürasyon baseline’ları ve sapma tespiti.

### 4.9 Farkındalık ve süreç

- Kullanıcı ve yönetici eğitimleri, kimlik avı simülasyonları.
- Güvenlik açığı bildirimi (responsible disclosure) ve iletişim kanalı.

---

## 5. MonitraNG bileşenleriyle ilişki (taslak)

Bu bölüm, platformu bölmeden **nerede dokunulabileceğini** gösterir; bağlayıcı mimari karar değildir. **Müşteri IT güvenlik izleme** vizyonu için özellikle toplama, saklama ve kural motoru hatları önemlidir.

| Alan | Olası bağlantı |
|------|----------------|
| **MngEngine / Agent** | Müşteri ağındaki cihazlardan syslog, SNMP, özel API veya dosya ile güvenlik logu / metrik toplama (mevcut izleme kanallarıyla örtüşebilir). |
| **MngReactor** | Ingest, normalizasyon, olay üretimi; güvenlik veri akışının platforma girmesi. |
| **MngDataGateway** | Güvenlik olayları, uyarılar ve raporlar için dataset’ler; sorgu ve dashboard beslemesi. |
| **MngWorkflow** | Eşik ve kural tabanlı uyarı, bildirim (e-posta, webhook, ticket) zinciri. |
| **MngKeeper** | Müşteri kullanıcıları için kimlik, yetki ve denetim izi (platform tarafı). |
| **Mng.Ui** | Güvenlik panelleri, kural yönetimi, müşteri operatörüne uyarı akışı. |
| **Gateway / API** | Oran sınırlama, kimlik doğrulama, müşteri API entegrasyonları. |
| **Observability** | Platform sağlığı; güvenlik pipeline’ının izlenmesi. |

İleride bu başlıklardan biri ürünleştiğinde, ilgili servis için `docs/content/{ServiceName}/support/...` altında ayrıntılı teknik belge açılabilir.

---

## 6. Önerilen fazlar (yüksek seviye)

Öncelik **müşteri ortamı güvenlik izleme** ise aşağıdaki sıra daha doğal olabilir; platform sıkılaştırması paralel veya sonraki fazda yürütülebilir.

| Faz | Odak | Örnek çıktılar |
|-----|------|----------------|
| **0 — Çerçeve** | Hedef senaryolar (ör. firewall + kritik sunucu), veri kaynakları listesi, MVP tehdit modeli | Bu belge, RACI, hangi cihaz/üretici ilk fazda |
| **1 — Toplama ve saklama** | Seçilen kaynaklardan güvenlik logu/olayının güvenilir biçimde alınması, saklama süresi | Collector / ingest taslağı, DG şema veya olay modeli |
| **2 — Tespit ve uyarı** | Eşikler, korelasyon kuralları, DDoS / yetkisiz erişim gibi örnek kullanım | Kural seti MVP, dashboard veya alarm listesi |
| **3 — Süreç ve platform** | Olay müdahalesi özeti, müşteri eğitimi; **paralelde** MonitraNG IAM/TLS/sırlar sıkılaştırması | Playbook özeti, kontrol listesi |

Fazlar müşteri ve iç kapasiteye göre yeniden sıralanabilir.

---

## 7. Karar gerektiren konular

- İlk fazda hangi **müşteri kaynakları** (firewall markası, syslog, bulut güvenlik ürünü, AD vb.)?
- Hedef: **tam SIEM** mi, yoksa **belirli senaryolar** (sınır trafiği + kritik sunucu girişi) ile sınırlı ürün mü?
- Veri **müşteri şebekesinde mi** kalacak (on-prem), **hibrit** mi, **tamamen barındırılan** mı?
- Regülasyon ve sertifikasyon beklentisi (ör. SOC 2, ISO) var mı?
- 7/24 **SOC hizmeti** sizin mi sunulacak, partner mi, yoksa ürün yalnızca **uyarı ve kayıt** mı?

Bu maddeler netleştikçe belge güncellenmeli ve gerekiyorsa `docs/content/security/` altına ek dokümanlar (ör. tehdit modeli, kontrol matrisi) eklenebilir.

---

## 8. Sonraki adımlar

1. Bu taslak üzerinde ekip içi görüşme: **müşteri güvenlik izleme** ile **platform güvenliği** ayrı gündem maddeleri.  
2. İlk faz için 2–3 **somut senaryo** (ör. firewall log + uyarı, belirli sunucularda başarısız SSH denemesi) ve “MVP güvenlik paketi” tanımı.  
3. Seçilen senaryolar için toplama yolu (Engine/Reactor/DG) ve teknik spike planı.  
4. **Tespit sonrası müdahale** ihtiyacı: bölüm 10’daki modlar (uyarı / onaylı / otomatik) ve müşteri firewall entegrasyon gerçekçiliği.  
5. **MngEngine** toplama ve **MngSimulator** ile test senaryoları: bölüm 11 ve `monitoring_plans/MONITORING_SIMULATOR.md` ile hizalama.

---

## 9. OT–IT sınırı — öncelikli senaryolar ve MVP veri kaynakları

**Odak:** Üretim / OT ile kurumsal / IT dünyasının birbirine bağlandığı katman: **DMZ**, **jump host / bastion**, **firewall veya katman-3 ayrımı** ile ayrılmış **zonlar** (ISA/IEC 62443 dilinde conduit ve zone düşüncesiyle uyumludur). Amaç, OT’ye doğrudan agresif tarama yapmadan **sınır trafiği, politika ihlalleri ve sınır üzerinden erişim** üzerinden görünürlük sağlamaktır.

### 9.1 Senaryo tablosu (özet)

| Senaryo | Ne olur? | Tipik veri kaynağı | MVP’de yapılabilecek (tespit / uyarı) |
|---------|----------|-------------------|--------------------------------------|
| **S1 — Yasak protokol OT tarafına** | IT segmentinden OT’ye SMB, RDP, genel web trafiği vb. taşınmaması gereken akış | DMZ / internal firewall **deny veya allow log**, NetFlow/IPFIX özeti | İki VLAN/zone arasında **beklenmeyen port/protokol**; politika ihlali alarmı |
| **S2 — Tek doğru kapı (jump host)** | Yönetim yalnızca bastion üzerinden olmalı | Bastion **auth log**, VPN / erişim cihazı logu | Başarısız giriş patteri; bastion dışından doğrudan OT adresine erişim denemesi (log üretiliyorsa) |
| **S3 — Bakım penceresi dışı erişim** | Planlı duruş dışında OT veya SCADA’ya oturum | AD / VPN / mühendis istasyonu logları | Zaman penceresi dışı oturum uyarısı (takvim müşteri ile tanımlanır) |
| **S4 — Sınırda trafik sıçraması** | DDoS veya anormal hacim sınırı etkiler | Firewall **trafik/olay log**, mümkünse NetFlow | Eşik aşımı; hedef IP/port özeti |
| **S5 — Yeni veya bilinmeyen kaynak** | OT’ye ilk kez konuşan IT adresi veya tersi | Firewall **yeni oturum / ilk paket** özellikleri (varsa), ARP/ND tablosu güncellemeleri (kaynak sınırlı) | “Bu kaynak IP daha önce bu conduit’ta yoktu” (baseline sonrası) |
| **S6 — Konfigürasyon / kural değişikliği** | Firewall veya yönlendiricide yetkisiz kural eklenmesi | Cihaz **yapılandırma değişiklik log**, syslog öncelikli | Değişiklik olayı + kullanıcı / oturum bilgisi (cihaz destekliyse) |

### 9.2 MVP için önerilen sıra (OT–IT önceliği)

1. **S1 + S4:** En çok anlatılabilir değer; firewall/NetFlow kaynağı netleştirilir.  
2. **S2:** Jump host logu — erişim yolunun tekilleştirilmesi ile uyumlu.  
3. **S3:** Müşteri ile “izinli pencereler” tanımı gerekir.  
4. **S5:** Baseline gerektirir; yanlış alarm riski yüksek; pilot segmentte denenir.  
5. **S6:** Cihaz API/syslog yeteneklerine bağlı; ikinci faz uygun.

### 9.3 Standartlarle ilişki (yüksek seviye)

- **IEC 62443:** Zone/conduit ayrımı, sınırda izleme ve erişim kontrolü ile örtüşür; ürününüz **görünürlük ve olay kaydı** sağlayarak müşterinin kendi uyum çalışmasını destekler; tesis veya ürün sertifikasyonu ayrı kapsamdır.

### 9.4 Teknik not

- OT içi **pasif keşif** veya **protokol derinliği** (Modbus/OPC vb.) bu tablonun ötesinde ayrı senaryo setidir; **sınır önceliği** ile karıştırılmamalıdır.

---

## 10. Tespit sonrası aksiyonlar (müdahale modları)

Tespit (ör. DDoS belirtisi, politika ihlali) sonrasında yalnızca **kayıt ve uyarı** ile yetinilmeyebilir; müşteri ihtiyacına göre **otomatik veya yarı otomatik müdahale** tanımlanabilir. Örnek: güvenlik duvarında belirli bir kaynak IP için **geçici veya kalıcı blok kuralı** eklemek.

### 10.1 Teknik olarak mümkün olan

- Çoğu firewall, WAF veya bulut güvenlik grubunda **API**, **yönetim otomasyonu** veya **yetkili script** ile kural ekleme / güncelleme mümkündür (üretici ve modele göre değişir).
- Platform tarafında olay → **aksiyon zinciri**: örneğin **MngWorkflow** ile webhook, dahili otomasyon veya müşteri ortamındaki bir **entegrasyon uç noktasına** istek (müşteri firewall API’sine erişim genelde müşteri ağı veya özel bağlantı ile sınırlıdır; mimari ayrı tasarlanır).
- Aksiyon türleri (ürün dilinde seçenek): **yalnız izle ve uyarı**, **onay sonrası blok**, **otomatik geçici blok (TTL)**, **otomatik kalıcı blok** (yüksek risk; nadiren önerilir).

### 10.2 Dikkat ve sınırlar

- **Dağıtık hizmet dışı bırakma (DDoS):** Trafik tek IP’den gelmeyebilir; **tek IP blok** her zaman etkili olmaz. Koruma bazen **ISP, scrubbing merkezi veya bulut Anti-DDoS** katmanında yapılır; firewall satırı yetersiz kalabilir.
- **Yanlış pozitif:** Otomatik blok, meşru trafiği keser (paylaşımlı çıkış IP, CDN, yanlış eşik). OT ortamında kesinti maliyeti yüksektir.
- **Değişiklik yönetimi:** Kritik tesislerde otomatik kural değişikliği için **onay**, **audit log** ve **geri alma** (rollback) beklentisi olabilir.
- **Entegrasyon çeşitliliği:** Her müşteri farklı cihaz kullanır; “her yerde tek düğme” genelde **entegrasyon kataloğu** (hangi üretici, hangi API, hangi kimlik bilgisi) ve güvenli sır yönetimi gerektirir.

### 10.3 Önerilen ürün yaklaşımı

- MVP’de öncelik: **tespit + net uyarı + playbook**; müdahale için **onaylı** veya **kısa süreli otomatik** seçenekler değerlendirilir.
- Otomatik kalıcı blok gibi agresif modlar yalnızca **açık seçim**, **test ortamı** veya **sıkı politika** ile sunulmalıdır.

---

## 11. Sonraki planlama notları (MngEngine, MngSimulator ve devam)

Bu bölüm **yer tutucu** ve iç planlama için referanstır; teknik detay ilgili servis dokümanlarında derinleştirilecektir.

| Konu | Planlama yönü |
|------|----------------|
| **MngEngine** | Müşteri veya sınır ağından **güvenlik logu / metrik / NetFlow benzeri** verinin toplanması; mevcut collector ve job modeli ile uyum. Güvenlik senaryoları için hangi protokoller ve kaynaklar öncelikli (syslog, SNMP trap, dosya tail, API poller) ayrı teknik çalışma. |
| **MngReactor** | Olay normalizasyonu, eşik sonrası olay üretimi, güvenlik pipeline’ına besleme. |
| **MngWorkflow** | Uyarı → bildirim → (isteğe bağlı) **müdahale** veya webhook zinciri (bölüm 10 ile birlikte). |
| **MngSimulator** | Test ve demo için **sentetik trafik veya log üretimi** (ör. eşik doğrulama, yanlış alarm ayarı); mevcut plan: `docs/content/monitoring_plans/MONITORING_SIMULATOR.md`, `MngSim` ile ilişki değerlendirilecek. |
| **MngDataGateway / Mng.Ui** | Güvenlik olayları ve uyarıların saklanması, pano ve kural yönetimi. |

**Not:** Engine ve Simulator işleri bu belgenin kapsamını aşan **ayrı görev ve milestone** olarak yürütülmeli; ilerleme burada yüksek seviye güncellenir.

---

## 12. İlgili dokümanlar (MonitraNG)

- Altyapı ve operasyon: `docs/content/infrastructure/`  
- Monitoring mimarisi ve gözlemlenebilirlik: `docs/content/monitoring_plans/`  
- Simulator planı (referans): `docs/content/monitoring_plans/MONITORING_SIMULATOR.md`  
- Dokümantasyon standartları: `docs/content/DOCUMENTATION_STANDARDS.md`
