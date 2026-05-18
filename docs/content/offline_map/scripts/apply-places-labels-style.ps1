# GeoServer tr_rail:places katmanına "yerleşim isimleri" (şehir, ilçe, köy etiketleri) stilini uygular.
# Önce places_labels.sld stilini yükler, sonra places layer'ının varsayılan stili yapar.
# Kullanım: .\apply-places-labels-style.ps1 [-BaseUrl "http://localhost:8082/geoserver"] [-User "admin"] [-Password "geoserver"]

param(
    [string]$BaseUrl = "http://localhost:8082/geoserver",
    [string]$User = "admin",
    [string]$Password = "geoserver"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sldPath = Join-Path $scriptDir "styles\places_labels.sld"
if (-not (Test-Path $sldPath)) {
    Write-Error "SLD dosyasi bulunamadi: $sldPath"
}

$rest = "$BaseUrl/rest"
$cred = [System.Management.Automation.PSCredential]::new($User, (ConvertTo-SecureString $Password -AsPlainText -Force))
$sldContent = Get-Content $sldPath -Raw -Encoding UTF8

# Stil yükle veya güncelle (zaten varsa POST 403 döner; bu durumda PUT ile güncelle)
Write-Host "Stil places_labels yukleniyor..."
$styleUri = "$rest/workspaces/tr_rail/styles/places_labels"
$headers = @{
    "Content-Type" = "application/vnd.ogc.sld+xml"
}
$updated = $false
try {
    Invoke-RestMethod -Uri $styleUri -Method Get -Credential $cred -AllowUnencryptedAuthentication -ErrorAction Stop | Out-Null
    Invoke-RestMethod -Uri $styleUri -Method Put -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
    Write-Host "  stil guncellendi."
    $updated = $true
} catch {
    # Stil yoksa oluştur; "already exists" (403) ise PUT dene
    try {
        $createUri = "$rest/workspaces/tr_rail/styles?name=places_labels"
        Invoke-RestMethod -Uri $createUri -Method Post -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
        Write-Host "  stil olusturuldu."
        $updated = $true
    } catch {
        # 403 Style already exists: mevcut stili PUT ile güncelle
        Invoke-RestMethod -Uri $styleUri -Method Put -Credential $cred -Body $sldContent -Headers $headers -AllowUnencryptedAuthentication | Out-Null
        Write-Host "  stil guncellendi (zaten vardi)."
        $updated = $true
    }
}
if (-not $updated) { throw "Stil yuklenemedi." }

# places layer varsayılan stilini ata
Write-Host "places layer varsayilan stili ataniyor..."
$layerBody = '{"layer":{"defaultStyle":{"name":"places_labels","workspace":{"name":"tr_rail"}}}}'
Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/layers/places" -Method Put -Credential $cred -Body $layerBody -ContentType "application/json" -AllowUnencryptedAuthentication | Out-Null
Write-Host "  places varsayilan stil: tr_rail:places_labels"

Write-Host "Tamamlandi. WMTS/WMS ile tr_rail:places cagirildiginda yerlesim isimleri etiket olarak gorunecek."
Write-Host "Tile cache kullaniyorsaniz: Tile Caching -> Tile Layers -> tr_rail:places -> Seed/Truncate ile cache temizleyin."
