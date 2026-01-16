# Automatic Version Bump Script
# Detects changed services and increments version numbers
# Usage: .\bump-versions.ps1 [-BumpType patch|minor|major] [-DryRun]

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("patch", "minor", "major")]
    [string]$BumpType = "patch",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

# Service definitions with their csproj/package.json paths
$Services = @{
    "MngAdmin" = @{
        Type = "Backend"
        CsprojPath = "MngAdmin\Presentation\MngAdmin.Api\MngAdmin.Api.csproj"
        Name = "MngAdmin"
    }
    "MngScheduler" = @{
        Type = "Backend"
        CsprojPath = "MngScheduler\Presentation\MngScheduler.Api\MngScheduler.Api.csproj"
        Name = "MngScheduler"
    }
    "MngNotifier" = @{
        Type = "Backend"
        CsprojPath = "MngNotifier\Presentation\MngNotifier.Api\MngNotifier.Api.csproj"
        Name = "MngNotifier"
    }
    "MngLLM" = @{
        Type = "Backend"
        CsprojPath = "MngLLM\Presentation\MngLLM.Api\MngLLM.Api.csproj"
        Name = "MngLLM"
    }
    "MngGateway" = @{
        Type = "Backend"
        CsprojPath = "MngGateway\Presentation\MngGateway.Api\MngGateway.Api.csproj"
        Name = "MngGateway"
    }
    "MngDataGateway" = @{
        Type = "Backend"
        CsprojPath = "MngDataGateway\Presentation\MngDataGateway.Api\MngDataGateway.Api.csproj"
        Name = "MngDataGateway"
    }
    "MngKeeper" = @{
        Type = "Backend"
        CsprojPath = "MngKeeper\Presentation\MngKeeper.Api\MngKeeper.Api.csproj"
        Name = "MngKeeper"
    }
    "MngHub" = @{
        Type = "Backend"
        CsprojPath = "MngHub\Presentation\MngHub.Api\MngHub.Api.csproj"
        Name = "MngHub"
    }
    "MngReactor" = @{
        Type = "Backend"
        CsprojPath = "MngReactor\Presentation\MngReactor.Api\MngReactor.Api.csproj"
        Name = "MngReactor"
    }
    "Mng.Ui" = @{
        Type = "WebUI"
        PackageJsonPath = "Mng.Ui\package.json"
        Name = "Mng.Ui"
    }
    "MngDomainUI" = @{
        Type = "WebUI"
        PackageJsonPath = "MngDomainUI\package.json"
        Name = "MngDomainUI"
    }
}

# Get git root directory
$gitRoot = git rev-parse --show-toplevel 2>$null
if (-not $gitRoot) {
    Write-Host "❌ Not in a git repository!" -ForegroundColor Red
    exit 1
}

# Save current location
$originalLocation = Get-Location
Set-Location $gitRoot

# Function to get changed files between HEAD and origin/main
function Get-ChangedFiles {
    # Get current branch
    $currentBranch = git rev-parse --abbrev-ref HEAD
    
    # If on main, compare with HEAD~1 (last commit)
    if ($currentBranch -eq "main" -or $currentBranch -eq "master") {
        $baseRef = "HEAD~1"
    } else {
        # Compare with origin/main or origin/master
        $mainRef = "origin/main"
        try {
            git rev-parse --verify $mainRef | Out-Null
        } catch {
            $mainRef = "origin/master"
            try {
                git rev-parse --verify $mainRef | Out-Null
            } catch {
                # Fallback to HEAD~1 if no remote branch exists
                $mainRef = "HEAD~1"
            }
        }
        $baseRef = $mainRef
    }
    
    # Get changed files
    $changedFiles = git diff --name-only $baseRef...HEAD 2>$null
    if (-not $changedFiles) {
        # Fallback: check staged files
        $changedFiles = git diff --cached --name-only 2>$null
    }
    
    return $changedFiles
}

# Function to detect which services changed
function Get-ChangedServices {
    param([string[]]$ChangedFiles)
    
    $changedServices = @{}
    
    foreach ($serviceName in $Services.Keys) {
        $service = $Services[$serviceName]
        
        # Check if any changed file belongs to this service
        $servicePath = $serviceName -replace "Mng\.Ui", "Mng.Ui"
        $serviceBasePath = $servicePath.Split('\')[0]
        
        $isChanged = $false
        foreach ($file in $ChangedFiles) {
            if ($file -match "^$serviceBasePath\\.*" -or $file -match "^$servicePath\\") {
                $isChanged = $true
                break
            }
        }
        
        if ($isChanged) {
            $changedServices[$serviceName] = $service
        }
    }
    
    return $changedServices
}

# Function to bump version in csproj file
function Bump-BackendVersion {
    param(
        [string]$CsprojPath,
        [string]$BumpType
    )
    
    if (-not (Test-Path $CsprojPath)) {
        Write-Host "  ⚠️  File not found: $CsprojPath" -ForegroundColor Yellow
        return $null
    }
    
    [xml]$csproj = Get-Content $CsprojPath
    
    # Find PropertyGroup with Version
    $propertyGroup = $csproj.Project.PropertyGroup | Where-Object { $_.Version -ne $null } | Select-Object -First 1
    
    if (-not $propertyGroup) {
        Write-Host "  ⚠️  Version not found in: $CsprojPath" -ForegroundColor Yellow
        return $null
    }
    
    $currentVersion = $propertyGroup.Version
    $parts = $currentVersion.Split('.')
    
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($BumpType) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }
    
    $newVersion = "$major.$minor.$patch"
    
    if ($DryRun) {
        Write-Host "  [DRY RUN] Would update: $currentVersion → $newVersion" -ForegroundColor Cyan
        return $newVersion
    }
    
    # Update version
    $propertyGroup.Version = $newVersion
    $propertyGroup.AssemblyVersion = "$newVersion.0"
    $propertyGroup.FileVersion = "$newVersion.0"
    
    $csproj.Save($CsprojPath)
    
    Write-Host "  ✅ Updated: $currentVersion → $newVersion" -ForegroundColor Green
    
    return $newVersion
}

