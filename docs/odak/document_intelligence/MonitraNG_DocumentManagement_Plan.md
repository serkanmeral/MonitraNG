# MonitraNG DocumentManagement Planı

## 1. Amaç

Bu dokümanın amacı, MonitraNG platformu içinde geliştirilecek DocumentManagement modülünü uygulanabilir fazlara bölmek ve CursorAI gibi geliştirme araçlarına verilebilecek net bir teknik plan oluşturmaktır.

DocumentManagement modülü yalnızca dosya yükleme ve klasörleme sistemi olarak düşünülmemelidir. Uzun vadede bu modül, MonitraNG içinde kurumsal bilgi yönetimi, doküman zekası, iş bağlamı ile ilişkilendirme ve yapay zeka destekli analiz yeteneklerinin merkezi olacaktır.

İlk müşteri ihtiyacı, kaynaklar benzeri bir yapı oluşturmaktır. Bu yapı tree görünümünde klasörler, alt klasörler ve dosyalar içerecektir. Kullanıcılar sistem içinde markdown doküman oluşturabilecek veya harici dosyalarını sisteme yükleyebilecektir.

## 2. Genel Vizyon

Modülün uzun vadeli hedefi aşağıdaki başlıklardan oluşur:

- Kaynak ağacı yönetimi
- Klasör ve alt klasör yapısı
- Markdown doküman oluşturma
- Harici dosya yükleme
- Grup tabanlı yetkilendirme
- Dosya ve doküman metadata yönetimi
- OperationCore work item entegrasyonu
- Text tabanlı dokümanlardan içerik çıkarma
- Yapay zeka destekli özetleme
- Alakalı ve benzer dokümanları bulma
- Kurumsal bilgi asistanı altyapısı
- Versiyonlama ve audit altyapısı

## 3. Temel Kavramlar

### 3.1 Resource

Resource, DocumentManagement içindeki ana kayıt tipidir. Bir resource aşağıdaki türlerden biri olabilir:

- folder
- markdown
- file

Folder, tree yapısında klasörleri temsil eder.

Markdown, sistem içinde markdown editör ile oluşturulan dokümanları temsil eder.

File, upload edilen PDF, DOCX, XLSX, PPTX, TXT, MD, PNG, JPG gibi dosyaları temsil eder.

### 3.2 Parent-Child Yapısı

Her resource bir parentId alanına sahip olabilir. parentId null ise kök seviyededir. parentId dolu ise başka bir resource altında yer alır. Sadece folder tipindeki resource kayıtlarının altında başka resource kayıtları bulunabilir.

### 3.3 Storage

Dosyanın fiziksel içeriği MinIO üzerinde saklanmalıdır. MongoDB üzerinde yalnızca metadata ve ilişki bilgileri tutulmalıdır.

Markdown dokümanlar için iki seçenek vardır:

1. Markdown içeriği MongoDB içinde content alanında saklanabilir.
2. Markdown içeriği .md dosyası olarak MinIO üzerinde saklanabilir, MongoDB'de metadata tutulur.

Önerilen yaklaşım: Tutarlılık açısından markdown içerikler de MinIO üzerinde .md dosyası olarak saklanmalı, MongoDB'de metadata ve gerekiyorsa content preview tutulmalıdır.

## 4. Faz Planı

## Faz 1 - Kaynak Ağacı ve Temel Doküman Yönetimi

### Amaç

Müşterinin öncelikli ihtiyacını karşılamak. Kullanıcıların klasör ağacı oluşturabildiği, markdown doküman yazabildiği ve dosya yükleyebildiği çalışan bir DocumentManagement çekirdeği oluşturmak.

### Kapsam

- Resources ana ekranı
- Tree view klasör yapısı
- Klasör oluşturma
- Alt klasör oluşturma
- Klasör yeniden adlandırma
- Klasör silme veya soft delete
- Seçilen klasördeki dosya ve dokümanları listeleme
- Markdown doküman oluşturma ekranı
- Markdown editör ve preview desteği
- Markdown dokümanı kaydetme
- Dosya yükleme ekranı
- PDF, DOCX, MD, TXT gibi temel dosya tiplerini yükleme
- Dosya metadata kaydı
- MinIO dosya saklama
- MongoDB metadata saklama
- Kullanıcı bilgisiyle createdBy, updatedBy alanlarını doldurma

