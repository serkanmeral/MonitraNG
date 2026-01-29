# MngDataGateway Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/data/api/v1/` (ör. `https://gateway.example.com/data/api/v1/datasets`)
- **Kimlik doğrulama:** Çoğu endpoint `Authorization: Bearer <access_token>` gerektirir. Token MngKeeper `POST /keeper/api/auth/token` ile alınır.
- **Content-Type:** `application/json` (file upload veya belirtilen yerler hariç).

---

## 1. Health — `api/v1/health`

Uygulama sağlık ve hazırlık kontrolleri. Auth gerekmez.

### 1.1 Health check

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health` |
| **Auth** | Yok (AllowAnonymous) |
| **Amaç** | MongoDB, RabbitMQ ve disk durumunu döndürür. |

#### Response (200 OK / 503)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `status` | string | `healthy`, `degraded`, `unhealthy` |
| `timestamp` | string (ISO 8601) | Kontrol zamanı. |
| `checks` | object | `MongoDB`, `RabbitMQ`, `Disk` alt nesneleri; her biri `status`, `responseTimeMs`, `message` içerir. |

503: `status === "unhealthy"` ise döner.

---

### 1.2 Liveness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/live` |
| **Auth** | Yok |

#### Response (200 OK)

`{ "status": "alive", "timestamp": "..." }`

---

### 1.3 Readiness

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/health/ready` |
| **Auth** | Yok |

#### Response (200 OK / 503)

`status`: `ready` | `not ready`; `checks`: `mongodb`, `rabbitmq`. MongoDB ve RabbitMQ sağlıklı değilse 503.

---

## 2. Version — `api/v1/version`

Sürüm bilgisi. Auth gerekmez.

### 2.1 Detaylı sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version` |
| **Auth** | Yok |

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `Product` | string | Ürün adı. |
| `Version` | string | Bilgilendirme sürümü. |
| `AssemblyVersion` | string | Derleme sürümü. |
| `BuildDate` | string | Derleme tarihi. |
| `Company`, `Copyright` | string | Şirket / telif. |
| `Environment` | string | Ortam (örn. Production). |
| `Runtime` | object | Framework, OS, MachineName, ProcessorCount. |
| `Dependencies` | object | MongoDB, RabbitMQ sürümleri. |

---

### 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version/short` |
| **Auth** | Yok |

#### Response (200 OK)

`{ "Version": "1.0.2" }`

---

## 3. Datasets — `api/v1/datasets`

Dataset şema CRUD. Tüm endpoint’ler JWT ile yetkilidir; domain JWT’den alınır.

### 3.1 Dataset oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/datasets` |
| **Auth** | Evet |

#### Request body (CreateDatasetDto)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek / Varsayılan |
|----------|-----|---------|----------|--------------------|
| `name` | string | Evet | Benzersiz dataset adı (@ ile başlayabilir). 2–100 karakter, `^@?[a-zA-Z][a-zA-Z0-9_-]*$` | `"@tasks"`, `"@users"` |
| `description` | string | Hayır | Açıklama (max 1000). | — |
| `category` | string | Hayır | Kategori ID referansı. | — |
| `forceSchema` | boolean | Hayır | Şema zorunluluğu. | `true` |
| `logging` | string | Hayır | `self`, `none`, `common`. | `"none"` |
| `publishMode` | string | Hayır | `none`, `basic`, `full`. | `"none"` |
| `fields` | array | Hayır | Alan tanımları (fieldType, name, vb.). | — |
| `validations` | array | Hayır | Validasyon kuralları. | — |
| `queries` | array | Hayır | Ön tanımlı sorgular. | — |
| `indexList` | array | Hayır | Index tanımları (metadata). | — |
| `permissions` | object | Hayır | read/create/update/delete grupları. | — |

#### Response (201 Created)

Oluşturulan dataset şeması (DatasetResponseDto): `name`, `dataId`, `description`, `category`, `forceSchema`, `logging`, `publishMode`, `fields`, `validations`, `queries`, `indexList`, `permissions` vb.

#### Hata (400 / 404)

- 400: Geçersiz veri, aynı isimde dataset var (INVALID_OPERATION).
- 401: Yetkisiz.

---

### 3.2 Dataset listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/datasets` |
| **Auth** | Evet |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `pageNumber` | number | Hayır | Sayfa numarası. | `1` |
| `pageSize` | number | Hayır | Sayfa boyutu (max 100). | `20` |

