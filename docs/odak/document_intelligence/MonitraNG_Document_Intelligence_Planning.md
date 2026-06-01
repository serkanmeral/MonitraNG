# MonitraNG Document Intelligence Planlama Dokümanı

## 1. Amaç

Bu doküman, MonitraNG platformu içerisinde geliştirilecek **Document Intelligence** modülünün fazlara ayrılmış teknik ve fonksiyonel planını içerir.

Modülün ilk amacı müşterinin öncelikli ihtiyacı olan kaynak ağacı, klasör yönetimi, markdown doküman oluşturma ve dosya yükleme özelliklerini karşılamaktır. Uzun vadeli hedef ise bu yapıyı yapay zeka destekli kurumsal bilgi yönetimi, doküman analizi, benzer doküman bulma ve OperationCore entegrasyonu ile zenginleştirmektir.

Bu doküman CursorAI veya benzeri geliştirme asistanlarına doğrudan verilebilecek şekilde hazırlanmıştır.

---

## 2. Ürün Konumlandırması

Modülün adı **Document Intelligence** olarak belirlenmiştir.

Bu isimlendirme özellikle tercih edilmiştir çünkü hedef yalnızca klasik bir dosya yönetim sistemi geliştirmek değildir. Modül zaman içerisinde aşağıdaki yetenekleri kapsayacaktır:

- Kaynak ve klasör yönetimi
- Dosya yükleme
- Markdown tabanlı doküman oluşturma
- Kurumsal bilgi ağacı
- Yetki kontrollü doküman erişimi
- Doküman özetleme
- İçerik çıkarımı
- Benzer doküman bulma
- OperationCore WorkItem entegrasyonu
- AI destekli soru-cevap
- Kurumsal hafıza oluşturma

---

## 3. Temel Yaklaşım

Document Intelligence içinde dosya sistemi ve wiki mantığı ayrı modüller olarak düşünülmeyecektir. Tüm içerikler tek bir kaynak ağacı altında yönetilecektir.

Örnek yapı:

```text
Resources
 ├── IT
 │    ├── LDAP Kurulum Rehberi.md
 │    ├── Active Directory Topolojisi.pdf
 │    └── Network
 │         ├── Firewall Rehberi.md
 │         └── Switch Envanteri.xlsx
 ├── İnsan Kaynakları
 │    └── Personel Prosedürü.docx
 └── Operasyon
      ├── Bakım Talimatı.md
      └── Arıza Analiz Raporu.pdf
```

Bu yapı içinde klasör, markdown doküman ve yüklenmiş dosya aynı ağaç içerisinde birlikte yönetilecektir.

---

## 4. Ana Kararlar

### 4.1 Modül Adı

Modül adı **Document Intelligence** olacaktır.

### 4.2 Kaynak Ağacı

Tüm dosyalar, klasörler ve markdown dokümanlar tek bir tree yapısı altında tutulacaktır.

### 4.3 Wiki ve Dosya Yönetimi Ayrımı

Ayrı bir wiki modülü yapılmayacaktır. Markdown doküman oluşturma özelliği Document Intelligence içindeki doğal doküman tipi olacaktır.

### 4.4 Yetkilendirme

Yetkilendirme kullanıcı bazlı değil, öncelikli olarak **grup bazlı** olacaktır.

Yetkiler klasör seviyesinde tanımlanacaktır. Dosyalar ve markdown dokümanlar varsayılan olarak içinde bulundukları klasörün yetkilerini miras alacaktır.

### 4.5 Dosya Bazlı Yetki

İlk fazlarda dosya bazlı özel yetki önerilmemektedir. Bu, yönetim karmaşıklığını artıracağı için sonraki fazlara bırakılacaktır.

### 4.6 Yetki Mirası

Alt klasörler üst klasörden yetki mirası alabilecektir. Gerekirse bir klasörde miras kırılabilecek ve o klasör için özel grup yetkileri tanımlanabilecektir.

### 4.7 Versiyonlama

Versiyonlama ilk günden veri modeli seviyesinde tasarlanacaktır. İlk fazda gelişmiş versiyon arayüzü yapılmasa bile dokümanların gelecekte versiyon geçmişi taşıyabilmesi için altyapı hazır olmalıdır.

### 4.8 Check-In / Check-Out

