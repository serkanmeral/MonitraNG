# Show current versions of all services
# Usage: .\scripts\show-versions.ps1

Write-Host "`n=== MonitraNG Service Versions ===" -ForegroundColor Cyan
Write-Host ""

# Backend Services
Write-Host "Backend Services:" -ForegroundColor Yellow
$backendServices = @("MngAdmin", "MngScheduler", "MngNotifier", "MngLLM", "MngGateway", "MngDataGateway", "MngKeeper", "MngHub")

foreach ($serviceName in $backendServices) {
    $csprojPath = "$serviceName\Presentation\$serviceName.Api\$serviceName.Api.csproj"
    
    if (Test-Path $csprojPath) {
        [xml]$csproj = Get-Content $csprojPath
        $propertyGroup = $csproj.Project.PropertyGroup | Where-Object { $_.Version -ne $null } | Select-Object -First 1
        
        if ($propertyGroup) {
            $version = $propertyGroup.Version
            $assemblyVersion = $propertyGroup.AssemblyVersion
            $fileVersion = $propertyGroup.FileVersion
            
            Write-Host "  $($serviceName.PadRight(20)) : $version (Assembly: $assemblyVersion, File: $fileVersion)" -ForegroundColor White
        } else {
            Write-Host "  $($serviceName.PadRight(20)) : Version not found" -ForegroundColor Gray
        }
    } else {
        Write-Host "  $($serviceName.PadRight(20)) : Project file not found" -ForegroundColor Gray
    }
}

# WebUI Services
Write-Host "`nWebUI Applications:" -ForegroundColor Yellow
$webUIServices = @("Mng.Ui", "MngDomainUI")

foreach ($serviceName in $webUIServices) {
    $packageJsonPath = "$serviceName\package.json"
    
    if (Test-Path $packageJsonPath) {
        $packageJson = Get-Content $packageJsonPath | ConvertFrom-Json
        
        if ($packageJson.version) {
            Write-Host "  $($serviceName.PadRight(20)) : $($packageJson.version)" -ForegroundColor White
        } else {
            Write-Host "  $($serviceName.PadRight(20)) : Version not found" -ForegroundColor Gray
        }
    } else {
        Write-Host "  $($serviceName.PadRight(20)) : package.json not found" -ForegroundColor Gray
    }
}

Write-Host ""
