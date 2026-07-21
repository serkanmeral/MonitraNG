# Fiyat Teklifi — Birleşik çalışma taslağı (İÇ)

> **İç kullanım.** Müşteriye giden sürüm: [Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md](./Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md) · Konuşma notları: [Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md](./Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md)

# Fiyat Teklifi

| | |
|:---|:---|
| **Teklif No** | ODK-FT-2026-001 |
| **Tarih** | 12 Temmuz 2026 |
| **Geçerlilik** | 30 gün |
| **Hazırlayan** | MonitraNG |
| **Müşteri** | Odak Kompozit |

---

## 1. Kapak Özeti

Bu teklif; Odak Kompozit’in MonitraNG platformu üzerinde yürütülecek **Döküman Zekası**, **Raporlama** ve **İzleme** hizmetleri ile, MonitraNG bulutunda barındırılacak **Dış Katılım Portalı (Anket)** hizmetinin kapsamını, teslimatlarını ve ticari koşullarını tanımlar.

| Kalem | Özet |
|:---|:---|
| Platform | MonitraNG |
| Dağıtım modeli | DI / Raporlama / İzleme: müşteri ortamı · Anket portalı: MonitraNG bulutu (`odak.monitrang.com`) |
| Hizmet paketleri | 4 ana başlık (DI · Raporlama · Monitoring · Dış Katılım Portalı; SIEM hariç) |
| Para birimi | USD (KDV hariç) · **Proje bedeli: 15.000 USD** |

---

## 2. Taraflar

| Rol | Unvan / Kurum |
|:---|:---|
| Hizmet sağlayan | MonitraNG |
| Hizmet alan | Odak Kompozit |
| İletişim (Müşteri) | *[Ad Soyad, unvan, e-posta, telefon]* |
| İletişim (MonitraNG) | **Serkan MERAL** · serkan.meral@outlook.com · 0532 420 67 56 |

---

## 3. Amaç ve Kapsam

Odak Kompozit’in operasyonel süreçlerinde kullanılan **dosyaların** kurumsal barındırılması ve yapay zekâ ile zenginleştirilmesi; sistem içinde üretilen **dökümanların** yönetimi; veriye dayalı **raporlama**; sunucu, veritabanı, servis, ağ ve saha cihazlarının **operasyonel izlenmesi**; fiziksel **üretim süreçlerinin** Monitoring ile entegre Operation Core workspace’inde yönetimi; müşteri ve tedarikçilere yönelik **dış anket portalı** yeteneklerinin MonitraNG üzerinde standartlaştırılması.

Bu teklif aşağıdaki dört hizmet paketini kapsar:

1. **Döküman Zekası işlemleri**
2. **Raporlama işlemleri**
3. **İzleme işlemleri**
4. **Dış Katılım Portalı (Anket)** — duyurular sonraki fazda

Kapsam dışı konular Bölüm 7’de belirtilmiştir.

---

## 4. Yapılacak Hizmetler

### 4.1 Döküman Zekası İşlemleri

MonitraNG Document Intelligence modülü üzerinde kurumsal içerik yönetimi. Bu pakette iki kavram bilinçli olarak ayrılır:

| Kavram | Tanım | Tipik işlemler |
|:---|:---|:---|
| **Dosya** | Müşterinin sisteme yüklediği içerik | Yükleme, klasör, yetki, metadata, etiket, AI zenginleştirme, arama |
| **Döküman (Belge)** | Collabora / LibreOffice ile sistem içinde oluşturulan / düzenlenen içerik (DOCX, XLSX, PPTX) | Oluşturma, düzenleme, şablon, antet/kapak, otomatik üretim, sürüm, AI |

---

#### 4.1.1 Dosyalar

Kurumun kendi dosyalarını Document Intelligence içinde merkezi olarak barındırması; klasör bazlı yetkilendirme; metadata ve etiketleme; bildirimler; yapay zekâ ile içerik zenginleştirme ve keşif.

##### A. Barındırma ve klasör yapısı

- Kurum dosyalarının DI modülü içinde barındırılması
- İstenen klasör hiyerarşisi altında düzenleme
- Desteklenen formatlarda yükleme (öncelikli: PDF, DOCX; diğer formatlar depolanabilir)

##### B. Yetkilendirme

Klasör altındaki dosyalar için rol / grup bazlı yetkiler:

- Görme
- İndirme
- Dosya ekleme

> İhtiyaç halinde silme / yönetme yetkileri proje analizinde netleştirilir.

##### C. Metadata ve manuel etiketleme

- Dosyaya özel metadata alanları (ör. proje, sipariş, revizyon, gizlilik düzeyi — alan seti analizde belirlenir)
- Manuel etiket atama (kurumsal etiket kataloğu ile)
- Metadata ve etiketler üzerinden filtreleme / arama desteği

##### D. Bildirimler

- Yeni dosya yüklendiğinde ilgili taraflara bildirim
- Yeni sürüm yüklendiğinde bildirim
- Alıcı kuralları proje analizinde tanımlanır (klasör izleyenler, yönetici, iş kaydı ilgilileri vb.)
- Kanallar: uygulama içi, e-posta; Telegram yapılandırılabilir (§4.1.2 / §4.2 ile ortak model)

##### E. Mevcut platform yeteneği (bu teklifte ayrı kalem değildir)

- Dosya / içeriğin iş kaydına bağlanması platformda mevcuttur; bu maddede yeniden fiyatlandırılmaz. Gerekirse mevcut bağlama yeteneği Dosya senaryolarında kullanılır.

##### F. Yapay zekâ yetenekleri

Yüklenen dosyaların içeriği (öncelikli PDF / DOCX) üzerinde aşağıdaki AI yetenekleri bu kapsamda yer alır:

| # | Yetenek | Açıklama |
|:--:|:---|:---|
| 1 | **Akıllı soru–cevap (RAG)** | Yetki sınırında soru sorma; cevap + kaynak dosya referansı |
| 2 | **Benzer / ilgili dosyalar** | Açılan veya seçilen dosyaya içerik benzerliğiyle ilişkili dosyaların listelenmesi |
| 3 | **Sürüm fark özeti** | Yeni sürüm yüklendiğinde önceki sürüme göre “ne değişti?” özeti |
| 4 | **Eksik / tutarsızlık uyarısı** | Tanımlı kontrol listesine göre eksik veya tutarsız bilgi uyarısı |
| 5 | **Çok dilli köprü** | Örn. Türkçe içerikten İngilizce özet / terim listesi üretimi |
| 6 | **Otomatik klasör önerisi** | Yükleme sırasında içeriğe göre hedef klasör önerisi |
| 7 | **Varlık çıkarma** | Firma, parça no, sipariş, tarih vb. yapılandırılmış alanların çıkarılması |
| — | **Otomatik etiketleme ve özet** | İçerikten etiket önerisi ve dosya özeti; arama ve keşfi destekler |

> AI çıktıları (özellikle etiketler) onaylanabilir modelde sunulabilir. Model yerleşimi (on-prem / müşteri onaylı servis) proje analizinde netleştirilir. Taranmış PDF’lerde OCR gerekebilir; OCR kapsamı ayrıca kararlaştırılır.

##### G. Arama ve varsayılan sorgular

- Metadata, etiket ve içerik zenginleştirmesine dayalı arama
- Hazır / varsayılan sorgular (ör. alakalı dosyalar, son yüklenenler, belirli etiket veya varlık içerenler)

##### H. Dosyalar — teslimatlar (özet)

- Klasör ve yetki modeli
- Metadata alan seti ve etiket kataloğu
- Bildirim kuralları
- AI zenginleştirme pipeline’ı (özet, etiket, 7 yetenek)
- Arama ve varsayılan sorgu seti
- Kullanıcı eğitimi ve kabul senaryoları

---

#### 4.1.2 Dökümanlar (Belge)

Sistem içinde **Collabora / LibreOffice** ile oluşturulan ve düzenlenen kurumsal dökümanlar. Bu teklifte **Markdown Sayfa** yer almaz (platformda mevcut özellik; ayrı kalem değildir).

##### A. Tanım ve türler

| Tür | Format | Editör |
|:---|:---|:---|
| **Belge** | DOCX | Collabora Writer |
| **Elektronik sayfa (Sheet)** | XLSX | Collabora Calc |
| **Sunum** | PPTX | Collabora Impress |

##### B. Yetkilendirme

Dosya yetkilerinden daha geniş; yetkiler ait olunan **klasörden** miras alınır:

- Görme
- İndirme
- Ekleme / oluşturma
- **Düzenleme (edit)**
- **Export**
- **Print**

##### C. Kurumsal kimlik — antet ve kapak

- İstenildiği kadar antet (letterhead) tanımı
- İstenildiği kadar kapak sayfası tanımı
- Üretilen dökümanlarda antet / kapakın otomatik uygulanması
- Şablon varsayılanı + üretim anında seçim / override

##### D. Oluşturma ve üretim kanalları

- Kullanıcıların manuel oluşturması (boş native veya şablondan)
- Sistemin otomatik üretmesi
- Tetikleyiciler:
  - Kullanıcı tıklaması
  - Bir olayın gerçekleşmesi (iş kaydı / süreç olayı vb.)
  - Belirli sürelerde / zamanlanmış üretim
- Üretim, seçilen **şablonlara** ve parametre / veri bağlamasına göre yapılır

##### E. Şablon, parametre ve çıktı

- Belge tasarımcısı ile parametreli şablonlar
- Şablon yayınlama / yayından kaldırma
- Veri bağlama (skaler ve tablo alanları — kapsam analizde netleşir)
- Belge kodu (`documentNo`) desteği
- PDF önizleme / PDF export
- Kayıtta sürüm notu
- Eşzamanlı düzenleme / oturum kilidi (çakışma yönetimi)

##### F. Bildirimler

- Döküman üretildiğinde bildirim
- Döküman güncellendiğinde / yeni sürümde bildirim
- İlgili olay ve alıcı kuralları proje analizinde tanımlanır
- **Kanallar:** uygulama içi bildirim, e-posta; **Telegram** bildirim kanalı olarak yapılandırılabilir
- Döküman bazlı / olay bazlı bildirim ayarları yapılabilir

> Ortak bildirim kanal modeli Raporlama (§4.2) ile paylaşılır.

##### G. Sürümleme

- Döküman versiyonlama
- Geçmiş sürümlere dönüş / geri alma
- Sürüm geçmişinin izlenebilirliği

##### H. Mevcut Office içeriğinin enjekte edilmesi (inject)

- Kurumun Word / Excel / PowerPoint dosyalarının mümkün olduğunca **döküman** olarak sisteme alınması
- Best-effort yaklaşım: makro, ActiveX vb. kayıplar kabul edilebilir
- Gerekirse kayıp parçalar geliştirici tarafından sisteme manuel tamamlanır

##### I. İzlenebilirlik ve kullanım

- Hangi kullanıcının hangi dökümanı ürettiği
- Ne zaman güncellendiği
- Anlık olarak bir dökümanda kimin çalıştığı
- Süre kullanımı: kullanıcı / döküman bazlı süre bilgisi **rapor dökümanları** ile sunulur (ayrı yönetim ekranı vaadi değildir)

##### J. Yapay zekâ yetenekleri

Dosyalar maddesindeki AI yetenekleri dökümanlara da uygulanır (özet, etiket, RAG, benzer dökümanlar, fark özeti, tutarsızlık uyarısı, varlık çıkarma vb.).

Buna ek olarak dökümana özgü AI yetenekleri:

| # | Yetenek | Açıklama |
|:--:|:---|:---|
| 1 | **Tam döküman çevirisi** | Örn. Türkçe dökümanı Çince / İngilizce vb. dile çevirerek yeni sürüm veya bağlı kopya oluşturma (iş amaçlı çeviri) |
| 2 | **Tek tık dil varyantı** | Aynı şablon / içerikten hedef dilde kopya üretme |
| 3 | **Seçili bölüm çevirisi** | Paragraf / slayt / seçim bazlı çeviri |
| 4 | **Çift dilli sürüm** | Kaynak + hedef dilin aynı dökümanda sunulması |
| 5 | **Hedef dilde özet** | Export / paylaşım öncesi seçilen dilde kısa özet |
| 6 | **Terim sözlüğü ile çeviri** | Kurumsal terimlerin tutarlı karşılıkları (ör. kalite / kompozit terimleri) |
| 7 | **Ton uyarlama** | Aynı içeriğin müşteri mektubu / iç rapor diline uyarlanması |
| 8 | **Şablon parametresi önerisi** | İş kaydı veya kaynak içerikten AI ile alan doldurma önerisi |
| 9 | **Özet sunum üretimi** | Uzun belgeden kısa PPTX özet sunum üretme |
| 10 | **Kontrol listesi AI** | Zorunlu alan / eksik bilgi kontrolü ve uyarı |