İlk fazlarda check-in/check-out mekanizması yapılmayacaktır. Başlangıçta “son kaydeden kazanır” yaklaşımı yeterlidir.

### 4.9 OperationCore Entegrasyonu

OperationCore entegrasyonu sonradan eklenen basit bir özellik olarak değil, ürünün temel kabiliyetlerinden biri olarak ele alınacaktır.

WorkItem oluşturulurken veya düzenlenirken Document Intelligence içinden dosya/doküman seçilebilecektir. Aynı şekilde doküman detay ekranında ilişkili WorkItem kayıtları gösterilebilecektir.

### 4.10 AI Hazırlığı

AI özellikleri Faz 3 ve sonrasında aktif hale gelecek olsa bile Faz 1'den itibaren text extraction, metadata extraction ve AI alanları için temel veri modeli hazırlanmalıdır.

---

## 5. Faz Planı

## Faz 1 - Resources ve Temel Doküman Yönetimi

### Amaç

Müşterinin ilk ihtiyacını karşılayan kaynak ağacı, klasör yönetimi, markdown doküman oluşturma ve dosya yükleme özelliklerini geliştirmek.

### Kapsam

- Resources ana ekranı
- Sol panelde tree görünümü
- Klasör oluşturma
- Alt klasör oluşturma
- Klasör yeniden adlandırma
- Klasör silme
- Klasör taşıma
- Klasör altında dosya/doküman listeleme
- Markdown doküman oluşturma
- Markdown editör
- Markdown preview
- Markdown dokümanı kaydetme
- Markdown dokümanı düzenleme
- Dosya yükleme
- Dosya metadata kaydı
- Dosya indirme
- Dosya görüntüleme için temel preview hazırlığı
- Grup bazlı klasör yetkileri
- Yetki mirası
- Temel arama
- Temel audit bilgileri

### UI Ekranları

```text
/pages/document-intelligence/resources
/pages/document-intelligence/resources/create-document
/pages/document-intelligence/resources/upload-document
/pages/document-intelligence/resources/detail/[id]
```

### Resources Ana Ekranı

Sol panel:

- Tree view
- Klasörler
- Alt klasörler
- Yetkili olunan kaynaklar

Sağ panel:

- Seçilen klasör içeriği
- Yeni Klasör butonu
- Yeni Doküman butonu
- Dosya Yükle butonu
- Liste/grid görünümü
- Arama alanı

### Create Document Ekranı

Alanlar:

- Başlık
- Hedef klasör
- Açıklama
- Etiketler
- Markdown editör
- Markdown preview
- Kaydet
- Taslak olarak kaydet

### Upload Document Ekranı

Alanlar:

- Hedef klasör
- Dosya seçimi
- Açıklama
- Etiketler
- Upload butonu

İlk aşamada desteklenecek dosya tipleri:

- PDF
- DOCX
- MD
- TXT
- XLSX
- PPTX
- PNG
- JPG
- ZIP

### Faz 1 Çıktısı

Kullanıcılar yetkili oldukları klasörlerde kaynak ağacı oluşturabilir, markdown doküman yazabilir, dosya yükleyebilir ve dokümanlara erişebilir.

---

## Faz 2 - OperationCore Entegrasyonu

### Amaç

Document Intelligence ile OperationCore WorkItem yapısını birbirine bağlamak.

### Kapsam

- WorkItem create/edit ekranında doküman seçebilme
- WorkItem detayında ilişkili dokümanları gösterme
- Doküman detayında ilişkili WorkItem kayıtlarını gösterme
- İlişki tipi belirleme
- Çift yönlü navigasyon
- Yetki kontrollü ilişki gösterimi

### İlişki Tipleri

```text
reference  -> Referans doküman
attachment -> Ek doküman
evidence   -> Kanıt / çıktı dokümanı
output     -> İş sonucunda üretilen doküman
```

### WorkItem Tarafındaki Davranış

WorkItem oluştururken veya düzenlerken “İlgili Dokümanlar” alanı bulunmalıdır.

Kullanıcı bu alandan Document Intelligence kaynak ağacını açabilmeli ve yetkili olduğu dokümanları seçebilmelidir.

### Document Tarafındaki Davranış

Doküman detay ekranında “İlişkili İşler” bölümü bulunmalıdır.

Bu bölümde aşağıdaki bilgiler gösterilebilir:

