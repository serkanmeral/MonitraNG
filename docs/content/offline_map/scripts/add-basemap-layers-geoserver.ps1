# Adım 6: GeoServer tr_rail workspace'e arka plan katmanlarını ekler (roads, waterways, water_areas, landuse)
# Önce PostGIS'te bu tabloların dolu olması gerekir. configure-geoserver-tr_rail.ps1 zaten çalışmış olmalı.
# Kullanım: .\add-basemap-layers-geoserver.ps1 [-BaseUrl "http://localhost:8082/geoserver"] [-User "admin"] [-Password "geoserver"]

param(
    [string]$BaseUrl = "http://localhost:8082/geoserver",
    [string]$User = "admin",
    [string]$Password = "geoserver"
)

$ErrorActionPreference = "Stop"
$rest = "$BaseUrl/rest"
$cred = [System.Management.Automation.PSCredential]::new($User, (ConvertTo-SecureString $Password -AsPlainText -Force))

foreach ($layer in @("roads","waterways","water_areas","landuse")) {
    Write-Host "Layer $layer..."
    $ftBody = "{`"featureType`":{`"name`":`"$layer`"}}"
    try {
        Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/datastores/postgis/featuretypes/$layer" -Method Get -Credential $cred -AllowUnencryptedAuthentication -ErrorAction Stop | Out-Null
        Write-Host "  zaten yayimda."
    } catch {
        Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/datastores/postgis/featuretypes" -Method Post -Credential $cred -Body $ftBody -ContentType "application/json" -AllowUnencryptedAuthentication | Out-Null
        Write-Host "  yayimlandi."
    }
}
Write-Host "Tamamlandi. GWC tile cache icin: Tile Caching -> tr_rail:roads, waterways, water_areas, landuse -> Truncate (Turkce: Kes/Temizle)."