> Çeviri, **onaylı yeminli çeviri** veya piksel-mükemmel biçim garantisi değildir. Karmaşık yerleşim, makro ve formül koruması kapsam dışıdır; çıktı insan kontrolüne açıktır.

##### K. Opsiyonel kalemler *(ayrı fiyatlandırılır — bkz. §6.2)*

| Opsiyon | Açıklama |
|:---|:---|
| **Onaylı yayın** | Taslak → inceleme / onay → yayında yaşam döngüsü |
| **Toplu içerik güncellemesi** | Belirli kritere uyan dökümanlarda metadata, etiket ve/veya içerik alanlarının toplu güncellenmesi (dry-run + onay önerilir) |

##### L. Dökümanlar — teslimatlar (özet)

- Klasör yetki modeli (edit / export / print dahil)
- Antet ve kapak katalogları
- Şablon seti ve üretim tetikleyicileri (manuel / olay / zamanlı)
- Sürümleme ve oturum / kilit davranışı
- Inject yaklaşımı ve sınırları
- Bildirim kuralları
- AI seti (Dosya mirası + döküman dil / üretim AI’ları)
- Süre kullanımına ilişkin rapor dökümanları
- Kullanıcı eğitimi ve kabul senaryoları
- *(Opsiyon seçildiyse)* onaylı yayın ve/veya toplu içerik güncelleme

---

### 4.2 Raporlama İşlemleri

MonitraNG Raporlama modülü üzerinde parametreli tablo raporlarının tasarlanması, kataloglanması, görüntülenmesi, dışa aktarımı, (gerektiğinde) Document Intelligence belge üretimi ve bildirimlerle desteklenmesi.

#### 4.2.1 Veri kaynakları

Raporlar aşağıdaki kaynak tiplerinden beslenebilir:

| Kaynak tipi | Açıklama |
|:---|:---|
| **MonitraNG içi veriler** | Platformdaki dataset / iş verileri |
| **HTTP endpoint** | MonitraNG dışı, HTTP ile erişilen sistemler |
| **Veritabanı sorgusu** | Dahili veya harici veritabanlarına tanımlı sorgular |

**Erişim kuralı (zorunlu):** HTTP veya veritabanı kaynağı ancak sisteme tanıtılan ve doğrulanan bağlantı bilgileriyle kullanılabilir. En azından erişim adresi, veri alabilecek kullanıcı / kimlik bilgisi ve gerekli bağlantı parametreleri kaydedilir; bağlantı testi başarılı olmadan kaynak aktif sayılmaz. **Erişilemeyen kaynaktan rapor üretilemez.**

Müşteri; firewall/allowlist, hesap ve okuma yetkisini sağlar. Kaynak erişiminin kesilmesi altyapı/erişim kapsamındadır.

#### 4.2.2 Analiz ve rapor kataloğu

- Rapor ihtiyacı ve önceliklerin belirlenmesi
- Kaynak envanterinin çıkarılması (MonitraNG / HTTP / DB)
- Rapor kataloğu ve kategori yapısının tasarlanması
- Rol / yetki modelinin netleştirilmesi

#### 4.2.3 Rapor tasarımı ve çalıştırma

- Parametreli tablo raporlarının oluşturulması
- Kolon, filtre, sıralama, sayfalama
- Expand / satır detayı ve ilişkili alt görünümler (child)
- Özet (aggregate) kart / footer tanımları
- Rapor, sütun ve child seviyesinde yetkilendirme
- Designer ve Runner deneyimi
- Merkezi katalog kaydı

#### 4.2.4 Görüntüleme, paylaşım ve linkleme

- Browse / rapor görüntüleme
- Embed (gömülü) yüzey
- Paylaşılabilir link (parametreler dahil)
- Sütundan başka rapora deep link

#### 4.2.5 Dışa aktarım ve belge üretimi

- CSV ve Excel export (sütun seçimi dahil)
- Gerekli raporlar için Document Intelligence şablon bağlama ve belge üretimi (rapor çalıştırma / satır bağlamı)

#### 4.2.6 Bildirimler

Raporlama olayları için bildirim ayarları yapılabilir (ör. rapor hazır, zamanlanmış çıktı, hata, paylaşım vb. — olay seti analizde netleşir).

| Kanal | Durum |
|:---|:---|
| Uygulama içi bildirim | Mevcut kanal modeli |
| E-posta | Mevcut kanal modeli |
| **Telegram** | Bildirim seçeneklerine eklenebilir kanal |

> Aynı kanal modeli Döküman Zekası bildirimleri (§4.1) ile uyumludur; döküman ve raporlar için ayrı ayrı bildirim ayarları tanımlanabilir.

#### 4.2.7 Test, eğitim ve devreye alma

- Kullanıcı kabul testleri
- Kaynak bağlantı ve yetki doğrulaması
- Eğitim ve canlıya alma

> **Teslimatlar (özet):** veri kaynağı profilleri (erişilebilir olanlar), rapor kataloğu, rapor tanımları, export / DI belge bağları, bildirim ayarları (in-app / e-posta / Telegram), yetki matrisi, eğitim notu.

---

### 4.3 İzleme İşlemleri (Monitoring)

MonitraNG **operasyonel / metrik izleme** katmanı. Sunucu, veritabanı, servis, ağ ve saha cihazlarından durum/metrik toplama; eşik alarmı ve bildirim; fiziksel **üretim süreçlerinin** Operation Core workspace’i üzerinden yönetimi ve Monitoring verisiyle bağlanması.

> **Kapsam dışı:** Güvenlik olay SIEM (log korelasyonu, güvenlik paneli vb.) bu teklif kaleminde yer almaz. SIEM, Monitoring’den ayrı bir ürün katmanıdır. Tam MES (çizelgeleme, kapasite planlama, ağır BOM motoru) bu teklifte yoktur.

