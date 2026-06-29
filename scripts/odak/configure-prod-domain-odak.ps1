<#
.SYNOPSIS
  Production ortaminda domain tabanli URL + CORS ayarlarini gunceller (Odak prod).

.EXAMPLE
  pwsh -File .\scripts\odak\configure-prod-domain-odak.ps1 -Domain mng.odaksavunma.com -Deploy
#>
param(
    [string]$Domain = "mng.odaksavunma.com",
    [string]$Server = "192.168.20.8",
    [switch]$UseHttps = $true,
    [switch]$KeepIpFallback = $true,
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$scheme = if ($UseHttps) { "https" } else { "http" }
$publicBase = "${scheme}://${Domain}"
$ipFallback = "http://${Server}:3000"

$remoteAppsEnv = "/home/odak/MonitraNG/ApplicationResources/mng_apps/.env"
$remoteCommonEnv = "/home/odak/mng_common/.env"

Write-Host "=== Prod domain yapilandirma ===" -ForegroundColor Cyan
Write-Host "Domain     : $Domain"
Write-Host "Public base: $publicBase"
Write-Host "CORS[0]    : $publicBase"
if ($KeepIpFallback) { Write-Host "CORS[1]    : $ipFallback (IP fallback)" }

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

function Set-RemoteEnvLine {
    param([string]$Key, [string]$Value, [string]$File)
    $escapedVal = $Value -replace "'", "'\\''"
    return "if grep -q '^${Key}=' '$File' 2>/dev/null; then sed -i 's|^${Key}=.*|${Key}=${escapedVal}|' '$File'; else echo '${Key}=${escapedVal}' >> '$File'; fi"
}

try {
    $lines = @(
        Set-RemoteEnvLine "DOMAIN" $Domain $remoteAppsEnv
        Set-RemoteEnvLine "OPENAPI_SERVER_PATH" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "CORS_ALLOWED_ORIGIN_1" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "GATEWAY_URL" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "HUB_URL" "" $remoteAppsEnv
        Set-RemoteEnvLine "MNG_KEEPER_UI_BASE_URL" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "MNGKEEPER_ENGINE_URL" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "KEEPER_URL" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "DATAGATEWAY_URL" $publicBase $remoteAppsEnv
        Set-RemoteEnvLine "ODAK_KEYCLOAK_HOSTNAME" $Domain $remoteCommonEnv
    )
    if ($KeepIpFallback) {
        $lines += Set-RemoteEnvLine "CORS_ALLOWED_ORIGIN_2" $ipFallback $remoteAppsEnv
    }

    $remoteScript = "set -e`n" + ($lines -join "`n") + "`necho '=== guncel URL/CORS ==='`ngrep -E 'CORS_|GATEWAY_|HUB_|MNG_KEEPER|OPENAPI|DOMAIN' '$remoteAppsEnv' | grep -v LICENSE`ngrep ODAK_KEYCLOAK_HOSTNAME '$remoteCommonEnv'"
    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command (ConvertTo-UnixShell $remoteScript) -TimeOut 120
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.ExitStatus -ne 0) { throw "Env guncelleme basarisiz" }

    Write-Host "`nEnv guncellendi." -ForegroundColor Green

    if ($Deploy) {
        Write-Host "`nDeploy: sync Mng.Ui + mngui/mnggateway rebuild..." -ForegroundColor Cyan
        Remove-SSHSession $session.SessionId | Out-Null
        $session = $null

        $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
        Push-Location $repoRoot
        try {
            & (Join-Path $PSScriptRoot "sync-odak-prod.ps1") -Paths @("Mng.Ui", "ApplicationResources/mng_apps")
            & (Join-Path $PSScriptRoot "deploy-odak-prod.ps1") -Services "mngui,mnggateway"

            Initialize-OdakSshEnvironment -Server $Server
            $cred2 = Get-OdakSshCredential -Server $Server
            $s2 = New-SSHSession -ComputerName $Server -Credential $cred2 -AcceptKey
            $kc = @"
set -e
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.prod.yml --env-file .env up -d keycloak
sleep 3
docker exec mnggateway printenv | grep -i 'Cors__AllowedOrigins__0' || true
curl -sI -m 5 ${publicBase}/ | head -3
"@
            $dr = Invoke-SSHCommand -SessionId $s2.SessionId -Command (ConvertTo-UnixShell $kc) -TimeOut 300
            $dr.Output | ForEach-Object { Write-Host $_ }
            Remove-SSHSession $s2.SessionId | Out-Null
        } finally {
            Pop-Location
        }
        Write-Host "`nDeploy tamamlandi. Tarayicida Ctrl+F5 + yeniden login deneyin." -ForegroundColor Green
    } else {
        Write-Host "`nDeploy atlandi. Uygulamak icin: -Deploy ekleyin veya deploy-odak-prod.ps1 -Services mngui,mnggateway" -ForegroundColor Yellow
    }
}
finally {
    if ($session) { Remove-SSHSession $session.SessionId | Out-Null }
}
