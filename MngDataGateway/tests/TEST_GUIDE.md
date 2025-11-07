# Data CRUD Test Guide

## 📋 Test Öncesi Hazırlık

### 1. Gerekli Servisler

**MongoDB** ✅
```powershell
# MongoDB çalışıyor mu kontrol et
mongosh --eval "db.version()"
```

**RabbitMQ** ⚠️ (Opsiyonel - notification için)
```powershell
# RabbitMQ Management UI: http://localhost:15672
# Username: admin
# Password: admin123
```

### 2. Test Dataset Kontrolü

Dataset: `@test_tasks_224334`

**Fields:**
- `title` (text, mandatory)
- `description` (text)
- `priority` (number, mandatory)
- `isCompleted` (bool, mandatory)
- `dueDate` (datetime)
- `taskNumber` (incremental: TASK-{0:D6})

**MongoDB'de kontrol:**
```javascript
use monitra_seven_com
db['@datasets'].findOne({ name: "@test_tasks_224334" })
```

### 3. Token Hazırlığı

```powershell
# MngKeeper'dan token al
cd C:\Serkan\iSIM\MonitraNG\MngKeeper\tests
.\get-serkan-token.ps1
```

Token şuraya kaydedilir: `$env:TEMP\serkan_token.txt`

---

## 🚀 Test Adımları

### Adım 1: Uygulamayı Başlat

```powershell
cd C:\Serkan\iSIM\MonitraNG\MngDataGateway\Presentation\MngDataGateway.Api
dotnet run
```

**Beklenen çıktı:**
```
✅ RabbitMQ connection initialized
✅ Now listening on: https://0.0.0.0:5010
```

### Adım 2: Test Scriptini Çalıştır

**Yeni PowerShell penceresi aç:**

```powershell
cd C:\Serkan\iSIM\MonitraNG\MngDataGateway\tests
.\test-data-crud.ps1
```

---

## 🧪 Test Senaryoları

### TEST 1: CREATE ✅
**Endpoint:** `POST /api/data/@test_tasks_224334`

**Kontroller:**
- ✅ Data oluşturuldu mu?
- ✅ `__dataId` generate edildi mi?
- ✅ `taskNumber` incremental field oluştu mu? (TASK-000001)
- ✅ `__history` array var mı? (logging: "self" ise)
- ✅ Validation çalıştı mı? (mandatory fields)

### TEST 2: LIST ✅
**Endpoint:** `GET /api/data/@test_tasks_224334?skip=0&limit=10`

**Kontroller:**
- ✅ Pagination çalışıyor mu?
- ✅ `totalCount` doğru mu?
- ✅ `pageNumber` ve `totalPages` hesaplanıyor mu?

### TEST 3: GET BY ID ✅
**Endpoint:** `GET /api/data/@test_tasks_224334/{dataId}`

**Kontroller:**
- ✅ Doğru data dönüyor mu?
- ✅ 404 dönmüyor mu? (data varsa)

### TEST 4: UPDATE ✅
**Endpoint:** `PUT /api/data/@test_tasks_224334/{dataId}`

**Kontroller:**
- ✅ Data güncellendi mi?
- ✅ `__history` array'e eklendi mi? (logging: "self")
- ✅ Changed fields loglandı mı?

### TEST 5: DELETE (Soft) ✅
**Endpoint:** `DELETE /api/data/@test_tasks_224334/{dataId}`

**Kontroller:**
- ✅ Soft delete yapıldı mı?
- ✅ `__isDeleted: true` set edildi mi?
- ✅ `__deleteInfo` eklendi mi?

### TEST 6: Verify Deleted ✅
**Endpoint:** `GET /api/data/@test_tasks_224334/{dataId}`

**Kontroller:**
- ✅ 404 döndü mü? (deleted data görünmemeli)

### TEST 7: RESTORE ✅
**Endpoint:** `POST /api/data/@test_tasks_224334/{dataId}/restore`

**Kontroller:**
- ✅ Data restore edildi mi?
- ✅ `__isDeleted: false` oldu mu?
- ✅ `__restoreInfo` eklendi mi?

### TEST 8: Verify Restored ✅
**Endpoint:** `GET /api/data/@test_tasks_224334/{dataId}`

**Kontroller:**
- ✅ Data tekrar görünüyor mu?