### Faz 1 Dışında Bırakılanlar

- AI özetleme
- Benzer doküman bulma
- OperationCore entegrasyonu
- Detaylı versiyonlama
- Gelişmiş workflow
- OCR
- Kurumsal RAG asistanı

### Kabul Kriterleri

- Kullanıcı kök klasör ve alt klasör oluşturabilmelidir.
- Kullanıcı tree üzerinde klasörleri görebilmelidir.
- Kullanıcı seçilen klasöre markdown doküman oluşturabilmelidir.
- Kullanıcı seçilen klasöre dosya yükleyebilmelidir.
- Yüklenen dosyanın fiziksel içeriği MinIO üzerinde saklanmalıdır.
- Dosya metadata bilgileri MongoDB üzerinde saklanmalıdır.
- Silme işlemleri mümkünse soft delete mantığıyla yapılmalıdır.

## Faz 2 - Grup Tabanlı Yetkilendirme

### Amaç

DocumentManagement içindeki klasör ve doküman erişimlerini Keycloak grupları üzerinden yönetilebilir hale getirmek.

### Kapsam

- Folder level permission modeli
- Grup bazlı izin tanımı
- Permission inheritance
- Alt klasörlere miras kalan yetkiler
- İstenirse inheritance kırma
- Tree üzerinde yetkisiz klasörleri gizleme
- Dosya ve doküman listesinde güvenlik filtresi uygulama
- Download ve view yetki kontrolü
- Create, edit, delete, upload, move, share izinleri için altyapı

### Yetki Tipleri

- view
- create
- edit
- delete
- upload
- download
- move
- share

### Kabul Kriterleri

- Bir klasöre belirli gruplar için izin atanabilmelidir.
- Alt klasörler varsayılan olarak üst klasör yetkilerini miras almalıdır.
- inheritPermissions false olduğunda klasör kendi izinlerini kullanmalıdır.
- Kullanıcı yetkisi olmayan klasörleri tree üzerinde görmemelidir.
- Kullanıcı yetkisi olmayan dosyaları açamamalı ve indirememelidir.

## Faz 3 - OperationCore WorkItem Entegrasyonu

### Amaç

OperationCore içindeki WorkItem yapısı ile DocumentManagement kaynaklarını ilişkilendirmek. Böylece işler ve dokümanlar arasında çift yönlü bağ kurulacaktır.

### Kapsam

- WorkItem create/edit ekranında doküman seçimi
- DocumentManagement tree picker bileşeni
- Yetkili dokümanları seçebilme
- WorkItem ile bir veya birden fazla resource ilişkilendirme
- Doküman detay ekranında ilişkili WorkItem listesini gösterme
- İlişki tipi tanımlama
- İki yönlü güvenlik kontrolü

### İlişki Tipleri

- reference: Referans doküman
- attachment: Ek doküman
- evidence: Kanıt dokümanı
- output: İş sonucunda üretilen doküman

### Kabul Kriterleri

- WorkItem oluştururken kullanıcı DocumentManagement içinden dosya seçebilmelidir.
- WorkItem detayında ilişkili dokümanlar görüntülenebilmelidir.
- Doküman detayında ilişkili WorkItem kayıtları görüntülenebilmelidir.
- Kullanıcı görmeye yetkili olmadığı dokümanları WorkItem'a bağlayamamalıdır.
- Kullanıcı görmeye yetkili olmadığı WorkItem kayıtlarını doküman detayında görememelidir.

## Faz 4 - İçerik Çıkarma ve AI Hazırlık Altyapısı

### Amaç

Text tabanlı dosyaların yapay zeka işlemleri için hazırlanması. Bu fazda doğrudan AI cevap üretimi zorunlu değildir; ancak doküman içeriği çıkarılmalı, indekslenmeli ve ileride AI işlemlerinde kullanılabilecek hale getirilmelidir.

### Kapsam

