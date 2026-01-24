# File Field Type Implementation Specification

**Date:** 24 Ocak 2026  
**Status:** ✅ FINAL SPECIFICATION  
**Version:** 1.0  
**Authors:** Serkan Meral (Product), AI Assistant (Technical Design)

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Request/Response Formats](#requestresponse-formats)
4. [File Processing Pipeline](#file-processing-pipeline)
5. [Error Handling](#error-handling)
6. [Security](#security)
7. [Performance Considerations](#performance-considerations)
8. [Implementation Details](#implementation-details)

---

## 🎯 Overview

### Purpose
File field type, dataset records'a dosya ekleme, şifreleme, sıkıştırma ve yönetme imkanı sağlar.

### Key Features
- ✅ **Base64 Input:** Create/update'te base64 encoded dosya alınır
- ✅ **MinIO Storage:** Dosyalar MinIO'ya fiziki olarak kaydedilir
- ✅ **Database Path:** Database'de sadece dosya path'i tutulur
- ✅ **Metadata:** MinIO custom headers'da metadata tutulur
- ✅ **Compression:** Optional gzip compression
- ✅ **Encryption:** Optional AES-256-GCM encryption
- ✅ **Custom Folders:** Flexible folder structure
- ✅ **Array Support:** Birden fazla file per field
- ✅ **Download Endpoint:** Ayrı download endpoint

### Supported Formats
- **Images:** .jpg, .jpeg, .png, .gif, .webp, .bmp, .svg
- **Documents:** .pdf, .docx, .xlsx, .pptx, .txt, .rtf
- **Archives:** .zip, .rar, .7z
- **Videos:** .mp4, .avi, .mov, .mkv (configurable)
- **Custom:** Extension mapping'de tanımlanan tüm tipiler

---

## 🏗️ Architecture

### System Diagram

```
┌─────────────────────────────────────────┐
│  Mng.UI / Client Application            │
│  - File selected (browser)              │
│  - Convert to base64                    │
└──────────────────┬──────────────────────┘
                   │ POST /api/datasets/{name}/data
                   │ { content, folder, useCompression, useEncryption }
                   ↓
┌─────────────────────────────────────────┐
│  MngDataGateway API                     │
│  - JWT validation                       │
│  - Domain extraction                    │
│  - Request parsing                      │
└──────────────────┬──────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────┐
│  File Processing Pipeline               │
│  1. FileFieldValidator                  │
│     - Decode base64                     │
│     - Check magic bytes                 │
│     - Validate size/extension           │
│     - Validate folder path              │
│  2. FileCompressionService              │
│     - Optional gzip compression         │
│  3. FileEncryptionService               │
│     - Optional AES-256-GCM encryption   │
│  4. MinIOFileService                    │
│     - Upload to MinIO bucket            │
│     - Write metadata headers            │
│  5. DataController                      │
│     - Save path to MongoDB              │
└──────────────────┬──────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────┐
│  Storage Layer                          │
│  ├─ MinIO: /mng-{domain}/data/...       │
│  └─ MongoDB: path only                  │
└─────────────────────────────────────────┘
                   │
                   ├─────────────────────────────────────────┐
                   │                                         │
        (Download Request)              (Metadata Query)     │
                   │                                         │
                   ↓                                         ↓
        ┌──────────────────────┐         ┌──────────────────────┐
        │ GET /api/files/      │         │ GET /api/datasets    │
        │ download/{fileId}    │         │ /{name}/data/{id}    │
        └──────────────────────┘         └──────────────────────┘
                   │                                         │
                   ↓                                         ↓
        ┌──────────────────────┐         ┌──────────────────────┐
        │ File Retrieval       │         │ Data + Path Info     │
        │ 1. Get from MinIO    │         │ + Metadata           │
        │ 2. Decrypt (if)      │         └──────────────────────┘
        │ 3. Decompress (if)   │
        │ 4. Stream to client  │
        └──────────────────────┘
```

### Service Components

#### 1. FileFieldValidator
```csharp
public interface IFileFieldValidator
{
    ValidationResult ValidateBase64(string base64Content);
    ValidationResult ValidateFileSize(long size);
    ValidationResult ValidateExtension(string extension);
    ValidationResult ValidateFolderPath(string folderPath);
    string DetectMimeType(byte[] fileBytes);
    string GetExtensionFromMimeType(string mimeType);
}
```

#### 2. FileCompressionService
```csharp
public interface IFileCompressionService
{
    Task<byte[]> CompressAsync(byte[] data);
    Task<byte[]> DecompressAsync(byte[] data);
    bool IsCompressionFailed(Exception ex);
}
```

#### 3. FileEncryptionService
```csharp
public interface IFileEncryptionService
{
    Task<byte[]> EncryptAsync(byte[] plainData);
    Task<byte[]> DecryptAsync(byte[] encryptedData);
}
```

#### 4. MinIOFileService
```csharp
public interface IMinIOFileService
{
    Task<UploadResult> UploadFileAsync(
        string domain,
        string bucketName,
        string objectPath,
        byte[] fileContent,
        Dictionary<string, string> metadata);
    
    Task<byte[]> DownloadFileAsync(
        string bucketName,
        string objectPath);
    
    Task<FileMetadata> GetFileMetadataAsync(
        string bucketName,
        string objectPath);
    
    Task DeleteFileAsync(
        string bucketName,
        string objectPath);
}
```

---

## 📥 Request/Response Formats

### 1. File Upload - Single File (CREATE)

**HTTP Request:**
```http
POST /api/datasets/{datasetName}/data
Content-Type: application/json
Authorization: Bearer {jwt-token}

{
  "title": "Invoice January",
  "documentFile": {
    "content": "JVBERi0xLjQKJeLjz9M...[base64-content]...==",
    "folder": "invoices/2025",
    "useCompression": true,
    "useEncryption": true
  }
}
```

**Response (201 Created):**
```json
{
  "__dataId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Invoice January",
  "documentFile": "/mng-meral/data/@invoices/550e8400-e29b/invoices/2025/a7f3k9m2p5q8r1s4.pdf",
  "__createInfo": {
    "createdAt": "2025-01-24T10:30:00Z",
    "userInfo": {
      "uid": "user-uuid",
      "userName": "serkan",
      "domain": "meral"
    }
  }
}
```

### 2. File Upload - Array of Files (CREATE)

**HTTP Request:**
```http
POST /api/datasets/@projects/data
Content-Type: application/json
Authorization: Bearer {jwt-token}

{
  "name": "New Project",
  "attachments": [
    {
      "content": "iVBORw0KGgo...[base64-image]...==",
      "folder": "documents",
      "useCompression": false,
      "useEncryption": true
    },
    {
      "content": "JVBERi0xLjQK...[base64-pdf]...==",
      "folder": "documents",
      "useCompression": true,
      "useEncryption": true
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "__dataId": "proj-uuid-123",
  "name": "New Project",
  "attachments": [
    "/mng-meral/data/@projects/proj-uuid-123/documents/b1e5q7w9x2z4c6v8.png",
    "/mng-meral/data/@projects/proj-uuid-123/documents/d9f1h3j5k7m9n1p3.pdf"
  ],
  "__createInfo": { ... }
}
```

### 3. File Upload - UPDATE (PUT)

**HTTP Request:**
```http
PUT /api/datasets/@invoices/data/550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json
Authorization: Bearer {jwt-token}

{
  "title": "Invoice January - Updated",
  "documentFile": {
    "content": "JVBERi0xLjQKJeLjz9M...[new-base64]...==",
    "folder": "invoices/2025/updated",
    "useCompression": true,
    "useEncryption": true
  }
}
```

**Response (200 OK):**
```json
{
  "__dataId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Invoice January - Updated",
  "documentFile": "/mng-meral/data/@invoices/550e8400-e29b/invoices/2025/updated/x8y2z5a1b4c7d9e2.pdf",
  "__lastUpdateInfo": {
    "updatedAt": "2025-01-24T11:00:00Z",
    "userInfo": { ... }
  }
}
```

### 4. File Download - GET Endpoint

**HTTP Request:**
```http
GET /api/files/download/{fileId}
Authorization: Bearer {jwt-token}
```

**URL Parameters:**
```
fileId: {record-uuid}
```

**Response (200 OK):**
```
[Binary PDF/Image Content]

Headers:
  Content-Type: application/pdf (or image/jpeg, etc.)
  Content-Disposition: attachment; filename="invoice.pdf"
  Content-Length: 2048576
  Cache-Control: private, max-age=3600
```

**Error Responses:**

**404 Not Found:**
```json
{
  "error": "File not found",
  "fileId": "invalid-uuid"
}
```

**403 Forbidden:**
```json
{
  "error": "Access denied",
  "message": "You don't have permission to download this file"
}
```

**500 Internal Server Error:**
```json
{
  "error": "File retrieval failed",
  "message": "Decryption failed: Invalid key or corrupted data"
}
```

### 5. GET Data with File Path

**HTTP Request:**
```http
GET /api/datasets/@invoices/data/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer {jwt-token}
```

**Response (200 OK):**
```json
{
  "__dataId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Invoice January",
  "documentFile": "/mng-meral/data/@invoices/550e8400-e29b/invoices/2025/a7f3k9m2p5q8r1s4.pdf",
  // Download URL oluşturmak için:
  // GET /api/files/download/550e8400-e29b-41d4-a716-446655440000
  "__createInfo": { ... }
}
```

---

## 🔄 File Processing Pipeline

### Step-by-Step Flow

#### Step 1: Input Parsing
```csharp
// Controller'da request parse
var fileUploadRequest = new FileUploadRequest
{
    Content = request.DocumentFile.Content,        // base64
    Folder = request.DocumentFile.Folder,          // optional
    UseCompression = request.DocumentFile.UseCompression ?? true,
    UseEncryption = request.DocumentFile.UseEncryption ?? true
};
```

#### Step 2: Validation
```csharp
// 1. Base64 decode
byte[] decodedBytes = Convert.FromBase64String(fileUploadRequest.Content);

// 2. Magic bytes check
string detectedMimeType = _validator.DetectMimeType(decodedBytes);

// 3. File size validation
if (decodedBytes.Length > maxSize)
    throw new FileSizeExceededException();

// 4. MIME type validation
if (!allowedMimeTypes.Contains(detectedMimeType))
    throw new InvalidMimeTypeException();

// 5. Extension mapping
string extension = _validator.GetExtensionFromMimeType(detectedMimeType);

// 6. Folder path validation
var folderValidation = _validator.ValidateFolderPath(fileUploadRequest.Folder);
if (!folderValidation.IsValid)
    throw new InvalidFolderPathException();
```

#### Step 3: Compression (if enabled)
```csharp
if (fileUploadRequest.UseCompression)
{
    try
    {
        decodedBytes = await _compressionService.CompressAsync(decodedBytes);
        isCompressed = true;
    }
    catch (Exception ex)
    {
        // Compression fail → skip, don't abort
        isCompressed = false;
        _logger.LogWarning("Compression failed: {Message}", ex.Message);
    }
}
```

#### Step 4: Encryption (if enabled)
```csharp
if (fileUploadRequest.UseEncryption)
{
    try
    {
        decodedBytes = await _encryptionService.EncryptAsync(decodedBytes);
        isEncrypted = true;
    }
    catch (Exception ex)
    {
        _logger.LogError("Encryption failed: {Message}", ex.Message);
        throw new FileEncryptionFailedException();
    }
}
```

#### Step 5: MinIO Upload (with Retry)
```csharp
string fileId = Guid.NewGuid().ToString();
string objectPath = BuildObjectPath(domain, datasetName, recordId, folder, fileId, extension);
string bucketName = $"mng-{domain}";

var metadata = new Dictionary<string, string>
{
    ["x-amz-meta-original-filename"] = originalFileName,
    ["x-amz-meta-file-size"] = decodedBytes.Length.ToString(),
    ["x-amz-meta-mime-type"] = detectedMimeType,
    ["x-amz-meta-created-at"] = DateTime.UtcNow.ToString("O"),
    ["x-amz-meta-uploaded-at"] = DateTime.UtcNow.ToString("O"),
    ["x-amz-meta-uploaded-by"] = userName,
    ["x-amz-meta-domain-name"] = domain,
    ["x-amz-meta-dataset-name"] = datasetName,
    ["x-amz-meta-record-id"] = recordId,
    ["x-amz-meta-is-zipped"] = isCompressed.ToString().ToLower(),
    ["x-amz-meta-is-encrypted"] = isEncrypted.ToString().ToLower(),
    ["x-amz-meta-encryption-config"] = JsonConvert.SerializeObject(new
    {
        algorithm = "AES-256-GCM",
        keyDerivation = "PBKDF2"
    })
};

// Retry logic: 3 attempts with exponential backoff
var uploadResult = await _minioService.UploadWithRetryAsync(
    bucketName,
    objectPath,
    decodedBytes,
    metadata,
    maxRetries: 3
);
```

#### Step 6: Database Storage
```csharp
// Store only the path
var data = new Dictionary<string, object>
{
    ["documentFile"] = uploadResult.ObjectPath  // /mng-meral/data/@invoices/...
};

// Save to MongoDB
var savedRecord = await _dataRepository.InsertAsync(datasetName, data);
```

#### Step 7: Return Response
```csharp
return Created($"/api/datasets/{datasetName}/data/{recordId}", new
{
    __dataId = recordId,
    title = request.Title,
    documentFile = uploadResult.ObjectPath,
    __createInfo = new { ... }
});
```

---

## 📋 Folder Path Construction Rules

### Default Path (folder null/empty)
```
/mng-{domain}/data/{datasetName}/{recordId}/
```

### Custom Folder Path (folder provided)
```
/mng-{domain}/data/{datasetName}/{recordId}/{folder}/
```

### Examples

```
Domain: meral
Dataset: @invoices
Record: TASK-000001
Folder: invoices/2025

Result: /mng-meral/data/@invoices/TASK-000001/invoices/2025/{file-uuid}.pdf

Domain: test-domain
Dataset: @projects
Record: proj-uuid-123
Folder: null

Result: /mng-test-domain/data/@projects/proj-uuid-123/{file-uuid}.pdf
```

### Folder Validation Rules

| Rule | Constraint | Example |
|------|-----------|---------|
| **Max Depth** | 10 levels | ✅ a/b/c/d/e (5 levels) |
| **Max Length** | 512 chars | ✅ a/b/c/d/e/... |
| **Allowed Chars** | `[a-zA-Z0-9_-/]` | ✅ my-folder_2025 |
| **Traversal** | No `..` | ❌ ../../../ |
| **Leading/Trailing** | No leading/trailing `/` | ✅ invoices/2025 |
| **Consecutive Slashes** | No `//` | ❌ invoices//2025 |

**Validation Algorithm:**
```csharp
public ValidationResult ValidateFolderPath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return ValidationResult.Success();  // Default path
    
    // Normalize
    path = path.Trim().Replace("\\", "/");
    path = Regex.Replace(path, "/+", "/");  // Remove consecutive slashes
    
    // Check length
    if (path.Length > 512)
        return ValidationResult.Fail("Path too long");
    
    // Check depth
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments.Length > 10)
        return ValidationResult.Fail("Path too deep");
    
    // Check forbidden sequences
    if (path.Contains(".."))
        return ValidationResult.Fail("Relative paths not allowed");
    
    // Check allowed characters per segment
    var pattern = @"^[a-zA-Z0-9_-]+$";
    foreach (var segment in segments)
    {
        if (!Regex.IsMatch(segment, pattern))
            return ValidationResult.Fail($"Invalid segment: {segment}");
    }
    
    return ValidationResult.Success();
}
```

---

## 🔐 Encryption & Compression

### Compression Strategy

**Algorithm:** gzip (deflate compression)  
**Compression Level:** 6 (default - balanced)

**When Enabled:**
```
Original: 10MB PDF
Compressed: ~7MB (30% reduction typical)
CPU Cost: ~50ms
Memory: ~5MB temporary
```

**On Failure:**
```
Compression fails (corrupted data, disk space, etc.)
  ↓
isCompressed = false
  ↓
Set in metadata: x-amz-meta-is-zipped: false
  ↓
Store original data to MinIO
  ↓
Continue operation (non-fatal)
```

### Encryption Strategy

**Algorithm:** AES-256-GCM (Galois/Counter Mode)  
**Key Size:** 256 bits (32 bytes)  
**Nonce Size:** 96 bits (12 bytes, random per encryption)  
**Authentication Tag Size:** 128 bits (16 bytes)

**Key Management:**
```json
{
  "Key": "base64-encoded-256-bit-key",
  "Source": "appsettings.json",
  "Rotation": "Manual (not implemented in Phase 1)",
  "Backup": "Secure storage required"
}
```

**Encryption Flow:**
```
1. Generate random 96-bit nonce
2. Encrypt data with AES-256-GCM
   - Input: compressed data (if enabled)
   - Output: ciphertext + authentication tag
3. Combine: nonce (12 bytes) + tag (16 bytes) + ciphertext
4. Upload combined data to MinIO
5. Store nonce in metadata (accessible on download)
```

**Decryption Flow:**
```
1. Download encrypted data from MinIO
2. Parse: nonce (first 12 bytes) + tag (next 16 bytes) + ciphertext
3. Decrypt with AES-256-GCM
   - Verify authentication tag
   - Recover plaintext (originally compressed, if applicable)
4. Decompress (if isZipped=true in metadata)
5. Return data
```

---

## ❌ Error Handling

### Validation Errors (HTTP 400)

```json
{
  "error": "Validation failed",
  "details": [
    {
      "field": "content",
      "message": "Base64 decode failed: Invalid characters"
    }
  ]
}

{
  "error": "Validation failed",
  "details": [
    {
      "field": "folder",
      "message": "Path traversal detected: ../../etc/passwd"
    }
  ]
}

{
  "error": "Validation failed",
  "details": [
    {
      "field": "fileSize",
      "message": "File size 15MB exceeds maximum 10MB"
    }
  ]
}

{
  "error": "Validation failed",
  "details": [
    {
      "field": "mimeType",
      "message": "MIME type text/html not allowed (allowed: application/pdf, image/*)"
    }
  ]
}
```

### Processing Errors (HTTP 500)

```json
{
  "error": "File processing failed",
  "message": "Encryption failed: Invalid key format",
  "timestamp": "2025-01-24T10:30:00Z",
  "correlationId": "req-uuid-123"
}

{
  "error": "File processing failed",
  "message": "MinIO connection timeout after 3 attempts",
  "timestamp": "2025-01-24T10:30:00Z",
  "correlationId": "req-uuid-123"
}
```

### Retry Logic

```csharp
public async Task<UploadResult> UploadWithRetryAsync(
    string bucket,
    string objectPath,
    byte[] data,
    Dictionary<string, string> metadata,
    int maxRetries = 3)
{
    TimeSpan[] backoffDelays = new[]
    {
        TimeSpan.Zero,           // Attempt 1: immediate
        TimeSpan.FromSeconds(1), // Attempt 2: after 1s
        TimeSpan.FromSeconds(2)  // Attempt 3: after 2s
    };
    
    Exception? lastException = null;
    
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            if (attempt > 0)
            {
                await Task.Delay(backoffDelays[attempt]);
                _logger.LogWarning("Retry attempt {Attempt} for {Path}", attempt + 1, objectPath);
            }
            
            return await _minioClient.UploadAsync(bucket, objectPath, data, metadata);
        }
        catch (Exception ex)
        {
            lastException = ex;
            _logger.LogError(ex, "Upload attempt {Attempt} failed", attempt + 1);
        }
    }
    
    throw new FileUploadFailedException(
        $"Upload failed after {maxRetries} attempts",
        lastException
    );
}
```

---

## 🔒 Security

### Access Control

**File Download Access Check:**
```csharp
public async Task<bool> HasAccessAsync(
    string userId,
    string fileId)
{
    var file = await _fileRepository.GetByIdAsync(fileId);
    
    // 1. Domain check (JWT)
    var userDomain = ExtractDomainFromToken();
    if (file.Domain != userDomain)
        return false;  // Different domain
    
    // 2. Owner check
    if (file.OwnerId == userId)
        return true;   // Owner has access
    
    // 3. Admin check
    if (IsAdmin(userId))
        return true;   // Admin has access
    
    // 4. Public check (if applicable)
    if (file.IsPublic)
        return true;   // Public file
    
    // 5. Shared users check
    if (file.SharedUsers?.Contains(userId) == true)
        return true;   // Explicitly shared
    
    // 6. Group check
    var userGroups = await GetUserGroupsAsync(userId);
    if (file.SharedGroups?.Any(g => userGroups.Contains(g)) == true)
        return true;   // Shared with user's group
    
    return false;  // No access
}
```

### Data Protection

**Encryption:**
- ✅ AES-256-GCM at rest (MinIO)
- ✅ HTTPS in transit
- ✅ Random nonce per file

**Access Logging:**
- ✅ Log all download attempts
- ✅ Track failed access attempts
- ✅ Monitor unusual patterns

**Metadata Security:**
- ✅ Metadata immutable (no update after creation)
- ✅ Permission check on all operations
- ✅ Domain isolation enforced

---

## ⚡ Performance Considerations

### File Size Limits

```
Recommended Maximum: 100MB per file
Hard Limit: 1GB per file (configurable)
Array Size: Max 10 files per field
```

### Memory Usage

```
Base64 Decoding: Input size × 1.33
Compression: Input size × 0.5 (typical)
Encryption: Small overhead (< 1MB)
Total Buffer: ~Input size + 5MB overhead
```

**Example (100MB file):**
```
Base64 input: 133MB (in memory)
After decode: 100MB
After compress: 70MB (typical)
After encrypt: 70MB + 28 bytes
Peak memory: ~200MB for this operation
```

### Optimization Strategies

1. **Streaming for Large Files:**
```csharp
// Stream directly to MinIO, don't load in memory
await minioClient.PutObjectAsync(new PutObjectArgs()
    .WithBucket(bucket)
    .WithObject(path)
    .WithStreamData(stream)
    .WithObjectSize(stream.Length));
```

2. **Parallel Uploads (Array):**
```csharp
var uploadTasks = files.Select(f =>
    UploadFileAsync(f)
).ToArray();

await Task.WhenAll(uploadTasks);
```

3. **Compression Level Tuning:**
```
Level 1-3: Fast, less compression
Level 6: Default (balanced)
Level 9: Slow, best compression
```

---

## 📊 Dataset Field Definition

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
    "maxSize": 10485760,
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
    "maxSize": 5242880,
    "maxFiles": 10,
    "allowedExtensions": [".pdf", ".jpg", ".png", ".docx"],
    "defaultCompression": true,
    "defaultEncryption": true
  }
}
```

### Field Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `fieldType` | string | ✅ | - | "file" |
| `name` | string | ✅ | - | Field name |
| `title` | string | ❌ | - | Display name |
| `mandatory` | bool | ❌ | false | Required field |
| `isArray` | bool | ❌ | false | Multiple files |
| `fileOptions` | object | ❌ | {} | File options |

### File Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `maxSize` | number | 10485760 | Max file size (bytes) |
| `maxFiles` | number | 1 | Max files (if array) |
| `allowedExtensions` | string[] | all | Allowed file extensions |
| `defaultCompression` | bool | true | Default compression |
| `defaultEncryption` | bool | true | Default encryption |

---

## 🧪 Testing Strategy

### Unit Tests

**FileFieldValidator:**
```csharp
[Test]
public void ValidateBase64_ValidInput_Success()
{
    var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("test"));
    var result = _validator.ValidateBase64(base64);
    Assert.IsTrue(result.IsValid);
}

[Test]
public void ValidateFileSize_ExceedsLimit_Fail()
{
    var result = _validator.ValidateFileSize(11 * 1024 * 1024); // 11MB
    Assert.IsFalse(result.IsValid);
}

[Test]
public void ValidateFolderPath_Traversal_Fail()
{
    var result = _validator.ValidateFolderPath("../../../etc/passwd");
    Assert.IsFalse(result.IsValid);
}
```

**FileEncryptionService:**
```csharp
[Test]
public async Task EncryptDecrypt_RoundTrip_Success()
{
    byte[] originalData = Encoding.UTF8.GetBytes("secret data");
    
    var encrypted = await _encryptionService.EncryptAsync(originalData);
    var decrypted = await _encryptionService.DecryptAsync(encrypted);
    
    Assert.AreEqual(originalData, decrypted);
}

[Test]
public async Task Decrypt_InvalidData_Fail()
{
    byte[] invalidData = new byte[100];
    new Random().NextBytes(invalidData);
    
    Assert.ThrowsAsync<CryptographicException>(
        async () => await _encryptionService.DecryptAsync(invalidData)
    );
}
```

### Integration Tests

```csharp
[Test]
public async Task UploadAndDownloadFile_Success()
{
    // Setup
    var domain = "test-domain";
    var dataset = "@invoices";
    var recordId = "TASK-000001";
    var fileContent = File.ReadAllBytes("sample.pdf");
    var base64 = Convert.ToBase64String(fileContent);
    
    // Upload
    var uploadRequest = new FileUploadRequest
    {
        Content = base64,
        Folder = "invoices/2025",
        UseCompression = true,
        UseEncryption = true
    };
    
    var uploadResponse = await _dataController.CreateAsync(dataset, new
    {
        title = "Invoice",
        documentFile = uploadRequest
    });
    
    Assert.AreEqual(201, uploadResponse.StatusCode);
    var filePath = uploadResponse.Body.documentFile;
    
    // Download
    var downloadResponse = await _filesController.DownloadAsync(filePath);
    
    Assert.AreEqual(200, downloadResponse.StatusCode);
    var downloadedContent = await downloadResponse.Content.ReadAsByteArrayAsync();
    
    // Verify
    CollectionAssert.AreEqual(fileContent, downloadedContent);
}
```

---

## 📋 Configuration Reference

### appsettings.json

```json
{
  "MngDataGatewaySettings": {
    "FileStorage": {
      "Minio": {
        "Endpoint": "minio:9000",
        "AccessKey": "minioadmin",
        "SecretKey": "minioadmin",
        "BucketName": "datasets",
        "UseSSL": false
      },
      "Encryption": {
        "Enabled": true,
        "Algorithm": "AES-256-GCM",
        "Key": "base64-encoded-256-bit-key-here",
        "KeyDerivation": "PBKDF2",
        "Iterations": 10000,
        "SaltLength": 16
      },
      "Compression": {
        "Algorithm": "gzip",
        "Level": 6,
        "Enabled": true
      },
      "Validation": {
        "MaxFileSize": 104857600,
        "MaxFolderDepth": 10,
        "MaxPathLength": 512,
        "AllowedExtensions": [
          ".pdf", ".docx", ".xlsx", ".pptx",
          ".jpg", ".jpeg", ".png", ".gif",
          ".mp4", ".avi", ".mov"
        ]
      },
      "Retry": {
        "MaxAttempts": 3,
        "BackoffDelayMs": [0, 1000, 2000]
      }
    }
  }
}
```

---

## 🚀 Deployment Checklist

- [ ] MinIO setup complete
- [ ] Encryption key generated & secured
- [ ] appsettings.json configured
- [ ] Database indexes created
- [ ] Services registered in DI
- [ ] Error handling tested
- [ ] Security reviewed
- [ ] Performance tested (100MB+ files)
- [ ] Documentation updated
- [ ] API documentation published

---

**Status:** ✅ FINAL SPECIFICATION  
**Version:** 1.0  
**Last Updated:** 24 January 2026  
**Next Step:** Begin Phase 1 Implementation

