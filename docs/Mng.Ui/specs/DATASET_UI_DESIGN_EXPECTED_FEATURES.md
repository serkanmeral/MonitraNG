# Dataset UI Design - Beklenen Özellikler ve Eksiklikler

**Tarih:** 13 Ocak 2026  
**Durum:** 📋 Analiz Tamamlandı - Backend vs UI Design Karşılaştırması  
**Amaç:** Backend'deki dataset fonksiyonlarını analiz ederek UI design dokümantasyonundaki eksiklikleri tespit etmek

---

## 🔍 Analiz Özeti

Backend'deki `MngDataGateway` servisini ve dataset ile ilgili tüm fonksiyonları analiz ettim. UI design dokümantasyonu (`DATASET_UI_DESIGN.md`) ile backend implementasyonu karşılaştırıldığında aşağıdaki eksiklikler tespit edildi.

---

## ❌ Tespit Edilen Eksiklikler

### 1. **Validation Definitions (Step 4) - ⚠️ KRİTİK EKSİK**

**Backend Durumu:**
- ✅ Backend'de `DatasetSchema.validations` field'ı mevcut (List<ValidationDefinition>)
- ✅ `ValidationService` tam implement edilmiş
- ✅ İki tip validation destekleniyor:
  - **Expression-based validation**: JavaScript benzeri expression'lar (örn: `endDate > startDate`, `price / pageCount <= 10`)
  - **HTTP-based validation**: External API endpoint'leri ile validation

**ValidationDefinition Yapısı:**
```typescript
{
  name: string;                    // Validation adı (unique)
  description?: string;            // Açıklama (opsiyonel)
  type: "expression" | "http";     // Validation tipi
  expression?: string;             // Expression-based için (örn: "endDate > startDate")
  url?: string;                    // HTTP-based için (validation endpoint URL)
  method?: "GET" | "POST";        // HTTP method (default: "POST")
  fields?: string[];              // HTTP-based için (hangi field'lar gönderilecek)
  when?: "create" | "update" | "both";  // Ne zaman çalışacak (default: "both")
  order?: number;                  // Execution order (default: 0)
}
```

**UI Design Dokümantasyonundaki Durum:**
- ❌ Validation Definitions hiç bahsedilmemiş
- ❌ Step 4 olarak "Permissions" gösterilmiş (ama backend'de henüz implement edilmemiş)
- ❌ ValidationDefinitions için UI tasarımı yok
- ❌ Expression editor yok
- ❌ HTTP validation URL/method/fields yönetimi yok

**HTTP Validation Detayları:**
- **URL**: Validation endpoint URL'i (örn: `https://api.example.com/validate`)
- **Method**: GET veya POST (default: POST)
  - **POST**: Data JSON body olarak gönderilir
  - **GET**: Data query parameters olarak gönderilir (şu anda tam implement edilmemiş, POST kullanılması önerilir)
- **Fields**: Hangi field'lar gönderilecek (opsiyonel, belirtilmezse tüm field'lar gönderilir)
- **When**: Ne zaman çalışacak (`create`, `update`, `both`)
- **Order**: Execution order (birden fazla validation varsa sıralama)
- **Response Format**: Validation endpoint'i şu format'ta response dönmeli:
  ```json
  {
    "isValid": true/false,
    "errorMessage": "Hata mesajı (opsiyonel)"
  }
  ```
