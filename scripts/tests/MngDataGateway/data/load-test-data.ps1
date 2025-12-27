# Load Test Data Script
# Populates test datasets with sample data

$baseUrl = "https://localhost:5010"

# Token'ı yükle (ortak script kullanarak)
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

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Ignore SSL certificate errors
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

Write-Host "`n📦 Loading test data...`n" -ForegroundColor Cyan

# 1. Load task_states
Write-Host "1️⃣ Loading task_states data..." -ForegroundColor Yellow
$taskStates = @(
    @{ name = "To Do" },
    @{ name = "In Progress" },
    @{ name = "Review" },
    @{ name = "Done" },
    @{ name = "Cancelled" }
)

$taskStatesIds = @()
foreach ($state in $taskStates) {
    try {
        $body = $state | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_states" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        $taskStatesIds += $response.data.__dataId
        Write-Host "  ✅ Created: $($state.name)" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to create $($state.name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n2️⃣ Loading task_types data..." -ForegroundColor Yellow
$taskTypes = @(
    @{ name = "Bug" },
    @{ name = "Feature" },
    @{ name = "Enhancement" },
    @{ name = "Documentation" },
    @{ name = "Refactoring" },
    @{ name = "Testing" }
)

$taskTypesIds = @()
foreach ($type in $taskTypes) {
    try {
        $body = $type | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_types" -Method POST -Headers $headers -Body $body
        $taskTypesIds += $response.data.__dataId
        Write-Host "  ✅ Created: $($type.name)" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to create $($type.name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n3️⃣ Loading task_priorities data..." -ForegroundColor Yellow
$taskPriorities = @(
    @{ name = "Low" },
    @{ name = "Medium" },
    @{ name = "High" },
    @{ name = "Critical" }
)

$taskPrioritiesIds = @()
foreach ($priority in $taskPriorities) {
    try {
        $body = $priority | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_priorities" -Method POST -Headers $headers -Body $body
        $taskPrioritiesIds += $response.data.__dataId
        Write-Host "  ✅ Created: $($priority.name)" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to create $($priority.name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n4️⃣ Loading tasks data..." -ForegroundColor Yellow

# Get IDs from API (if arrays are empty, fetch from API)
if ($taskStatesIds.Count -eq 0) {
    try {
        $statesResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_states" -Headers $headers -SkipCertificateCheck
        $taskStatesIds = $statesResponse.data.items | ForEach-Object { $_.__dataId }
    } catch {
        Write-Host "  ⚠️  Could not fetch task_states IDs" -ForegroundColor Yellow
    }
}

if ($taskTypesIds.Count -eq 0) {
    try {
        $typesResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_types" -Headers $headers -SkipCertificateCheck
        $taskTypesIds = $typesResponse.data.items | ForEach-Object { $_.__dataId }
    } catch {
        Write-Host "  ⚠️  Could not fetch task_types IDs" -ForegroundColor Yellow
    }
}

if ($taskPrioritiesIds.Count -eq 0) {
    try {
        $prioritiesResponse = Invoke-RestMethod -Uri "$baseUrl/api/data/@task_priorities" -Headers $headers -SkipCertificateCheck
        $taskPrioritiesIds = $prioritiesResponse.data.items | ForEach-Object { $_.__dataId }
    } catch {
        Write-Host "  ⚠️  Could not fetch task_priorities IDs" -ForegroundColor Yellow
    }
}

# Get IDs from arrays (we'll use the first few)
$toDoStateId = $taskStatesIds | Where-Object { $_ } | Select-Object -First 1
$inProgressStateId = $taskStatesIds | Where-Object { $_ } | Select-Object -Skip 1 -First 1
$doneStateId = $taskStatesIds | Where-Object { $_ } | Select-Object -Skip 3 -First 1

$bugTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -First 1
$featureTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -Skip 1 -First 1
$enhancementTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -Skip 2 -First 1
$documentationTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -Skip 3 -First 1
$refactoringTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -Skip 4 -First 1
$testingTypeId = $taskTypesIds | Where-Object { $_ } | Select-Object -Skip 5 -First 1

$lowPriorityId = $taskPrioritiesIds | Where-Object { $_ } | Select-Object -First 1
$mediumPriorityId = $taskPrioritiesIds | Where-Object { $_ } | Select-Object -Skip 1 -First 1
$highPriorityId = $taskPrioritiesIds | Where-Object { $_ } | Select-Object -Skip 2 -First 1
$criticalPriorityId = $taskPrioritiesIds | Where-Object { $_ } | Select-Object -Skip 3 -First 1

$tasks = @(
    @{
        title = "Fix login bug"
        description = "Users cannot login with special characters"
        task_state = $toDoStateId
        task_types = @($bugTypeId, $enhancementTypeId)
        task_priority = $highPriorityId
        priority_value = 8
        isCompleted = $false
        dueDate = (Get-Date).AddDays(3).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Add user dashboard"
        description = "Create new dashboard for users"
        task_state = $inProgressStateId
        task_types = @($featureTypeId)
        task_priority = $mediumPriorityId
        priority_value = 5
        isCompleted = $false
        dueDate = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Update API documentation"
        description = "Document new endpoints"
        task_state = $toDoStateId
        task_types = @($documentationTypeId) # Documentation
        task_priority = $lowPriorityId
        priority_value = 2
        isCompleted = $false
        dueDate = (Get-Date).AddDays(14).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Refactor authentication service"
        description = "Improve code quality"
        task_state = $inProgressStateId
        task_types = @($enhancementTypeId, $refactoringTypeId) # Refactoring
        task_priority = $mediumPriorityId
        priority_value = 6
        isCompleted = $false
        dueDate = (Get-Date).AddDays(5).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Critical security patch"
        description = "Fix security vulnerability"
        task_state = $toDoStateId
        task_types = @($bugTypeId)
        task_priority = $criticalPriorityId
        priority_value = 10
        isCompleted = $false
        dueDate = (Get-Date).AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Write unit tests"
        description = "Add tests for new features"
        task_state = $toDoStateId
        task_types = @($testingTypeId) # Testing
        task_priority = $mediumPriorityId
        priority_value = 4
        isCompleted = $false
        dueDate = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ssZ")
    },
    @{
        title = "Completed task example"
        description = "This task is already done"
        task_state = $doneStateId
        task_types = @($featureTypeId)
        task_priority = $lowPriorityId
        priority_value = 1
        isCompleted = $true
        dueDate = (Get-Date).AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
)

$taskCount = 0
foreach ($task in $tasks) {
    try {
        $body = $task | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$baseUrl/api/data/@tasks" -Method POST -Headers $headers -Body $body -SkipCertificateCheck
        $taskCount++
        Write-Host "  ✅ Created task: $($task.title) (Task Number: $($response.data.task_number))" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to create task '$($task.title)': $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "  Request body: $body" -ForegroundColor Gray
    }
}

Write-Host "`n✅ Test data loaded successfully!" -ForegroundColor Green
Write-Host "   - Task States: $($taskStatesIds.Count)" -ForegroundColor Cyan
Write-Host "   - Task Types: $($taskTypesIds.Count)" -ForegroundColor Cyan
Write-Host "   - Task Priorities: $($taskPrioritiesIds.Count)" -ForegroundColor Cyan
Write-Host "   - Tasks: $taskCount" -ForegroundColor Cyan
Write-Host ""

