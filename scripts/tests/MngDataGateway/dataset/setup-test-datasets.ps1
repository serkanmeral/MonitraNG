# Test Datasets Setup Script
# Creates 4 datasets: task_states, task_types, task_priorities, tasks

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

Write-Host "`n🚀 Setting up test datasets...`n" -ForegroundColor Cyan

# 1. Create task_states dataset
Write-Host "1️⃣ Creating task_states dataset..." -ForegroundColor Yellow
$taskStatesSchema = @{
    Name = "@task_states"
    Description = "Task states dataset"
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "name"
            title = "State Name"
            mandatory = $true
            unique = $true
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $taskStatesSchema -SkipCertificateCheck
    Write-Host "✅ task_states dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  task_states dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create task_states: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
        }
    }
}

# 2. Create task_types dataset
Write-Host "`n2️⃣ Creating task_types dataset..." -ForegroundColor Yellow
$taskTypesSchema = @{
    Name = "@task_types"
    Description = "Task types dataset"
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "name"
            title = "Type Name"
            mandatory = $true
            unique = $true
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $taskTypesSchema -SkipCertificateCheck
    Write-Host "✅ task_types dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  task_types dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create task_types: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 3. Create task_priorities dataset
Write-Host "`n3️⃣ Creating task_priorities dataset..." -ForegroundColor Yellow
$taskPrioritiesSchema = @{
    Name = "@task_priorities"
    Description = "Task priorities dataset"
    ForceSchema = $true
    Logging = "none"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "text"
            name = "name"
            title = "Priority Name"
            mandatory = $true
            unique = $true
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $taskPrioritiesSchema -SkipCertificateCheck
    Write-Host "✅ task_priorities dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  task_priorities dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create task_priorities: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 4. Create tasks dataset with relations and incremental field
Write-Host "`n4️⃣ Creating tasks dataset..." -ForegroundColor Yellow
$tasksSchema = @{
    Name = "@tasks"
    Description = "Tasks dataset with relations"
    ForceSchema = $true
    Logging = "self"
    PublishMode = "none"
    Fields = @(
        @{
            fieldType = "incremental"
            name = "task_number"
            title = "Task Number"
            mandatory = $true
            unique = $true
            incrementalOptions = @{
                format = "TASK-{0:D6}"
                startValue = 1
                incrementStep = 1
            }
        },
        @{
            fieldType = "text"
            name = "title"
            title = "Task Title"
            mandatory = $true
        },
        @{
            fieldType = "text"
            name = "description"
            title = "Task Description"
            mandatory = $false
        },
        @{
            fieldType = "relation"
            name = "task_state"
            title = "Task State"
            mandatory = $true
            relationDataset = "@task_states"
            isArray = $false
        },
        @{
            fieldType = "relation"
            name = "task_types"
            title = "Task Types"
            mandatory = $false
            relationDataset = "@task_types"
            isArray = $true
        },
        @{
            fieldType = "relation"
            name = "task_priority"
            title = "Task Priority"
            mandatory = $true
            relationDataset = "@task_priorities"
            isArray = $false
        },
        @{
            fieldType = "number"
            name = "priority_value"
            title = "Priority Value"
            mandatory = $false
        },
        @{
            fieldType = "bool"
            name = "isCompleted"
            title = "Is Completed"
            mandatory = $false
            defaultValue = $null
        },
        @{
            fieldType = "datetime"
            name = "dueDate"
            title = "Due Date"
            mandatory = $false
        },
        @{
            fieldType = "persons"
            name = "assign_user"
            title = "Assigned User"
            mandatory = $false
            isArray = $false
        },
        @{
            fieldType = "persons"
            name = "watcher_users"
            title = "Watcher Users"
            mandatory = $false
            isArray = $true
        },
        @{
            fieldType = "personGroups"
            name = "signedGroups"
            title = "Signed Groups"
            mandatory = $false
            isArray = $false
        }
    )
    Queries = @(
        @{
            name = "high_priority_tasks"
            description = "Get tasks by priority range"
            pipeline = @(
                @{
                    "$match" = @{
                        priority_value = @{
                            "$gte" = ":minPriority"
                        }
                    }
                },
                @{
                    "$sort" = @{
                        priority_value = -1
                        task_number = 1
                    }
                }
            )
            parameters = @("minPriority")
        }
    )
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/datasets" -Method POST -Headers $headers -Body $tasksSchema -SkipCertificateCheck
    Write-Host "✅ tasks dataset created" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "⚠️  tasks dataset already exists" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create tasks: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host $_.ErrorDetails.Message
    }
}

Write-Host "`n✅ All datasets created successfully!`n" -ForegroundColor Green

