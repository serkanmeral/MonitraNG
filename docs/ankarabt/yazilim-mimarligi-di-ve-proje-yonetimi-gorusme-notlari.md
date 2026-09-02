# Yazılım Mimarlığı, Document Intelligence ve Proje Yönetimi Görüşme Notları

**Tarih:** 4 Ağustos 2026  
**Durum:** İlk değerlendirme — daha sonra devam edilecek

## Görüşmenin Bağlamı

AnkaraBT kapsamındaki olası yazılım mimarlığı görevi için, bir yazılım mimarının üreteceği bilgi ve dokümanların MonitraNG içerisinde nasıl yönetilebileceği konuşuldu.

`docs/ankarabt/` altında bulunan aşağıdaki dosyalar proje için örnek kaynak belgeler olarak ele alındı:

- Teknik şartname
- Personel nitelikleri

Bu belgelerin içerikleri bu görüşmede ayrıntılı olarak incelenmedi.

## Document Intelligence İçin Ana Yaklaşım

Document Intelligence yalnızca bir dosya arşivi olmamalı; kurumun yaşayan **mimari bilgi ve karar yönetim sistemi** olarak konumlandırılmalıdır.

Önerilen üç bilgi katmanı:

1. **Kaynak belgeler**
   - Teknik şartnameler
   - Personel nitelikleri
   - Mevzuat ve standartlar
   - Müşteri talepleri
   - Toplantı kayıtları

2. **Mimari bilgi**
   - Gereksinimler ve kısıtlar
   - Fonksiyonel olmayan gereksinimler
   - Mimari kararlar (ADR)
   - Riskler ve varsayımlar
   - Sistemler, bileşenler ve entegrasyonlar
   - Veri, güvenlik ve dağıtım mimarileri

3. **Üretilen çıktılar**
   - Yazılım mimarisi dokümanları
   - C4/UML görünümleri
   - Mimari karar kayıtları
   - Gereksinim izlenebilirlik matrisi
   - Mimari değerlendirme ve onay paketleri
   - Baseline ve sürüm paketleri

## Bir Yazılım Mimarının Temel Doküman İhtiyaçları

- Proje kapsamı, paydaşlar ve mimari ilkeler
- Fonksiyonel olmayan gereksinimler
- Sistem bağlamı ve C4 görünümleri
- Entegrasyon ve API sözleşmeleri
- Veri mimarisi
- Güvenlik ve yetkilendirme mimarisi
- Deployment, ölçekleme ve felaket kurtarma
- Gözlemlenebilirlik, SLO ve operasyon
- ADR'ler, riskler, varsayımlar ve teknik borç
- Gereksinim–karar–bileşen–iş kaydı–test izlenebilirliği

## Document Intelligence İçin Önerilen Zenginleştirmeler

- Standart mimari doküman türleri ve şablonları
- Proje bazlı Mimari Çalışma Alanı
- Dokümanlar arasında anlamlı ilişkiler:
  - `implements`
  - `derivedFrom`
  - `supersedes`
  - `dependsOn`
  - `conflictsWith`
- İnceleme, onay ve yayın akışları
- Mimari baseline oluşturma
- DOCX ve PDF içerik araması
- Gereksinim ve mimari karar çıkarımı
- Yetkiye duyarlı yapay zekâ asistanı
- Eksik, çelişkili veya etkilenmiş dokümanları gösteren panolar

DI içerisinde tam kapsamlı yeni bir UML aracı geliştirmek yerine Mermaid, PlantUML ve draw.io gibi araçların çıktılarının desteklenmesi; DI'ın bunların kaydını, ilişkilerini, sürümlerini ve yönetişimini üstlenmesi önerildi.

İlk değerli sürüm için öncelikler:

1. Mimari doküman türleri ve şablonları
2. Dokümanlar arası ilişki modeli
3. İnceleme, onay ve baseline yönetimi
4. DOCX/PDF içerik araması

Yapay zekâ özelliklerinin sağlam bir metadata ve ilişki modelinden sonra eklenmesi gerektiği değerlendirildi.

## Microsoft Project Benzeri Proje Yönetimi

