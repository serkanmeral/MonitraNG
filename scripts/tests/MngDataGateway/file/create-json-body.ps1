$base64File = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "test_image_base64.txt"
$outputFile = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "test_image_upload_body.json"

if (Test-Path $base64File) {
    $base64 = Get-Content $base64File -Raw -Encoding UTF8
    $base64 = $base64.Trim()
    
    Write-Host "Base64 loaded: $($base64.Length) characters"
    
    # Create JSON body
    $body = @{
        title = "Test Image Upload"
        documentFile = @{
            content = $base64
            folder = "test/images"
            useCompression = $true
            useEncryption = $true
        }
        amount = 100.0
    }
    
    # Convert to JSON with proper formatting
    $json = $body | ConvertTo-Json -Depth 10
    
    # Save to file
    $json | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "JSON body saved to: $outputFile" -ForegroundColor Green
    Write-Host "JSON length: $($json.Length) characters" -ForegroundColor Gray
    Write-Host ""
    Write-Host "First 200 chars of JSON:" -ForegroundColor Yellow
    Write-Host $json.Substring(0, [Math]::Min(200, $json.Length))
} else {
    Write-Host "Base64 file not found: $base64File" -ForegroundColor Red
}