- PDF içinden text çıkarma
- DOCX içinden text çıkarma
- MD içeriği okuma
- TXT içeriği okuma
- İçerik çıkarma job altyapısı
- Content extraction status alanları
- Extracted text saklama stratejisi
- Büyük dosyalar için chunk altyapısı
- Search index hazırlığı
- AI işlemleri için metadata hazırlığı

### Desteklenecek İlk Dosya Tipleri

- PDF
- DOCX
- MD
- TXT

### Kabul Kriterleri

- Desteklenen dosya yüklendiğinde içerik çıkarma işlemi başlatılmalıdır.
- İçerik çıkarma başarılı veya hatalı olarak işaretlenmelidir.
- Çıkarılan metin güvenli ve sorgulanabilir şekilde saklanmalıdır.
- Büyük dokümanlar chunk mantığıyla parçalanabilmelidir.

## Faz 5 - AI Doküman Özeti ve Anahtar Kelime Üretimi

### Amaç

Yüklenen veya oluşturulan text tabanlı dokümanlar için yapay zeka destekli özet, anahtar kelime ve otomatik etiket üretmek.

### Kapsam

- Doküman özeti oluşturma
- Kısa özet ve detaylı özet ayrımı
- Anahtar kelime üretimi
- Otomatik etiket önerisi
- Özetin doküman metadata alanında gösterilmesi
- Özet oluşturma durum takibi
- Manuel yeniden özetleme tetikleme

### Kabul Kriterleri

- Desteklenen dokümanlar için özet üretilebilmelidir.
- Kullanıcı doküman detayında özeti görebilmelidir.
- Anahtar kelimeler doküman detayında gösterilmelidir.
- AI işlemi başarısız olursa hata bilgisi saklanmalıdır.

## Faz 6 - Benzer ve Alakalı Dokümanları Bulma

### Amaç

Dokümanlar arasında içerik benzerliği kurarak kullanıcıya alakalı dosyaları göstermek.

### Kapsam

- Embedding üretimi
- Vector index altyapısı
- Similar documents sorgusu
- Doküman detayında benzer dokümanları gösterme
- WorkItem bağlamında önerilen dokümanları gösterme
- Yetki filtreli benzer doküman sorgusu

### Kabul Kriterleri

- Bir doküman detayında benzer dokümanlar listelenebilmelidir.
- Benzerlik sonucu kullanıcı yetkilerine göre filtrelenmelidir.
- Yetkisiz dokümanlar AI veya similarity sonuçlarında görünmemelidir.
- Benzerlik skoru veya açıklaması gösterilebilmelidir.

## Faz 7 - Kurumsal Bilgi Asistanı ve RAG

### Amaç

DocumentManagement içindeki yetkili dokümanlar üzerinden soru-cevap yapabilen kurumsal bilgi asistanı altyapısını oluşturmak.

### Kapsam

- RAG tabanlı soru-cevap
- Kullanıcı yetkilerine göre kaynak filtreleme
- Cevapta kullanılan kaynak dokümanları gösterme
- Klasör veya kapsam bazlı soru sorma
- OperationCore WorkItem bağlamında soru sorma
- Audit log tutma

### Kabul Kriterleri

- Kullanıcı yalnızca yetkili olduğu dokümanlar üzerinden cevap almalıdır.
- Cevapta kullanılan kaynaklar listelenmelidir.
- Sistem kaynak göstermeden kesin cevap veriyor gibi davranmamalıdır.
- AI cevapları audit amaçlı loglanmalıdır.

## Faz 8 - Versiyonlama, Audit ve Kurumsal Olgunlaşma

### Amaç

DocumentManagement modülünü kurumsal kullanıma daha uygun hale getirmek.

### Kapsam

- Doküman versiyonlama
- Eski versiyonu görüntüleme
- Yeni versiyon yükleme
- Markdown doküman revizyon geçmişi
- Audit log
- Kim ne zaman görüntüledi bilgisi
- Kim ne zaman indirdi bilgisi
- Arşivleme
- Soft delete restore
- Paylaşım linki altyapısı
- Okundu bilgisi

### Kabul Kriterleri