- WorkItem başlığı
- Workspace / Board bilgisi
- State
- Priority
- Atanan kişi
- Oluşturulma tarihi
- İlişki tipi

### Yetkilendirme

İlişki listelenirken iki taraflı yetki kontrolü yapılmalıdır:

- Kullanıcı dokümanı görebiliyor mu?
- Kullanıcı WorkItem kaydını görebiliyor mu?

Yetkisi olmayan kayıtlar listelenmemelidir.

### Faz 2 Çıktısı

Document Intelligence ve OperationCore arasında çift yönlü, yetki kontrollü doküman-iş ilişkisi kurulmuş olur.

---

## Faz 3 - Text Extraction ve AI Özetleme

### Amaç

Text tabanlı dosyalardan içerik çıkarımı yapmak ve AI destekli doküman özetleme kabiliyeti kazandırmak.

### Kapsam

- PDF text extraction
- DOCX text extraction
- MD text extraction
- TXT text extraction
- İçerik metnini normalize etme
- İçerik uzunluğu hesaplama
- AI özet üretme
- Anahtar kelime üretme
- Otomatik etiket önerisi
- AI işlem durumlarının tutulması

### Desteklenecek İlk Dosya Tipleri

```text
PDF
DOCX
MD
TXT
```

### AI İşlem Alanları

Her doküman için aşağıdaki bilgiler tutulmalıdır:

- contentExtracted
- extractedText
- contentLength
- summary
- keywords
- aiTags
- aiProcessedAt
- aiProcessingStatus
- aiProcessingError

### UI Davranışı

Doküman detay ekranında şu bölümler eklenmelidir:

- AI Özeti
- Anahtar Kelimeler
- Önerilen Etiketler
- İçerik çıkarım durumu

### Faz 3 Çıktısı

Kullanıcılar PDF, DOCX, MD ve TXT gibi text tabanlı dokümanlarda otomatik özet ve anahtar kelime bilgilerini görebilir.

---

## Faz 4 - Semantic Search ve Benzer Dokümanlar

### Amaç

Doküman içeriklerinden embedding üreterek benzer doküman bulma ve semantik arama yeteneği kazandırmak.

### Kapsam

- Extract edilmiş text üzerinden embedding üretimi
- Embedding metadata yönetimi
- Vector search altyapısı
- Benzer doküman bulma
- Doküman detayında “Benzer Dokümanlar” bölümü
- Arama ekranında semantik arama seçeneği

### Benzer Doküman Örneği

```text
Bu dokümana benzer içerikler:

- LDAP Kurulum Rehberi.docx (%91)
- Keycloak Federation.pdf (%88)
- Kullanıcı Yönetimi.md (%84)
```

### Yetkilendirme

Semantic search sonuçları da yetki kontrollü olmalıdır. Kullanıcının göremeyeceği dokümanlar semantik arama sonuçlarında veya benzer doküman listesinde gösterilmemelidir.

### Faz 4 Çıktısı

Kullanıcılar klasik dosya adı araması dışında, içerik anlamına göre benzer dokümanları bulabilir.

---

## Faz 5 - Kurumsal Bilgi Asistanı

### Amaç

Document Intelligence üzerinde yetki kontrollü RAG tabanlı soru-cevap yeteneği geliştirmek.

### Kapsam

- Doküman içerikleri üzerinden soru-cevap
- Kaynak göstererek cevap üretme
- Kullanıcı yetkilerine göre kaynak filtreleme
- OperationCore ilişkilerini cevaplarda kullanabilme
- Cevap içinde ilgili dokümanları ve WorkItem kayıtlarını gösterebilme

### Örnek Kullanıcı Sorusu

```text
LDAP senkronizasyon süreci nasıl çalışıyor?
```

Sistem yetkili dokümanları tarar ve cevap üretirken kaynaklarını gösterir.

### Faz 5 Çıktısı

Document Intelligence, MonitraNG içinde kurumsal hafıza ve bilgi asistanı rolünü üstlenir.

---

## Faz 6 - Kurumsal Olgunlaşma

### Amaç

Document Intelligence modülünü kurumsal doküman yaşam döngüsü, onay süreçleri ve gelişmiş denetim özellikleri ile olgunlaştırmak.

### Olası Kapsam