#### Response (200 OK)

Sayfalı sonuç: `items` (DatasetResponseDto[]), `totalCount`, `pageNumber`, `pageSize`.

---

### 3.3 Dataset getir (ad)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/datasets/{name}` |
| **Auth** | Evet |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `name` | string | Evet | Dataset adı (örn. `@tasks`). |

#### Response (200 OK)

Tek DatasetResponseDto. 404: DATASET_NOT_FOUND.

---

### 3.4 Dataset güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/v1/datasets/{name}` |
| **Auth** | Evet |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `name` | string | Evet | Dataset adı. |

#### Request body (UpdateDatasetDto)

Güncellenecek alanlar: `description`, `category`, `forceSchema`, `logging`, `publishMode`, `fields`, `validations`, `queries`, `indexList`, `permissions`. Tam liste schema ile uyumlu olmalı.

#### Response (200 OK)

Güncellenmiş DatasetResponseDto. 404: Dataset bulunamadı.

---

### 3.5 Dataset sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/datasets/{name}` |
| **Auth** | Evet |

**Not:** Yalnızca şema metadata silinir; collection ve veri silinmez.

#### Response (204 No Content)

Başarılı. 404: Dataset bulunamadı.

---

### 3.6 Dataset geri yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/datasets/{name}/restore` |
| **Auth** | Evet |

#### Response (200 OK)

Geri yüklenen DatasetResponseDto. 404/400: Bulunamadı veya geçersiz işlem.

---

## 4. Dataset Categories — `api/v1/dataset-categories`

Dataset kategorileri CRUD. JWT gerekir.

### 4.1 Kategori oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/dataset-categories` |
| **Auth** | Evet |

#### Request body (CreateDatasetCategoryDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `categoryName` | string | Evet | Kategori adı. |
| `description` | string | Hayır | Açıklama. |

#### Response (201 Created)

DatasetCategoryResponseDto: `dataId`, `categoryName`, `description` vb. Location header: ilgili GET endpoint’i.

---

### 4.2 Kategori listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/dataset-categories` |
| **Auth** | Evet |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `pageNumber` | number | Hayır | Sayfa numarası. | `1` |
| `pageSize` | number | Hayır | Sayfa boyutu (max 100). | `20` |
| `search` | string | Hayır | Kategori adı/açıklama araması. | — |

#### Response (200 OK)

Sayfalı sonuç: `items`, `totalCount`, `pageNumber`, `pageSize`.

---

### 4.3 Kategori getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/dataset-categories/{dataId}` |
| **Auth** | Evet |

#### Response (200 OK)

Tek kategori. 404: CATEGORY_NOT_FOUND.

---

### 4.4 Kategori güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/v1/dataset-categories/{dataId}` |
| **Auth** | Evet |

#### Request body (UpdateDatasetCategoryDto)

`categoryName`, `description` (güncellenecek alanlar).

#### Response (200 OK)

Güncellenmiş kategori. 404: Kategori bulunamadı.

---

### 4.5 Kategori sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/dataset-categories/{dataId}` |
| **Auth** | Evet |

#### Response (204 No Content)

404: Kategori bulunamadı.

---

### 4.6 Kategori geri yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/dataset-categories/{dataId}/restore` |
| **Auth** | Evet |

#### Response (200 OK)

Geri yüklenen kategori. 404/400: Bulunamadı veya geçersiz işlem.

---

## 5. Data — `api/v1/data/{datasetName}`

Dataset verisi CRUD, liste, filtre, arama, CSV, query, aggregate, predefined query, bulk. Domain JWT’den alınır; dataset bazlı permission (read/create/update/delete) uygulanır.

### 5.1 Veri oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}` |
| **Auth** | Evet (create yetkisi) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `datasetName` | string | Evet | Dataset adı (örn. `@tasks`). |

#### Request body

JSON object; dataset şemasındaki alanlara uygun. File alanları: `{ "content": "<base64>", "folder?", "useCompression?", "useEncryption?", "originalFileName?" }` veya mevcut `{ "path", "upload_person", "upload_time", "file_name", "file_ext", "file_size" }` objesi.

#### Response (200 OK)

`DataResponseDto`: `success`, `data` (eklenen kayıt, `__dataId` dahil), `meta`, `warnings`. Standart hata: `ErrorResponseDto` (error.code, error.message, meta).

