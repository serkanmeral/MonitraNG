# Deploy MngLanding static files to production (www.monitrang.com).
# Independent from MngUI / Docker CI-CD pipeline.
#
# Usage (from MonitraNG root):
#   .\scripts\deployment\deploy-www-landing.ps1
#   .\scripts\deployment\deploy-www-landing.ps1 -Server monitrang-server -DryRun

param(
    [string]$Source = "",
    [string]$Server = "monitrang-server",
    [string]$RemotePath = "/var/www/www.monitrang.com",
    [switch]$ApplyNginxConfig,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptPath "..\..")
if (-not $Source) {
    $Source = Join-Path $repoRoot "MngLanding"
}

if (-not (Test-Path (Join-Path $Source "index.html"))) {
    throw "index.html not found under Source: $Source"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = "${RemotePath}.backup-${timestamp}"

Write-Host "=== MonitraNG www landing deploy ===" -ForegroundColor Cyan
Write-Host "Source:      $Source"
Write-Host "Server:      $Server"
Write-Host "Remote path: $RemotePath"
Write-Host "Backup:      $backupPath"
Write-Host ""

if ($DryRun) {
    Write-Host "[DryRun] Would deploy files and optionally apply nginx config." -ForegroundColor Yellow
    exit 0
}

# Remote: ensure target dir + backup existing content
$remotePrep = @"
set -e
mkdir -p '$RemotePath'
if [ -f '$RemotePath/index.html' ]; then
  rm -rf '$backupPath'
  cp -a '$RemotePath' '$backupPath'
  echo "Backup created: $backupPath"
fi
"@

ssh -o BatchMode=yes -o ConnectTimeout=20 $Server $remotePrep

# Upload files (scp recursive)
scp -o BatchMode=yes -o ConnectTimeout=20 -r `
    (Join-Path $Source "index.html") `
    (Join-Path $Source "css") `
    (Join-Path $Source "js") `
    "${Server}:${RemotePath}/"

if (Test-Path (Join-Path $Source "assets")) {
    scp -o BatchMode=yes -o ConnectTimeout=20 -r `
        (Join-Path $Source "assets") `
        "${Server}:${RemotePath}/"
}

# Permissions for nginx
ssh -o BatchMode=yes -o ConnectTimeout=20 $Server "chown -R www-data:www-data '$RemotePath' 2>/dev/null || chown -R nginx:nginx '$RemotePath' 2>/dev/null || true"

if ($ApplyNginxConfig) {
    $nginxConfLocal = Join-Path $repoRoot "ApplicationResources\mng_common\nginx\conf.d\www.monitrang.conf"
    if (-not (Test-Path $nginxConfLocal)) {
        throw "Nginx config not found: $nginxConfLocal"
    }

    Write-Host "Applying nginx config..." -ForegroundColor Cyan
    scp -o BatchMode=yes -o ConnectTimeout=20 $nginxConfLocal "${Server}:/etc/nginx/sites-available/www.monitrang.conf"

    $nginxApply = @'
set -e
TS=$(date +%Y%m%d%H%M%S)
MON=/etc/nginx/sites-available/monitrang
if [ -f "$MON" ]; then
  cp "$MON" "${MON}.bak-landing-$TS"
  sed -i "/^# monitrang.com \/ www HTTPS (ana domain -> uygulama)/,\$d" "$MON"
fi
ln -sf /etc/nginx/sites-available/www.monitrang.conf /etc/nginx/sites-enabled/www.monitrang.conf
nginx -t
systemctl reload nginx
echo "Nginx reloaded."
'@

    ssh -o BatchMode=yes -o ConnectTimeout=20 $Server $nginxApply
}

Write-Host ""
Write-Host "Smoke test:" -ForegroundColor Cyan
ssh -o BatchMode=yes -o ConnectTimeout=20 $Server @"
curl -sk -o /dev/null -w 'www: %{http_code}\n' https://www.monitrang.com/
curl -sk -o /dev/null -w 'root: %{http_code} redirect=%{redirect_url}\n' https://monitrang.com/
curl -sk -o /dev/null -w 'app: %{http_code}\n' https://app.monitrang.com/
"@

Write-Host ""
Write-Host "Deploy complete. URL: https://www.monitrang.com" -ForegroundColor Green
