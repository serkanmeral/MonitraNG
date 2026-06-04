# Production sunucuda Docker kurulumu (Debian 13 — KURULUM.md ile uyumlu)
# Gereksinim: odak kullanicisi sudo yetkisi (parola: ODAK_SUDO_PASSWORD veya SSH ile ayni)
# Kullanım: pwsh -File .\scripts\odak\setup-docker-odak-prod.ps1

param(
    [string]$Server = "192.168.20.8",
    [string]$User = "odak"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -User $User -Server $Server

# Sudo parolasi: env veya SSH ile ayni
$sudoPass = $env:ODAK_SUDO_PASSWORD
if ([string]::IsNullOrWhiteSpace($sudoPass)) { $sudoPass = $env:ODAK_SSH_PASSWORD }

$escapedSudo = $sudoPass.Replace("'", "'\''")

$remote = @"
set -e
if command -v docker >/dev/null 2>&1; then
  echo 'Docker zaten kurulu:'
  docker --version
  docker compose version
  exit 0
fi
export DEBIAN_FRONTEND=noninteractive
SUDO='echo '$escapedSudo' | sudo -S'
eval "`$SUDO apt-get update -qq"
eval "`$SUDO apt-get install -y -qq ca-certificates curl gnupg lsb-release"
eval "`$SUDO install -m 0755 -d /etc/apt/keyrings"
curl -fsSL https://download.docker.com/linux/debian/gpg | eval "`$SUDO gpg --dearmor -o /etc/apt/keyrings/docker.gpg"
eval "`$SUDO chmod a+r /etc/apt/keyrings/docker.gpg"
echo "deb [arch=`$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian `$(. /etc/os-release && echo `$VERSION_CODENAME) stable" | eval "`$SUDO tee /etc/apt/sources.list.d/docker.list > /dev/null"
eval "`$SUDO apt-get update -qq"
eval "`$SUDO apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin"
eval "`$SUDO systemctl enable --now docker"
eval "`$SUDO usermod -aG docker odak"
docker --version
docker compose version
"@

Write-Host "Docker kurulumu basliyor ($Server) — uzun surebilir..." -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $session.SessionId -Command $remote -TimeOut 1800
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
Remove-SSHSession -SessionId $session.SessionId | Out-Null
if ($r.ExitStatus -ne 0) { throw "Docker kurulumu basarisiz (exit $($r.ExitStatus))" }
Write-Host "Docker kuruldu. Yeni SSH oturumunda 'docker ps' deneyin (docker grubu)." -ForegroundColor Green
