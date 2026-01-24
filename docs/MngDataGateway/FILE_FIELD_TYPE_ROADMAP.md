# File Field Type Implementation Roadmap

**Date:** 24 Ocak 2026  
**Last Updated:** 24 Ocak 2026  
**Status:** 🟢 Phase 1 & 2 Complete - Production Ready  
**Participants:** Serkan Meral (Product), AI Assistant (Technical)

---

## 📋 Overview

Bu roadmap, MngDataGateway'de **file field type** desteği eklenmesinin planını içerir. File field type, dataset kayıtlarına dosya ekleme, şifreleme, sıkıştırma gibi özellikleri sağlayacaktır.

### Temel Özellikler:
- ✅ Base64 input handling (CREATE/PUT işlemlerinde)
- ✅ MinIO'ya fiziki dosya kaydetme
- ✅ Database'de sadece path bilgisi tutma
- ✅ Dosya metadata'sı MinIO headers'ına yazma
- ✅ İsteğe bağlı compression (gzip)
- ✅ İsteğe bağlı encryption (AES-256-GCM)
- ✅ Array field desteği
- ✅ Custom folder yapısı
- ✅ Ayrı download endpoint

---

## 🎯 Final Decisions (Tartışmalardan Çıkan)

### Konu 1: GUID + Extension Naming ✅
- **GUID Format:** `{Guid}.{lowercase-extension}`
- **Örnek:** `550e8400-e29b-41d4-a716-446655440000.pdf`
- **Extension Mapping:** `appsettings.json` config dosyasında
- **Validation:** Magic bytes check (file header validation)

### Konu 2: Metadata Storage ✅
- **Lokasyon:** MinIO custom headers (`x-amz-meta-*`)
- **Metadata Yapısı:**
  ```
  - originalFileName
  - fileSize
  - mimeType
  - createdAt
  - uploadedAt
  - uploadedBy (username)
  - domainName
  - datasetName
  - recordId
  - isZipped
  - isEncrypted
  - encryptionConfig (algorithm, keyDerivation)
  ```
- **Permission:** ❌ KALDIRILAN (dataset'ten runtime'da gelecek)
- **Immutable:** ✅ Metadata sadece yazılır, değiştirilmez

### Konu 3: Compression & Encryption ✅
- **Request Object:**
  ```json
  {
    "content": "base64-string",          // REQUIRED
    "folder": "/books/images",          // OPTIONAL (null/empty ise default)
    "useCompression": false,            // OPTIONAL (default: true)
    "useEncryption": true               // OPTIONAL (default: true)
  }
  ```
- **Order:** Compress → Encrypt (optimal for efficiency)
- **Encryption Key:** Build-in config (appsettings.json)
- **Compression Fail:** Skip compression, set isCompressed=false

### Konu 4: Folder Validation ✅
- **Rules:** MinIO naming rules uyarınca
- **Max Depth:** 10 levels
- **Max Path Length:** 512 characters
- **Allowed Chars:** alphanumeric + `-` + `_` + `/`
- **Traversal Protection:** `..` sequence blocked

### Konu 5: Database Storage ✅
- **Tutulacak Bilgi:** Sadece **path**
- **Path Format:** `/mng-{domain}/data/{datasetName}/{recordId}/{folder?}/{guid}.{ext}`
- **Örnek:** `/mng-meral/data/@invoices/550e8400-e29b/docs/550e8400-e29b.pdf`

### Konu 6: File Retrieval ✅
- **Ayrı Endpoint:** `GET /api/files/download/{fileId}`
- **Response:** Binary stream (base64 değil)
- **Decrypt/Decompress:** Automatic on download

### Konu 7: Retry Mechanism ✅
- **Attempt Count:** 3 times
- **Backoff Strategy:** Exponential (1s, 2s, 4s)

---

## 🗂️ MinIO Folder Structure

```
Bucket: mng-{domain}
├── data/
│   └── {datasetName}/
│       └── {recordId}/
│           ├── {file-uuid-1}.pdf
│           ├── {file-uuid-2}.png
│           ├── folder-1/
│           │   ├── {file-uuid-3}.docx
│           │   └── {file-uuid-4}.xlsx
│           └── folder-2/
│               └── {file-uuid-5}.jpg

Örnek:
/mng-meral/data/@invoices/TASK-000001/
/mng-meral/data/@invoices/TASK-000001/books/images/
/mng-meral/data/@users/user-uuid/documents/
```

