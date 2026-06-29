# Gelistirme makinesinde (interneti olan) Odak servis image'larini build eder.
# Prod/test sunucularinda build YAPMAZ — offline deploy icin image uretir.
#
# On kosul: docker info calisiyor (Windows'ta WSL2 + Docker Desktop)
#
# Kullanim:
#   .\scripts\odak\build-odak-docker-images.ps1 -Services mngdocument
#   .\scripts\odak\build-odak-docker-images.ps1 -Services mngdocument,mngui -Target prod
#   .\scripts\odak\prefetch-odak-docker-base-images.ps1   # ilk kurulumda base image'lar

param(
    [Parameter(Mandatory)]
    [string]$Services,
    [string]$Version = "latest",
    [ValidateSet("prod", "test")]
    [string]$Target = "test",
    [switch]$NoCache,
    [switch]$Export,
    [string]$ArchivePath = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "OdakDockerOffline.ps1")

Assert-OdakDockerAvailable
$serviceList = Resolve-OdakDockerServiceList -Services @($Services -split ',')

$built = @()
foreach ($svc in $serviceList) {
    $built += Build-OdakDockerServiceImage -ServiceName $svc -Version $Version -Target $Target -NoCache:$NoCache
}

Write-Host "Build tamam: $($built -join ', ')" -ForegroundColor Green

if ($Export) {
    if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
        $serverHint = if ($Target -eq "prod") { "192.168.20.8" } else { "192.168.20.20" }
        $ArchivePath = Get-OdakDockerDefaultArchivePath -ServiceNames $serviceList -Version $Version -Server $serverHint
    }
    Export-OdakDockerImages -ImageRefs $built -ArchivePath $ArchivePath
}
