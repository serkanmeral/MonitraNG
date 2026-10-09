# MonitraNG — Odak Kompozit Dijital Dönüşüm Uygulaması

Bu dokümanda Odak Kompozit firması için geliştirilen / geliştirilmekte olan yazılıma ait içerik ve faz planlaması konularına değinilmiştir.

## Uygulama amacı

- Müşterinin SIEM güvenlik ihtiyaçlarının karşılanması
- Wiki formatında kendi sayfalarının oluşturulabilmesi
- Süreçleri dinamik olarak yönetebilecekleri bir operasyon merkezi geliştirilmesi
- Müşterinin kendisine ait iş paketlerinin, kalemlerinin, sevkiyatlarının ve ayrıca personel eğitiminin kayıt altına alınabileceği bir modülün geliştirilmesi
- Sistemin otomatik olarak DOCX formatında doküman üretmesinin sağlanması
- Güvenli dokümantasyon sistemi kurularak kullanıcıların uygulama arayüzleri aracılığıyla, uygulamanın da şablonlar kullanarak otomatik olarak DOCX, XLSX, PPTX formatında dokümanlar üretebilmesi
- Collabora Online ile dokümanların tarayıcı üzerinden görüntülenmesi ve yetki dahilinde düzenlenebilmesi
- Üretilen dokümanlar için kullanıcı yetkilerinin (görebilme, düzenleyebilme, export veya çıktı alabilme) sağlanması
- Yapay zeka ile dokümanların özetlerinin çıkarılabilmesi, otomatik etiketleme yapılabilmesi, ilişkili dokümanların bulunabilmesi ve doküman keşfet ile içeriğe göre dokümanların aranabilmesi
- Müşterinin kendi dosyalarını sisteme alabilmesi ve bunlar için de yapay zeka desteğinin verilebilmesi
- Hem üretilen dokümanlar hem de eklenen dosyalar için meta etiketlendirmesinin yapılabilmesi
- Üretim yönetimi (MES Prod) süreci ile üretimlerin baştan sona takip edilebilmesi
- Alarm ve olay bildirimlerinin uygulama içi, e-posta ve Telegram kanalları üzerinden iletilebilmesi
- Bir siber sağlık modülü geliştirilerek müşterinin sistemindeki çalışan bilgisayarların, yazılımların, servislerin sağlıklarının sürekli takip edilmesi; bir uyarı halinde otomatik olarak alarm ve iş süreçlerinin başlatılabilmesi; yapay zeka destekli anomaly detection, kestirimci bakım ve sağlık önerilerinin sunulabilmesi
- Sistemdeki IP tabanlı cihazların (kamera vb.) sürekli aktif olduğundan emin olunması; kamera bir alarm ürettiğinde bunun otomatik olarak iş süreçlerinin başlatılabilmesi
- Bir raporlama servisi geliştirilerek hem kendi ürettiğimiz verilerden hem de erişebildiğimiz DB veya HTTP verilerinden raporların anlık veya zamanlanmış olarak üretilmesi

---

## Bakım

Tüm fazlar için bakım tarafımızdan sağlanacaktır. Bakım bitiş tarihi: **11.07.2027**.

---

## Fazlar

### Faz 1

**Amaç**

Bu fazda dijital dönüşüm uygulamasının temel altyapısı ve ilk kullanım modülleri hayata geçirilmiştir. Amaç; güvenli bir yazılım ortamı oluşturmak, kurumsal kimlik doğrulamayı Active Directory ile bağlamak ve güvenlik izleme (SIEM), kurum içi bilgi paylaşımı (Wiki) ile dinamik süreç yönetimi (Operasyon Merkezi) yeteneklerini müşterinin kullanımına sunmaktır.

**Yapılanlar**

- **Test ve prod yazılım ortamlarının kurulması**  
  Uygulamanın doğrulama ve deneme için test ortamı ile canlı kullanım için prod ortamı ayrı ayrı kurulmuştur. Böylece yeni geliştirmeler ve değişiklikler test ortamında doğrulanıp prod ortamına alınabilir; yapılandırma, erişim ve çalışma düzeni bu iki ortam üzerinden yönetilir.

- **Active Directory entegrasyonu**  
  Kullanıcı girişleri kurumsal Active Directory ile entegre edilmiştir. Kimlik doğrulama AD üzerinden yapılır; kullanıcıların sisteme erişimi kurumsal hesaplarıyla sağlanır ve yetkilendirme altyapısı bu kimlik modeline bağlanmıştır.

- **SIEM modülü**  
  Siber güvenlik ihtiyaçlarına yönelik SIEM (güvenlik bilgi ve olay yönetimi) modülü geliştirilmiş; güvenlik olaylarının izlenmesi ve yönetilmesi sağlanmıştır.
  - **Kaynaklar:** Windows DC ve terminal makineleri, Linux sunucuları ile firewall verileri sisteme alınarak sürecin çalıştığı ispatlanmıştır.
  - **Veri toplama:** NXLog ve rsyslog üzerinden gelebilen her türlü veri otomatik olarak içeri alınmıştır.
  - **Alarm ve bildirim:** Alarm politikaları belirlenmiş; alarm durumuna göre uygulama içi ve e-posta bildirim mekanizmaları hayata geçirilmiştir.

