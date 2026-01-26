# MonitraNG Infrastructure Template

Bu repository, MonitraNG infrastructure'ını **template** olarak sağlar.

## 🎯 Ne İçerir?

### Infrastructure Services
- ✅ **MngKeeper** - Identity & Access Management (IAM)
- ✅ **MngDataGateway** - Generic Data Layer
- ✅ **MngHub** - Event Hub & Real-time Communication
- ⏳ **MngScheduler** - Task Scheduling (Planlanmış)

### Supporting Services
- ✅ **MongoDB** - NoSQL Database
- ✅ **Keycloak** - Authentication Provider
- ✅ **RabbitMQ** - Message Broker
- ✅ **Redis** - Cache & Session Store
- ✅ **MinIO** - Object Storage (S3-compatible)
- ✅ **Seq** - Log Aggregation

### DevOps
- ✅ **Docker Compose** - Infrastructure services
- ✅ **CI/CD Templates** - GitHub Actions workflows
- ✅ **Deployment Scripts** - Production deployment
- ✅ **MkDocs** - Documentation system

---

## 🚀 Yeni Proje Oluşturma

### 1. Template'den Yeni Repository Oluştur

**GitHub'da:**
1. Bu repository'de "Use this template" butonuna tıklayın
2. Yeni repository adı girin (örn: `MyNewProject`)
3. "Create repository from template" butonuna tıklayın

**Veya manuel:**

```bash
# Clone
git clone https://github.com/serkanmeral/MonitraNG-Infrastructure-Template.git MyNewProject
cd MyNewProject

# Yeni repository oluştur (GitHub'da)
# Sonra:
git remote remove origin
git remote add origin https://github.com/serkanmeral/MyNewProject.git
git push -u origin main
```

### 2. Proje İsmini Değiştir

**PowerShell Script (Windows):**

```powershell
# Proje ismini değiştir
$oldName = "MonitraNG"
$newName = "MyNewProject"

# Tüm dosyalarda değiştir
Get-ChildItem -Recurse -File | ForEach-Object {
    (Get-Content $_.FullName) -replace $oldName, $newName | Set-Content $_.FullName
}

# Namespace'leri değiştir
Get-ChildItem -Recurse -Filter "*.cs" | ForEach-Object {
    (Get-Content $_.FullName) -replace "namespace Mng", "namespace MyNew" | Set-Content $_.FullName
}
```

**Bash Script (Linux/Mac):**

```bash
# Proje ismini değiştir
OLD_NAME="MonitraNG"
NEW_NAME="MyNewProject"

# Tüm dosyalarda değiştir
find . -type f -name "*.cs" -o -name "*.md" -o -name "*.json" | xargs sed -i "s/$OLD_NAME/$NEW_NAME/g"

# Namespace'leri değiştir
find . -type f -name "*.cs" | xargs sed -i "s/namespace Mng/namespace MyNew/g"
```

### 3. Geliştirmeye Başla

```bash
# Infrastructure hazır, direkt business logic'e geç
# Authentication: MngKeeper kullan
# Data Layer: MngDataGateway kullan
# Events: MngHub kullan
```

---

## 📋 Infrastructure Bileşenleri

### MngKeeper (IAM)
- Multi-tenant domain yönetimi
- Kullanıcı ve grup yönetimi
- JWT token authentication
- Keycloak entegrasyonu

**Kullanım:**
```csharp
// Authentication için
POST /api/auth/token
// Domain, user, group management için
GET /api/domain
GET /api/user
GET /api/group
```

### MngDataGateway (Generic Data)
- Dynamic schema management
- Generic CRUD operations
- Query ve filtering
- Relation expansion

**Kullanım:**
```csharp
// Dataset oluştur
POST /api/datasets
// Data CRUD
POST /api/data/{dataset}
GET /api/data/{dataset}
PUT /api/data/{dataset}/{id}
DELETE /api/data/{dataset}/{id}
```

### MngHub (Event Hub)
- Real-time notifications (SignalR)
- RabbitMQ event consumption
- Domain-based routing

**Kullanım:**
```csharp
// SignalR connection
hubConnection.on("message", (message) => {
    // Handle real-time message
});
```

---

## 🛠️ Kurulum

### 1. Infrastructure Services

```bash
cd ApplicationResources/mng_common
docker compose up -d
```

**Servisler:**
- MongoDB: `localhost:27017`
- Keycloak: `localhost:8080`
- RabbitMQ: `localhost:5672`
- Redis: `localhost:6379`
- MinIO: `localhost:9090`

### 2. Application Services

```bash
cd ApplicationResources/mng_apps
docker compose up -d
```

**Servisler:**
- MngKeeper: `https://localhost:5001`
- MngDataGateway: `https://localhost:5010`
- MngHub: `http://localhost:5020`

---

## 📚 Dokümantasyon

- [Infrastructure Overview](docs/INFRASTRUCTURE_OVERVIEW.md)
- [DevOps Roadmap](docs/devops-roadmap.md)
- [Architecture Guide](MngDataGateway/docs/ARCHITECTURE_GUIDE.md)

---

## 🔄 Template Güncellemeleri

Template'e yeni özellik eklendiğinde:

```bash
# Yeni projeler template'den oluşturulduğu için
# Otomatik olarak güncel template'i kullanır

# Mevcut projeler için:
# Template'den güncellemeleri manuel olarak merge edin
```

---

## 📝 Notlar

- Infrastructure servisleri generic ve reusable
- Business logic eklemek için yeni servisler oluşturun
- MngKeeper, MngDataGateway, MngHub'ı kullanın
- Clean Architecture pattern'ini takip edin

---

## 🤝 Katkıda Bulunma

Template'e katkıda bulunmak için:
1. Infrastructure geliştirmeleri yapın
2. Template repository'ye pull request gönderin
3. Template güncellendiğinde tüm projeler faydalanır

---

**Template Version:** 1.0.0
**Last Updated:** 2025-01-XX

