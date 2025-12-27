# Fix paths in test files to point to auth folder
$baseDir = $PSScriptRoot
$authDir = Join-Path $baseDir "auth"

# Find all .ps1 files except those in auth folder
$testFiles = Get-ChildItem -Path $baseDir -Filter "*.ps1" -Recurse -File | 
    Where-Object { $_.FullName -notlike "*\auth\*" -and $_.Name -ne "fix-paths.ps1" -and $_.Name -ne "move-tests.ps1" }

Write-Host "Found $($testFiles.Count) files to update" -ForegroundColor Cyan

foreach ($file in $testFiles) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $updated = $false
    
    # Calculate relative path to auth folder
    $relativePath = "..\auth"
    $fileDepth = ($file.FullName.Substring($baseDir.Length + 1) -split '\\').Count - 1
    if ($fileDepth -gt 0) {
        $relativePath = ("..\" * $fileDepth) + "auth"
    }
    
    # Fix get-token.ps1 references in Join-Path
    if ($content -like "*Join-Path*get-token.ps1*") {
        $content = $content -replace 'Join-Path \$scriptPath "get-token\.ps1"', "Join-Path `$scriptPath `"$relativePath\get-token.ps1`""
        $content = $content -replace "Join-Path `$scriptPath 'get-token\.ps1'", "Join-Path `$scriptPath '$relativePath\get-token.ps1'"
        $updated = $true
    }
    
    # Fix load-token.ps1 references in Join-Path
    if ($content -like "*Join-Path*load-token.ps1*") {
        $content = $content -replace 'Join-Path \$scriptPath "load-token\.ps1"', "Join-Path `$scriptPath `"$relativePath\load-token.ps1`""
        $content = $content -replace "Join-Path `$scriptPath 'load-token\.ps1'", "Join-Path `$scriptPath '$relativePath\load-token.ps1'"
        $updated = $true
    }
    
    # Fix direct path references (./ or .\)
    if ($content -like "*./get-token.ps1*" -or $content -like "*.\get-token.ps1*") {
        $content = $content -replace '\./get-token\.ps1', "$relativePath\get-token.ps1"
        $content = $content -replace '\.\\get-token\.ps1', "$relativePath\get-token.ps1"
        $updated = $true
    }
    if ($content -like "*./load-token.ps1*" -or $content -like "*.\load-token.ps1*") {
        $content = $content -replace '\./load-token\.ps1', "$relativePath\load-token.ps1"
        $content = $content -replace '\.\\load-token\.ps1', "$relativePath\load-token.ps1"
        $updated = $true
    }
    
    if ($updated) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "Updated: $($file.Name)" -ForegroundColor Green
    }
}

Write-Host "`nPath fixing complete!" -ForegroundColor Green
