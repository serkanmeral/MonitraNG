# Hazir Docker image tarball'ini Odak sunucusuna yukler (docker load). Deploy yapmaz.
# Ucuncu parti image bundle (gotenberg, collabora) veya baska makinede uretilmis .tar icin.
#
# Kullanim:
#   .\scripts\odak\import-odak-docker-bundle-remote.ps1 -ArchivePath .\artifacts\odak-docker\base-images.tar
#   .\scripts\odak\import-odak-docker-bundle-remote.ps1 -Server 192.168.20.8 -ArchivePath .\artifacts\odak-docker\192-168-20-8-mngdocument-latest.tar

param(
    [string]$Server = "192.168.20.8",
    [Parameter(Mandatory)]
    [string]$ArchivePath
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
. (Join-Path $PSScriptRoot "OdakDockerOffline.ps1")

if (-not (Test-Path $ArchivePath)) { throw "Archive yok: $ArchivePath" }

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
Import-OdakDockerArchiveRemote -Server $Server -Credential $cred -LocalArchivePath $ArchivePath
Write-Host "Import tamam: $Server" -ForegroundColor Green
