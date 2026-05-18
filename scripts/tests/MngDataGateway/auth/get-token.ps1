# Common Token Retrieval Script
# This script retrieves an authentication token from MngKeeper and saves it to a common location
#
# Kullanım:
#   .\get-token.ps1
#   .\get-token.ps1 -DomainName "meral" -Username "meral_admin" -Password "Sifreniz"
# Gateway arkasında Keeper: https://localhost:5040/keeper/api/auth/token
# Keeper direkt:            https://localhost:5001/api/auth/token (-KeeperBaseUrl "https://localhost:5001" -KeeperPath "/api/auth/token")

param(
    [string]$KeeperBaseUrl = "https://localhost:5040",
    [string]$KeeperPath = "/keeper/api/auth/token",   # Gateway: /keeper/api/auth/token | Keeper direkt: /api/auth/token
    [string]$DomainName = "meral",
    [string]$Username = "meral_admin",
    [string]$Password = "Admin123!",
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# SSL/TLS: self-signed sertifika kabul (localhost icin)
# Oncelik: TLS 1.2 zorla; sonra sertifika dogrulamasini atla
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
} catch { }

# Sertifika dogrulamasini atla (localhost self-signed icin)
try {
    add-type -TypeDefinition @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@ -ErrorAction Stop
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
} catch {
    # .NET Core / PowerShell 7'de CertificatePolicy yok; sadece callback kullan
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

$tokenUri = "$KeeperBaseUrl$KeeperPath"
Write-Host ""
Write-Host "Token alma islemi basliyor..." -ForegroundColor Cyan
Write-Host "   URI: $tokenUri"
Write-Host "   Domain: $DomainName"
Write-Host "   Username: $Username"
Write-Host ""

$requestBody = @{
    username = $Username
    password = $Password
    domain   = $DomainName
} | ConvertTo-Json

# HTTPS + curl.exe varsa: curl -k ile SSL atla (PowerShell/.NET SSL sorununa karsi guvenilir)
$useCurl = $tokenUri.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
if ($useCurl) {
    try {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        $requestBody | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
        $responseJson = & curl.exe -s -k -X POST -H "Content-Type: application/json" -d "@$bodyFile" $tokenUri 2>&1 | Out-String
        Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        $responseJson = $responseJson.Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($responseJson)) {
            throw "curl basarisiz veya bos yanit (exit: $LASTEXITCODE)"
        }
        $tokenResponse = $responseJson | ConvertFrom-Json
        $accessToken = $tokenResponse.accessToken
        if ([string]::IsNullOrEmpty($accessToken)) {
            throw "Yanitda accessToken yok"
        }
        $accessToken | Out-File -FilePath $TokenFile -Encoding utf8 -NoNewline
        Write-Host "Basarili: Token alindi (curl ile) ve kaydedildi." -ForegroundColor Green
        Write-Host "   Dosya: $TokenFile"
        return $accessToken
    } catch {
        Write-Host "curl denemesi basarisiz, .NET ile deneniyor..." -ForegroundColor Yellow
    }
}

try {
    # .NET: HttpWebRequest (ServicePointManager SSL callback)
    $req = [System.Net.HttpWebRequest]::Create($tokenUri)
    $req.Method = "POST"
    $req.ContentType = "application/json"
    $req.Accept = "application/json"
    $req.Timeout = 15000
    $req.ReadWriteTimeout = 15000
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($requestBody)
    $req.ContentLength = $bodyBytes.Length
    $reqStream = $req.GetRequestStream()
    $reqStream.Write($bodyBytes, 0, $bodyBytes.Length)
    $reqStream.Close()

    $resp = $req.GetResponse()
    $respStream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($respStream)
    $responseJson = $reader.ReadToEnd()
    $reader.Close()
    $respStream.Close()
    $resp.Close()

    $tokenResponse = $responseJson | ConvertFrom-Json
    $accessToken = $tokenResponse.accessToken

    if ([string]::IsNullOrEmpty($accessToken)) {
        Write-Host "HATA: Yanitda accessToken yok. Yanit: $responseJson" -ForegroundColor Red
        exit 1
    }

    $accessToken | Out-File -FilePath $TokenFile -Encoding utf8 -NoNewline
    Write-Host "Basarili: Token alindi ve kaydedildi." -ForegroundColor Green
    Write-Host "   Dosya: $TokenFile"
    return $accessToken

} catch {
    Write-Host "HATA: Token alinamadi!" -ForegroundColor Red
    Write-Host "   $($_.Exception.Message)"
    $statusCode = $null
    try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }
    if ($statusCode) {
        Write-Host "   HTTP: $statusCode"
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
                $reader.Close()
                if ($body) { Write-Host "   Yanit: $body" }
            }
        } catch { }
    }
    Write-Host ""
    Write-Host "Oneriler:" -ForegroundColor Yellow
    if ($_.Exception.Message -match "SSL|certificate|could not be established") {
        Write-Host "  - SSL hatasi: PowerShell 7 dene (SkipCertificateCheck): pwsh .\get-token.ps1"
        Write-Host "  - Veya sunucuda HTTP aciksa (sadece dev): -KeeperBaseUrl ""http://localhost:PORT"""
    }
    Write-Host "  - Keeper veya Gateway calisiyor mu? (Or: https://localhost:5040 veya 5001)"
    Write-Host "  - Domain / kullanici / sifre dogru mu? Ornek: .\get-token.ps1 -DomainName meral -Username meral_admin -Password Sifreniz"
    Write-Host "  - Keeper dogrudan calisiyorsa: .\get-token.ps1 -KeeperBaseUrl ""https://localhost:5001"" -KeeperPath ""/api/auth/token"""
    Write-Host ""
    exit 1
}
