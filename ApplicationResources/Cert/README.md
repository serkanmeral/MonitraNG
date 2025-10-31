# SSL/TLS Sertifika Yönetimi

MonitraNG platformu için merkezi sertifika yönetimi.

## 📁 Klasör Yapısı

```
Cert/
├── dev/                    # Development sertifikaları
│   ├── monitrang.local.pfx           # Wildcard cert (.pfx)
│   ├── monitrang.local.crt           # Certificate (.crt)
│   ├── monitrang.local.key           # Private key
│   └── monitrang.local-password.txt  # Password (gitignore'da)
├── staging/                # Staging sertifikaları
│   └── staging.monitrang.com.pfx
├── production/             # Production sertifikaları
│   └── monitrang.com.pfx
└── README.md               # Bu dosya
```

## 🔐 Sertifika Türleri

### Development
- **Type:** Wildcard certificate
- **Domain:** `*.monitrang.local`
- **Covers:** 
  - `api.monitrang.local` (MngKeeper, MngReactor, MngEngine)
  - `app.monitrang.local` (Frontend)
  - `admin.monitrang.local`
- **Source:** .NET dev-certs veya mkcert
- **Validity:** 1 year

### Production
- **Type:** Wildcard certificate
- **Domain:** `*.monitrang.com`
- **Source:** Let's Encrypt
- **Auto-renewal:** Yes (Certbot)

## 🚀 Kurulum

### Development Sertifikası Oluşturma

```powershell
# Otomatik kurulum
.\setup-certificate.ps1 -Environment dev

# Manuel kurulum (.NET dev-certs)
cd ApplicationResources/Cert/dev
dotnet dev-certs https -ep monitrang.local.pfx -p "dev-password-123"
dotnet dev-certs https --trust
```

### Sertifikayı Tüm Servislerde Kullanma

Her servisin `appsettings.Development.json` dosyasında:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:5443",
        "Certificate": {
          "Path": "../../../ApplicationResources/Cert/dev/monitrang.local.pfx",
          "Password": "dev-password-123"
        }
      }
    }
  }
}
```

### Environment Variables (Önerilen)

```bash
# .env file
CERT_PATH=./ApplicationResources/Cert/dev/monitrang.local.pfx
CERT_PASSWORD=dev-password-123
ASPNETCORE_ENVIRONMENT=Development

# Kullanım
{
  "Certificate": {
    "Path": "${CERT_PATH}",
    "Password": "${CERT_PASSWORD}"
  }
}
```

## 📊 Port Planlaması

| Service | HTTP | HTTPS | Domain (Dev) |
|---------|------|-------|--------------|
| **MngKeeper** | 5001 | 5443 | api.monitrang.local/mngkeeper |
| **MngReactor** | 5002 | 5444 | api.monitrang.local/mngreactor |
| **MngEngine** | 5003 | 5445 | api.monitrang.local/mngengine |
| **Mng.Ui** | 3000 | 3443 | app.monitrang.local |

## 🔒 Güvenlik

### Development
- ✅ Self-signed certificate (trusted locally)
- ✅ Password config'de (development only)

### Production
- ✅ Let's Encrypt certificate
- ✅ Password environment variable'da
- ✅ Azure Key Vault / HashiCorp Vault (optional)
- ✅ Auto-renewal configured
- ✅ Strong password (32+ chars)

## 🔄 Yenileme

### Development
```powershell
# Manuel yenileme (yearly)
.\setup-certificate.ps1 -Environment dev -Renew
```

### Production
```bash
# Certbot auto-renewal (configured)
certbot renew --dry-run
```

## 🧪 Test

```powershell
# Sertifika bilgilerini görüntüle
openssl pkcs12 -in dev/monitrang.local.pfx -passin pass:dev-password-123 -noout -info

# HTTPS endpoint test
curl -k https://localhost:5443/health
```

## ⚠️ Güvenlik Notları

1. **ASLA production sertifika password'ünü config'e koymayın!**
2. **Development sertifikaları git'e commit edilebilir**
3. **Production sertifikaları gitignore'da olmalı**
4. **Password'ler environment variable'da olmalı**
5. **Production'da reverse proxy kullanın**

## 🔗 İlgili Dosyalar

- [setup-certificate.ps1](../setup-certificate.ps1)
- [MngKeeper appsettings](../../MngKeeper/Presentation/MngKeeper.Api/appsettings.json)
- [Docker nginx config](../nginx/)