#### 4.3.1 Analiz ve envanter

- İzlenecek varlık envanterinin çıkarılması (sunucu, DB, servis, kamera, sensör, endpoint)
- Toplama yöntemlerinin belirlenmesi (poll / push / protokol)
- Eşik, alarm ve bildirim kurallarının netleştirilmesi
- Erişim bilgilerinin sisteme tanıtılması (adres, kimlik, protokol parametreleri — erişilemeyen kaynaktan izleme yapılamaz)

#### 4.3.2 Sunucu metrikleri

- **Windows** sunucu metriklerinin toplanması
- **Linux** sunucu metriklerinin toplanması
- Tipik metrikler: CPU, bellek, disk, yük ve analizde kararlaştırılan ek host metrikleri

#### 4.3.3 Veritabanı metrikleri

İlk fazda desteklenecek motorlar:

| Motor | Kapsam |
|:---|:---|
| MongoDB | Bağlantı / sağlık / performans metrikleri |
| Oracle | Bağlantı / sağlık / performans metrikleri |
| Microsoft SQL Server | Bağlantı / sağlık / performans metrikleri |
| PostgreSQL | Bağlantı / sağlık / performans metrikleri |

> Detay metrik seti (ör. bağlantı sayısı, depolama, sorgu gecikmesi) proje analizinde netleştirilir.

#### 4.3.4 Servis izleme

Çalışması gereken servislerin ayakta olduğunun doğrulanması:

- Windows Service
- systemd birimleri
- Process / süreç adı bazlı kontrol

#### 4.3.5 Ağ ve erişilebilirlik

- **HTTP endpoint** erişilebilirlik / health kontrolleri
- **IP tabanlı cihazlarda ping** ile ayakta olma kontrolü

#### 4.3.6 Saha cihazları ve protokoller

Veri üretebilen sensör ve benzeri cihazlardan toplama:

| Protokol | Not |
|:---|:---|
| **MQTT** | Publish/subscribe cihaz verisi |
| **SNMP** | Ağ / cihaz OID metrikleri |
| **TCP** | TCP tabanlı cihaz/sensör akışları |
| **OPC UA** | Endüstriyel / saha veri bağlama |

#### 4.3.7 Güvenlik kamerası alarmları

- Müşteri kamera / NVR sisteminin ürettiği **alarm** olaylarının yakalanması
- Yayın yöntemi müşteri kurulumuna göre değişebilir (ör. webhook, MQTT, SNMP trap, ONVIF veya üreticiye özgü entegrasyon)
- Teklifte yetenek olarak yer alır; bağlanacak protokol/enstrüman analiz ve keşifte belirlenir

#### 4.3.8 Alarm, dashboard ve bildirimler

- Dashboard / panel ile metrik ve durum görselleştirme
- Eşik ve kural tabanlı alarmlar
- Bildirim kanalları (**standart**):
  - Uygulama içi bildirim
  - E-posta
  - **Telegram**

> Bildirim kanal modeli Döküman Zekası ve Raporlama ile ortaktır (§4.1 / §4.2).

#### 4.3.9 Yapay zekâ yetenekleri (Monitoring AI)

Eşik alarmlarına ek olarak, toplanan metrik ve olaylar üzerinde **standart** AI destekleri:

| # | Yetenek | Açıklama |
|:--:|:---|:---|
| 1 | **Anomaly Detection** | Normal davranıştan sapmaların otomatik tespiti (eşik aşılmasa bile olağandışı metrik/sinyal uyarısı). İstatistiksel / hafif ML yaklaşımı; yeterli metrik geçmişi birikince etkinleşir |
| 2 | **Alarm açıklaması** | Alarm anında kısa, anlaşılır “neden alarm?” özeti ve ilgili metrik bağlamı |
| 3 | **Kök neden önerisi** | Olası nedenlere yönelik yönlendirici öneriler (heuristic + AI; kesin teşhis garantisi değildir) |
| 4 | **Alarm gürültü azaltma** | Tekrarlayan / flapping alarmların özetlenmesi; bildirim yorgunluğunun azaltılması |
| 5 | **Doğal dil sorgu** | Metrik / alarm geçmişine yetki sınırında soru sorma (ör. “dün gece hangi veritabanı yavaşladı?”) |
| 6 | **Kapasite / trend notu** | Basit trend ile doluluk / tükenme uyarı metni (ör. disk doluluk öngörüsü) |
| 7 | **Sensör / kamera alarm özeti** | Yoğun alarm dönemlerinin zaman ve bölge bazlı özetlenmesi |
| 8 | **Eşik önerisi** | İlk kurulum ve ayarlarda metrik bazlı eşik önerileri |

> Anomaly ve AI uyarıları mevcut bildirim kanallarına (in-app / e-posta / Telegram) bağlanabilir. Model yerleşimi proje analizinde netleştirilir.

#### 4.3.10 Üretim operasyonu (Operation Core workspace)

Bu madde **fiziksel ürün üretimini** (fabrika / hat / iş emri) kapsar; IT helpdesk senaryosu değildir. Operation Core üzerinde tarafımızdan bir **Üretim workspace** oluşturulur; süreçler bu workspace içinde dinamik olarak tanımlanır. Monitoring’de asset olarak tanımlı sensör ve benzeri kaynakların verileri üretim süreçlerinde kullanılır.

##### A. Workspace ve süreç tanımı

- 1 adet **Üretim workspace** kurulumu (Operation Core)
- Dinamik süreç yapılandırması: iş tipleri, durumlar, akışlar, formlar, board/kuyruklar
- Temel iskelet (keşif atölyesinde netleştirilir); örnek çerçeve:
  - **Üretim emri** yaşam döngüsü (aç → planla → üretimde → kalite → sevk / kapat)
  - İsteğe bağlı kalite kuyruğu (**NCR**) — kompozit / kalite ihtiyacına göre
- **İş paketi:** zorunlu teslimat değildir; ihtiyaç halinde üretim emrine **isteğe bağlı referans / parametre** olarak bağlanabilir
- Süreçler sonradan kurum tarafından da genişletilebilir / değiştirilebilir (dinamik model)

##### B. Monitoring ↔ üretim köprüsü