- Doküman onay süreçleri
- Taslak / İncelemede / Onaylandı / Yayında / Arşivlendi durumları
- Revizyon notları
- Doküman yayınlama
- Doküman arşivleme
- Okundu bilgisi
- Dağıtım listeleri
- Gelişmiş audit log
- Gelişmiş raporlar
- ISO 9001 / ISO 27001 doküman yönetimi desteği

Bu faz şu an için ileri faz olarak değerlendirilecektir.

---

## 6. Veri Modeli Taslakları

## 6.1 dm_resources

Klasör, markdown doküman ve yüklenen dosyaların ana kaydıdır.

```json
{
  "__dataId": "guid",
  "parentId": "guid | null",
  "type": "folder | markdown | file",
  "name": "string",
  "title": "string",
  "description": "string",
  "extension": "string",
  "mimeType": "string",
  "size": 0,
  "storageProvider": "minio",
  "storagePath": "string",
  "contentType": "markdown | binary | text",
  "tags": ["string"],
  "currentVersionId": "guid | null",
  "createdBy": {
    "userId": "string",
    "username": "string",
    "displayName": "string",
    "email": "string"
  },
  "createdAt": "datetime",
  "updatedBy": {
    "userId": "string",
    "username": "string",
    "displayName": "string",
    "email": "string"
  },
  "updatedAt": "datetime",
  "__isDeleted": false
}
```

### Notlar

- `type = folder` olan kayıtlarda storagePath boş olabilir.
- `type = markdown` olan kayıtlarda içerik md dosyası olarak MinIO’da saklanabilir veya ayrı content koleksiyonunda tutulabilir.
- `type = file` olan kayıtlarda orijinal dosya MinIO’da tutulur.

---

## 6.2 dm_resource_permissions

Klasör seviyesinde grup bazlı yetkileri tutar.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "groupId": "string",
  "groupName": "string",
  "permissions": [
    "view",
    "create",
    "edit",
    "delete",
    "upload",
    "download",
    "move",
    "share"
  ],
  "inheritPermissions": true,
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "updatedBy": "userInfo",
  "updatedAt": "datetime",
  "__isDeleted": false
}
```

### Notlar

- Yetkiler öncelikli olarak klasör kaynakları için kullanılacaktır.
- Dosya ve markdown dokümanlar klasör yetkisini miras alacaktır.
- `inheritPermissions = false` olduğunda ilgili klasör kendi yetkileri ile çalışır.

---

## 6.3 dm_resource_versions

Doküman ve dosya versiyonlarını tutar.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "versionNumber": 1,
  "versionLabel": "v1",
  "changeNote": "string",
  "storagePath": "string",
  "size": 0,
  "mimeType": "string",
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "__isDeleted": false
}
```

### Notlar

- İlk fazda UI üzerinde gelişmiş versiyon yönetimi olmayabilir.
- Ancak her yeni kayıt ilk versiyon olarak oluşturulmalıdır.
- Gelecekte versiyon geçmişi ve geri dönme özellikleri eklenebilir.

---

## 6.4 entity_links

Genel amaçlı entity ilişki koleksiyonudur. Yalnızca doküman-workitem değil, gelecekte diğer modüller arası ilişkiler için de kullanılabilir.

```json
{
  "__dataId": "guid",
  "sourceModule": "documentIntelligence",
  "sourceType": "resource",
  "sourceId": "guid",
  "targetModule": "operationCore",
  "targetType": "workItem",
  "targetId": "guid",
  "relationType": "reference | attachment | evidence | output",
  "description": "string",
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "__isDeleted": false
}
```

### Notlar

Bu koleksiyon ileride şu ilişkiler için de kullanılabilir:

- Document ↔ WorkItem
- Document ↔ Asset
- Document ↔ Alarm
- WorkItem ↔ Asset
- Alarm ↔ Asset

---

## 6.5 dm_resource_ai

Dokümanların AI ve içerik analiz bilgilerini tutar.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "contentExtracted": false,
  "extractedTextStoragePath": "string",
  "contentLength": 0,
  "summary": "string",
  "keywords": ["string"],
  "aiTags": ["string"],
  "embeddingGenerated": false,
  "embeddingModel": "string",
  "embeddingDate": "datetime | null",
  "aiProcessingStatus": "pending | processing | completed | failed",
  "aiProcessingError": "string",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