#### Hata (400 / 403 / 404)

- 400: Validasyon hatası (validationErrors dizisi).
- 403: Dataset için create yetkisi yok.
- 404: Dataset bulunamadı.

---

### 5.2 Veri listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/data/{datasetName}` |
| **Auth** | Evet (read yetkisi) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `datasetName` | string | Evet | Dataset adı. |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `skip` | number | Hayır | Atlanacak kayıt sayısı. | `0` |
| `limit` | number | Hayır | Döndürülecek max kayıt (max 1000). | `50` |
| `expand` | boolean | Hayır | İlişkili alanları genişlet. | `true` |
| `deep` | number | Hayır | İç içe expansion derinliği. | config’ten |
| `showHistory` | boolean | Hayır | `__history` alanını dahil et. | `false` |
| `showQuery` | boolean | Hayır | Sadece aggregate pipeline döndür. | `false` |
| `showDataset` | boolean | Hayır | Dataset şemasını döndür (veri yerine). | `false` |
| `sort` | string | Hayır | MongoDB tarzı: `"alan1,-alan2"`. | — |
| `filter` | string | Hayır | REST tarzı: `"alan:op:değer"` (örn. `price:gte:20`). | — |
| `fields` | string | Hayır | Virgülle ayrılmış alan listesi. | — |
| `search` | string | Hayır | Metin araması (ana + relation). | — |
| `format` | string | Hayır | `json` veya `csv`. | `"json"` |

#### Response (200 OK)

- `format=json`: Body bir dizi (array); toplam kayıt sayısı `X-Total-Count` header’ında.
- `format=csv`: `Content-Type: text/csv`; `X-Total-Count` yine dolu.

#### Hata (403 / 404)

403: read yetkisi yok. 404: Dataset yok.

---

### 5.3 Tekil veri getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/data/{datasetName}/{dataId}` |
| **Auth** | Evet (read) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `datasetName` | string | Evet | Dataset adı. |
| `dataId` | string | Evet | Kayıt ID’si (`__dataId`). |

#### Query parametreleri

`expand`, `deep`, `showHistory`, `showQuery`, `showDataset`, `sort`, `fields` (5.2 ile aynı anlamda).

#### Response (200 OK)

Tek elemanlı dizi `[ { ... } ]`. 404: DATA_NOT_FOUND veya DATASET_NOT_FOUND.

---

### 5.4 Veri güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/v1/data/{datasetName}/{dataId}` |
| **Auth** | Evet (update yetkisi) |

#### Request body

Güncellenecek alanları içeren JSON object. File alanları 5.1 ile aynı kurallarla (content ile yeni yükleme veya mevcut path objesi).

#### Response (200 OK)

DataResponseDto ile güncellenmiş kayıt. 404: Dataset veya data bulunamadı. 400: Validasyon hatası.

---

### 5.5 Veri sil (soft delete)

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/data/{datasetName}/{dataId}` |
| **Auth** | Evet (delete yetkisi) |

#### Response (200 OK)

`DataResponseDto` veya `{ "message": "Data deleted successfully", "dataId": "..." }`. 404: Kayıt/dataset bulunamadı.

---

### 5.6 Veri geri yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}/{dataId}/restore` |
| **Auth** | Evet |

#### Response (200 OK)

`{ "message": "Data restored successfully", "dataId": "..." }`. 404: Silinmiş kayıt bulunamadı.

---

### 5.7 Gelişmiş sorgu (match)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}/query` |
| **Auth** | Evet (read) |

#### Request body (QueryRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `match` | object | Evet | MongoDB tarzı `$match` koşulu. |

#### Query parametreleri

`expand`, `deep`, `showHistory`, `showQuery`, `showDataset`, `sort`, `fields`, `skip`, `limit` (5.2’deki gibi).

#### Response (200 OK)

Kayıt dizisi veya `showQuery=true` ise `{ "query": [ ... ] }` (pipeline). 404: Dataset bulunamadı.

---

### 5.8 Ham aggregate pipeline

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}/aggregate` |
| **Auth** | Evet (read) |

#### Request body (AggregateRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `pipeline` | array | Evet | MongoDB aggregate aşamaları dizisi. |

#### Response (200 OK)

Pipeline sonucu (dizi). 400: Geçersiz pipeline (INVALID_PIPELINE). 404: Dataset bulunamadı.

---

### 5.9 Ön tanımlı sorgu çalıştır

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}/queries/{queryName}` |
| **Auth** | Evet (read) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `datasetName` | string | Evet | Dataset adı. |
| `queryName` | string | Evet | Şemada tanımlı sorgu adı. |

