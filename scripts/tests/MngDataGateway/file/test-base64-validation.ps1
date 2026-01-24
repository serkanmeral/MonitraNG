$base64File = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "test_image_base64.txt"

if (Test-Path $base64File) {
    $base64 = Get-Content $base64File -Raw
    $base64 = $base64.Trim()
    
    Write-Host "Base64 length: $($base64.Length) characters"
    Write-Host "First 50 chars: $($base64.Substring(0, [Math]::Min(50, $base64.Length)))"
    Write-Host "Last 50 chars: $($base64.Substring([Math]::Max(0, $base64.Length - 50)))"
    
    # Remove whitespace
    $cleanBase64 = $base64 -replace '\s+', ''
    Write-Host "After whitespace removal: $($cleanBase64.Length) characters"
    
    # Check regex
    $isValid = $cleanBase64 -match '^[A-Za-z0-9+/]*={0,2}$'
    Write-Host "Regex validation: $isValid"
    
    if (-not $isValid) {
        Write-Host "Invalid characters found!" -ForegroundColor Red
        $invalidChars = $cleanBase64 -replace '[A-Za-z0-9+/=]', ''
        if ($invalidChars.Length -gt 0) {
            Write-Host "Invalid characters: $($invalidChars.Substring(0, [Math]::Min(20, $invalidChars.Length)))" -ForegroundColor Red
        }
    }
    
    # Try to decode
    try {
        $bytes = [Convert]::FromBase64String($cleanBase64)
        Write-Host "Decode successful: $($bytes.Length) bytes" -ForegroundColor Green
    }
    catch {
        Write-Host "Decode failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Base64 file not found: $base64File" -ForegroundColor Red
}
