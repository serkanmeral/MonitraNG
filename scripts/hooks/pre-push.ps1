# Pre-push hook for Windows (PowerShell)
# Bump versions for changed services and auto-commit
# Install: copy this to .git/hooks/pre-push (Git on Windows may run this via the shell hook)

param(
    [string]$RemoteName = "origin",
    [string]$RemoteUrl = ""
)

$gitRoot = git rev-parse --show-toplevel 2>$null
if (-not $gitRoot) {
    Write-Host "Not in a git repository!" -ForegroundColor Red
    exit 1
}

Set-Location $gitRoot

$bumpScriptPath = Join-Path $gitRoot "scripts\bump-versions.ps1"
if (-not (Test-Path $bumpScriptPath)) {
    Write-Host "Version bump script not found. Continuing with push..." -ForegroundColor Yellow
    exit 0
}

Write-Host "`nChecking for version updates (patch + auto-commit)..." -ForegroundColor Cyan
& $bumpScriptPath -BumpType patch -AutoCommit

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nVersion bump script encountered an error." -ForegroundColor Red
    $response = Read-Host "Continue with push anyway? (y/n)"
    if ($response -ne "y" -and $response -ne "Y") { exit 1 }
}

exit 0