| Seviye | Yetenek | Bu teklifte |
|:---|:---|:---|
| Görünürlük | Üretim emri kartında ilgili sensör / hat metriklerinin canlı görünümü | **Standart** |
| Alarm köprüsü | Eşik veya anomaly uyarısının ilgili emre not / olay olarak düşmesi + bildirim | **Standart** |
| Süreç tetiki | Kritik sapmada otomatik durum değişimi veya NCR açma | **Opsiyon / keşif sonrası** (§6.2) |

- Bağlanacak kaynaklar Monitoring asset envanterinde tanımlı ve erişilebilir olmalıdır
- Kurum içinde fiziksel sensör henüz yoksa, gösterim **simulator** uygulaması üzerinden yapılır

##### C. Üretime özel eforsuz wow yetenekleri (standart)

| # | Yetenek | Açıklama |
|:--:|:---|:---|
| 1 | **Emirde canlı sensör şeridi** | Operatör, iş kaydı içinde ilgili hat/fırın/sensör durumunu görür |
| 2 | **Alarm / anomaly → emre otomatik not** | Sapma, üretim emri zaman çizelgesine işlenir; bildirim kanallarına düşer |
| 3 | **Emir bağlamlı alarm açıklaması** | AI açıklaması üretim emri referansıyla (ör. hangi emri etkilediği) sunulur |
| 4 | **Vardiya / gün özeti** | Emir ve sensör/alarm özetinin e-posta veya Telegram ile iletilmesi |
| 5 | **İlgili metriklere deep link** | Emirden Monitoring grafik / detay görünümüne geçiş |

##### D. Opsiyonel üretim AI / tetik

| Opsiyon | Açıklama |
|:---|:---|
| **Onaylı NCR taslak önerisi** | Kritik sapmada yarı doldurulmuş NCR formu önerisi; insan onayıyla kayıt |
| **Süreç tetiki** | Kritik sapmada emri belirli duruma alma veya NCR açma otomasyonu |

##### E. Bilinçli sınırlar

- Tam MES (ileri çizelgeleme, kapasite optimizasyonu, ağır malzeme motoru) kapsam dışıdır
- İş paketi medya / DI paketleri bu maddenin zorunlu parçası değildir (DI / rapor paketleriyle ilişkilendirilebilir)
- SIEM bu maddede yoktur

#### 4.3.11 Test, eğitim ve devreye alma

- Kaynak bağlantı ve metrik doğrulama
- Alarm / bildirim / anomaly uçtan uca testleri
- Üretim workspace süreç ve sensör köprüsü senaryo testleri (gerekirse simulator)
- Operasyon eğitimi ve canlıya alma

> **Teslimatlar (özet):** varlık / kaynak envanteri, toplama konfigürasyonları (sunucu, DB, servis, ping/HTTP, MQTT/SNMP/TCP/OPC UA, kamera alarmı), dashboard seti, alarm kuralları, Monitoring AI, **Üretim workspace + emir süreci + Monitoring köprüsü + üretim wow seti**, bildirim ayarları, (gerekirse) simulator senaryosu, operasyon rehberi.

---

### 4.4 Dış Katılım Portalı (Anket)

Kurumun **MonitraNG kullanıcısı olmayan** müşteri ve tedarikçilerine e-posta ile ulaştırılan anketlerin hazırlanması, yayınlanması, yanıtlanması ve sonuçlarının izlenmesi. Bu paket, iç IAM’li MonitraNG uygulamalarından ayrı bir **dış katılım** uygulamasıdır.

#### 4.4.1 Konumlandırma ve barındırma

| Konu | Karar |
|:---|:---|
| Katılımcı | MonitraNG hesabı gerekmez; e-postadaki link ile erişim |
| Yayın adresi | MonitraNG bulutu — örn. `odak.monitrang.com` (müşteri sunucusunda barındırılmaz) |
| Barındırma süresi | Bu teklifin **onaylanmasından itibaren 1 yıl** (süre sonunda yenileme ayrıca anlaşılır) |
| Dil | **TR + EN** |
| Veri sahipliği | Yanıtlar Odak tenant’ına aittir; runtime MonitraNG edge’inde çalışır |

#### 4.4.2 Anket yaşam döngüsü

- Kurum admin’inin anket hazırlaması (soru tipleri: çoktan seçmeli, serbest metin, puan, evet/hayır ve analizde netleşen ek tipler)
- E-posta ile davet / yayın
- Katılımcının link ile ankete girip yanıtlaması (süreli / politikaya bağlı link)
- Markalı dış sayfa (kurum kimliği)
- KVKK / aydınlatma onayı (gerekli metinler analizde)
- Anket kapanış tarihi ve (gerekirse) yanıt kotası
- Yanıtlamayanlara hatırlatma e-postası (yapılandırılabilir)

#### 4.4.3 Sonuçlar ve senkronizasyon

Yanıtlar **her iki tarafta** tutulur / görüntülenir:

1. **Dış portal (admin)** — anket sonuç özeti, tablo, basit grafik, dışa aktarım (ör. Excel)
2. **Odak MonitraNG** — aynı yanıtların kurum MonitraNG ortamına aktarımı / senkronu (raporlama ve operasyonel kullanım için)

Yeni yanıt geldiğinde admin bildirimi (e-posta / uygulama içi / Telegram — ortak kanal modeli) yapılandırılabilir.

#### 4.4.4 Sonraki faz — Duyurular *(bu teklifte uygulama kapsamı dışı / sonraki dilim)*

Aynı dış portal omurgası üzerinde **duyuru** yayınlama (e-posta + link ile görüntüleme, isteğe bağlı okundu onayı) **anketlerden sonra** planlanır. Bu teklifin standart teslimatı anket MVP’sidir; duyuru ayrı dilim veya ek anlaşma ile açılır.

#### 4.4.5 Test, eğitim ve devreye alma

- Portal yayını (`odak.monitrang.com`) ve TLS
- TR/EN arayüz doğrulama
- Davet → yanıt → portal sonuç → MonitraNG senkron uçtan uca testi
- Admin eğitimi

> **Teslimatlar (özet):** dış anket portalı (TR+EN), e-posta davet akışı, admin sonuç ekranı, MonitraNG yanıt senkronu, 1 yıllık barındırma (onay tarihinden), operasyon notu. Duyuru modülü sonraki faz.

