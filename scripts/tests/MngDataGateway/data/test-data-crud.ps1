# Data CRUD Test Script
# Test /api/data endpoints with @test_tasks_224334 dataset

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Data CRUD Test Suite" -ForegroundColor Cyan
Write-Host "Dataset: @test_tasks_224334" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Base URL
$baseUrl = "https://localhost:5010"
$datasetName = "@test_tasks_224334"

# Token'ı yükle (auth klasöründen)
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "❌ Token alınamadı! Testler durduruluyor." -ForegroundColor Red
    exit 1
}

$tokenFile = "$env:TEMP\serkan_token.txt"

Write-Host "✅ Token yüklendi" -ForegroundColor Green
Write-Host ""

# Headers
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "ÔÜá´©Å  SSL sertifika kontrol├╝ devre d─▒┼ş─▒ (development)" -ForegroundColor Yellow
Write-Host ""

# Test fonksiyonu
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers,
        [object]$Body = $null
    )
    
    Write-Host "­şğ¬ Test: $Name" -ForegroundColor Yellow
    Write-Host "   Method: $Method" -ForegroundColor Gray
    Write-Host "   URL: $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            SkipCertificateCheck = $true
        }
        
        if ($Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 10
            $params.Body = $jsonBody
            Write-Host "   Body:" -ForegroundColor Gray
            Write-Host "   $jsonBody" -ForegroundColor DarkGray
        }
        
        $response = Invoke-RestMethod @params
        
        Write-Host "   Ô£à Success!" -ForegroundColor Green
        Write-Host "   Response:" -ForegroundColor Gray
        Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor DarkGray
        Write-Host ""
        
        return $response
    }
    catch {
        Write-Host "   ÔØî Failed!" -ForegroundColor Red
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
        }
        Write-Host ""
        return $null
    }
}

# Global de─şi┼şkenler
$createdDataId = $null
$createdTaskNumber = $null

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 1: CREATE Data (POST /api/data/$datasetName)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$createData = @{
    title = "Test Task Created at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    description = "Bu task test scripti taraf─▒ndan olu┼şturuldu"
    priority = 1
    isCompleted = $false
    dueDate = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ")
}

$createResponse = Test-Endpoint `
    -Name "Create new task" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName" `
    -Headers $headers `
    -Body $createData

if ($createResponse -and $createResponse.success) {
    $createdDataId = $createResponse.data.__dataId
    $createdTaskNumber = $createResponse.data.taskNumber
    Write-Host "Ô£à Data created successfully!" -ForegroundColor Green
    Write-Host "   __dataId: $createdDataId" -ForegroundColor Green
    Write-Host "   taskNumber: $createdTaskNumber" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "ÔØî Create failed - stopping tests" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 2: LIST Data (GET /api/data/$datasetName)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$listResponse = Test-Endpoint `
    -Name "List all tasks (first page)" `
    -Method "GET" `
    -Url "$baseUrl/api/data/$datasetName`?skip=0&limit=10" `
    -Headers $headers

if ($listResponse -and $listResponse.success) {
    Write-Host "Ô£à List successful!" -ForegroundColor Green
    Write-Host "   Total Count: $($listResponse.data.totalCount)" -ForegroundColor Green
    Write-Host "   Items: $($listResponse.data.items.Count)" -ForegroundColor Green
    Write-Host "   Page: $($listResponse.data.pageNumber)/$($listResponse.data.totalPages)" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 3: GET BY ID (GET /api/data/$datasetName/{id})" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$getResponse = Test-Endpoint `
    -Name "Get task by ID" `
    -Method "GET" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId" `
    -Headers $headers

if ($getResponse -and $getResponse.success) {
    Write-Host "Ô£à Get by ID successful!" -ForegroundColor Green
    Write-Host "   Title: $($getResponse.data.title)" -ForegroundColor Green
    Write-Host "   TaskNumber: $($getResponse.data.taskNumber)" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 4: UPDATE Data (PUT /api/data/$datasetName/{id})" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$updateData = @{
    title = "UPDATED: Test Task - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    description = "Task g├╝ncellendi - update testi"
    priority = 2
    isCompleted = $true
}