#### Request body (PredefinedQueryRequestDto)

Parametre anahtar-değer çiftleri (sorgu tanımındaki parametrelere uygun). Boş `{}` veya null olabilir.

#### Response (200 OK)

Sorgu sonucu (dizi). 404: QUERY_NOT_FOUND veya dataset bulunamadı. 400: Parametre/validasyon hatası.

---

### 5.10 Toplu ekleme (bulk)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/data/{datasetName}/bulk` |
| **Auth** | Evet (create yetkisi) |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `items` | array | Evet | Eklenmek istenen kayıtlar (object[]). Max 1000 öğe. |

#### Response (200 OK)

BulkInsertResultDto: `insertedCount`, `successfulItems` (__dataId ile), `errors` (BulkInsertErrorDto: index, code, message). 400: INVALID_REQUEST, BATCH_SIZE_EXCEEDED. 404: Dataset bulunamadı.

---

## 6. Files — `api/v1/files`

Dosya yükleme, indirme ve metadata. JWT ve dataset/alan bazlı yetki (create/read) kullanılır.

### 6.1 Dosya yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/files/upload` |
| **Auth** | Evet (ilgili dataset için create) |

#### Request body (FileUploadRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `content` | string | Evet | Base64 kodlu dosya içeriği. |
| `datasetName` | string | Evet | Dataset adı (file alanı bu dataset’te olmalı). |
| `fieldName` | string | Evet | File tipindeki alan adı. |
| `folder` | string | Hayır | MinIO alt klasörü. |
| `useCompression` | boolean | Hayır | Sıkıştırma. |
| `useEncryption` | boolean | Hayır | Şifreleme. |
| `recordId` | string | Hayır | Kayıt ID (yoksa geçici ID üretilir). |

#### Response (201 Created)

FileUploadResponseDto (DataResponseDto içinde): `filePath`, `originalFileName`, `fileSize`, `mimeType`, `isCompressed`, `isEncrypted`, `uploadedAt`. Bu `filePath` veya `{ path, upload_person, upload_time, file_name, file_ext, file_size }` objesi data kaydında kullanılır.

#### Hata (400 / 403 / 404)

- 400: INVALID_REQUEST (content/dataset/field eksik), INVALID_FIELD_TYPE, validation.
- 403: Domain veya create yetkisi yok.
- 404: DATASET_NOT_FOUND, FIELD_NOT_FOUND.

---

### 6.2 Dosya indir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/files/download` |
| **Auth** | Evet (ilgili dataset için read) |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `filePath` | string | Evet | MinIO yolu (örn. `/mng-{domain}/data/users/{dataset}/{recordId}/...`). |

#### Response (200 OK)

Dosya gövdesi (binary); `Content-Disposition`, `Content-Type` set. 403: Başka domain’e ait veya read yetkisi yok. 404: FILE_NOT_FOUND, INVALID_PATH.

---

### 6.3 Dosya metadata

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/files/metadata` |
| **Auth** | Evet (read) |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `filePath` | string | Evet | 6.2 ile aynı path formatı. |

#### Response (200 OK)

FileMetadataResponseDto: `filePath`, `originalFileName`, `fileSize`, `mimeType`, `isCompressed`, `isEncrypted`, `uploadedBy`, `datasetName`, `recordId`, `createdAt`, `uploadedAt`, `rawMetadata`. 403/404: 6.2 ile aynı mantık.

---

## Hata yanıtları (ortak)

Tüm endpoint’lerde hata gövdesi tutarlıdır:

| Alan | Tip | Açıklama |
|------|-----|----------|
| `success` | boolean | `false` |
| `error` | object | `code`, `message`, `details` (opsiyonel; validasyonlarda dizilere izin verilir). |
| `meta` | object | `timestamp`, `path`. |

HTTP durum kodları: 400 (Bad Request), 401 (Unauthorized), 403 (Forbidden), 404 (Not Found), 500 (Internal Server Error).

---

API sürümleme ve davranış ayrıntıları için [API Versioning](../support/guides/API_VERSIONING.md) sayfasına bakınız.
