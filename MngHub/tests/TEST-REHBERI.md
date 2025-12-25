# MngHub SignalR Test Rehberi

## 🎯 Test Senaryosu

MngKeeper'da domain ve kullanıcı oluşturulduğunda, bu event'lerin RabbitMQ üzerinden MngHub'a gelmesi ve SignalR ile uygun room'lara gönderilmesi.

---

## 📋 Ön Gereksinimler

1. **MngKeeper** çalışıyor olmalı (Docker veya local)
2. **MngHub** çalışıyor olmalı (`http://localhost:5020`)
3. **RabbitMQ** çalışıyor olmalı (`localhost:5672`)
4. **Node-RED** çalışıyor olmalı (`http://localhost:1880`)

---

## 🔧 Adım 1: Servisleri Kontrol Edin

### MngHub Kontrolü
```powershell
curl http://localhost:5020/health
```
**Beklenen:** `{"status":"healthy","service":"MngHub",...}`

### MngKeeper Kontrolü
```powershell
curl -k https://localhost:5001/health
```
**Beklenen:** `{"status":"healthy",...}`

---

## 🔑 Adım 2: JWT Token Alın

### Test Domain için Token

```powershell
$baseUrl = "https://localhost:5001"
$domainName = "test-domain-20251223030900"
$username = "test-domain-20251223030900_admin"
$password = "AdminPass123!"

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$tokenBody = @{
    username = $username
    password = $password
    domain = $domainName
} | ConvertTo-Json

$tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
    -Method POST `
    -Body $tokenBody `
    -ContentType "application/json" `
    -SkipCertificateCheck

Write-Host "Token: $($tokenResponse.accessToken)"
```

**Alternatif:** Yeni bir domain oluşturup token alın:
```powershell
cd MngHub/tests
pwsh -ExecutionPolicy Bypass -File test-signalr-events.ps1
```

---

## 📥 Adım 3: Node-RED Flow'unu İçe Aktarın

1. Node-RED'i açın: **http://localhost:1880**

2. Flow'u içe aktarın:
   - Sağ üst köşedeki **menü** (☰) → **Import**
   - `MngHub/tests/node-red-signalr-simple-flow.json` dosyasını açın
   - İçeriği kopyalayıp Node-RED'e yapıştırın
   - **Import** butonuna tıklayın

3. Token'ı güncelleyin:
   - **"MngHub SignalR"** websocket in node'una çift tıklayın
   - **Client** alanındaki token'ı Adım 2'de aldığınız güncel token ile değiştirin
   - **Done** butonuna tıklayın

4. Deploy edin:
   - Sağ üstteki **Deploy** (kırmızı) butonuna tıklayın

---

## 🧪 Adım 4: Test Senaryoları

### Test 1: Domain Oluşturma Event'i

1. **MngKeeper'da yeni bir domain oluşturun:**
   ```powershell
   $domainBody = @{
       domainName = "test-domain-$(Get-Date -Format 'yyyyMMddHHmmss')"
       displayName = "Test Domain"
       adminEmail = "admin@test.com"
       adminPassword = "AdminPass123!"
       settings = @{
           maxUsers = 100
           maxAssets = 1000
           enableMqtt = $false
       }
   } | ConvertTo-Json

   Invoke-RestMethod -Uri "https://localhost:5001/api/domain" `
       -Method POST `
       -Body $domainBody `
       -ContentType "application/json" `
       -SkipCertificateCheck
   ```

2. **Node-RED Debug panelinde kontrol edin:**
   - **System Events** debug node'unda mesaj görmelisiniz
   - Routing Key: `system.mngkeeper.domain.created`
   - Room: `global` (tüm bağlı kullanıcılar görebilir)

### Test 2: Kullanıcı Oluşturma Event'i

1. **Token alın** (yukarıdaki domain için):
   ```powershell
   # Domain oluşturduktan sonra admin token alın
   $tokenBody = @{
       username = "{domainName}_admin"
       password = "AdminPass123!"
       domain = "{domainName}"
   } | ConvertTo-Json

   $tokenResponse = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
       -Method POST `
       -Body $tokenBody `
       -ContentType "application/json" `
       -SkipCertificateCheck

   $token = $tokenResponse.accessToken
   ```

