# Copies staged agent packages to Odak collector volume /home/odak/mnglogs-agent-packages
param(
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$LocalDir = "",
    [string]$RemoteDir = "/home/odak/mnglogs-agent-packages"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
if (-not $LocalDir) {
    $LocalDir = Join-Path $repoRoot "MngLogs\artifacts\agent-packages"
}
if (-not (Test-Path $LocalDir)) {
    throw "Local package dir missing: $LocalDir — run MngLogs/scripts/stage-agent-packages.ps1 first"
}

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server -User $User

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    Invoke-SSHCommand -SessionId $session.SessionId -Command "mkdir -p '$RemoteDir'" -TimeOut 20 | Out-Null
} finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

Get-ChildItem -LiteralPath $LocalDir -File | ForEach-Object {
    Send-OdakRemoteFile -ComputerName $Server -Credential $cred -LocalPath $_.FullName -RemoteDestination "$RemoteDir/$($_.Name)" -AcceptKey
}

Write-Host "Published to ${User}@${Server}:$RemoteDir"