- **Wiki modülü**  
  Müşterinin kendi sayfalarını wiki formatında oluşturup düzenleyebileceği bir içerik / bilgi tabanı modülü geliştirilmiştir. Sayfa erişimi ve yönetimi için yetkilendirme yapılmıştır; kullanıcılar yetkilerine göre içerikleri görüntüleyebilir veya yönetebilir.

- **Operasyon Merkezi modülü**  
  İş süreçlerinin dinamik olarak tanımlanıp yürütülebileceği bir operasyon merkezi geliştirilmiştir. Süreç tanımı, adımlar, atama ve durum takibi uygulama üzerinden yönetilebilir. Proof (kanıt / örnek) amaçlı olarak IT yardım masası, stok, depo, demirbaş ve zimmet süreçleri hazırlanmıştır.

**Durum:** Teslim edildi

**Teslim tarihi:** 18.06.2025

**Ücret:** 3.000 USD

### Faz 2

**Amaç**

Bu fazda operasyonel iş kayıtları ile güvenli dokümantasyon altyapısı hayata geçirilmiştir. Amaç; müşterinin iş paketleri, kalemler, sevkiyatlar ve personel eğitimi süreçlerini sistem üzerinde kayıt altına alabilmesi; Collabora editörü ile dokümanları tarayıcı üzerinden görüntüleyip düzenleyebilmesi; şablonlarla otomatik DOCX / XLSX / PPTX üretebilmesi; üretilen ve yüklenen içerikler için yetki ve meta yönetimi sağlayabilmesidir.

**Yapılanlar**

- **İş paketleri, kalemler, sevkiyatlar ve personel eğitimi**  
  Operasyonel iş verileri merkezi ve izlenebilir şekilde kayıt altına alınmıştır:
  - **İş paketleri:** Üst seviye iş / proje paketlerinin tanımlanması, durumu ve takibi
  - **Kalemler:** İş paketine bağlı kalemlerin kayıt altına alınması ve izlenmesi
  - **Sevkiyatlar:** Sevkiyat kayıtlarının tutulması ve genel durumunun yönetilmesi
  - **Personel eğitimi:** Personel eğitim kayıtlarının (katılım, eğitim konusu, tarih vb.) sistemde tutulması; eğitim süreçlerinin izlenebilir hale getirilmesi

- **Güvenli doküman sistemi ve Collabora editörü**  
  Dokümanların sistem içinde güvenli şekilde üretilmesi, saklanması ve yönetilmesi için dokümantasyon altyapısı kurulmuştur. Collabora Online entegrasyonu ile müşteri; Word (DOCX), Excel (XLSX) ve PowerPoint (PPTX) dokümanlarını ek bir masaüstü uygulamasına ihtiyaç duymadan, uygulama arayüzü üzerinden tarayıcıda açabilir, görüntüleyebilir ve yetkisi dahilinde düzenleyebilir. Ortak çalışma, biçimlendirme ve ofis dosyası düzenleme yetenekleri uygulama içinde sunulur.

- **Şablon tabanlı otomatik doküman üretimi**  
  Tanımlı şablonlar kullanılarak sistem tarafından otomatik olarak DOCX, XLSX ve PPTX formatında doküman üretimi sağlanmıştır.  
  **Proof örneği:** Bir iş süreci tamamlandığında sistem otomatik olarak; her bir kalem için uygunluk belgesini DOCX, sevkiyatların genel durumunu XLSX, kapanmış iş paketinin genel sonucunu ise PPTX olarak üretebildiği gösterilmiştir. Üretilen çıktılar güvenli doküman sisteminde saklanır ve Collabora ile açılıp incelenebilir / düzenlenebilir.

- **Doküman yetkilendirme**  
  Üretilen dokümanlar için kullanıcı bazlı yetkiler tanımlanmıştır. Kullanıcılar yetkilerine göre dokümanları görebilir, Collabora üzerinden düzenleyebilir, export edebilir veya çıktı alabilir.

- **Müşteri dosyalarının sisteme alınması**  
  Müşterinin kendi dosyalarını sisteme yükleyebilmesi sağlanmıştır. Yüklenen dosyalar güvenli doküman yapısı içinde yönetilebilir; uygun formatlar Collabora editörü ile açılabilir.

- **Meta bilgisi yönetimi**  
  Hem sistemde üretilen dokümanlar hem de müşteri tarafından eklenen dosyalar için meta bilgisi girilebilmesi sağlanmıştır (örnek alanlar: doküman / dosya tipi, etiketler, ilişkili iş paketi veya kalem, tarih, açıklama). Böylece içerikler sınıflandırılabilir ve daha kolay bulunabilir.