- Dokümanın eski versiyonları korunabilmelidir.
- Kullanıcı yeni versiyon yükleyebilmelidir.
- Kritik işlemler audit log olarak saklanmalıdır.
- Silinen dokümanlar restore edilebilmelidir.

## 5. Önerilen MongoDB Koleksiyonları

## 5.1 dm_resources

DocumentManagement içindeki klasör, markdown doküman ve upload edilmiş dosyaları temsil eder.

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
  "size": "number",
  "storageProvider": "minio",
  "storageBucket": "string",
  "storagePath": "string",
  "contentType": "markdown | binary",
  "tags": ["string"],
  "keywords": ["string"],
  "summary": "string",
  "shortSummary": "string",
  "contentExtracted": false,
  "contentExtractionStatus": "pending | processing | completed | failed | skipped",
  "contentExtractionError": "string",
  "contentLength": 0,
  "embeddingGenerated": false,
  "embeddingModel": "string",
  "embeddingDate": "datetime | null",
  "inheritPermissions": true,
  "createdBy": {
    "userId": "string",
    "userName": "string",
    "email": "string"
  },
  "createdAt": "datetime",
  "updatedBy": {
    "userId": "string",
    "userName": "string",
    "email": "string"
  },
  "updatedAt": "datetime",
  "__isDeleted": false
}
```

## 5.2 dm_resource_permissions

Klasör bazlı grup izinlerini temsil eder.

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
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "updatedBy": "userInfo",
  "updatedAt": "datetime",
  "__isDeleted": false
}
```

## 5.3 dm_resource_links

DocumentManagement kaynakları ile diğer modül kayıtları arasındaki ilişkileri temsil eder.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "targetModule": "operationCore",
  "targetType": "workItem",
  "targetId": "guid",
  "relationType": "reference | attachment | evidence | output",
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "__isDeleted": false
}
```

## 5.4 dm_resource_chunks

AI ve search işlemleri için çıkarılmış text parçalarını temsil eder.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "chunkIndex": 0,
  "text": "string",
  "tokenCount": 0,
  "embeddingGenerated": false,
  "embeddingModel": "string",
  "embeddingVectorRef": "string",
  "createdAt": "datetime",
  "__isDeleted": false
}
```

## 5.5 dm_resource_versions

