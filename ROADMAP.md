# MonitraNG Development Roadmap

## 🎯 Öncelikli Görevler (Sıralı)

### 1. User CRUD İşlemleri Test ✅ Hazır, Test Edilecek
**Endpoint'ler:**
- POST `/api/user` - Create user
- GET `/api/user` - Get users (pagination)
- GET `/api/user/{userId}` - Get user by ID
- PUT `/api/user/{userId}` - Update user
- DELETE `/api/user/{userId}` - Delete user
- POST `/api/user/{userId}/groups/{groupId}` - Add to group
- DELETE `/api/user/{userId}/groups/{groupId}` - Remove from group

**Test Senaryoları:**
- [ ] User oluşturma (domain içinde)
- [ ] User listesi (pagination, search, filter)
- [ ] User detay
- [ ] User güncelleme
- [ ] User silme
- [ ] User'ı gruba ekleme
- [ ] User'ı gruptan çıkarma
- [ ] Multi-tenant izolasyonu (farklı domain'lerde)

**Test Script:** `MngKeeper/tests/user-crud-test.ps1`

---

### 2. Group CRUD İşlemleri Test ✅ Hazır, Test Edilecek
**Endpoint'ler:**
- POST `/api/group` - Create group
- GET `/api/group` - Get groups (pagination)
- PUT `/api/group/{groupId}` - Update group
- DELETE `/api/group/{groupId}` - Delete group

**Test Senaryoları:**
- [ ] Group oluşturma
- [ ] Group listesi (pagination, search, filter)
- [ ] Group güncelleme (name, description, permissions)
- [ ] Group silme
- [ ] Multi-tenant izolasyonu

**Test Script:** `MngKeeper/tests/group-crud-test.ps1` (oluşturulacak)

---

### 3. RabbitMQ Event Publishing 🔄 Tasarım + İmplementasyon

**Amaç:** Tüm CRUD işlemlerinin event olarak yayınlanması

**Event'ler:**
```
Domain Events:
- domain.created
- domain.updated
- domain.deleted

User Events:
- user.created
- user.updated
- user.deleted
- user.group.added
- user.group.removed

Group Events:
- group.created
- group.updated
- group.deleted
```

**İmplementasyon:**
- [ ] Event model'leri oluşturma
- [ ] Domain event handler'lar
- [ ] RabbitMQ publisher entegrasyonu
- [ ] Event consumer'lar (MngReactor için)
- [ ] Event logging
- [ ] Dead letter queue
- [ ] Retry mechanism

**Teknoloji:**
- RabbitMQ (Topic Exchange)
- MediatR Notifications
- EventPublisher service

---

### 4. Dosyalama Sistemi 📁 Tasarım Gerekli

**Kararlaştırılacak Konular:**

#### A. Dosya Tipleri
- [ ] User profile photos
- [ ] Document uploads (PDF, Excel, etc.)
- [ ] Asset images
- [ ] Report files
- [ ] Log files
- [ ] Backup files

#### B. Storage Seçenekleri
1. **Local File System**
   - ✅ Basit
   - ✅ Hızlı
   - ❌ Ölçeklenebilirlik sınırlı
   - ❌ Backup karmaşık

2. **MongoDB GridFS** (Önerilen - Zaten var)
   - ✅ MongoDB ile entegre
   - ✅ Multi-tenant support (domain bazlı)
   - ✅ Metadata storage
   - ❌ Büyük dosyalar için yavaş

3. **Azure Blob Storage**
   - ✅ Sınırsız ölçeklenebilir
   - ✅ CDN desteği
   - ✅ Backup/versioning
   - ❌ Maliyet
   - ❌ External dependency

4. **MinIO (Self-hosted S3)**
   - ✅ S3 compatible
   - ✅ Self-hosted
   - ✅ Docker ile kolay
   - ❌ Ekstra container

#### C. Dosya Yapısı
```
Option 1: Domain-based
/files/{domainId}/{userId}/{fileId}.ext

Option 2: Category-based
/files/{category}/{domainId}/{fileId}.ext
  - category: profiles, documents, assets, reports

Option 3: Date-based
/files/{domainId}/{yyyy}/{MM}/{dd}/{fileId}.ext
```

#### D. Özellikler
- [ ] Upload/Download API
- [ ] Multi-part upload (büyük dosyalar)
- [ ] File metadata (name, size, type, owner)
- [ ] Access control (domain-based, user-based)
- [ ] File versioning
- [ ] Thumbnail generation (images)
- [ ] Virus scanning
- [ ] Size limits
- [ ] Quota management (per domain)

#### E. API Tasarımı
```http
POST   /api/files/upload
GET    /api/files/{fileId}
GET    /api/files/{fileId}/download
DELETE /api/files/{fileId}
GET    /api/files?domain={domainId}&userId={userId}
POST   /api/files/{fileId}/share
```

---

## 📅 Tahmini Süre

| Görev | Süre | Öncelik |
|-------|------|---------|
| User CRUD Test | 2-3 saat | 🔴 Yüksek |
| Group CRUD Test | 1-2 saat | 🔴 Yüksek |
| RabbitMQ Events | 1 gün | 🟡 Orta |
| Dosyalama Sistemi Tasarım | 2-3 saat | 🟡 Orta |
| Dosyalama Sistemi İmplementasyon | 2-3 gün | 🟢 Düşük |

---

## 🎯 Kararlar

### Dosyalama için kararlaştırılacaklar:
1. **Storage backend:** GridFS mi, MinIO mu, Azure Blob mu?
2. **Dosya kategorileri:** Hangi tip dosyalar saklanacak?
3. **Klasör yapısı:** Domain-based mi, category-based mi?
4. **File size limits:** Max dosya boyutu?
5. **Quota:** Domain başına limit?
6. **Virus scan:** Gerekli mi?
7. **Versioning:** Dosya versiyonlama olacak mı?

---

## 📝 Notlar

- User ve Group CRUD'lar zaten hazır, sadece test edilecek
- RabbitMQ publisher servisi var, sadece event handler'lar eklenecek
- Dosyalama sistemi sıfırdan tasarlanacak

**Yeni session'da bu roadmap'e göre ilerleriz!**