# Function to bump version in package.json file
function Bump-WebUIVersion {
    param(
        [string]$PackageJsonPath,
        [string]$BumpType
    )
    
    if (-not (Test-Path $PackageJsonPath)) {
        Write-Host "  ⚠️  File not found: $PackageJsonPath" -ForegroundColor Yellow
        return $null
    }
    
    $packageJson = Get-Content $PackageJsonPath | ConvertFrom-Json
    
    if (-not $packageJson.version) {
        Write-Host "  ⚠️  Version not found in: $PackageJsonPath" -ForegroundColor Yellow
        return $null
    }
    
    $currentVersion = $packageJson.version
    $parts = $currentVersion.Split('.')
    
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($BumpType) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }
    
    $newVersion = "$major.$minor.$patch"
    
    if ($DryRun) {
        Write-Host "  [DRY RUN] Would update: $currentVersion → $newVersion" -ForegroundColor Cyan
        return $newVersion
    }
    
    # Update version
    $packageJson.version = $newVersion
    
    # Update version in package.json while preserving formatting
    $packageJson.version = $newVersion
    
    # Read original file to get formatting style
    $originalContent = Get-Content $PackageJsonPath -Raw
    
    # Convert to JSON with same formatting as original
    $jsonContent = $packageJson | ConvertTo-Json -Depth 10
    
    # Try to preserve original formatting by replacing only the version line
    $versionRegex = '("version"\s*:\s*")[^"]+(")'
    $newContent = $originalContent -replace $versionRegex, "`$1$newVersion`$2"
    
    Set-Content -Path $PackageJsonPath -Value $newContent -NoNewline
    
    Write-Host "  ✅ Updated: $currentVersion → $newVersion" -ForegroundColor Green
    
    return $newVersion
}

# Main execution
Write-Host "`n=== MonitraNG Version Bump ===" -ForegroundColor Cyan
Write-Host "Bump Type: $BumpType" -ForegroundColor Yellow
if ($DryRun) {
    Write-Host "Mode: DRY RUN (no files will be modified)" -ForegroundColor Cyan
}

# Get changed files
Write-Host "`n1. Detecting changed files..." -ForegroundColor Yellow
$changedFiles = Get-ChangedFiles

if (-not $changedFiles) {
    Write-Host "  ℹ️  No changed files detected. Skipping version bump." -ForegroundColor Gray
    exit 0
}

Write-Host "  Found $($changedFiles.Count) changed file(s)" -ForegroundColor Gray

# Detect changed services
Write-Host "`n2. Detecting changed services..." -ForegroundColor Yellow
$changedServices = Get-ChangedServices -ChangedFiles $changedFiles

if ($changedServices.Count -eq 0) {
    Write-Host "  ℹ️  No service changes detected. Skipping version bump." -ForegroundColor Gray
    exit 0
}

Write-Host "  Changed services:" -ForegroundColor Gray
foreach ($serviceName in $changedServices.Keys) {
    Write-Host "    - $serviceName" -ForegroundColor White
}

# Bump versions
Write-Host "`n3. Bumping versions..." -ForegroundColor Yellow
$updatedServices = @()

foreach ($serviceName in $changedServices.Keys) {
    $service = $changedServices[$serviceName]
    Write-Host "`n  Processing: $serviceName ($($service.Type))" -ForegroundColor Cyan
    
    if ($service.Type -eq "Backend") {
        $newVersion = Bump-BackendVersion -CsprojPath $service.CsprojPath -BumpType $BumpType
        if ($newVersion) {
            $updatedServices += @{
                Name = $serviceName
                Version = $newVersion
                Type = "Backend"
            }
        }
    } elseif ($service.Type -eq "WebUI") {
        $newVersion = Bump-WebUIVersion -PackageJsonPath $service.PackageJsonPath -BumpType $BumpType
        if ($newVersion) {
            $updatedServices += @{
                Name = $serviceName
                Version = $newVersion
                Type = "WebUI"
            }
        }
    }
}

if ($updatedServices.Count -eq 0) {
    Write-Host "`n  ℹ️  No versions were updated." -ForegroundColor Gray
    exit 0
}

Write-Host "`n✅ Version bump completed!" -ForegroundColor Green
Write-Host "`nSummary:" -ForegroundColor Cyan
foreach ($updated in $updatedServices) {
    Write-Host "  $($updated.Name): v$($updated.Version) ($($updated.Type))" -ForegroundColor Gray
}

if (-not $DryRun) {
    Write-Host "`n⚠️  Version files have been updated. Don't forget to:" -ForegroundColor Yellow
    Write-Host "  1. Review the changes (git diff)" -ForegroundColor Gray
    Write-Host "  2. Stage the version files (git add)" -ForegroundColor Gray
    Write-Host "  3. Commit the version updates" -ForegroundColor Gray
    Write-Host "  4. Continue with your git push" -ForegroundColor Gray
    
    # Auto-stage version files if not in hook context
    if (-not $env:GIT_HOOK_RUNNING) {
        Write-Host "`n💡 Tip: Run 'git add' to stage the version changes before committing." -ForegroundColor Cyan
    }
}

# Restore original location
Set-Location $originalLocation

Write-Host ""