---

## 5. Proje Yaklaşımı ve Zaman Planı

| Faz | Açıklama | Tahmini Süre |
|:---|:---|:---|
| Keşif & Analiz | İhtiyaç netleştirme, envanter, önceliklendirme | *[X iş günü]* |
| Kurulum & Geliştirme | DI / rapor / izleme+üretim workspace / anket portalı | *[X iş günü]* |
| Test & UAT | Senaryo testleri, düzeltmeler | *[X iş günü]* |
| Eğitim & Canlıya Alma | Kullanıcı eğitimi, go-live | *[X iş günü]* |
| **Toplam** | | ***[X iş günü]*** |

> Detaylı Gantt / kilometre taşları sözleşme aşamasında netleştirilir.

---

## 6. Fiyatlandırma

Tutarlar **KDV hariç** olup Amerikan Doları (**USD**) cinsindendir. **Proje bedeli: 15.000 USD** (dört paket dahil; opsiyonlar hariç).

### 6.1 Hizmet Kalemleri

| No | Hizmet Paketi | Kapsam Özeti | Birim | Adet | Durum |
|:--:|:---|:---|:---:|:---:|:---|
| 1 | Döküman Zekası işlemleri | Dosyalar + Dökümanlar (Collabora; antet/kapak; üretim; AI; inject) | Paket | 1 | Dahil |
| 2 | Raporlama işlemleri | Çoklu kaynak (MNG/HTTP/DB), katalog, export, DI belge, bildirim (+Telegram) | Paket | 1 | Dahil |
| 3 | İzleme işlemleri | Metrik Monitoring + Üretim workspace (OC) + anomaly/AI + bildirim (SIEM hariç) | Paket | 1 | Dahil |
| 4 | Dış Katılım Portalı (Anket) | `odak.monitrang.com` · TR+EN · yanıt portal+MNG · **1 yıl barındırma** | Paket | 1 | Dahil |
| | | | | **Proje bedeli (USD)** | **15.000** |

### 6.2 Opsiyonel Kalemler *(isteğe bağlı)*

| No | Kalem | Açıklama | Tutar (USD) |
|:--:|:---|:---|---:|
| O1 | **Onaylı yayın** | Döküman yaşam döngüsü: taslak → onay → yayında | Teklif üzerine |
| O2 | **Toplu içerik güncellemesi** | Kritere uyan dökümanlarda metadata / etiket / içerik alanlarının toplu güncellenmesi | Teklif üzerine |
| O3 | **Duyuru modülü** | Dış portalda duyuru yayınlama (anket sonrası faz) | Teklif üzerine |
| O4 | Ek şablon / rapor / izleme kaynağı | Kapsam dışı her ek birim | Teklif üzerine |
| O5 | Genişletilmiş eğitim | Ek oturum / rol bazlı eğitim | Teklif üzerine |
| O6 | Bakım & Destek (aylık / yıllık) | Öncelikli destek, küçük iyileştirmeler | Teklif üzerine |
| O7 | Anket portalı barındırma yenileme | 1. yıl sonrası yıllık barındırma | Teklif üzerine |
| O8 | **Üretim süreç tetiki / NCR taslak** | Kritik sensör sapmasında otomatik durum/NCR veya onaylı NCR taslağı | Teklif üzerine |

### 6.3 Ödeme Planı

| Taksit | Oran | Tutar (USD) | Koşul |
|:---|:---:|---:|:---|
| 1 | %100 | 15.000 | Canlıya alma / teslim |
| | | **15.000** | |

---

## 7. Varsayımlar, Kısıtlar ve Kapsam Dışı

### 7.1 Varsayımlar

- Müşteri ortamına gerekli erişim (ağ, sunucu, hesap) zamanında sağlanır; gecikmeler proje takvimini kaydırabilir.
- Veri kaynakları, örnek belgeler ve karar verici kullanıcılar keşif / UAT takvimine katılır.
- Altyapı (sunucu, depolama, lisanslar) müşteri tarafında hazırdır veya ayrıca anlaşılır (anket portalı hariç — MonitraNG bulutu).
- Telegram bot / chat bilgileri müşteri ile birlikte yapılandırılır.
- Anket için KVKK aydınlatma metinleri müşteri ile sağlanır veya onaylanır; gönderen kimliği / SMTP itibarı analizde netleşir.
- Üretim süreç detayı (durum seti, NCR dahil mi vb.) keşif atölyesinde netleştirilir.

### 7.2 Kısıtlar *(paket bazlı)*

#### 7.2.1 Döküman Zekası

| Kısıt | Açıklama |
|:---|:---|
| **Inject / LibreOffice** | LibreOffice / Collabora tarafından yönetilemeyen **makro, ActiveX** ve benzeri bileşenleri barındıran dökümanlar **inject edilemez**. Kayıp parçalar best-effort dışı bırakılır veya ayrıca manuel tamamlanır. |
| **Çeviri** | İş amaçlı çeviridir; **yeminli / noter onaylı çeviri değildir**. Karmaşık yerleşimde biçim sapması olabilir. |
| **AI formatları** | Özet / etiket / arama öncelikli PDF ve DOCX içindir. Taranmış PDF’te OCR yoksa kalite düşer; OCR kapsamı ayrıca kararlaştırılır. |
| **AI çıktıları** | Etiket, özet, tutarsızlık uyarısı vb. **öneri niteliğindedir**; insan kontrolüne açıktır. |
| **Markdown Sayfa** | Bu teklifte yoktur (platformda mevcut özellik). |
| **Opsiyonlar** | Onaylı yayın ve toplu içerik güncelleme seçilmedikçe teslim edilmez (O1 / O2). |

#### 7.2.2 Raporlama

| Kısıt | Açıklama |
|:---|:---|
| **Erişim zorunlu** | HTTP veya DB kaynağı sisteme tanıtılmalı (adres, okuma yetkili kullanıcı, bağlantı parametreleri) ve doğrulanmalıdır. **Erişilemeyen kaynaktan rapor üretilemez.** |
| **Dış sistem sorumluluğu** | Kaynak kesintisi, firewall engeli veya dış API/DB şema değişimi MonitraNG kusuru sayılmaz; ilgili raporlar çalışmayabilir. |
| **Export hacmi** | Çok büyük sonuç kümelerinde soft limit / onaylı üst sınır uygulanabilir (performans). |
| **Yetkili sorgu** | Harici DB/API için serbest kontrolsüz sorgu varsayılmaz; kayıtlı ve onaylı kaynak profilleri kullanılır. |