---

## 📊 Request/Response Examples

### File Upload (Single)

**Request:**
```http
POST /api/datasets/@invoices/data/TASK-000001
Content-Type: application/json

{
  "title": "Invoice January",
  "documentFile": {
    "content": "JVBERi0xLjQKJeLjz9M...",
    "folder": "invoices/2025",
    "useCompression": true,
    "useEncryption": true
  }
}
```

**Response (201):**
```json
{
  "__dataId": "TASK-000001",
  "title": "Invoice January",
  "documentFile": "/mng-meral/data/@invoices/TASK-000001/invoices/2025/550e8400-e29b.pdf",
  "__createInfo": { ... }
}
```

### File Upload (Array)

**Request:**
```http
POST /api/datasets/@projects/data
Content-Type: application/json

{
  "name": "New Project",
  "attachments": [
    {
      "content": "base64-string-1",
      "folder": "documents",
      "useCompression": true,
      "useEncryption": true
    },
    {
      "content": "base64-string-2",
      "folder": "documents",
      "useCompression": false,
      "useEncryption": true
    }
  ]
}
```

**Response:**
```json
{
  "__dataId": "proj-uuid-123",
  "name": "New Project",
  "attachments": [
    "/mng-meral/data/@projects/proj-uuid-123/documents/file-uuid-1.pdf",
    "/mng-meral/data/@projects/proj-uuid-123/documents/file-uuid-2.png"
  ]
}
```

### File Download

**Request:**
```http
GET /api/files/download/file-uuid-1
Authorization: Bearer {jwt-token}
```

**Response (200):**
```
Binary stream (PDF content)
Headers:
  Content-Type: application/pdf
  Content-Disposition: attachment; filename="invoice.pdf"
```

### File Retrieval with Data

**Request:**
```http
GET /api/datasets/@invoices/data/TASK-000001
Authorization: Bearer {jwt-token}
```

**Response (200):**
```json
{
  "__dataId": "TASK-000001",
  "title": "Invoice January",
  "documentFile": "/mng-meral/data/@invoices/TASK-000001/invoices/2025/550e8400-e29b.pdf",
  // File download'u için frontend GET /api/files/download/{fileId} çağırır
}
```

---

## 📝 Dataset Field Definition

### Single File Field

```json
{
  "fieldType": "file",
  "name": "invoice",
  "title": "Fatura Dosyası",
  "description": "Fatura PDF dosyası",
  "mandatory": false,
  "isArray": false,
  
  "fileOptions": {
    "maxSize": 10485760,              // 10MB (bytes)
    "allowedExtensions": [".pdf"],
    "defaultCompression": true,
    "defaultEncryption": true
  }
}
```

### Array File Field

```json
{
  "fieldType": "file",
  "name": "attachments",
  "title": "Ekler",
  "mandatory": false,
  "isArray": true,
  
  "fileOptions": {
    "maxSize": 5242880,               // 5MB per file
    "maxFiles": 10,                   // Max 10 files
    "allowedExtensions": [".pdf", ".jpg", ".png", ".docx"],
    "defaultCompression": true,
    "defaultEncryption": true
  }
}
```

---

## 🔄 Request Object Detayları

### POST/PUT Request Body'de File Object

```json
{
  "content": "base64-encoded-file-content",
  "folder": "/custom/path/to/folder",
  "useCompression": true,
  "useEncryption": true
}
```

**Field Açıklamaları:**

| Field | Type | Required | Default | Açıklama |
|-------|------|----------|---------|----------|
| `content` | string (base64) | ✅ | - | Base64 encoded file content |
| `folder` | string | ❌ | null | Custom folder path. Null/empty ise default |
| `useCompression` | boolean | ❌ | true | Dosyayı gzip ile sıkıştır |
| `useEncryption` | boolean | ❌ | true | Dosyayı AES-256-GCM ile şifrele |

**Folder Path Örnekleri:**

