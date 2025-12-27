# MngKeeper Gateway - Sorun Giderme Rehberi

## MngKeeper'ın Çalıştığını Kontrol Etme

### 1. Docker Container Durumu

```powershell
# Container durumunu kontrol et
cd ApplicationResources/mng_apps
docker-compose ps mngkeeper

# Container loglarını kontrol et
docker logs mngkeeper --tail 50
```

**Beklenen Durum:**
- Status: `Up` ve `healthy`
- Ports: `0.0.0.0:5001->5001/tcp`

### 2. Direkt API Erişimi

```powershell
# Version endpoint'i ile test
Invoke-RestMethod -Uri "https://localhost:5001/api/version/short" `
    -Method Get `
    -SkipCertificateCheck

# Token alma ile test
$tokenResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        username = "serkan.meral"
        password = "Serkan123!"
        domain = "meral"
    } | ConvertTo-Json) `
    -SkipCertificateCheck
```

**Beklenen Sonuç:**
- Version endpoint'i versiyon string'i döndürmeli
- Token endpoint'i `accessToken` içeren bir response döndürmeli

## Gateway Üzerinden Erişim

### 1. Gateway Container Durumu

```powershell
# Gateway container durumunu kontrol et
docker-compose ps mnggateway

# Gateway loglarını kontrol et
docker logs mnggateway --tail 50
```

### 2. Gateway Üzerinden Token Alma

```powershell
# Gateway üzerinden token alma
$tokenResponse = Invoke-RestMethod -Uri "http://localhost:5040/keeper/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        username = "serkan.meral"
        password = "Serkan123!"
        domain = "meral"
    } | ConvertTo-Json) `
    -SkipCertificateCheck

$token = $tokenResponse.accessToken
```

**Beklenen Sonuç:**
- `accessToken` içeren bir response döndürmeli

### 3. Gateway Üzerinden API Erişimi

```powershell
# Token ile API erişimi
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$domains = Invoke-RestMethod -Uri "http://localhost:5040/keeper/api/domain" `
    -Method Get `
    -Headers $headers `
    -SkipCertificateCheck
```

## Yaygın Sorunlar ve Çözümleri

### Sorun 1: MngKeeper Container Çalışmıyor

**Belirtiler:**
- `docker-compose ps mngkeeper` komutu container'ı göstermiyor veya `Exited` durumunda

**Çözüm:**
```powershell
# Container'ı başlat
docker-compose up -d mngkeeper

# Logları kontrol et
docker logs mngkeeper --tail 100
```

### Sorun 2: Gateway'den MngKeeper'a Bağlanılamıyor (502 Bad Gateway)

**Belirtiler:**
- Gateway loglarında "Error connecting to downstream service" hatası
- 502 Bad Gateway hatası

**Çözüm:**
1. MngKeeper'ın HTTPS üzerinden çalıştığını kontrol et
2. Ocelot.json'da `DownstreamScheme`'in `https` olduğundan emin ol
3. `DangerousAcceptAnyServerCertificateValidator: true` eklendiğinden emin ol

```powershell
# Gateway'i yeniden build et
cd ApplicationResources/mng_apps
docker-compose build mnggateway
docker-compose up -d mnggateway
```

### Sorun 3: Auth Endpoint Gateway Üzerinden Çalışmıyor (401 Unauthorized)

**Belirtiler:**
- `/keeper/api/auth/token` endpoint'i 401 hatası veriyor
- Gateway loglarında "authenticated route" uyarısı

**Çözüm:**
1. Ocelot.json'da auth route'unun `Priority: 2` olduğundan emin ol
2. Auth route'unda `AuthenticationOptions` olmadığından emin ol
3. Gateway'i yeniden build et ve başlat

```powershell
# Ocelot.json'u kontrol et
docker exec mnggateway cat /app/ocelot.json | ConvertFrom-Json | 
    Select-Object -ExpandProperty Routes | 
    Where-Object { $_.UpstreamPathTemplate -like "*auth*" }

# Gateway'i yeniden build et
docker-compose build mnggateway
docker-compose up -d mnggateway
```

### Sorun 4: Token Alındı Ama Diğer Endpoint'ler 401 Veriyor

**Belirtiler:**
- Token başarıyla alınıyor
- Diğer endpoint'ler 401 hatası veriyor

**Çözüm:**
1. Token'ın doğru gönderildiğinden emin ol:
```powershell
$headers = @{
    "Authorization" = "Bearer $token"  # "Bearer " öneki önemli!
    "Content-Type" = "application/json"
}
```

2. Gateway'in JWT validation'ının çalıştığını kontrol et:
```powershell
docker logs mnggateway | Select-String -Pattern "JWT|Bearer|token" -Context 2
```

### Sorun 5: Docker Image Güncellenmiyor

**Belirtiler:**
- Kod değişiklikleri container'a yansımıyor
- Ocelot.json değişiklikleri container'da görünmüyor

**Çözüm:**
```powershell
# Image'ı yeniden build et (cache olmadan)
docker-compose build --no-cache mnggateway

# Container'ı yeniden oluştur
docker-compose up -d --force-recreate mnggateway
```

## Test Scriptleri

### Gateway Routing Testi

```powershell
cd scripts/tests/MngGateway
.\test-gateway-keeper.ps1
```

Bu script:
1. Gateway health check yapar
2. Gateway üzerinden domain listesi alır
3. Gateway üzerinden user listesi alır
4. Gateway üzerinden group listesi alır

## Docker Compose Komutları

### Tüm Servisleri Başlatma

```powershell
cd ApplicationResources/mng_apps
docker-compose up -d
```

### Sadece MngKeeper ve Gateway'i Başlatma

```powershell
docker-compose up -d mngkeeper mnggateway
```

### Servisleri Yeniden Başlatma

```powershell
docker-compose restart mngkeeper mnggateway
```

### Servisleri Durdurma

```powershell
docker-compose stop mngkeeper mnggateway
```

### Logları İzleme

```powershell
# MngKeeper logları
docker-compose logs -f mngkeeper

# Gateway logları
docker-compose logs -f mnggateway

# Her ikisini birlikte
docker-compose logs -f mngkeeper mnggateway
```

## Network Kontrolü

### Container'ların Aynı Network'te Olduğunu Kontrol Etme

```powershell
# Network bilgilerini görüntüle
docker network inspect mng_common_mng_network | 
    ConvertFrom-Json | 
    Select-Object -ExpandProperty Containers | 
    Format-Table -AutoSize
```

**Beklenen:**
- `mngkeeper` ve `mnggateway` container'ları aynı network'te olmalı

### Container İçinden Bağlantı Testi

```powershell
# Gateway'den MngKeeper'a bağlantı testi
docker exec mnggateway curl -k https://mngkeeper:5001/api/version/short
```

**Beklenen:**
- Version string döndürmeli (hata olmamalı)

## Postman Testleri

### Direkt MngKeeper

- **URL**: `https://localhost:5001/api/auth/token`
- **Method**: POST
- **Body**: 
```json
{
    "username": "serkan.meral",
    "password": "Serkan123!",
    "domain": "meral"
}
```

### Gateway Üzerinden

- **URL**: `http://localhost:5040/keeper/api/auth/token`
- **Method**: POST
- **Body**: Aynı (yukarıdaki gibi)

**Not:** Gateway üzerinden erişimde SSL sertifika hatası olmaz (HTTP kullanılıyor).

