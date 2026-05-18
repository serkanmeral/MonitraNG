# GeoServer Faz 1.5: Workspace tr_rail, PostGIS store, layers (railways, stations, places)
# Kullanım: .\configure-geoserver-tr_rail.ps1 [-BaseUrl "http://localhost:8082/geoserver"] [-User "admin"] [-Password "geoserver"]
# Varsayılan şifre çoğu Docker imajında "geoserver"; mng_others'ta GEOSERVER_ADMIN_PASSWORD ile değiştirilmişse onu kullanın.

param(
    [string]$BaseUrl = "http://localhost:8082/geoserver",
    [string]$User = "admin",
    [string]$Password = "geoserver"
)

$ErrorActionPreference = "Stop"
$rest = "$BaseUrl/rest"
$cred = [System.Management.Automation.PSCredential]::new($User, (ConvertTo-SecureString $Password -AsPlainText -Force))

# Workspace
Write-Host "Workspace tr_rail..."
try {
    Invoke-RestMethod -Uri "$rest/workspaces/tr_rail" -Method Get -Credential $cred -AllowUnencryptedAuthentication -ErrorAction Stop | Out-Null
    Write-Host "  zaten mevcut."
} catch {
    $body = '{"workspace":{"name":"tr_rail"}}'
    Invoke-RestMethod -Uri "$rest/workspaces" -Method Post -Credential $cred -Body $body -ContentType "application/json" -AllowUnencryptedAuthentication | Out-Null
    Write-Host "  olusturuldu."
}

# PostGIS store
Write-Host "PostGIS store..."
$dsBody = @{
    dataStore = @{
        name = "postgis"
        type = "PostGIS"
        enabled = $true
        connectionParameters = @{
            entry = @(
                @{ "@key" = "host"; "$" = "postgis" },
                @{ "@key" = "port"; "$" = "5432" },
                @{ "@key" = "database"; "$" = "gis" },
                @{ "@key" = "schema"; "$" = "public" },
                @{ "@key" = "user"; "$" = "gisuser" },
                @{ "@key" = "passwd"; "$" = "gispass" }
            )
        }
    }
} | ConvertTo-Json -Depth 10
try {
    Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/datastores/postgis" -Method Get -Credential $cred -AllowUnencryptedAuthentication -ErrorAction Stop | Out-Null
    Write-Host "  zaten mevcut."
} catch {
    Invoke-RestMethod -Uri "$rest/workspaces/tr_rail/datastores" -Method Post -Credential $cred -Body $dsBody -ContentType "application/json" -AllowUnencryptedAuthentication | Out-Null
    Write-Host "  olusturuldu."
}

# Layers
foreach ($layer in @("railways","stations","places")) {
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

Write-Host "Tamamlandi. WMS: $BaseUrl/tr_rail/wms  WMTS: $BaseUrl/gwc/service/wmts"
