$imagePath = "C:\Users\serkan.meral\.cursor\projects\c-Serkan-iSIM-MonitraNG\assets\c__Users_serkan.meral_AppData_Roaming_Cursor_User_workspaceStorage_199304743e6958370214c104f9ac726d_images_images-e2786c95-a85a-43ee-acc0-ecc15c6d028b.png"

if (Test-Path $imagePath) {
    $bytes = [System.IO.File]::ReadAllBytes($imagePath)
    $base64 = [System.Convert]::ToBase64String($bytes)
    
    Write-Host "Base64 encoded image (first 100 chars): $($base64.Substring(0, [Math]::Min(100, $base64.Length)))..."
    Write-Host "Total length: $($base64.Length) characters"
    Write-Host ""
    Write-Host "Full base64 string:"
    Write-Host $base64
    
    # Save to file
    $outputPath = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "test_image_base64.txt"
    $base64 | Out-File -FilePath $outputPath -Encoding utf8 -NoNewline
    Write-Host ""
    Write-Host "Base64 saved to: $outputPath"
} else {
    Write-Host "File not found: $imagePath" -ForegroundColor Red
}