### Notlar

- Büyük extracted text verisi doğrudan MongoDB içine gömülmeyebilir. MinIO veya ayrı storage path üzerinden saklanabilir.
- Summary, keywords ve aiTags hızlı erişim için MongoDB’de tutulabilir.

---

## 7. API Taslakları

## 7.1 Resource API

```text
GET    /api/document-intelligence/resources/tree
GET    /api/document-intelligence/resources/{id}
GET    /api/document-intelligence/resources/{id}/children
POST   /api/document-intelligence/resources/folder
PUT    /api/document-intelligence/resources/{id}/rename
PUT    /api/document-intelligence/resources/{id}/move
DELETE /api/document-intelligence/resources/{id}
```

## 7.2 Markdown Document API

```text
POST   /api/document-intelligence/resources/markdown
PUT    /api/document-intelligence/resources/markdown/{id}
GET    /api/document-intelligence/resources/markdown/{id}/content
```

## 7.3 Upload API

```text
POST   /api/document-intelligence/resources/upload
GET    /api/document-intelligence/resources/{id}/download
GET    /api/document-intelligence/resources/{id}/preview
```

## 7.4 Permission API

```text
GET    /api/document-intelligence/resources/{id}/permissions
PUT    /api/document-intelligence/resources/{id}/permissions
POST   /api/document-intelligence/resources/{id}/break-inheritance
POST   /api/document-intelligence/resources/{id}/restore-inheritance
```

## 7.5 OperationCore Link API

```text
GET    /api/document-intelligence/resources/{id}/links
POST   /api/document-intelligence/resources/{id}/links
DELETE /api/document-intelligence/resources/{id}/links/{linkId}

GET    /api/operation-core/workitems/{id}/documents
POST   /api/operation-core/workitems/{id}/documents
DELETE /api/operation-core/workitems/{id}/documents/{linkId}
```

## 7.6 AI API

```text
POST   /api/document-intelligence/resources/{id}/extract-text
POST   /api/document-intelligence/resources/{id}/generate-summary
GET    /api/document-intelligence/resources/{id}/ai
GET    /api/document-intelligence/resources/{id}/similar
POST   /api/document-intelligence/search/semantic
POST   /api/document-intelligence/assistant/ask
```

---

## 8. Yetkilendirme Kuralları

### 8.1 Temel İzinler

```text
view      -> Kaynağı görebilir
create    -> Alt klasör veya doküman oluşturabilir
edit      -> Düzenleyebilir
delete    -> Silebilir
upload    -> Dosya yükleyebilir
download  -> Dosya indirebilir
move      -> Taşıyabilir
share     -> Paylaşabilir veya ilişkilendirebilir
```

### 8.2 Tree Filtreleme

Kullanıcı yalnızca `view` yetkisine sahip olduğu klasörleri ve kaynakları görmelidir.

### 8.3 AI Yetki Kontrolü

AI özetleme, benzer doküman ve RAG cevaplarında yalnızca kullanıcının görmeye yetkili olduğu dokümanlar kullanılmalıdır.

### 8.4 OperationCore Yetki Kontrolü

Document Intelligence içinde ilişkili WorkItem listelenirken kullanıcının WorkItem üzerinde görüntüleme yetkisi kontrol edilmelidir.

---

## 9. Teknik Mimari Notları

### 9.1 Storage

Dosyalar MinIO üzerinde saklanacaktır.

MongoDB yalnızca metadata, ilişki, permission, AI summary ve index bilgilerini tutacaktır.

### 9.2 Multi-Tenant Yapı

MonitraNG mevcut domain/tenant mimarisine uygun çalışmalıdır.

Her tenant/domain kendi MongoDB veritabanı ve MinIO bucket/path izolasyonuna sahip olmalıdır.

### 9.3 Token ve Kullanıcı Bilgisi

Kullanıcı bilgileri access token üzerinden alınmalıdır.

Kaydedilecek audit bilgileri:

- userId
- username
- displayName
- email
- domain
- groups

### 9.4 Event Yayını

İleride RabbitMQ üzerinden şu eventler yayınlanabilir:

```text
document.created
document.updated
document.deleted
document.uploaded
document.linked
document.summary.generated
document.embedding.generated
```

Bu eventler OperationCore, Notification veya Audit servisleri tarafından kullanılabilir.

