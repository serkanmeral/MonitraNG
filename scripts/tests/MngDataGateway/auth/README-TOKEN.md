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
- `-KeeperBaseUrl`: MngKeeper URL'i (varsayılan: `https://localhost:5001`)
- `-DomainName`: Domain adı (varsayılan: `meral`)
- `-Username`: Kullanıcı adı (varsayılan: `serkan.meral`)
- `-Password`: Şifre (varsayılan: `Serkan123!`)
- `-TokenFile`: Token dosyası yolu (varsayılan: `$env:TEMP\serkan_token.txt`)

**Örnek:**
```powershell
.\get-token.ps1 -DomainName "meral" -Username "serkan.meral" -Password "Serkan123!"
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

