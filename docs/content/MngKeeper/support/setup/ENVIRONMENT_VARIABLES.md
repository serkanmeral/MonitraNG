# MngKeeper - Environment Variables

Bu dokümantasyon MngKeeper'ın kullandığı environment değişkenlerini açıklar.

## 🎯 Kullanım

Environment değişkenleri `.env` dosyası veya sistem environment değişkenleri ile tanımlanabilir.

**.NET Configuration Naming Convention:**
```bash
# Hierarchical configuration için "__" (double underscore) kullanılır
MngKeeperSettings__Server__Host=0.0.0.0
MngKeeperSettings__Server__Port=5001
```

---

## 🖥️ Server Configuration

MngKeeper'ın çalışacağı host ve port ayarları.

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__Server__Host` | `0.0.0.0` | Host address. Options: `0.0.0.0` (all interfaces), `localhost` (local only), or specific IP |
| `MngKeeperSettings__Server__Port` | `5001` | HTTPS port |
| `MngKeeperSettings__Server__Scheme` | `https` | Protocol scheme (`https` or `http`) |
| `MngKeeperSettings__OpenApiServerPath` | `https://localhost:5001` | OpenAPI/Swagger base URL |

### Örnekler

**Development (localhost only):**
```bash
MngKeeperSettings__Server__Host=localhost
MngKeeperSettings__Server__Port=5001
MngKeeperSettings__OpenApiServerPath=https://localhost:5001
```

**Production (all interfaces):**
```bash
MngKeeperSettings__Server__Host=0.0.0.0
MngKeeperSettings__Server__Port=443
MngKeeperSettings__OpenApiServerPath=https://api.monitrang.com
```

**Docker Container:**
```bash
MngKeeperSettings__Server__Host=0.0.0.0
MngKeeperSettings__Server__Port=5001
MngKeeperSettings__OpenApiServerPath=https://mngkeeper.local:5001
```

---

## 💾 MongoDB Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__MongoDB__ConnectionString` | - | MongoDB connection string |
| `MngKeeperSettings__MongoDB__DatabaseName` | `mngkeeper` | Main database name |

### Örnek
```bash
MngKeeperSettings__MongoDB__ConnectionString=mongodb://admin:admin123@localhost:27017
MngKeeperSettings__MongoDB__DatabaseName=mngkeeper
```

---

## 🔐 Keycloak Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__Keycloak__BaseUrl` | - | Keycloak server URL |
| `MngKeeperSettings__Keycloak__AdminUsername` | - | Keycloak admin username |
| `MngKeeperSettings__Keycloak__AdminPassword` | - | Keycloak admin password |
| `MngKeeperSettings__Keycloak__ClientId` | `mng-keeper-admin` | Client ID |
| `MngKeeperSettings__Keycloak__ClientSecret` | - | Client secret |
| `MngKeeperSettings__Keycloak__DefaultAdminPassword` | `Admin123!` | Default password for domain admins |

### Örnek
```bash
MngKeeperSettings__Keycloak__BaseUrl=http://localhost:8080
MngKeeperSettings__Keycloak__AdminUsername=admin
MngKeeperSettings__Keycloak__AdminPassword=admin123
MngKeeperSettings__Keycloak__ClientSecret=MSG9QBXtnFNlPHCA8DrmTqXpQHXhZ2HK
```

---

## 🔴 Redis Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__Redis__ConnectionString` | - | Redis connection string |

### Örnek
```bash
MngKeeperSettings__Redis__ConnectionString=localhost:6379,password=redis123
```

---

## 🐰 RabbitMQ Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__RabbitMQ__Host` | `localhost` | RabbitMQ host |
| `MngKeeperSettings__RabbitMQ__Port` | `5672` | RabbitMQ port |
| `MngKeeperSettings__RabbitMQ__Username` | - | RabbitMQ username |
| `MngKeeperSettings__RabbitMQ__Password` | - | RabbitMQ password |
| `MngKeeperSettings__RabbitMQ__VirtualHost` | `/` | Virtual host |

### Örnek
```bash
MngKeeperSettings__RabbitMQ__Host=localhost
MngKeeperSettings__RabbitMQ__Port=5672
MngKeeperSettings__RabbitMQ__Username=admin
MngKeeperSettings__RabbitMQ__Password=admin123
```