Versiyonlama fazında kullanılacak doküman versiyon kayıtlarını temsil eder.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "versionNo": 1,
  "storagePath": "string",
  "size": "number",
  "mimeType": "string",
  "changeNote": "string",
  "createdBy": "userInfo",
  "createdAt": "datetime",
  "__isDeleted": false
}
```

## 5.6 dm_resource_audit_logs

Doküman işlemleri için audit log kayıtlarını temsil eder.

```json
{
  "__dataId": "guid",
  "resourceId": "guid",
  "action": "view | create | update | delete | download | upload | move | share | summarize | link | unlink",
  "user": "userInfo",
  "details": {},
  "createdAt": "datetime"
}
```

## 6. API Taslağı

## 6.1 Resource API

```http
GET    /api/document-management/resources/tree
GET    /api/document-management/resources/children?parentId={parentId}
GET    /api/document-management/resources/{id}
POST   /api/document-management/resources/folder
PUT    /api/document-management/resources/{id}
DELETE /api/document-management/resources/{id}
POST   /api/document-management/resources/{id}/move
```

## 6.2 Markdown Document API

```http
POST /api/document-management/resources/markdown
PUT  /api/document-management/resources/markdown/{id}
GET  /api/document-management/resources/markdown/{id}/content
```

## 6.3 Upload / Download API

```http
POST /api/document-management/resources/upload
GET  /api/document-management/resources/{id}/download
GET  /api/document-management/resources/{id}/preview
```

## 6.4 Permission API

```http
GET  /api/document-management/resources/{id}/permissions
PUT  /api/document-management/resources/{id}/permissions
POST /api/document-management/resources/{id}/inherit-permissions
```

## 6.5 OperationCore Link API

```http
POST   /api/document-management/resource-links
DELETE /api/document-management/resource-links/{id}
GET    /api/document-management/resources/{id}/linked-workitems
GET    /api/operation-core/workitems/{id}/linked-resources
```

## 6.6 AI API

```http
POST /api/document-management/resources/{id}/extract-content
POST /api/document-management/resources/{id}/summarize
GET  /api/document-management/resources/{id}/similar
POST /api/document-management/assistant/ask
```

## 7. UI Planı

## 7.1 Ana Resources Ekranı

Route önerisi:

```text
/pages/document-management/resources
```

Ekran yapısı:

- Sol panel: Tree view
- Sağ panel: Seçilen klasör içeriği
- Üst aksiyonlar: Yeni Klasör, Yeni Doküman, Dosya Yükle
- Arama alanı
- Liste veya grid görünümü

## 7.2 Create Markdown Document Ekranı

Route önerisi:

```text
/pages/document-management/resources/create-document
```

Özellikler:

- Başlık
- Açıklama
- Hedef klasör seçimi
- Markdown editör
- Markdown preview
- Kaydet
- Taslak olarak kaydet

## 7.3 Upload Document Ekranı

Route önerisi:

```text
/pages/document-management/resources/upload-document
```

Özellikler:

- Hedef klasör seçimi
- Dosya seçimi
- Açıklama
- Etiketler
- Upload progress
- Upload sonrası metadata gösterimi

## 7.4 Resource Detail Ekranı

Route önerisi:

```text
/pages/document-management/resources/detail/[id]
```

Bölümler:

- Temel metadata
- Preview veya içerik gösterimi
- AI özeti
- Anahtar kelimeler
- Benzer dokümanlar
- İlişkili WorkItem'lar
- Versiyonlar
- Audit geçmişi

## 7.5 WorkItem İçinde Doküman Seçimi

OperationCore WorkItem create/edit ekranına aşağıdaki bölüm eklenmelidir:

```text
İlgili Dokümanlar
```

Bu bölümde:

- Doküman seç butonu
- Document tree picker modal
- Seçili dokümanlar listesi
- İlişki tipi seçimi
- Bağlantıyı kaldırma

## 8. Güvenlik Kuralları

- Tree verisi kullanıcı yetkilerine göre filtrelenmelidir.
- Download endpoint'i mutlaka view veya download yetkisi kontrol etmelidir.
- AI similarity sonuçları yetki filtresinden geçmelidir.
- RAG cevapları yalnızca kullanıcının erişebildiği dokümanlardan üretilmelidir.
- WorkItem link listelerinde hem doküman hem WorkItem yetkisi kontrol edilmelidir.
- Admin kullanıcılar için tenant/domain sınırları yine korunmalıdır.

## 9. Teknik Mimari Notları

- Metadata MongoDB üzerinde tutulmalıdır.
- Fiziksel dosyalar MinIO üzerinde tutulmalıdır.
- Domain bazlı tenant izolasyonu korunmalıdır.
- API çağrılarında access token üzerinden domain, user ve group bilgileri alınmalıdır.
- MngOperations doğrudan MongoDB'ye erişmemeli; mevcut mimari kararına göre DG üzerinden çalışmalıdır.
- Dosya yükleme sırasında MinIO path benzersiz olmalıdır.
- Dosya adı kullanıcıya orijinal adla gösterilmeli, storage tarafında güvenli sistem adı kullanılmalıdır.
- Büyük AI işlemleri background job olarak çalışmalıdır.
- Content extraction ve AI işlemleri senkron request içinde uzun süre bekletilmemelidir.

## 10. Önerilen Geliştirme Sırası

1. dm_resources koleksiyon modelini oluştur.
2. Folder CRUD endpointlerini geliştir.
3. Tree query endpointini geliştir.
4. MinIO upload altyapısını bağla.
5. Upload endpointini geliştir.
6. Markdown create/update endpointlerini geliştir.
7. Resources ana ekranını geliştir.
8. Markdown editor ekranını geliştir.
9. Upload ekranını geliştir.
10. Resource detail ekranını geliştir.
11. Permission modelini ekle.
12. Tree ve liste sorgularına permission filtresi ekle.
13. OperationCore link koleksiyonunu ekle.
14. WorkItem ekranına document picker ekle.
15. Document detail ekranına linked WorkItems ekle.
16. Content extraction altyapısını ekle.
17. AI summary altyapısını ekle.
18. Similar documents altyapısını ekle.
19. RAG assistant altyapısını ekle.
20. Versioning ve audit özelliklerini ekle.

## 11. Fazlara Göre Öncelik Özeti

| Faz | Başlık | Öncelik | Durum |
|---|---|---:|---|
| Faz 1 | Kaynak Ağacı ve Temel Doküman Yönetimi | Çok yüksek | İlk geliştirme |
| Faz 2 | Grup Tabanlı Yetkilendirme | Çok yüksek | Faz 1 sonrası hemen |
| Faz 3 | OperationCore Entegrasyonu | Yüksek | WorkItem modülü hazır olduğu için erken yapılabilir |
| Faz 4 | İçerik Çıkarma ve AI Hazırlık | Yüksek | AI öncesi zorunlu altyapı |
| Faz 5 | AI Özet ve Anahtar Kelime | Orta/Yüksek | İlk AI değeri |
| Faz 6 | Benzer Doküman Bulma | Orta/Yüksek | Vector altyapısı gerektirir |
| Faz 7 | Kurumsal Bilgi Asistanı | Orta | Olgun AI fazı |
| Faz 8 | Versiyonlama ve Audit | Orta/Yüksek | Kurumsal olgunlaşma |

## 12. CursorAI İçin İlk Uygulama Talimatı

İlk geliştirme görevi aşağıdaki gibi verilmelidir:

```text
MonitraNG projesinde DocumentManagement modülü için Faz 1 geliştirmesini başlat.