```
input: null           → /mng-meral/data/@invoices/record-id/
input: ""             → /mng-meral/data/@invoices/record-id/
input: "documents"    → /mng-meral/data/@invoices/record-id/documents/
input: "/docs/2025"   → /mng-meral/data/@invoices/record-id/docs/2025/
input: "a/b/c/d/e"    → /mng-meral/data/@invoices/record-id/a/b/c/d/e/
```

---

## 🔐 Encryption Details

### Configuration (appsettings.json)

```json
{
  "MngDataGatewaySettings": {
    "FileStorage": {
      "Minio": {
        "Endpoint": "minio:9000",
        "AccessKey": "minioadmin",
        "SecretKey": "minioadmin",
        "BucketName": "datasets"
      },
      "Encryption": {
        "Enabled": true,
        "Algorithm": "AES-256-GCM",
        "Key": "base64-encoded-256-bit-key",
        "KeyDerivation": "PBKDF2",
        "Iterations": 10000,
        "SaltLength": 16
      },
      "Compression": {
        "Algorithm": "gzip",
        "Level": 6
      }
    }
  }
}
```

### Encryption Flow

```
1. Şifreleme Anahtarı: appsettings.json'dan load edilir (256-bit)
2. Base64 decode: İnput string binary'ye çevrilir
3. Compression (optional): gzip ile sıkıştırılır
4. Encryption: AES-256-GCM ile şifrelenir
5. Nonce + Tag + Ciphertext: Birleştirilir
6. MinIO'ya kaydetme: Şifreli data MinIO'ya yazılır
7. Metadata: isEncrypted=true yazılır

Decryption (download sırasında ters sırada):
1. MinIO'dan al
2. Nonce, Tag, Ciphertext parse et
3. AES-256-GCM ile decrypt et
4. gzip ile decompress et (if needed)
5. Binary'yi base64'e encode et (response)
6. İnsan tarafından kullanılabilir hale getir
```

---

## 🛠️ Implementation Phases

### Phase 1: Foundation (Week 1-2)