**Durum:** Teslim edildi

**Teslim tarihi:** 11.07.2026

**Ücret:** 10.000 USD

### Faz 3

**Amaç**

Bu fazda dijital dönüşümün ileri yetenekleri hayata geçirilmektedir. Amaç; yapay zeka ile doküman ve dosya keşfini güçlendirmek; üretimlerin baştan sona takip edilebildiği bir üretim yönetimi (MES Prod) süreci sunmak; siber sağlık ve IP tabanlı cihaz (kamera vb.) izleme ile operasyonel güvenliği artırmak; anomali tespiti, kestirimci bakım ve sağlık önerileri ile proaktif müdahaleyi desteklemek; bildirimleri uygulama içi, e-posta ve Telegram kanallarına taşımak; alarm durumlarında Faz 1 Operasyon Merkezi süreçlerini otomatik tetiklemek; üretilen çıktıların Faz 2 doküman / Collabora altyapısı ile bütünleşik çalışmasını sağlamak ve esnek bir raporlama servisi sunmaktır.

**Kapsam (geliştirme devam ediyor)**

Öncelikli olarak üretim yönetimi (MES Prod), bildirim kanallarının genişletilmesi, siber sağlık / cihaz izleme ve yapay zeka destekli doküman keşfi üzerinde çalışılmaktadır; raporlama ile birlikte tüm kapsam tamamlanacaktır.

- **Yapay zeka destekli doküman ve dosya işlemleri**  
  Hem sistemde üretilen dokümanlar hem de müşterinin yüklediği dosyalar için yapay zeka yetenekleri geliştirilmektedir:
  - **Üretilen dokümanlar:** Özet çıkarma, otomatik etiketleme, ilişkili doküman bulma, içerik tabanlı keşif / arama
  - **Yüklenen dosyalar:** Aynı keşif ve destek yeteneklerinin müşteri dosyalarını da kapsaması  
  Böylece kullanıcılar içeriğe göre arama yapabilecek; sonuçlar Faz 2’deki güvenli doküman ve Collabora yapısıyla birlikte kullanılabilecektir.

- **Üretim yönetimi (MES Prod)**  
  Bir üretim yönetimi süreci oluşturularak üretimlerin baştan sona takip edilebilmesi sağlanacaktır (MES Prod).  
  **Hedef akış:** Üretim emri / üretim kaydı oluşturulması → üretim adımlarının izlenmesi → durum güncellemeleri → tamamlanma / kapanış. Üretim adımları, durumlar ve ilgili operasyonel kayıtlar sistem üzerinden izlenebilir hale getirilecektir.

- **Bildirim kanalları (uygulama içi, e-posta, Telegram)**  
  Alarm ve olay bildirimleri; uygulama içi ve e-posta kanallarına ek olarak Telegram üzerinden de iletilebilecektir. SIEM, siber sağlık, IP / kamera izleme ve Operasyon Merkezi süreçlerinden doğan kritik uyarılar ilgili kullanıcılara birden fazla kanalda ulaşabilecektir.

- **Siber sağlık modülü**  
  Müşteri ortamındaki çalışan bilgisayarların, yazılımların ve servislerin sağlık durumu sürekli takip edilecektir. Bir uyarı oluştuğunda otomatik alarm üretilecek; gerektiğinde Faz 1’de kurulan Operasyon Merkezi üzerinden ilgili iş süreçleri başlatılabilecektir.  
  - **Yapay zeka destekli anomaly detection:** Olağandışı davranış ve sapmaların tespiti  
  - **Kestirimci bakım:** Olası arıza / bozulma risklerinin önceden değerlendirilmesi  
  - **Sağlık önerileri:** İzleme sonuçlarına göre iyileştirme ve müdahale önerilerinin sunulması

- **IP tabanlı cihaz izleme (kamera vb.)**  
  Sistemdeki IP tabanlı cihazların (özellikle kameraların) sürekli aktif olduğu izlenecektir. Cihaz erişilemez olduğunda veya kamera bir alarm ürettiğinde, bunun otomatik olarak Operasyon Merkezi iş süreçlerine bağlanması sağlanacaktır.

- **Raporlama servisi**  
  Hem platformun kendi ürettiği verilerden hem de erişilebilir veritabanı (DB) veya HTTP kaynaklarından rapor üretimi geliştirilmektedir. Raporlar anlık (isteğe bağlı) veya zamanlanmış (periyodik) olarak oluşturulabilecektir; çıktılar gerektiğinde Faz 2 doküman altyapısı ve Collabora ile birlikte değerlendirilebilecektir.

**Durum:** Geliştirme aşamasında (devam ediyor)

**Teslim tarihi:** —

**Ücret:** 0 USD  
*Not: Faz 3 için herhangi bir ücretlendirme yapılmayacaktır.*
