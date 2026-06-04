# Production mng_common dosya senkronu (192.168.20.8)
param([switch]$SkipEnvBootstrap)

$scriptDir = $PSScriptRoot
& (Join-Path $scriptDir "sync-odak-source.ps1") -Server 192.168.20.8 -MngCommonOnly

if (-not $SkipEnvBootstrap) {
    & (Join-Path $scriptDir "bootstrap-odak-prod.ps1")
}