---

## 10. CursorAI İçin Geliştirme Sırası

Aşağıdaki sıra uygulanabilir geliştirme sırası olarak önerilir.

### Step 1

Veri modellerini oluştur:

- dm_resources
- dm_resource_permissions
- dm_resource_versions
- entity_links
- dm_resource_ai

### Step 2

Resource backend servislerini oluştur:

- Create folder
- Rename folder
- Move folder
- Delete resource
- Get tree
- Get children

### Step 3

Markdown document servislerini oluştur:

- Create markdown
- Update markdown
- Get markdown content
- Save as `.md` file or content storage

### Step 4

Upload servislerini oluştur:

- Upload file
- Save metadata
- Save file to MinIO
- Download file
- Basic preview endpoint

### Step 5

Permission servislerini oluştur:

- Get permissions
- Update folder permissions
- Apply inheritance
- Break inheritance
- Filter tree by permissions

### Step 6

Nuxt UI ekranlarını oluştur:

- Resources page
- Tree component
- Folder content list
- Create folder modal
- Create markdown page
- Upload document page
- Resource detail page

### Step 7

OperationCore entegrasyonunu ekle:

- WorkItem document selector
- entity_links kullanımı
- WorkItem detail documents tab
- Resource detail related workitems tab

### Step 8

AI altyapısını ekle:

- Text extraction jobs
- Summary generation
- Keywords
- AI metadata UI

### Step 9

Semantic search ekle:

- Embedding generation
- Similar documents
- Semantic search endpoint

### Step 10

RAG assistant ekle:

- Ask endpoint
- Permission aware retrieval
- Source based answer

---

## 11. Başlangıçta Yapılmayacaklar

İlk fazlarda aşağıdaki özellikler yapılmayacaktır:

- Check-in / check-out
- Dosya bazlı özel yetki
- Gelişmiş onay süreçleri
- E-imza
- OCR
- Gelişmiş doküman yaşam döngüsü
- ISO doküman onay akışları
- Public share link
- Harici kullanıcı paylaşımı

Bu özellikler daha sonraki fazlarda değerlendirilecektir.

---

## 12. Nihai Hedef

Document Intelligence modülü uzun vadede MonitraNG içindeki kurumsal bilgi merkezi olacaktır.

Hedeflenen nihai yapı:

```text
Document Intelligence
        ↓
Knowledge Base
        ↓
Corporate Memory
        ↓
AI Assistant
```

Uzun vadede kullanıcı şu tarz sorular sorabilmelidir:

```text
Geçen yıl Ankara fabrikasındaki UPS arızası ile ilgili tüm işleri, dokümanları ve alınan aksiyonları getir.
```

Sistem bu soruya aşağıdaki kaynakları birleştirerek cevap verebilmelidir:

- Dokümanlar
- WorkItem kayıtları
- Alarm kayıtları
- Asset bilgileri
- Raporlar
- AI özetleri

Bu nedenle ilk fazda sade ve uygulanabilir başlamak, ancak veri modelini ve mimariyi uzun vadeli kurumsal bilgi yönetimine uygun tasarlamak esastır.

---

## 13. Özet Faz Tablosu

| Faz | Ad | Ana Amaç |
|---|---|---|
| Faz 1 | Resources ve Temel Doküman Yönetimi | Tree, klasör, markdown, upload, yetki |
| Faz 2 | OperationCore Entegrasyonu | WorkItem ↔ Doküman ilişkisi |
| Faz 3 | Text Extraction ve AI Özetleme | PDF/DOCX/MD/TXT içerik çıkarımı ve özet |
| Faz 4 | Semantic Search | Benzer doküman ve vektör arama |
| Faz 5 | Kurumsal Bilgi Asistanı | RAG tabanlı soru-cevap |
| Faz 6 | Kurumsal Olgunlaşma | Onay, lifecycle, audit, compliance |

---

## 14. Sonuç

Bu plan, müşterinin mevcut kaynak yönetimi talebini karşılayacak şekilde hızlı başlanabilir bir Faz 1 sunar. Aynı zamanda MonitraNG'in uzun vadeli Document Intelligence vizyonuna uygun şekilde AI, semantik arama, OperationCore entegrasyonu ve kurumsal bilgi yönetimi için güçlü bir temel oluşturur.