- **Authorization**: HTTP validation request'lerinde otomatik olarak Authorization header'ı gönderilir (mevcut kullanıcının token'ı)
- **Timeout**: Default 30 saniye (config'den değiştirilebilir)
- **Error Handling**: Timeout veya network error durumunda validation geçerli sayılır (safe default)

**UI Design Dokümantasyonundaki Durum:**
- ❌ Validation Definitions hiç bahsedilmemiş
- ❌ Step 4 olarak "Permissions" gösterilmiş (ama backend'de henüz implement edilmemiş)
- ❌ ValidationDefinitions için UI tasarımı yok
- ❌ Expression editor yok
- ❌ HTTP validation URL/method/fields yönetimi yok

**Öneri:**
- Step 4 olarak "Validation Definitions" eklenmeli (Permissions şu anda backend'de yok, Step 5 olabilir)
- Validation Definitions için ayrı bir step oluşturulmalı
- Validation tipine göre dinamik form alanları gösterilmeli:
  - **Expression-based**: Expression editor (syntax highlighting, field name suggestions, örnek: `endDate > startDate`, `price / pageCount <= 10`)
  - **HTTP-based**: 
    - URL input (validation endpoint)
    - Method dropdown (GET/POST)
    - Fields multi-select (hangi field'lar gönderilecek - opsiyonel)
    - When dropdown (create/update/both)
    - Order number input
    - Response format açıklaması (isValid, errorMessage)
    - Authorization header bilgisi (otomatik gönderildiği belirtilmeli)

---

### 2. **Field-Level Validation Rules - ⚠️ ÖNEMLİ EKSİK**

**Backend Durumu:**
- ✅ Backend'de `FieldDefinition.validation` field'ı mevcut (FieldValidationRules)
- ✅ `ValidationService.ValidateFieldLevelRules` tam implement edilmiş
- ✅ Field type'a göre farklı validation rules destekleniyor

**FieldValidationRules Yapısı:**
```typescript
{
  // Number fields için
  min?: number;                    // Minimum değer
  max?: number;                    // Maximum değer
  
  // Text fields için
  minLength?: number;              // Minimum uzunluk
  maxLength?: number;              // Maximum uzunluk
  pattern?: string;                // Regex pattern
  
  // Array fields için
  minItems?: number;               // Minimum item sayısı
  maxItems?: number;               // Maximum item sayısı
  
  // DateTime fields için
  minDate?: Date;                  // Minimum tarih
  maxDate?: Date;                  // Maximum tarih
  
  // Custom error message
  message?: string;                // Özel hata mesajı
}
```

**UI Design Dokümantasyonundaki Durum:**
- ❌ Field modal'ında validation rules için alanlar yok
- ❌ Field type'a göre dinamik validation form alanları yok
- ❌ Regex pattern editor yok
- ❌ Date range picker yok (minDate/maxDate için)

**Öneri:**
- Field modal'ına "Validation Rules" section'ı eklenmeli
- Field type'a göre dinamik validation form alanları gösterilmeli:
  - **text**: minLength, maxLength, pattern (regex)
  - **number**: min, max
  - **datetime**: minDate, maxDate (date picker)
  - **array**: minItems, maxItems
- Regex pattern için syntax highlighting ve test editor'ü eklenmeli

---

### 3. **Query Parameters - ⚠️ GÜNCELLENMESİ GEREKEN**

**Backend Durumu:**
- ✅ Backend'de query parameters artık `QueryParameterDefinition` listesi olarak destekleniyor (yeni format)
- ✅ Backward compatibility: Eski `List<string>` formatı da destekleniyor
- ✅ QueryParameterDefinition yapısı:
```typescript
{
  name: string;                    // Parameter adı
  type: string;                    // Parameter tipi (text, number, bool, datetime, vb.)
  description?: string;            // Açıklama (opsiyonel)
  required: boolean;               // Zorunlu mu (default: true)
}
```

**UI Design Dokümantasyonundaki Durum:**
- ⚠️ Query modal'ında sadece "comma-separated string" olarak gösterilmiş
- ❌ QueryParameterDefinition listesi için UI tasarımı yok
- ❌ Parameter type, description, required alanları yok

**Öneri:**
- Query modal'ında parameters için ayrı bir section eklenmeli
- Parameter listesi için bir table/list component'i kullanılmalı
- Her parameter için: name, type (dropdown), description, required (checkbox)
- Parameter ekleme/düzenleme/silme işlemleri yapılabilmeli

---

### 4. **Default Value - ⚠️ EKSİK**

**Backend Durumu:**
- ✅ Backend'de `FieldDefinition.defaultValue` field'ı mevcut (BsonValue)
- ✅ Field type'a göre farklı default value tipleri destekleniyor

**UI Design Dokümantasyonundaki Durum:**
- ❌ Field modal'ında default value için alan yok

**Öneri:**
- Field modal'ına "Default Value" field'ı eklenmeli
- Field type'a göre dinamik input gösterilmeli:
  - **text**: Text input
  - **number**: Number input
  - **bool**: Checkbox
  - **datetime**: Date picker
  - **object**: JSON editor
  - **array**: Array editor (opsiyonel)

---

### 5. **Object Field Schema - ℹ️ BİLGİ NOTU**

**Backend Durumu:**
- ✅ Backend'de `object` field type'ı destekleniyor
- ⚠️ Object field için özel bir schema field'ı yok (sadece fieldType: "object")
- ✅ Object field için validation rules kullanılabilir (FieldValidationRules)

**UI Design Dokümantasyonundaki Durum:**
- ⚠️ Object field için "Object Schema (JSON)" gösterilmiş
- ⚠️ Backend'de object field için schema field'ı yok, bu yüzden bu alan backend'e gönderilmiyor olabilir

**Not:**
- Object field için schema definition backend'de desteklenmiyor
- UI'da gösterilen "Object Schema" alanı muhtemelen sadece dokümantasyon amaçlı veya gelecekte eklenmesi planlanan bir özellik
- Object field'lar için validation rules kullanılabilir (FieldValidationRules)

---

### 6. **Query Pipeline - ⚠️ DETAY EKSİK**

**Backend Durumu:**
- ✅ Backend'de `QueryDefinition.pipeline` field'ı mevcut (List<BsonDocument>)
- ✅ MongoDB aggregation pipeline olarak saklanıyor

**UI Design Dokümantasyonundaki Durum:**
- ✅ JSON editor olarak gösterilmiş
- ⚠️ Pipeline validation ve syntax checking detayları yok
- ⚠️ Parameter placeholder'ları (`:parameterName`) nasıl kullanılacağı açıklanmamış

**Öneri:**
- Pipeline editor'de parameter placeholder'ları için syntax highlighting eklenmeli
- Parameter placeholder format'ı açıklanmalı (örn: `:startDate`, `:endDate`)
- Pipeline validation (MongoDB syntax) eklenmeli (opsiyonel, backend'de zaten validate ediliyor)

---

## 📋 Öncelik Sırası

### 🔴 Yüksek Öncelik (Kritik)
1. **Validation Definitions (Step 4)** - Backend'de tam implement edilmiş, UI'da hiç yok
2. **Field-Level Validation Rules** - Backend'de tam implement edilmiş, UI'da hiç yok
3. **Query Parameters (yeni format)** - Backend'de implement edilmiş, UI'da eski format gösterilmiş

### 🟡 Orta Öncelik (Önemli)
4. **Default Value** - Backend'de destekleniyor, UI'da eksik
5. **Query Pipeline Detayları** - Mevcut ama detaylandırılması gerekiyor

### 🟢 Düşük Öncelik (Bilgi)
6. **Object Field Schema** - Backend'de desteklenmiyor, UI'da gösterilmiş (dokümantasyon amaçlı olabilir)

---

## 📝 Dokümantasyon Güncelleme Önerileri

1. **DATASET_UI_DESIGN.md** dosyasına aşağıdaki bölümler eklenmeli:
   - **Step 4: Validation Definitions** (Permissions yerine veya Permissions Step 5 olarak)
   - **Field-Level Validation Rules** (Field modal'ında)
   - **Query Parameters (yeni format)** (Query modal'ında)
   - **Default Value** (Field modal'ında)

2. **DTO Yapıları** bölümü güncellenmeli:
   - `ValidationDefinition` interface'i eklenmeli
   - `FieldValidationRules` interface'i eklenmeli
   - `QueryParameterDefinition` interface'i eklenmeli
   - `FieldDefinition` interface'ine `validation` ve `defaultValue` field'ları eklenmeli

3. **Implementasyon Checklist** güncellenmeli:
   - Validation Definitions step'i eklenmeli
   - Field-level validation rules eklenmeli
   - Query parameters (yeni format) eklenmeli

---

## 🔗 İlgili Backend Dosyaları

- **ValidationService.cs**: Validation servisi implementasyonu
- **DatasetSchema.cs**: ValidationDefinition, FieldValidationRules entity'leri
- **FieldValidationRules.cs**: Field-level validation rules entity
- **DatasetService.cs**: Dataset CRUD işlemleri, field validation
- **QueryParameterDefinition**: Query parameter yapısı (yeni format)

---

**Son Güncelleme:** 13 Ocak 2026  
**Durum:** 📋 Analiz Tamamlandı - Dokümantasyon Güncellemesi Bekleniyor