---

## 📡 MQTT Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__Mqtt__BrokerHost` | `localhost` | MQTT broker host |
| `MngKeeperSettings__Mqtt__BrokerPort` | `1883` | MQTT broker port |
| `MngKeeperSettings__Mqtt__Username` | - | MQTT username |
| `MngKeeperSettings__Mqtt__Password` | - | MQTT password |
| `MngKeeperSettings__Mqtt__TopicPrefix` | `MNG` | Topic prefix |

### Örnek
```bash
MngKeeperSettings__Mqtt__BrokerHost=localhost
MngKeeperSettings__Mqtt__BrokerPort=1883
MngKeeperSettings__Mqtt__Username=monitrang
MngKeeperSettings__Mqtt__TopicPrefix=MNG
```

---

## 🗄️ MinIO Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__MinIO__Endpoint` | - | MinIO endpoint (host:port) |
| `MngKeeperSettings__MinIO__AccessKey` | - | MinIO access key |
| `MngKeeperSettings__MinIO__SecretKey` | - | MinIO secret key |
| `MngKeeperSettings__MinIO__UseSSL` | `false` | Use SSL/TLS |
| `MngKeeperSettings__MinIO__Region` | `us-east-1` | MinIO region |

### Örnek
```bash
MngKeeperSettings__MinIO__Endpoint=localhost:9090
MngKeeperSettings__MinIO__AccessKey=admin
MngKeeperSettings__MinIO__SecretKey=admin123
MngKeeperSettings__MinIO__UseSSL=false
```

---

## 🔒 Certificate Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `MngKeeperSettings__CertificateSettings__DNS` | `localhost` | DNS name for certificate |
| `MngKeeperSettings__CertificateSettings__MNG_CERT_FILE` | - | Certificate file path |
| `MngKeeperSettings__CertificateSettings__MNG_KEY_FILE` | - | Private key file path |
| `MngKeeperSettings__CertificateSettings__MNG_CERT_FILE_CONTENT` | - | Certificate content (base64) |
| `MngKeeperSettings__CertificateSettings__MNG_KEY_FILE_CONTENT` | - | Private key content (base64) |

### Örnek (File-based)
```bash
MngKeeperSettings__CertificateSettings__DNS=localhost
MngKeeperSettings__CertificateSettings__MNG_CERT_FILE=/app/certs/cert.crt
MngKeeperSettings__CertificateSettings__MNG_KEY_FILE=/app/certs/cert.key
```

### Örnek (Content-based - for containers)
```bash
MngKeeperSettings__CertificateSettings__DNS=monitrang.com
MngKeeperSettings__CertificateSettings__MNG_CERT_FILE_CONTENT=LS0tLS1CRUdJTi...
MngKeeperSettings__CertificateSettings__MNG_KEY_FILE_CONTENT=LS0tLS1CRUdJTi...
```

---

## 📝 Logging Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `Serilog__MinimumLevel__Default` | `Information` | Minimum log level |
| `Seq__ServerUrl` | `http://localhost:5341` | Seq server URL |

### Örnek
```bash
Serilog__MinimumLevel__Default=Information
Seq__ServerUrl=http://localhost:5341
```

---

## 🌍 ASP.NET Core Environment

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | Environment name |

### Örnek
```bash
# Development
ASPNETCORE_ENVIRONMENT=Development

# Production
ASPNETCORE_ENVIRONMENT=Production
```

---

## 💻 Kullanım Örnekleri

### 1. PowerShell ile Environment Variable Set Etme

```powershell
# Server configuration
$env:MngKeeperSettings__Server__Host = "0.0.0.0"
$env:MngKeeperSettings__Server__Port = "8443"
$env:MngKeeperSettings__OpenApiServerPath = "https://api.monitrang.com:8443"

# Run application
dotnet run --project Presentation/MngKeeper.Api
```

### 2. Linux/macOS ile Export

```bash
# Server configuration
export MngKeeperSettings__Server__Host=0.0.0.0
export MngKeeperSettings__Server__Port=8443
export MngKeeperSettings__OpenApiServerPath=https://api.monitrang.com:8443

# Run application
dotnet run --project Presentation/MngKeeper.Api
```