#### 7.2.3 İzleme (Monitoring) ve Üretim workspace

| Kısıt | Açıklama |
|:---|:---|
| **Erişim zorunlu** | Sunucu, DB, sensör, kamera, endpoint vb. için erişim bilgisi yoksa veya bağlantı başarısızsa izleme yapılamaz. |
| **Simulator** | Kurum içinde fiziksel sensör / saha cihazı henüz yoksa ilgili senaryolar **simulator** ile gösterilir; bu, sahadaki gerçek cihazın yerine geçmez. |
| **Kamera protokolü** | Alarm yayın yöntemi müşteri kurulumuna bağlıdır; keşifte belirlenir. Kapalı / belgesiz üretici API’leri ek süre gerektirebilir. |
| **Anomaly Detection** | Anlamlı sonuç için yeterli **metrik geçmişi** gerekir; ilk günlerde uyarı kalitesi sınırlı olabilir. |
| **SIEM yok** | Güvenlik olay SIEM bu pakette değildir. |
| **Tam MES yok** | İleri çizelgeleme, kapasite optimizasyonu, ağır BOM motoru kapsam dışıdır. |
| **İş paketi** | Üretim emrine zorunlu teslimat değildir; isteğe bağlı parametre / referans olabilir. |
| **Süreç tetiki / NCR taslak** | O8 seçilmedikçe otomatik durum değişimi veya NCR taslağı teslim edilmez. |

#### 7.2.4 Dış Katılım Portalı (Anket)

| Kısıt | Açıklama |
|:---|:---|
| **Barındırma** | Müşteri sunucusunda yayınlanmaz; `odak.monitrang.com` (MonitraNG bulutu). |
| **Süre** | Barındırma teklif onayından itibaren **1 yıl**; sonrası yenileme (O7). |
| **Duyuru** | Bu dilimde yoktur; sonraki faz / O3. |
| **E-posta teslimi** | Davet “gönderildi” ≠ alıcıda “görüldü”; spam / kurumsal filtre MonitraNG kontrolü dışındadır. |
| **Kötüye kullanım** | Rate limit / bot koruması uygulanabilir; aşırı trafikte erişim kısıtlanabilir. |

#### 7.2.5 Ortak

| Kısıt | Açıklama |
|:---|:---|
| **AI genel** | Model kalitesi dil, içerik ve ortama bağlıdır; “hatasız otomasyon” vaadi yoktur. |
| **3. parti** | Donanım, OS, kamera SDK, DB client lisansları vb. bu teklife dahil değildir (aksi yazılmadıkça). |
| **Bildirim kanalı** | Telegram için gerekli kimlikler sağlanmazsa kanal açılamaz; in-app / e-posta diğer kurallara bağlıdır. |

### 7.3 Kapsam Dışı *(aksi yazılı olarak eklenmedikçe)*

- Donanım / işletim sistemi / üçüncü parti yazılım lisansları
- Müşteri ağı ve güvenlik cihazlarının fiziksel kurulumu
- Bu teklifte listelenmeyen ek iş süreçleri veya özel geliştirmeler
- Sürekli işletim (7/24 NOC) hizmeti — ayrı sözleşme konusu
- **Markdown Sayfa** (mevcut platform özelliği; bu teklif kalemi değil)
- **SIEM / güvenlik olay izleme**
- **Tam MES**
- **Duyuru modülü** (O3 / sonraki faz)
- Anket portalının müşteri sunucusunda barındırılması
- 1 yıllık anket barındırmasının ötesi (O7)
- Yeminli çeviri; makro / ActiveX / formül sadakati garantisi
- Inject’te yönetilemeyen bileşenlerin birebir taşınması

---
## 8. Kabul Kriterleri

Her hizmet paketi için kabul; aşağıdaki koşulların sağlanmasıyla tamamlanmış sayılır:

1. Tanımlı senaryoların başarıyla çalışması
2. Yetki ve erişim kontrollerinin doğrulanması
3. Teslimat listesindeki çıktıların aktarılması
4. Müşteri tarafından UAT / teslim tutanağının imzalanması

---

## 9. Gizlilik ve Ticari Koşullar

- Bu teklif ve ekleri gizli bilgi niteliğindedir; üçüncü taraflarla paylaşılmaz.
- Fiyatlar teklif geçerlilik süresi içinde sabittir.
- Kapsam değişikliği yazılı onay ve ek teklif / change order ile yapılır.
- Genel sözleşme şartları imza aşamasında ayrıca düzenlenir.

---

## 10. Onay

Bu teklifin kabulü, yukarıdaki kapsam ve ticari koşulların onaylandığı anlamına gelir.

| | MonitraNG | Odak Kompozit |
|:---|:---|:---|
| Ad Soyad | Serkan MERAL | |
| Unvan | | |
| E-posta | serkan.meral@outlook.com | |
| Telefon | 0532 420 67 56 | |
| İmza | | |
| Tarih | | |

---

## Ek A — Hizmet Detay Matrisi *(doldurulacak)*

