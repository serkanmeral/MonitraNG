# GeoServer tr_rail:railways katmanına "çizgi" stilini uygular (kırmızı nokta yerine siyah-beyaz kesik çizgi).
# Kullanım: .\apply-railways-line-style.ps1 [-BaseUrl "http://localhost:8082/geoserver"] [-User "admin"] [-Password "geoserver"]

param(
    [string]$BaseUrl = "http://localhost:8082/geoserver",
    [string]$User = "admin",
    [string]$Password = "geoserver"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sldPath = Join-Path $scriptDir "styles\railways_line.sld"
if (-not (Test-Path $sldPath)) {
    Write-Error "SLD dosyasi bulunamadi: $sldPath"
}

$rest = "$BaseUrl/rest"
$cred = [System.Management.Automation.PSCredential]::new($User, (ConvertTo-SecureString $Password -AsPlainText -Force))
$sldContent = Get-Content $sldPath -Raw -Encoding UTF8
$headers = @{ "Content-Type" = "application/vnd.ogc.sld+xml" }

Write-Host "Stil railways_line yukleniyor..."
$styleUri = "$rest/workspaces/tr_rail/styles/railways_line"
$updated = $false
try {
    Invoke-RestMethod -Uri $styleUri -Method Get -Credential $cred -AllowUnencryptedAuthentication -ErrorAction Stop | Out-Null
    Invoke-RestMethod -Uri $styleUri -Method Put -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
    Write-Host "  stil guncellendi."
    $updated = $true
} catch {
    try {
        $createUri = "$rest/workspaces/tr_rail/styles?name=railways_line"
        Invoke-RestMethod -Uri $createUri -Method Post -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
        Write-Host "  stil olusturuldu."
        $updated = $true
    } catch {
        Invoke-RestMethod -Uri $styleUri -Method Put -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
        Write-Host "  stil guncellendi (zaten vardi)."
        $updated = $true
    }
}
if (-not $updated) { throw "Stil yuklenemedi." }

Write-Host "railways layer varsayilan stili ataniyor..."
$layerBody = '{"layer":{"defaultStyle":{"name":"railways_line","workspace":{"name":"tr_rail"}}}}'
Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/layers/railways" -Method Put -Credential $cred -Body $layerBody -ContentType "application/json" -AllowUnencryptedAuthentication | Out-Null
Write-Host "  railways varsayilan stil: tr_rail:railways_line"

Write-Host "Tamamlandi. Tile Caching -> Tile Layers -> tr_rail:railways -> Truncate ile cache temizleyin; haritayi yenileyin."