# Production kaynak senkronu (192.168.20.8) — kod kopyasi; test verisi/secret DEGIL
# -IncludeMngCommon: production VM'e kendi mng_common dosyalarini gonderir (ayri volume'lar)
# Kullanım: .\scripts\odak\sync-odak-prod.ps1 [-Full] [-PathsCsv ...] [-IncludeMngCommon]
# SSH: .env.odak.prod.local

param(
    [string[]]$Paths = @(),
    [string]$PathsCsv = "",
    [switch]$Full,
    [switch]$IncludeMngCommon
)

if ($PathsCsv -and $Paths.Count -eq 0) {
    $Paths = $PathsCsv -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}
# pwsh -File ile -Paths @('a','b') tek eleman sayilabiliyor; virgullu tek string'i ayir
if ($Paths.Count -eq 1 -and $Paths[0] -match ',') {
    $Paths = $Paths[0] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}

$prodServer = "192.168.20.8"
$scriptDir = $PSScriptRoot
$syncScript = Join-Path $scriptDir "sync-odak-source.ps1"

$params = @{ Server = $prodServer }
if ($Full) { $params.Full = $true }
if ($IncludeMngCommon) { $params.IncludeMngCommon = $true }
if ($Paths.Count -gt 0) { $params.Paths = $Paths }

& $syncScript @params