| Paket | İş Kalemi | Öncelik | Durum | Not |
|:---|:---|:---:|:---:|:---|
| Döküman Zekası · Dosyalar | Barındırma ve klasör yapısı | P1 | Taslak | |
| Döküman Zekası · Dosyalar | Klasör yetkileri (görme / indirme / ekleme) | P1 | Taslak | |
| Döküman Zekası · Dosyalar | Metadata alanları | P1 | Taslak | |
| Döküman Zekası · Dosyalar | Manuel etiketleme | P1 | Taslak | |
| Döküman Zekası · Dosyalar | Bildirimler (yeni dosya / sürüm) | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: soru–cevap (RAG) | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: benzer / ilgili dosyalar | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: sürüm fark özeti | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: eksik / tutarsızlık uyarısı | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: çok dilli köprü | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: otomatik klasör önerisi | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: varlık çıkarma | P1 | Taslak | |
| Döküman Zekası · Dosyalar | AI: otomatik etiket + özet; arama / varsayılan sorgular | P1 | Taslak | |
| Döküman Zekası · Dosyalar | İş kaydına bağlama | — | Mevcut | Ayrı kalem değil |
| Döküman Zekası · Dökümanlar | Türler: Belge / Sheet / Sunum (Collabora) | P1 | Taslak | Markdown Sayfa yok |
| Döküman Zekası · Dökümanlar | Klasör yetkileri (edit / export / print dahil) | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Antet ve kapak katalogları | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Manuel + otomatik üretim (tıklama / olay / zamanlı) | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Şablon, parametre, belge kodu, PDF | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Bildirimler | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Sürümleme ve geri dönüş | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Office inject (best-effort) | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | İzlenebilirlik + süre rapor dökümanları | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | AI: Dosya AI mirası | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | AI: çeviri / dil varyantı / çift dil / hedef dil özeti | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | AI: terim sözlüğü, ton, parametre önerisi, özet sunum, checklist | P1 | Taslak | |
| Döküman Zekası · Dökümanlar | Opsiyon: onaylı yayın | P2 | Opsiyon | §6.2 O1 |
| Döküman Zekası · Dökümanlar | Opsiyon: toplu içerik güncellemesi | P2 | Opsiyon | §6.2 O2 |
| Raporlama | Veri kaynakları: MonitraNG / HTTP / DB | P1 | Taslak | Erişim tanımı zorunlu |
| Raporlama | Katalog, designer, runner, parametre, expand, özet | P1 | Taslak | Mevcut fonksiyonlar |
| Raporlama | Viewer, paylaşım, linkleme | P1 | Taslak | |
| Raporlama | Export CSV/Excel + DI belge üretimi | P1 | Taslak | |
| Raporlama | Bildirimler (in-app, e-posta, Telegram) | P1 | Taslak | Döküman ile ortak kanal |
| İzleme | Windows / Linux sunucu metrikleri | P1 | Taslak | SIEM yok |
| İzleme | DB metrikleri (MongoDB, Oracle, SQL Server, PostgreSQL) | P1 | Taslak | |
| İzleme | Servis izleme (Windows Service / systemd / process) | P1 | Taslak | |
| İzleme | HTTP health + IP ping | P1 | Taslak | |
| İzleme | MQTT / SNMP / TCP / OPC UA cihaz verisi | P1 | Taslak | OPC UA |
| İzleme | Güvenlik kamerası alarm yakalama | P1 | Taslak | Protokol keşifte |
| İzleme | Dashboard, eşik alarm, bildirim (in-app/mail/Telegram) | P1 | Taslak | Standart |
| İzleme | AI: Anomaly Detection | P1 | Taslak | Standart |
| İzleme | AI: alarm açıklaması, kök neden önerisi, gürültü azaltma | P1 | Taslak | |
| İzleme | AI: doğal dil sorgu, trend notu, kamera/sensör özeti, eşik önerisi | P1 | Taslak | |
| İzleme · Üretim | OC Üretim workspace + dinamik süreç (fiziksel üretim) | P1 | Taslak | IT helpdesk değil |
| İzleme · Üretim | Üretim emri akışı (+ isteğe bağlı NCR) | P1 | Taslak | Keşifte netleşir |
| İzleme · Üretim | İş paketi = isteğe bağlı parametre | — | Not | Zorunlu değil |
| İzleme · Üretim | Sensör görünürlük + alarm→emir notu | P1 | Taslak | Standart köprü |
| İzleme · Üretim | Wow: canlı şerit, bağlamlı açıklama, vardiya özeti, deep link | P1 | Taslak | |
| İzleme · Üretim | Simulator ile demo (fiziki sensör yoksa) | P1 | Taslak | |
| İzleme · Üretim | Opsiyon: süreç tetiki / NCR taslak | P2 | Opsiyon | §6.2 O8 |
| Dış Katılım · Anket | Portal `odak.monitrang.com` (müşteri sunucusu değil) | P1 | Taslak | |
| Dış Katılım · Anket | Anket oluşturma + e-posta davet + yanıt | P1 | Taslak | Katılımcı ≠ MNG user |
| Dış Katılım · Anket | Sonuçlar: portal admin + MonitraNG senkron | P1 | Taslak | Her iki taraf |
| Dış Katılım · Anket | TR + EN | P1 | Taslak | |
| Dış Katılım · Anket | Barındırma 1 yıl (teklif onayından) | P1 | Taslak | Sonrası O7 |
| Dış Katılım · Duyuru | Duyuru yayınlama | P2 | Sonraki faz | Opsiyon O3 |

---

## Ek B — Revizyon Geçmişi

| Sürüm | Tarih | Açıklama | Yazar |
|:---:|:---|:---|:---|
| 0.1 | 12.07.2026 | İlk taslak — başlıklar ve teklif iskeleti | MonitraNG |
| 0.2 | 12.07.2026 | §4.1.1 Dosyalar detaylandırıldı (çekirdek + AI); Dosya / Döküman ayrımı | MonitraNG |
| 0.3 | 12.07.2026 | §4.1.2 Dökümanlar; dil AI; opsiyon O1/O2; Markdown Sayfa kapsam dışı | MonitraNG |
| 0.4 | 12.07.2026 | §4.2 Raporlama (çoklu kaynak + erişim kuralı); Telegram bildirim; döküman bildirim kanalları | MonitraNG |
| 0.5 | 12.07.2026 | §4.3 İzleme: metrik Monitoring (SIEM hariç); sunucu/DB/servis/protokol/kamera; bildirim standart | MonitraNG |
| 0.6 | 12.07.2026 | §4.3.9 Monitoring AI: anomaly + eforsuz wow maddeleri standart | MonitraNG |
| 0.7 | 12.07.2026 | §4.4 Dış Katılım Portalı (Anket); TR+EN; 1 yıl host; duyuru sonraki faz | MonitraNG |
| 0.8 | 12.07.2026 | §4.3.10 Üretim workspace (OC) + Monitoring köprüsü + üretim wow; O8 | MonitraNG |
| 0.9 | 12.07.2026 | §7 Varsayımlar / paket bazlı kısıtlar / kapsam dışı toparlandı | MonitraNG |

---

*MonitraNG — Odak Kompozit Fiyat Teklifi · ODK-FT-2026-001 · v0.9*
