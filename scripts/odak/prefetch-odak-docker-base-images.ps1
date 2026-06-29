# Gelistirme makinesinde Docker base / ucuncu parti image'larini onceden ceker.
# Offline ortamlara gitmeden once bir kez calistirin (internet gerekir).
#
# Kullanim:
#   .\scripts\odak\prefetch-odak-docker-base-images.ps1
#   .\scripts\odak\prefetch-odak-docker-base-images.ps1 -IncludeThirdParty -ExportBundle
#
# -IncludeThirdParty: gotenberg + collabora (prod'da build degil pull ile gelir)
# -ExportBundle: cekilen image'lari artifacts/odak-docker/base-images.tar olarak kaydeder (prod'a aktarim icin)

param(
    [switch]$IncludeThirdParty,
    [switch]$ExportBundle
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "OdakDockerOffline.ps1")

Invoke-OdakDockerBaseImagePrefetch -IncludeThirdParty:$IncludeThirdParty

if ($ExportBundle) {
    $images = @($script:OdakDockerBaseImages)
    if ($IncludeThirdParty) { $images += $script:OdakDockerThirdPartyImages }
    $archive = Join-Path $script:OdakDockerArtifactDir "base-images.tar"
    Export-OdakDockerImages -ImageRefs $images -ArchivePath $archive
    Write-Host "Prod/test'e aktarmak icin:" -ForegroundColor Cyan
    Write-Host "  deploy-odak-offline.ps1 -SkipBuild -SkipSync -SkipDeploy -ArchivePath `"$archive`"" -ForegroundColor Gray
}