### TEST 9: Incremental Field ✅
**Multiple CREATE requests**

**Kontroller:**
- ✅ Her taskNumber unique mi?
- ✅ Sequential artıyor mu? (TASK-000001, TASK-000002, ...)
- ✅ Gap yok mu? (normal şartlarda)

---

## 📊 MongoDB Kontrolleri

### 1. Data Collection
```javascript
use monitra_seven_com
db['@test_tasks_224334'].find().pretty()
```

### 2. Counters Collection
```javascript
use monitra_seven_com
db['@__counters'].find().pretty()

// Beklenen:
// { "_id": "@test_tasks_224334.taskNumber", "value": 4, "lastUpdated": ISODate(...) }
```

### 3. Indexes
```javascript
use monitra_seven_com
db['@test_tasks_224334'].getIndexes()
```

### 4. Notification Errors (varsa)
```javascript
use monitra_system
db['@notification_errors'].find().pretty()
```

---

## 🐰 RabbitMQ Kontrolleri

### Management UI
**URL:** http://localhost:15672  
**Login:** admin / admin123

### Kontroller:

1. **Exchanges**
   - `monitra.data.events.seven` exchange oluştu mu?
   - Type: `topic` mi?

2. **Published Events**
   - Message count artıyor mu?
   - Routing keys:
     - `dataset.@test_tasks_224334.created`
     - `dataset.@test_tasks_224334.updated`
     - `dataset.@test_tasks_224334.deleted`
     - `dataset.@test_tasks_224334.restored`

3. **Event Payload Sample**
   ```json
   {
     "eventId": "guid...",
     "eventType": "dataset.data.created",
     "domain": { "name": "seven", "databaseName": "monitra_seven_com" },
     "dataset": { "name": "@test_tasks_224334" },
     "data": { "__dataId": "...", "title": "...", "taskNumber": "TASK-000001" },
     "actor": { "userId": "...", "email": "serkan@seven.com" }
   }
   ```

---

## ✅ Başarı Kriterleri

**Phase 1 Test - PASS Koşulları:**

1. ✅ Tüm 9 test senaryosu başarılı
2. ✅ Incremental field düzgün çalışıyor
3. ✅ Validation hataları doğru dönüyor (400)
4. ✅ Soft delete/restore çalışıyor
5. ✅ History tracking çalışıyor (logging: "self")
6. ✅ RabbitMQ event'leri publish ediliyor
7. ✅ Counters collection güncelleniyor
8. ✅ Pagination doğru çalışıyor

---

## 🐛 Hata Durumları

### Validation Errors (Expected - 400)

**Test: Mandatory field eksik**
```powershell
$invalidData = @{ description = "No title!" }
Invoke-RestMethod -Uri "$baseUrl/api/data/@test_tasks_224334" `
  -Method POST -Headers $headers -Body ($invalidData | ConvertTo-Json)
```

**Beklenen Response:**
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Validation failed",
    "details": [
      { "field": "title", "message": "Field 'title' is required" }
    ]
  }
}
```

### RabbitMQ Connection Fail (Warning)

Eğer RabbitMQ çalışmıyorsa:
- ⚠️ Data işlemleri devam eder
- ⚠️ Event publish fail olur
- ⚠️ @notification_errors'a log düşer
- ✅ User response etkilenmez

---

## 📝 Test Sonrası Cleanup (Opsiyonel)

```javascript
// Test data'ları temizle
use monitra_seven_com
db['@test_tasks_224334'].deleteMany({ title: /Test Task/ })

// Counter'ı sıfırla
db['@__counters'].deleteOne({ _id: "@test_tasks_224334.taskNumber" })
```

---

## 🎯 Next Steps After Tests

**Tüm testler başarılı ise:**

1. ✅ Phase 1 Implementation Complete
2. 📝 Commit changes
3. 🚀 Phase 2 Planning:
   - Bulk insert
   - Relation expansion
   - Dynamic defaults
   - Advanced queries

**Test fail ise:**

1. 🐛 Log'ları kontrol et (Serilog console)
2. 🔍 MongoDB'yi kontrol et
3. 🐰 RabbitMQ'yu kontrol et
4. 💻 Debugger ile takip et

---

**Test Duration:** ~30-60 saniye  
**Test Coverage:** Phase 1 Core CRUD ✅