#### 1.1: FileFieldValidator Service
- ✅ Extension mapping (appsettings.json'dan)
- ✅ Magic bytes validation
- ✅ File size check
- ✅ Folder path validation
- ✅ Content-Type detection

#### 1.2: FileEncryptionService
- ✅ AES-256-GCM encryption
- ✅ Random nonce generation
- ✅ Encrypt method
- ✅ Decrypt method
- ✅ Key management

#### 1.3: FileCompressionService
- ✅ Gzip compression
- ✅ Gzip decompression
- ✅ Compression level configuration
- ✅ Error handling (compression fail → skip)

#### 1.4: MinIOService Enhancement
- ✅ Bucket initialization
- ✅ File upload with metadata headers
- ✅ File download
- ✅ Metadata retrieval
- ✅ Object deletion
- ✅ Retry mechanism (3 attempts)

### Phase 2: Integration (Week 3)

#### 2.1: DataController - File Field Handling
- ✅ Request parsing (base64 + folder + options)
- ✅ File validation
- ✅ Compression & Encryption
- ✅ MinIO upload
- ✅ Database path storage
- ✅ Error handling

#### 2.2: File Download Endpoint
- ✅ GET /api/files/download/{fileId}
- ✅ Access control check
- ✅ Decryption & Decompression
- ✅ Response streaming
- ✅ Content-Type headers

#### 2.3: Dataset Schema Update
- ✅ Field definition support (fileOptions)
- ✅ Validation rules (maxSize, allowedExtensions)
- ✅ Array support

### Phase 3: Optimization & Enhancement (Future)

#### 3.1: Memory Optimization (Optional)
- ⏳ Compression skip for small files (< 1KB)
- ⏳ Compression ratio check (skip if < 5% reduction)
- ⏳ Array pooling (if file size limit increases)
- ⏳ Base64 string early release (GC optimization)
- ⏳ MemoryStream capacity hints
- **Status:** ✅ Analyzed - See `FILE_FIELD_MEMORY_OPTIMIZATION.md`
- **Priority:** 🟢 Low (5MB limit için yeterli)

#### 3.2: Performance Tuning (Future)
- ⏳ Parallel compression (for multiple files)
- ⏳ Connection pooling optimization
- ⏳ Timeout configuration tuning
- ⏳ Large file handling (if limit increases)

#### 3.3: Monitoring & Metrics (Future)
- ⏳ File operation metrics (upload/download counts)
- ⏳ Error tracking dashboard
- ⏳ Performance metrics (upload time, compression ratio)
- ⏳ Storage usage tracking

#### 3.4: Additional Features (Future)
- ⏳ Presigned URL functionality (direct download links)
- ⏳ Soft delete/trash mechanism
- ⏳ File versioning
- ⏳ Virus scanning (ClamAV integration)
- ⏳ Public share links

### Phase 4: Testing (Ongoing)

#### 4.1: Unit Tests
- ✅ FileFieldValidator
- ✅ FileEncryptionService
- ✅ FileCompressionService
- ✅ MinIO operations

#### 4.2: Integration Tests
- ✅ File upload (single & array)
- ✅ File download
- ✅ Compression/Encryption flow
- ✅ Folder path validation
- ✅ Error scenarios

#### 4.3: E2E Tests
- ✅ Complete upload/download flow
- ✅ Multiple file handling (array fields)
- ✅ Compression/Encryption flow
- ✅ Permission checks
- ✅ Metadata retrieval
- ⏳ Large file handling (if limit increases)
- ⏳ Concurrent upload stress tests

---

## 📂 Project Structure

```
MngDataGateway/
├── Core/
│   └── MngDataGateway.Application/
│       ├── Services/
│       │   ├── Files/
│       │   │   ├── FileFieldValidator.cs
│       │   │   ├── FileEncryptionService.cs
│       │   │   ├── FileCompressionService.cs
│       │   │   └── FileProcessingPipeline.cs
│       │   └── Storage/
│       │       └── MinIOFileService.cs
│       └── DTOs/
│           ├── FileUploadRequest.cs
│           ├── FileMetadataDto.cs
│           └── FileOptions.cs
├── Infrastructure/
│   └── MngDataGateway.Infrastructure/
│       └── Services/
│           ├── MinioService.cs
│           └── FileRepository.cs
└── Presentation/
    └── MngDataGateway.Api/
        ├── Controllers/
        │   ├── DataController.cs (file upload endpoint)
        │   └── FilesController.cs (download endpoint)
        └── BackgroundServices/
            └── FileCleanupService.cs (soft delete)
```

---

## 🔄 Error Handling

### Validation Errors (400)

```
"Base64 decode failed"
"Invalid MIME type: text/html (allowed: application/pdf)"
"File size 15MB exceeds limit 10MB"
"Invalid folder path: ../../../etc/passwd"
"Folder depth exceeds maximum (max: 10)"
"Invalid characters in folder path"
```

### Processing Errors (500)

```
"Compression failed: {detail}"
"Encryption failed: {detail}"
"MinIO connection timeout"
"MinIO upload failed"
```

### Retry Logic

```
Attempt 1: Immediate
Attempt 2: After 1 second
Attempt 3: After 2 seconds
If all fail: Return 500 error
```

---

## 📊 Metadata Structure (MinIO Headers)

```
x-amz-meta-original-filename: "invoice.pdf"
x-amz-meta-file-size: "2048576"
x-amz-meta-mime-type: "application/pdf"
x-amz-meta-created-at: "2025-01-24T10:30:00Z"
x-amz-meta-uploaded-at: "2025-01-24T10:30:00Z"
x-amz-meta-uploaded-by: "serkan.meral"
x-amz-meta-domain-name: "meral"
x-amz-meta-dataset-name: "@invoices"
x-amz-meta-record-id: "TASK-000001"
x-amz-meta-is-zipped: "false"
x-amz-meta-is-encrypted: "true"
x-amz-meta-encryption-config: "{\"algorithm\":\"AES-256-GCM\",\"keyDerivation\":\"PBKDF2\"}"
```

---

## 🎯 Implementation Checklist

### Configuration
- [ ] Extension mapping config oluştur
- [ ] Encryption key generate et
- [ ] Compression level ayarla
- [ ] MinIO endpoint config et

### Services
- [ ] FileFieldValidator implement et
- [ ] FileEncryptionService implement et
- [ ] FileCompressionService implement et
- [ ] FileProcessingPipeline implement et
- [ ] MinIOFileService enhance et

### Controllers
- [ ] DataController - file handling ekle
- [ ] FilesController - download endpoint ekle
- [ ] Error handling ekle
- [ ] Logging ekle

### Database
- [ ] File metadata schema oluştur
- [ ] MongoDB indexes create et
- [ ] Index queries test et

### Testing
- [ ] Unit tests write
- [ ] Integration tests write
- [ ] E2E tests write
- [ ] Performance tests run

### Documentation
- [ ] API documentation update
- [ ] Code comments add
- [ ] Deployment guide update
- [ ] Troubleshooting guide create

---

## 🚀 Success Criteria

### Phase 1 Completion
- ✅ Services implement & unit tested
- ✅ No compilation errors
- ✅ All tests passing (90%+ coverage)

### Phase 2 Completion
- ✅ Upload/download endpoints working
- ✅ Integration tests passing
- ✅ Encryption/compression verified
- ✅ File retrieval working

### Phase 3 Completion
- ✅ Performance benchmarks met
- ✅ Large files handled (100MB+)
- ✅ Monitoring metrics visible

### Phase 4 Completion
- ✅ All E2E tests passing
- ✅ Production ready
- ✅ Documentation complete

---

## 📅 Timeline Estimate

| Phase | Duration | Status | Completed Date |
|-------|----------|--------|----------------|
| Foundation (Phase 1) | 2 weeks | ✅ Complete | 24 Ocak 2026 |
| Integration (Phase 2) | 1 week | ✅ Complete | 24 Ocak 2026 |
| Optimization (Phase 3) | 1 week | ⏳ Future | - |
| Testing (Phase 4) | 2 weeks | 🟡 Partial | - |
| **Total** | **6 weeks** | 🟢 **Phase 1-2 Complete** | **24 Ocak 2026** |

---

## 📝 Current Configuration

### File Size Limits
- **Max File Size:** 5MB (5,242,880 bytes) - Configurable via `appsettings.json`
- **Config Path:** `FileStorage:Validation:MaxFileSize`
- **Default:** 5MB (changed from 100MB on 24 Ocak 2026)

### Memory Usage
- **Peak Memory:** ~22MB (temporary, during processing)
- **Status:** ✅ Acceptable for 5MB limit
- **Optimization Guide:** See `FILE_FIELD_MEMORY_OPTIMIZATION.md`

## 🔗 Related Documentation

- `/docs/MngDataGateway/specs/FILE_FIELD_TYPE_SPECIFICATION.md` - Detailed technical spec
- `/docs/MngDataGateway/api/DATASET_SCHEMA_SUMMARY.md` - Updated with file field type
- `/docs/MngDataGateway/FILE_FIELD_MEMORY_OPTIMIZATION.md` - Memory optimization guide
- `/docs/MngDataGateway/ROADMAP_MngDataGateway.md` - Main project roadmap

---

## 📞 Decision Log

### Decision 1: GUID Naming (✅ Accepted)
- **Date:** 24 Jan 2026
- **Decision:** `{GUID}.{lowercase-ext}` format
- **Rationale:** Security, uniqueness, simplicity
- **Status:** Final

### Decision 2: Metadata Location (✅ Accepted)
- **Date:** 24 Jan 2026
- **Decision:** MinIO custom headers, NOT database
- **Rationale:** Single source of truth, immutable
- **Status:** Final

### Decision 3: Encryption Key (✅ Accepted)
- **Date:** 24 Jan 2026
- **Decision:** Build-in config (appsettings.json)
- **Rationale:** Simplicity, centralized management
- **Status:** Final

### Decision 4: Database Storage (✅ Accepted)
- **Date:** 24 Jan 2026
- **Decision:** Path only, NO metadata duplication
- **Rationale:** DRY principle, minimal storage
- **Status:** Final

---

**Status:** 🟢 Ready for Implementation  
**Last Updated:** 24 January 2026  
**Next Step:** Begin Phase 1 Implementation

