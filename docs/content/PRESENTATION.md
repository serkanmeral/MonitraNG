# MonitraNG — Öne Çıkan Özellikler

Bu sayfa, MonitraNG platformunun **öne çıkan özelliklerini** özetler. Ürün tanıtımları, demo günleri ve satış görüşmelerinde referans olarak kullanılabilir.

---

## 1. Çok kiracılı (multi-tenant) yapı

MonitraNG, **domain bazlı çok kiracılı** mimari ile farklı müşteri veya iş birimlerini aynı altyapı üzerinde, birbirinden izole şekilde barındırır.

- Her domain kendi kullanıcıları, veri setleri, menüleri ve ayarlarıyla yönetilir.
- Kimlik ve yetkilendirme **Keycloak** ile merkezî ve güvenli biçimde sağlanır.
- Tek kurulum ile birden fazla tenant’a hizmet vererek maliyet ve operasyon avantajı sunar.

!!! success "Öne çıkan fayda"
    Aynı platformda birden fazla organizasyon; veri ve yetki izolasyonu, merkezî yönetim.

---

## 2. Otomatik yedekleme

**MngAdmin** ve ilgili bileşenlerle entegre **otomatik yedekleme** desteği sunulur.

- Veritabanı ve kritik veriler zamanlanmış yedeklerle güvence altına alınır.
- Yedekleme konfigürasyonu ve zamanlamalar operatör tarafından yönetilebilir.
- Kurumsal ihtiyaçlara uygun, tekrarlanabilir yedekleme süreçleri tanımlanabilir.

!!! success "Öne çıkan fayda"
    Veri kaybı riskini azaltan, operasyonel devamlılığı destekleyen yedekleme altyapısı.

---

## 3. Chatbot desteği

**MngLLM** servisi ile platform içinde **yapay zeka destekli chatbot** entegrasyonu bulunur.

- Kullanıcılar arayüz üzerinden doğal dille soru sorabilir ve dokümantasyona dayalı yanıtlar alabilir.
- Chatbot, mevcut doküman seti ve dataset bilgileriyle beslenebilir; çok dilli yanıt desteği planlanabilir.
- Teknik destek, kullanım kılavuzları ve self-servis bilgi erişimi için uygun bir araç olarak kullanılabilir.

!!! success "Öne çıkan fayda"
    Kullanıcıların “nasıl yaparım?” sorularına anında, tutarlı ve dokümana dayalı yanıt alması.

---

## 4. Dinamik menü

Uygulama menüsü **domain/kullanıcı/rol bazlı** olarak dinamik yapılandırılır.

- Menü öğeleri ve erişim hakları sunucu tarafından yönetilir; kod değişikliği olmadan menü güncellenebilir.
- Farklı roller ve domain’ler için farklı menü setleri tanımlanabilir.
- Side-menu yönetimi ile navigasyon, iş süreçlerine ve yetkilere göre kişiselleştirilir.

!!! success "Öne çıkan fayda"
    Her müşteri ve rol için özelleştirilebilir, bakımı kolay menü yapısı.

---

## 5. Detaylı yetkilendirme (backend + arayüz)

Platformda **hem backend hem arayüz tarafında** tutarlı ve ayrıntılı yetkilendirme desteği sunulur.

- **Backend:** API endpoint’leri rol ve izinlere göre korunur; JWT tabanlı kimlik doğrulama ve Keycloak entegrasyonu ile merkezî yetki yönetimi sağlanır.
- **Arayüz:** Sayfa, menü ve bileşen erişimi kullanıcı rolüne ve yetkilerine bağlıdır; yetkisi olmayan işlemler gizlenir veya devre dışı bırakılır.
- Domain, kullanıcı ve grup bazlı yetki modeli ile kurumsal hiyerarşi ve sorumluluk alanları yansıtılabilir.

!!! success "Öne çıkan fayda"
    Uçtan uca güvenli erişim; backend ile arayüzün aynı yetki modeliyle uyumlu çalışması.

---

## 6. Dataset altyapısı

**MngDataGateway** ile zengin bir **dataset** modeli sunulur.

- Şema tanımına dayalı veri modelleri (metin, sayı, tarih, ilişki, dosya vb.) oluşturulabilir.
- Alan seviyesinde ve ifade/HTTP tabanlı **validasyon** kuralları tanımlanabilir.
- **Sorgulama**, **filtreleme**, **sayfalama** ve **export** API’leri ile veri yönetimi ve entegrasyon kolaylaştırılır.
- Kategoriler, indeksler ve ilişkili veri tipleri (örn. Persons, PersonGroups) desteklenir.

!!! success "Öne çıkan fayda"
    Kod yazmadan, yapılandırma odaklı veri modeli ve iş kuralları; hızlı uyarlama ve ölçeklenebilirlik.

---

## 7. Otomatik sayfa oluşturma

**Otomatik formlar / automated forms** ve dataset tabanlı sayfa üretimi ile içerik **şemadan türetilir**.

- Dataset tanımına göre listeleme, detay ve form sayfaları otomatik üretilebilir.
- Alan tipleri, validasyonlar ve ilişkiler tek yerden yönetilir; değişiklikler sayfalara yansır.
- Özel iş akışları ve alan tipleri (dosya yükleme, tarih seçici vb.) desteklenir.

!!! success "Öne çıkan fayda"
    Yeni veri tipleri ve ekranlar için geliştirme süresini kısaltan, yapılandırma odaklı sayfa üretimi.

---

## 8. Dinamik dashboard

Dashboard’lar **yapılandırılabilir widget** seti ile oluşturulabilir.