Microsoft Project benzeri bir proje yönetimi çözümünün geliştirilmesinin mümkün olduğu, ancak bunun DI içerisine eklenmek yerine ayrı bir **Proje Yönetimi modülü** olarak tasarlanmasının daha doğru olacağı değerlendirildi.

Önerilen sorumluluk ayrımı:

- **Proje Yönetimi:** WBS, takvim, bağımlılıklar, kilometre taşları, kaynaklar, maliyet ve baseline
- **OperationCore:** Günlük işlerin ve work item'ların yürütülmesi
- **Document Intelligence:** Şartname, karar, çıktı ve kanıt dokümanlarının yönetilmesi

Proje yönetimi için aşamalı özellik kapsamı:

1. Proje, WBS, görev, kilometre taşı, Gantt ve bağımlılıklar
2. Çalışma takvimleri, kritik yol, otomatik zamanlama ve baseline
3. Kaynak kapasitesi, kaynak dengeleme, bütçe ve maliyet
4. Portföy, program, risk, değişiklik, raporlama ve senaryo planlama

En zor alanların Gantt arayüzünden ziyade takvim hesapları, kritik yol, bağımlılık döngüleri, kaynak dengeleme ve baseline yönetimi olduğu not edildi.

## OperationCore Workspace Entegrasyonu

Proje planlamasının OperationCore workspace ve work item'larıyla birleştirilmesi mümkün ve tercih edilen yaklaşım olarak değerlendirildi.

Temel ilişki:

`Proje → WBS kalemi → OC Workspace → OC Work Item'ları`

Bir WBS kalemi:

- Tek bir work item'a
- Bir work item grubuna
- Bir etiket veya sorguya
- Birden fazla workspace içerisindeki work item'lara

bağlanabilir.

Proje görünümünde gösterilebilecek bilgiler:

- Planlanan ve gerçekleşen ilerleme
- Gantt üzerinde work item durumları
- Geciken ve bloklanan işler
- Kritik yol üzerindeki açık işler
- Kilometre taşı durumu
- Planlanan ve gerçekleşen tarihler
- Ekip veya workspace bazlı ilerleme
- Baseline sapması
- İlgili DI belgeleri ve teslimatlar

OC work item'larının yürütmenin tek kaynağı olması, proje modülünde aynı görevlerin yeniden oluşturulmaması gerektiği vurgulandı.

İlerleme hesabının yalnızca tamamlanan iş adedine göre değil; tahmini efor, planlanan süre, iş ağırlığı veya maliyet üzerinden ağırlıklı yapılması gerektiği değerlendirildi.

Olası olay tabanlı güncelleme akışı:

1. OC work item durumu değişir.
2. Değişiklik olayı yayımlanır.
3. Proje modülü ilgili WBS kalemini günceller.
4. Üst WBS kalemlerinin ilerlemesi yeniden hesaplanır.
5. Takvim, kritik yol ve sapma göstergeleri güncellenir.

Bir projenin tek bir workspace ile sınırlandırılmaması; birincil proje workspace'ine ek olarak ekip veya iş paketi bazlı bağlı workspace'lerin desteklenmesi önerildi.

## Genel Entegrasyon Vizyonu

`Şartname (DI) → Gereksinim → Proje/WBS → OC Work Item → Test/Çıktı → DI Kanıt Dokümanı`

Bu yapı içinde:

- DI kurumsal bilgi ve doküman katmanı,
- Proje Yönetimi planlama ve kontrol katmanı,
- OperationCore ise günlük yürütme katmanı

olarak görev yapacaktır.

## Daha Sonra Ele Alınacak Konular

- AnkaraBT teknik şartnamesinin mimari gereksinimlerinin çıkarılması
- Personel nitelikleri belgesinin görev ve sorumluluklarla ilişkilendirilmesi
- Mimari doküman taksonomisinin belirlenmesi
- Proje Yönetimi modülünün sınırlarının netleştirilmesi
- WBS ile OC work item ilişki modelinin tasarlanması
- İlerleme hesaplama yönteminin seçilmesi
- DI, Proje Yönetimi ve OperationCore arasındaki veri sahipliğinin kesinleştirilmesi
- İlk MVP kapsamının ve geliştirme fazlarının belirlenmesi
