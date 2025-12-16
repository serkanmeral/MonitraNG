# JWT Token Decode Script
param(
    [string]$Token
)

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "Token parametresi gerekli!" -ForegroundColor Red
    exit 1
}

$parts = $Token.Split('.')
if ($parts.Length -ne 3) {
    Write-Host "Geçersiz JWT token formatı!" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== JWT TOKEN DECODE ===" -ForegroundColor Cyan
Write-Host "`n1. HEADER:" -ForegroundColor Yellow
$header = $parts[0]
$headerPadding = 4 - ($header.Length % 4)
if ($headerPadding -ne 4) {
    $header += '=' * $headerPadding
}
$headerBytes = [Convert]::FromBase64String($header.Replace('-', '+').Replace('_', '/'))
$headerJson = [System.Text.Encoding]::UTF8.GetString($headerBytes)
Write-Host $headerJson | ConvertFrom-Json | ConvertTo-Json -Depth 10

Write-Host "`n2. PAYLOAD:" -ForegroundColor Yellow
$payload = $parts[1]
$payloadPadding = 4 - ($payload.Length % 4)
if ($payloadPadding -ne 4) {
    $payload += '=' * $payloadPadding
}
$payloadBytes = [Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
$payloadJson = [System.Text.Encoding]::UTF8.GetString($payloadBytes)
$payloadObj = $payloadJson | ConvertFrom-Json
Write-Host $payloadJson | ConvertFrom-Json | ConvertTo-Json -Depth 10

Write-Host "`n3. SIGNATURE:" -ForegroundColor Yellow
Write-Host "Signature (ilk 50 karakter): $($parts[2].Substring(0, [Math]::Min(50, $parts[2].Length)))..."

Write-Host "`n=== ÖNEMLİ CLAIM'LER ===" -ForegroundColor Cyan
if ($payloadObj.PSObject.Properties.Name -contains "user_groups") {
    Write-Host "user_groups: $($payloadObj.user_groups | ConvertTo-Json)" -ForegroundColor Green
} else {
    Write-Host "user_groups: BULUNAMADI" -ForegroundColor Red
}

if ($payloadObj.PSObject.Properties.Name -contains "isAdmin") {
    Write-Host "isAdmin: $($payloadObj.isAdmin)" -ForegroundColor Green
} elseif ($payloadObj.PSObject.Properties.Name -contains "is_admin") {
    Write-Host "is_admin: $($payloadObj.is_admin)" -ForegroundColor Green
} else {
    Write-Host "isAdmin/is_admin: BULUNAMADI" -ForegroundColor Red
}

if ($payloadObj.PSObject.Properties.Name -contains "domain_id") {
    Write-Host "domain_id: $($payloadObj.domain_id)" -ForegroundColor Green
} else {
    Write-Host "domain_id: BULUNAMADI" -ForegroundColor Red
}

if ($payloadObj.PSObject.Properties.Name -contains "domain_name") {
    Write-Host "domain_name: $($payloadObj.domain_name)" -ForegroundColor Green
} else {
    Write-Host "domain_name: BULUNAMADI" -ForegroundColor Red
}

Write-Host "`n=== JWT.IO KULLANIMI ===" -ForegroundColor Cyan
Write-Host "Token formatı doğru görünüyor (3 parça: header.payload.signature)" -ForegroundColor Green
Write-Host ""
Write-Host "Signature verification için:" -ForegroundColor Yellow
Write-Host "1. get-realm-public-key.ps1 script'ini çalıştırarak realm'in public key'ini alın" -ForegroundColor White
Write-Host "2. jwt.io'da 'Verify Signature' bölümünde public key'i yapıştırın" -ForegroundColor White
Write-Host "   veya JWKS URL kullanın (localhost için çalışmayabilir)" -ForegroundColor Gray