- Kullanıcı veya yönetici tarafından seçilen widget’lar tek bir dashboard’da bir araya getirilir.
- Grafik, tablo, özet kartları vb. bileşenlerle veri görselleştirme ve KPI takibi sağlanır.
- Widget kütüphanesi ve yerleşim mekanizması ile esnek dashboard deneyimi sunulur.

!!! success "Öne çıkan fayda"
    İhtiyaca göre özelleştirilebilir, veri odaklı kontrol panelleri; tek bir ekranda özet bilgi.

---

## 9. Dashboard döngüsü (rotation)

**Dashboard döngüsü** ile birden fazla dashboard sırayla ekranda gösterilir.

- Örn. lobi ekranları veya NOC ekranları için süreye göre otomatik geçiş yapılabilir.
- Kullanıcı veya operatör tarafından döngüye alınacak dashboard’lar ve süreler ayarlanabilir.
- Sürekli izleme ve bilgi paylaşımı senaryolarında kullanılır.

!!! success "Öne çıkan fayda"
    Büyük ekran ve sürekli izleme ortamlarında el ile değiştirme ihtiyacını azaltan, otomatik döngü desteği.

---

## 10. Lisanslama desteği

Platform, **domain ve kullanım bazlı lisanslama** ile yönetilebilir.

- Lisans bilgileri domain ile ilişkilendirilir; süre, kullanıcı sayısı veya özellik kapsamı kısıtlanabilir.
- Lisans yönetimi ve master key yapılandırması operatör tarafından yapılır.
- Kurumsal lisans modellerine uyum için altyapı sunulur.

!!! success "Öne çıkan fayda"
    Ticari modellerin (özellik/süre/kullanıcı kotaları) platform üzerinden yönetilebilmesi.

---

## 11. Profesyonel ve kullanıcı dostu arayüz

**Mng.Ui** (Nuxt 3 + Vuetify) ile modern, tutarlı ve kullanılabilir bir arayüz hedeflenir.

- Responsive tasarım, erişilebilirlik ve tutarlı bileşen kütüphanesi ile kurumsal kullanıcı deneyimi sunulur.
- Formlar, tablolar, filtreler ve geri bildirimlerle etkileşim net ve öngörülebilir kılınır.
- Tema ve dil tercihleri kullanıcı bazlı saklanabilir.

!!! success "Öne çıkan fayda"
    Eğitim maliyetini ve hata oranını düşüren, güven veren bir kullanıcı arayüzü.

---

## 12. Çok dilli destek (5 dil + runtime)

- **Arayüz** için **5 farklı dil** desteği (Türkçe, İngilizce ve diğerleri) sağlanır; dil seçimi kullanıcı tercihine bırakılır.
- **Dinamik araçlar** (otomatik formlar, chatbot, raporlar vb.) için **runtime’da dil seçimi** veya locale bazlı içerik sunma imkânı hedeflenir.
- Çok dilli müşteri ve uluslararası kullanım senaryolarına uyum kolaylaştırılır.

!!! success "Öne çıkan fayda"
    Yerel ve uluslararası kullanıcılar için tek platformda tutarlı çok dilli deneyim.

---

## 13. Tamamlanmış CI/CD süreçleri

MonitraNG, **GitLab tabanlı CI/CD** ile build, test ve deploy süreçlerini otomatikleştirir.

- Pipeline’lar derleme, test ve artefact üretimini kapsar; ortam bazlı deploy adımları tanımlanabilir.
- Dokümantasyon (MkDocs) build’i ve yayını CI/CD ile entegre edilebilir.
- Tutarlı sürümler, tekrarlanabilir kurulumlar ve hızlı güncelleme imkânı sunulur.

!!! success "Öne çıkan fayda"
    Güvenilir, tekrarlanabilir dağıtım; daha az manuel hata, daha hızlı teslimat.

---

## 14. Profesyonel dokümantasyon desteği

- **MkDocs** ile yapılandırılmış, arama destekli dokümantasyon sitesi sunulur.
- **Servis bazlı** doküman yapısı (Changelog, Roadmap, Technical Specs) ile geliştirici ve test ekipleri tek kaynaktan güncel bilgiye ulaşır.
- Kullanıcı rehberleri, API referansları, kurulum ve sorun giderme sayfaları tek çatı altında toplanır; gerektiğinde Chatbot ile entegre edilebilir.

!!! success "Öne çıkan fayda"
    Müşteri ve iç ekipler için güncel, tutarlı ve aranabilir dokümantasyon; proje olgunluğunun göstergesi.

---

## Sunumda kullanım önerileri

| Hedef kitle      | Vurgulanacak başlıklar |
|------------------|-------------------------|
| **İş / yönetim** | Multi-tenant, detaylı yetkilendirme, lisanslama, otomatik yedekleme, profesyonel arayüz, CI/CD ve dokümantasyon |
| **Teknik / IT**  | Dataset, otomatik sayfa, dinamik menü, detaylı yetkilendirme (backend + arayüz), dashboard/döngü, çok dilli altyapı, CI/CD |
| **Son kullanıcı**| Chatbot, dinamik menü, kullanıcı dostu arayüz, 5 dil, dashboard ve döngü |

Sunum sırasında bu sayfayı ekran paylaşımı ile açıp bölüm bölüm ilerleyebilir; detay için sol menüden ilgili servis sayfalarına (Technical Specs, Roadmap, Guides) geçiş yapılabilir.

---

*Bu sayfa platformun öne çıkan özelliklerinin özet referansıdır. Güncel teknik detaylar için ilgili servislerin [Technical Specs](MngKeeper/main/TECHNICAL_SPECS.md) ve [Roadmap](ROADMAP.md) sayfalarına bakınız.*
