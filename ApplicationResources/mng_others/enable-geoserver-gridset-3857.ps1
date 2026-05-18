<#
.SYNOPSIS
  GeoServer tr_rail:railways ve tr_rail:stations tile katmanlarına EPSG:3857 gridset ekler.
  Leaflet (OSM) tile indeksleri ile uyumluluk için kullanılır.

.DESCRIPTION
  GWC REST API ile mevcut katman XML'ini alır, gridSubsets içinde EPSG:3857 yoksa ekler ve PUT ile geri yükler.
  Ayrıntılar: GEOSERVER_GRIDSET_EPSG3857.md

.PARAMETER GeoServerBaseUrl
  GeoServer temel URL (örn. http://localhost:8082).

.PARAMETER UserName
  GeoServer admin kullanıcı adı.

.PARAMETER Password
  GeoServer admin şifresi.
#>
param(
    [string] $GeoServerBaseUrl = "http://localhost:8082",
    [string] $UserName = "admin",
    [string] $Password = "admin"
)

$ErrorActionPreference = "Stop"
$layers = @("tr_rail:railways", "tr_rail:stations")
$gridSetToAdd = "EPSG:3857"
$base = $GeoServerBaseUrl.TrimEnd("/")
$gwcRest = "$base/geoserver/gwc/rest/layers"
$cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${UserName}:${Password}"))
$headers = @{
    "Authorization" = "Basic $cred"
    "Content-Type"  = "text/xml"
}

function Add-GridSetToLayerXml {
    param([string] $xmlContent, [string] $gridSetName)
    if ($xmlContent -match "<gridSetName>\s*$([regex]::Escape($gridSetName))\s*</gridSetName>") {
        return $null
    }
    if ($xmlContent -match "<gridSetName>\s*EPSG:900913\s*</gridSetName>") {
        return $null
    }
    $insert = @"
  <gridSubset>
    <gridSetName>$gridSetName</gridSetName>
  </gridSubset>

</gridSubsets>
"@
    $pattern = "\s*</gridSubsets>\s*"
    if ($xmlContent -match $pattern) {
        return $xmlContent -replace $pattern, $insert
    }
    return $null
}

foreach ($layerName in $layers) {
    $encodedName = [Uri]::EscapeDataString($layerName)
    $url = "$gwcRest/$encodedName.xml"
    Write-Host "Katman: $layerName" -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri $url -Headers @{ "Authorization" = "Basic $cred" } -UseBasicParsing
        $body = $response.Content
    }
    catch {
        Write-Warning "GET başarısız ($url): $($_.Exception.Message)"
        continue
    }
    $updated = Add-GridSetToLayerXml -xmlContent $body -gridSetName $gridSetToAdd
    if (-not $updated) {
        Write-Host "  EPSG:3857 veya EPSG:900913 zaten mevcut, atlanıyor." -ForegroundColor Gray
        continue
    }
    try {
        Invoke-WebRequest -Uri $url -Method Put -Headers $headers -Body $updated -UseBasicParsing | Out-Null
        Write-Host "  EPSG:3857 gridset eklendi ve kaydedildi." -ForegroundColor Green
    }
    catch {
        Write-Warning "  PUT başarısız: $($_.Exception.Message)"
    }
}
Write-Host "Tamamlandı." -ForegroundColor Cyan