### 3. Docker Compose ile

```yaml
version: '3.8'

services:
  mngkeeper:
    image: mngkeeper:latest
    environment:
      - MngKeeperSettings__Server__Host=0.0.0.0
      - MngKeeperSettings__Server__Port=5001
      - MngKeeperSettings__OpenApiServerPath=https://mngkeeper:5001
      - MngKeeperSettings__MongoDB__ConnectionString=mongodb://admin:admin123@mongodb:27017
      - MngKeeperSettings__Keycloak__BaseUrl=http://keycloak:8080
      - MngKeeperSettings__Redis__ConnectionString=redis:6379,password=redis123
      - MngKeeperSettings__RabbitMQ__Host=rabbitmq
    ports:
      - "5001:5001"
```

### 4. .env Dosyası ile (.NET 6+)

**.env** dosyası oluşturun:

```bash
MngKeeperSettings__Server__Host=0.0.0.0
MngKeeperSettings__Server__Port=5001
MngKeeperSettings__Server__Scheme=https
MngKeeperSettings__OpenApiServerPath=https://localhost:5001
MngKeeperSettings__MongoDB__ConnectionString=mongodb://admin:admin123@localhost:27017
MngKeeperSettings__Keycloak__BaseUrl=http://localhost:8080
```

Program.cs'de yükleyin:

```csharp
builder.Configuration.AddEnvironmentVariables();
```

---

## 🎯 Öncelik Sırası

Configuration değerleri şu sırayla yüklenir (sonraki üsttekini override eder):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment Variables
4. Command-line arguments

**Örnek:**

```bash
# appsettings.json'da Port=5001
# Environment variable ile override et:
export MngKeeperSettings__Server__Port=8443

# Şimdi port 8443 olarak kullanılır
```

---

## ✅ Quick Start - Minimum Configuration

Development için minimum gerekli environment değişkenleri:

```bash
# Server
MngKeeperSettings__Server__Host=0.0.0.0
MngKeeperSettings__Server__Port=5001

# MongoDB
MngKeeperSettings__MongoDB__ConnectionString=mongodb://admin:admin123@localhost:27017

# Keycloak
MngKeeperSettings__Keycloak__BaseUrl=http://localhost:8080
MngKeeperSettings__Keycloak__AdminUsername=admin
MngKeeperSettings__Keycloak__AdminPassword=admin123
MngKeeperSettings__Keycloak__ClientSecret=your-client-secret

# Redis
MngKeeperSettings__Redis__ConnectionString=localhost:6379,password=redis123

# RabbitMQ
MngKeeperSettings__RabbitMQ__Host=localhost
MngKeeperSettings__RabbitMQ__Username=admin
MngKeeperSettings__RabbitMQ__Password=admin123

# MinIO
MngKeeperSettings__MinIO__Endpoint=localhost:9090
MngKeeperSettings__MinIO__AccessKey=admin
MngKeeperSettings__MinIO__SecretKey=admin123
```

**Diğer tüm ayarlar appsettings.json'dan alınır.**

---

## 🔍 Troubleshooting

### Port değişikliği uygulanmıyor

1. Environment variable doğru set edilmiş mi kontrol edin:
   ```bash
   # PowerShell
   $env:MngKeeperSettings__Server__Port
   
   # Linux/macOS
   echo $MngKeeperSettings__Server__Port
   ```

2. Naming convention doğru mu? (Double underscore `__` kullanılmalı)

3. Application restart edildi mi?

### OpenApiServerPath Swagger'da görünmüyor

OpenApiServerPath'i güncelledikten sonra:

1. Application'ı restart edin
2. Browser cache'i temizleyin
3. Swagger UI'ı yenileyin (Ctrl+F5)

---

## 📚 İlgili Dokümantasyon

- [README.md](README.md) - Genel kullanım kılavuzu
- [appsettings.json](Presentation/MngKeeper.Api/appsettings.json) - Development settings
- [appsettings.Production.template.json](Presentation/MngKeeper.Api/appsettings.Production.template.json) - Production template

---

**Son Güncelleme:** 2025-11-06  
**Version:** 1.0.0