2. **Kullanıcı oluşturun:**
   ```powershell
   $headers = @{
       "Authorization" = "Bearer $token"
       "Content-Type" = "application/json"
   }

   $userBody = @{
       username = "testuser"
       email = "testuser@test.com"
       password = "TestPass123!"
       firstName = "Test"
       lastName = "User"
       groupIds = @()
       isActive = $true
   } | ConvertTo-Json

   Invoke-RestMethod -Uri "https://localhost:5001/api/user" `
       -Method POST `
       -Headers $headers `
       -Body $userBody `
       -SkipCertificateCheck
   ```

3. **Node-RED Debug panelinde kontrol edin:**
   - **Domain Events** debug node'unda mesaj görmelisiniz
   - Routing Key: `{domainId}.usercreatedevent`
   - Room: `domain.{domainName}` (sadece o domain'deki kullanıcılar görebilir)

---

## ✅ Beklenen Sonuçlar

### Domain Created Event
- **Routing Key:** `system.mngkeeper.domain.created`
- **Exchange:** `mng.topics`
- **Room:** `global`
- **Debug Node:** System Events

### User Created Event
- **Routing Key:** `{domainId}.usercreatedevent` (örn: `6949dd9c9157b3751eb31d7a.usercreatedevent`)
- **Exchange:** `mngkeeper.events`
- **Room:** `domain.{domainName}` (örn: `domain.test-domain-20251223030900`)
- **Debug Node:** Domain Events

---

## 🔍 Debug Node'ları

Node-RED Debug panelinde (sağ tarafta) şu mesajları görmelisiniz:

1. **Raw SignalR** - Ham SignalR mesajları
2. **Parsed Message** - Parse edilmiş mesajlar
3. **System Events** - System routing key'li mesajlar (domain.created)
4. **Domain Events** - Domain routing key'li mesajlar (user.created)
5. **Global Events** - Global routing key'li mesajlar
6. **Other Events** - Diğer mesajlar

---

## 🐛 Sorun Giderme

### Mesaj Görünmüyor

1. **MngHub loglarını kontrol edin:**
   - MngHub console'unda connection ve message loglarını kontrol edin

2. **RabbitMQ bağlantısını kontrol edin:**
   - RabbitMQ Management UI: `http://localhost:15672`
   - Exchange'lerin oluşturulduğunu kontrol edin: `mng.topics`, `mngkeeper.events`

3. **Token'ın geçerli olduğundan emin olun:**
   - Token süresi dolmuş olabilir, yeni token alın

4. **WebSocket bağlantısını kontrol edin:**
   - Node-RED'de websocket in node'unun durumunu kontrol edin
   - Bağlantı hatası varsa, token'ı ve URL'yi kontrol edin

### Bağlantı Hatası

- MngHub'un çalıştığından emin olun: `http://localhost:5020/health`
- WebSocket URL formatını kontrol edin: `ws://localhost:5020/ws?access_token=TOKEN`
- Token'ın geçerli olduğundan emin olun

### Parse Hatası

- Raw SignalR Debug node'una bakın
- Mesaj formatını kontrol edin
- Function node'undaki parse logic'ini inceleyin

---

## 📝 Test Scripti

Hızlı test için:
```powershell
cd MngHub/tests
pwsh -ExecutionPolicy Bypass -File test-signalr-events.ps1
```

Bu script:
1. Domain oluşturur
2. Kullanıcı oluşturur
3. Token alır
4. Test bilgilerini gösterir

---

## 🎉 Başarılı Test

Test başarılı olduğunda:
- ✅ Domain created event'i **System Events** debug node'unda görünür
- ✅ User created event'i **Domain Events** debug node'unda görünür
- ✅ Mesajlar parse edilmiş format'ta görünür
- ✅ Routing key'lere göre doğru debug node'larına yönlendirilir

