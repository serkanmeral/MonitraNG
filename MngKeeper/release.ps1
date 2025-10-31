# Release Automation Script
# Creates a new release with version bump, changelog update, git tag and push

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$BumpType,
    
    [Parameter(Mandatory=$false)]
    [string]$Message = ""
)

Write-Host "`n=== MonitraNG Release Script ===" -ForegroundColor Cyan
Write-Host "Bump Type: $BumpType" -ForegroundColor Yellow

# 1. Get current version
$currentVersion = & .\version.ps1 current | Select-String -Pattern "Current Version: (.*)" | ForEach-Object { $_.Matches.Groups[1].Value }
Write-Host "`nCurrent Version: $currentVersion" -ForegroundColor Gray

# 2. Bump version
Write-Host "`n1. Bumping version..." -ForegroundColor Yellow
& .\version.ps1 "bump-$BumpType"

# Get new version
Start-Sleep -Seconds 1
$newVersionOutput = & .\version.ps1 current
$newVersion = $newVersionOutput | Select-String -Pattern "Current Version: (.*)" | ForEach-Object { $_.Matches.Groups[1].Value.Trim() }
Write-Host "New Version: $newVersion" -ForegroundColor Green

# 3. Update CHANGELOG.md
Write-Host "`n2. Updating CHANGELOG.md..." -ForegroundColor Yellow
$date = Get-Date -Format "yyyy-MM-dd"
$changelogEntry = @"

## [$newVersion] - $date

### Added
- $Message

"@

$changelog = Get-Content "CHANGELOG.md" -Raw
$changelog = $changelog -replace "## \[Unreleased\]", "## [Unreleased]$changelogEntry"
Set-Content "CHANGELOG.md" -Value $changelog
Write-Host "  ✅ CHANGELOG.md updated" -ForegroundColor Green

# 4. Build project
Write-Host "`n3. Building project..." -ForegroundColor Yellow
Push-Location "Presentation/MngKeeper.Api"
dotnet build --configuration Release
$buildSuccess = $LASTEXITCODE -eq 0
Pop-Location

if (-not $buildSuccess) {
    Write-Host "❌ Build failed! Aborting release." -ForegroundColor Red
    exit 1
}
Write-Host "  ✅ Build successful" -ForegroundColor Green

# 5. Git commit
Write-Host "`n4. Creating git commit..." -ForegroundColor Yellow
git add .
git commit -m "chore: Release v$newVersion"
Write-Host "  ✅ Git commit created" -ForegroundColor Green

# 6. Create git tag
Write-Host "`n5. Creating git tag..." -ForegroundColor Yellow
& .\version.ps1 tag
Write-Host "  ✅ Git tag created: v$newVersion" -ForegroundColor Green

# 7. Push to remote
Write-Host "`n6. Pushing to remote..." -ForegroundColor Yellow
$pushConfirm = Read-Host "Do you want to push to remote? (y/n)"
if ($pushConfirm -eq "y") {
    git push origin main
    git push origin "v$newVersion"
    Write-Host "  ✅ Pushed to remote" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  Push skipped" -ForegroundColor Yellow
    Write-Host "`nTo push manually, run:" -ForegroundColor Cyan
    Write-Host "  git push origin main" -ForegroundColor Gray
    Write-Host "  git push origin v$newVersion" -ForegroundColor Gray
}

Write-Host "`n🎉 Release v$newVersion completed!" -ForegroundColor Green
Write-Host "`nRelease Summary:" -ForegroundColor Cyan
Write-Host "  Version: $currentVersion → $newVersion" -ForegroundColor Gray
Write-Host "  Tag: v$newVersion" -ForegroundColor Gray
Write-Host "  Date: $date" -ForegroundColor Gray
Write-Host ""