Amaç:
Tree yapısında klasörler, alt klasörler, markdown dokümanlar ve upload edilmiş dosyalar yönetilebilsin.

Backend tarafında:
- dm_resources modelini oluştur.
- Folder CRUD endpointlerini oluştur.
- Tree endpointini oluştur.
- Markdown document create/update endpointlerini oluştur.
- Upload endpointini MinIO ile entegre et.
- MongoDB metadata kayıtlarını oluştur.
- Soft delete kullan.
- Access token üzerinden user ve domain bilgilerini kullan.

Frontend tarafında:
- /document-management/resources route'unu oluştur.
- Sol tarafta tree view göster.
- Sağ tarafta seçili klasör içeriğini göster.
- Yeni klasör, yeni markdown doküman, dosya yükle aksiyonlarını ekle.
- Markdown editor ve preview destekli create document ekranını oluştur.
- Upload document ekranını oluştur.

Faz 1 içinde AI, permission, OperationCore entegrasyonu ve versioning geliştirme. Ancak veri modelinde ileride kullanılacak alanlar için uygun hazırlık yapılabilir.
```

## 13. Açık Kararlar

Aşağıdaki konular geliştirme öncesinde veya geliştirme sırasında netleştirilmelidir:

- Markdown içerik MinIO üzerinde mi saklanacak, MongoDB içinde mi tutulacak?
- İlk fazda dosya preview desteklenecek mi?
- İlk fazda klasör silme fiziksel mi, soft delete mi olacak? Öneri: soft delete.
- Permission Faz 1 içinde minimum seviyede mi olacak, yoksa Faz 2'ye mi bırakılacak?
- AI işlemleri için kullanılacak model ve deployment şekli ne olacak? On-prem model mi, dış servis mi?
- Vector search için MongoDB Atlas Vector Search, Qdrant, Milvus veya başka bir altyapı mı kullanılacak?

## 14. Sonuç

DocumentManagement modülü ilk müşteri ihtiyacını karşılayacak şekilde sade bir kaynak ağacı ve dosya yönetimi olarak başlamalıdır. Ancak veri modeli ve mimari, ileride yapay zeka, OperationCore entegrasyonu, yetkilendirme, versiyonlama ve kurumsal bilgi yönetimi özelliklerini destekleyecek şekilde tasarlanmalıdır.

Bu yaklaşım sayesinde MonitraNG içinde DocumentManagement modülü kısa vadede müşteriye hızlı değer sunar, uzun vadede ise Document Intelligence ve kurumsal bilgi asistanı özelliklerine doğal olarak evrilebilir.
