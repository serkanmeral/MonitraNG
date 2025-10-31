# Version Management Script
# Usage: .\version.ps1 <command> [version]
# Commands: current, bump-major, bump-minor, bump-patch, set <version>, tag

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("current", "bump-major", "bump-minor", "bump-patch", "set", "tag")]
    [string]$Command,
    
    [Parameter(Mandatory=$false)]
    [string]$NewVersion
)

$ProjectFiles = @(
    "MngKeeper/Presentation/MngKeeper.Api/MngKeeper.Api.csproj",
    "MngKeeper/Core/MngKeeper.Application/MngKeeper.Application.csproj",
    "MngKeeper/Core/MngKeeper.Domain/MngKeeper.Domain.csproj",
    "MngKeeper/Infrastructure/MngKeeper.Infrastructure/MngKeeper.Infrastructure.csproj",
    "MngKeeper/Infrastructure/MngKeeper.Persistence/MngKeeper.Persistence.csproj"
)

function Get-CurrentVersion {
    $csprojPath = $ProjectFiles[0]
    [xml]$csproj = Get-Content $csprojPath
    $version = $csproj.Project.PropertyGroup.Version
    return $version
}

function Set-Version {
    param([string]$Version)
    
    Write-Host "Setting version to: $Version" -ForegroundColor Cyan
    
    foreach ($projectFile in $ProjectFiles) {
        if (Test-Path $projectFile) {
            [xml]$csproj = Get-Content $projectFile
            
            # Find or create PropertyGroup with Version
            $propertyGroup = $csproj.Project.PropertyGroup | Where-Object { $_.Version -ne $null } | Select-Object -First 1
            
            if ($propertyGroup -eq $null) {
                # Create new PropertyGroup
                $propertyGroup = $csproj.CreateElement("PropertyGroup")
                $csproj.Project.AppendChild($propertyGroup) | Out-Null
            }
            
            # Update or create Version elements
            if ($propertyGroup.Version -eq $null) {
                $versionElement = $csproj.CreateElement("Version")
                $propertyGroup.AppendChild($versionElement) | Out-Null
            }
            $propertyGroup.Version = $Version
            
            if ($propertyGroup.AssemblyVersion -eq $null) {
                $assemblyVersionElement = $csproj.CreateElement("AssemblyVersion")
                $propertyGroup.AppendChild($assemblyVersionElement) | Out-Null
            }
            $propertyGroup.AssemblyVersion = "$Version.0"
            
            if ($propertyGroup.FileVersion -eq $null) {
                $fileVersionElement = $csproj.CreateElement("FileVersion")
                $propertyGroup.AppendChild($fileVersionElement) | Out-Null
            }
            $propertyGroup.FileVersion = "$Version.0"
            
            $csproj.Save($projectFile)
            Write-Host "  ✅ Updated: $projectFile" -ForegroundColor Green
        }
    }
    
    Write-Host "`n✅ Version updated to $Version in all projects" -ForegroundColor Green
}

function Bump-Version {
    param(
        [ValidateSet("major", "minor", "patch")]
        [string]$Part
    )
    
    $currentVersion = Get-CurrentVersion
    $parts = $currentVersion.Split('.')
    
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($Part) {
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
    return $newVersion
}

function Create-GitTag {
    $version = Get-CurrentVersion
    $tagName = "v$version"
    
    Write-Host "`nCreating git tag: $tagName" -ForegroundColor Cyan
    
    # Check if tag already exists
    $existingTag = git tag -l $tagName
    if ($existingTag) {
        Write-Host "❌ Tag $tagName already exists!" -ForegroundColor Red
        return
    }
    
    # Create annotated tag
    git tag -a $tagName -m "Release $tagName"
    
    Write-Host "✅ Git tag created: $tagName" -ForegroundColor Green
    Write-Host "`nTo push the tag, run:" -ForegroundColor Yellow
    Write-Host "  git push origin $tagName" -ForegroundColor Gray
    Write-Host "  OR" -ForegroundColor Yellow
    Write-Host "  git push --tags" -ForegroundColor Gray
}

# Main execution
switch ($Command) {
    "current" {
        $version = Get-CurrentVersion
        Write-Host "`nCurrent Version: $version" -ForegroundColor Cyan
        Write-Host "`nVersion locations:" -ForegroundColor Yellow
        foreach ($file in $ProjectFiles) {
            if (Test-Path $file) {
                Write-Host "  ✅ $file" -ForegroundColor Gray
            }
        }
    }
    
    "bump-major" {
        $newVersion = Bump-Version -Part "major"
        Write-Host "Bumping MAJOR version: $(Get-CurrentVersion) → $newVersion" -ForegroundColor Yellow
        Set-Version -Version $newVersion
    }
    
    "bump-minor" {
        $newVersion = Bump-Version -Part "minor"
        Write-Host "Bumping MINOR version: $(Get-CurrentVersion) → $newVersion" -ForegroundColor Yellow
        Set-Version -Version $newVersion
    }
    
    "bump-patch" {
        $newVersion = Bump-Version -Part "patch"
        Write-Host "Bumping PATCH version: $(Get-CurrentVersion) → $newVersion" -ForegroundColor Yellow
        Set-Version -Version $newVersion
    }
    
    "set" {
        if ([string]::IsNullOrEmpty($NewVersion)) {
            Write-Host "❌ Version number required!" -ForegroundColor Red
            Write-Host "Usage: .\version.ps1 set 1.2.3" -ForegroundColor Yellow
            exit 1
        }
        
        # Validate version format
        if ($NewVersion -notmatch '^\d+\.\d+\.\d+$') {
            Write-Host "❌ Invalid version format! Use: MAJOR.MINOR.PATCH (e.g., 1.2.3)" -ForegroundColor Red
            exit 1
        }
        
        Set-Version -Version $NewVersion
    }
    
    "tag" {
        Create-GitTag
    }
}

Write-Host ""