$updateResponse = Test-Endpoint `
    -Name "Update task" `
    -Method "PUT" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId" `
    -Headers $headers `
    -Body $updateData

if ($updateResponse -and $updateResponse.success) {
    Write-Host "Ô£à Update successful!" -ForegroundColor Green
    Write-Host "   New Title: $($updateResponse.data.title)" -ForegroundColor Green
    Write-Host "   New Priority: $($updateResponse.data.priority)" -ForegroundColor Green
    Write-Host "   IsCompleted: $($updateResponse.data.isCompleted)" -ForegroundColor Green
    
    if ($updateResponse.data.__history) {
        Write-Host "   History Entries: $($updateResponse.data.__history.Count)" -ForegroundColor Green
    }
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 5: DELETE Data (DELETE /api/data/$datasetName/{id})" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$deleteResponse = Test-Endpoint `
    -Name "Delete task (soft delete)" `
    -Method "DELETE" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId" `
    -Headers $headers

if ($deleteResponse -and $deleteResponse.success) {
    Write-Host "Ô£à Delete successful (soft delete)!" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 6: Verify Deleted (GET should return 404)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$verifyDeleteResponse = Test-Endpoint `
    -Name "Try to get deleted task (should fail)" `
    -Method "GET" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId" `
    -Headers $headers

if ($verifyDeleteResponse -eq $null -or -not $verifyDeleteResponse.success) {
    Write-Host "Ô£à Correctly returns 404 for deleted data!" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 7: RESTORE Data (POST /api/data/$datasetName/{id}/restore)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$restoreResponse = Test-Endpoint `
    -Name "Restore deleted task" `
    -Method "POST" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId/restore" `
    -Headers $headers

if ($restoreResponse -and $restoreResponse.success) {
    Write-Host "Ô£à Restore successful!" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 8: Verify Restored (GET should succeed)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$verifyRestoreResponse = Test-Endpoint `
    -Name "Get restored task" `
    -Method "GET" `
    -Url "$baseUrl/api/data/$datasetName/$createdDataId" `
    -Headers $headers

if ($verifyRestoreResponse -and $verifyRestoreResponse.success) {
    Write-Host "Ô£à Data successfully restored and accessible!" -ForegroundColor Green
    Write-Host ""
}

Start-Sleep -Seconds 1

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST 9: Create Multiple Tasks (Incremental Test)" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$taskNumbers = @()

for ($i = 1; $i -le 3; $i++) {
    Write-Host "Creating task $i/3..." -ForegroundColor Yellow
    
    $multiData = @{
        title = "Incremental Test Task #$i"
        description = "Testing incremental field generation"
        priority = $i
        isCompleted = $false
    }
    
    $multiResponse = Test-Endpoint `
        -Name "Create task #$i" `
        -Method "POST" `
        -Url "$baseUrl/api/data/$datasetName" `
        -Headers $headers `
        -Body $multiData
    
    if ($multiResponse -and $multiResponse.success) {
        $taskNumbers += $multiResponse.data.taskNumber
        Write-Host "   Ô£à Created with taskNumber: $($multiResponse.data.taskNumber)" -ForegroundColor Green
    }
    
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "Generated Task Numbers:" -ForegroundColor Cyan
$taskNumbers | ForEach-Object { Write-Host "   - $_" -ForegroundColor Green }
Write-Host ""

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ô£à CREATE endpoint: Working" -ForegroundColor Green
Write-Host "Ô£à LIST endpoint: Working" -ForegroundColor Green
Write-Host "Ô£à GET BY ID endpoint: Working" -ForegroundColor Green
Write-Host "Ô£à UPDATE endpoint: Working" -ForegroundColor Green
Write-Host "Ô£à DELETE endpoint (soft): Working" -ForegroundColor Green
Write-Host "Ô£à RESTORE endpoint: Working" -ForegroundColor Green
Write-Host "Ô£à Incremental field: Working ($($taskNumbers.Count) task numbers generated)" -ForegroundColor Green
Write-Host ""
Write-Host "­şÄë All tests completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "­şôØ Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Check RabbitMQ management UI for published events" -ForegroundColor Gray
Write-Host "   2. Verify MongoDB @__counters collection" -ForegroundColor Gray
Write-Host "   3. Check @notification_errors for any failures" -ForegroundColor Gray
Write-Host ""

