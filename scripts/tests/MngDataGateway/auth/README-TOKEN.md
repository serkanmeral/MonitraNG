# Token Yönetimi

Bu klasörde token yönetimi için ortak scriptler bulunmaktadır.

## Scriptler

### `get-token.ps1`
Yeni bir token alır ve `$env:TEMP\serkan_token.txt` dosyasına kaydeder.

**Kullanım:**
```powershell
.\get-token.ps1
```

**Parametreler:**
- `-KeeperBaseUrl`: Gateway veya Keeper URL (varsayılan: `https://localhost:5040`)
- `-KeeperPath`: Token endpoint yolu. Gateway: `/keeper/api/auth/token`, Keeper direkt: `/api/auth/token`
- `-DomainName`: Domain adı (varsayılan: `meral`)
- `-Username`: Kullanıcı adı (varsayılan: `meral_admin`)
- `-Password`: Şifre (varsayılan: `Admin123!`)
- `-TokenFile`: Token dosyası yolu (varsayılan: `$env:TEMP\serkan_token.txt`)

**Örnekler:**
```powershell
# Varsayılan (Gateway 5040, domain meral, meral_admin)
.\get-token.ps1

# Kendi domain/kullanıcı/şifren
.\get-token.ps1 -DomainName "meral" -Username "meral_admin" -Password "Sifreniz"

# Keeper doğrudan 5001'de çalışıyorsa (Gateway yok)
.\get-token.ps1 -KeeperBaseUrl "https://localhost:5001" -KeeperPath "/api/auth/token"
```

### `load-token.ps1`
Mevcut token'ı dosyadan yükler veya yoksa yeni token alır.

**Kullanım:**
```powershell
$token = .\load-token.ps1
```

**Parametreler:**
- `-TokenFile`: Token dosyası yolu (varsayılan: `$env:TEMP\serkan_token.txt`)
- `-AutoRefresh`: Token yoksa otomatik olarak yeni token al (varsayılan: `$false`)

**Örnek:**
```powershell
# Token'ı yükle
$token = .\load-token.ps1

# Headers oluştur
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}
```

## Test Scriptlerinde Kullanım

Tüm test scriptlerinde token yükleme işlemi şu şekilde yapılmalıdır:

```powershell
# Token'ı yükle (ortak script kullanarak)
$scriptPath = Split-Path -Parent $MyInvocation.PSCommandPath
$loadTokenScript = Join-Path $scriptPath "load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı!" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}
```

## 401 Hatası Durumunda

Eğer 401 (Unauthorized) hatası alınırsa, token'ı yenilemek için:

```powershell
$scriptPath = Split-Path -Parent $MyInvocation.PSCommandPath
$getTokenScript = Join-Path $scriptPath "get-token.ps1"
if (Test-Path $getTokenScript) {
    $newToken = & $getTokenScript
    if (-not [string]::IsNullOrEmpty($newToken)) {
        $headers["Authorization"] = "Bearer $newToken"
        # İşlemi tekrar dene
    }
}
```

## Notlar

- Token dosyası `$env:TEMP\serkan_token.txt` konumunda saklanır
- Token'lar belirli bir süre sonra expire olur, bu durumda yeni token alınmalıdır
- Tüm test scriptleri bu ortak scriptleri kullanmalıdır

## Token alınamıyorsa (sorun giderme)

1. **Keeper / Gateway çalışıyor mu?**  
   Tarayıcıdan veya `Invoke-WebRequest` ile `https://localhost:5040` (veya 5001) açılıyor mu kontrol edin.

2. **Doğru URL ve path:**  
   - Gateway üzerinden Keeper kullanıyorsanız: `https://localhost:5040` + path `/keeper/api/auth/token` (get-token.ps1 varsayılanı).  
   - Keeper doğrudan çalışıyorsa: `.\get-token.ps1 -KeeperBaseUrl "https://localhost:5001" -KeeperPath "/api/auth/token"`

3. **Domain / kullanıcı / şifre:**  
   MngKeeper’da tanımlı domain, kullanıcı adı ve şifreyi kullanın. Parametreyle verin:  
   `.\get-token.ps1 -DomainName "DOMAIN" -Username "USER" -Password "PASS"`

4. **SSL sertifika hatası (self-signed):**  
   Script sertifika doğrulamasını devre dışı bırakıyor. Hâlâ hata alıyorsanız PowerShell 7+ kullanmayı deneyin: `pwsh .\get-token.ps1` (SkipCertificateCheck kullanılır).

5. **Bağlantı reddedildi / timeout:**  
   Firewall veya servis dinleme portu (5040/5001) kontrol edin; gerekirse Keeper/Gateway’i başlatın.

