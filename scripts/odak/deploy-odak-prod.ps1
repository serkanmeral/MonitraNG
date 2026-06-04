# Production mng_apps deploy (192.168.20.8) — test sunucudan BAGIMSIZ
# Kendi mng_common (Mongo/Keycloak/...) ayakta olmali; docker-compose.odak.prod.yml kullanilir
# Kullanım: .\scripts\odak\deploy-odak-prod.ps1 [-Services mngui,...] [-NoCache]
# Ön koşul: sync-odak-prod.ps1, production mng_common up, .env.odak.prod.example -> .env

param(
    [string]$Services = "",
    [switch]$NoBuild,
    [switch]$NoCache
)

$prodServer = "192.168.20.8"
$scriptDir = $PSScriptRoot
$deployScript = Join-Path $scriptDir "deploy-odak-apps.ps1"

$params = @{ Server = $prodServer }
if ($Services) { $params.Services = $Services }
if ($NoBuild) { $params.NoBuild = $true }
if ($NoCache) { $params.NoCache = $true }

& $deployScript @params
