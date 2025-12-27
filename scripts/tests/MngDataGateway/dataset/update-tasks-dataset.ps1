# Update @tasks dataset with persons/personGroups fields
# This script updates the existing @tasks dataset to add persons/personGroups field types

param(
    [string]$BaseUrl = "https://localhost:5010",
    [string]$TokenFile = "$env:TEMP\serkan_token.txt"
)

# Colors
$InfoColor = "Cyan"
$SuccessColor = "Green"
$ErrorColor = "Red"
$WarningColor = "Yellow"

Write-Host "`n🔄 Updating @tasks Dataset with persons/personGroups Fields" -ForegroundColor $InfoColor
Write-Host "=" * 60 -ForegroundColor Gray

# 1. Get token
Write-Host "`n1️⃣ Getting authentication token..." -ForegroundColor $InfoColor
$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) {
    $scriptPath = Get-Location
}
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "   ❌ load-token.ps1 bulunamadı! Path: $loadTokenScript" -ForegroundColor $ErrorColor
    exit 1
}

$token = & $loadTokenScript

if ([string]::IsNullOrEmpty($token)) {
    Write-Host "   ❌ Token alınamadı!" -ForegroundColor $ErrorColor
    exit 1
}

$TokenFile = "$env:TEMP\serkan_token.txt"

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "   ✅ Token loaded" -ForegroundColor $SuccessColor

# 2. Get current dataset
Write-Host "`n2️⃣ Getting current @tasks dataset..." -ForegroundColor $InfoColor
try {
    $currentDataset = Invoke-RestMethod -Uri "$BaseUrl/api/datasets/@tasks" -Headers $headers -SkipCertificateCheck
    Write-Host "   ✅ Current dataset retrieved" -ForegroundColor $SuccessColor
    Write-Host "   Current fields: $($currentDataset.fields.Count)" -ForegroundColor Gray
} catch {
    Write-Host "   ❌ Failed to get dataset: $($_.Exception.Message)" -ForegroundColor $ErrorColor
    Write-Host $_.ErrorDetails.Message
    exit 1
}

# 3. Check if persons/personGroups fields already exist
$hasAssignUser = $currentDataset.fields | Where-Object { $_.name -eq "assign_user" }
$hasWatcherUsers = $currentDataset.fields | Where-Object { $_.name -eq "watcher_users" }
$hasSignedGroups = $currentDataset.fields | Where-Object { $_.name -eq "signedGroups" }

if ($hasAssignUser -and $hasWatcherUsers -and $hasSignedGroups) {
    Write-Host "`n⚠️  All persons/personGroups fields already exist!" -ForegroundColor $WarningColor
    Write-Host "   assign_user: ✅" -ForegroundColor Green
    Write-Host "   watcher_users: ✅" -ForegroundColor Green
    Write-Host "   signedGroups: ✅" -ForegroundColor Green
    Write-Host "`n   Skipping update..." -ForegroundColor Yellow
    exit 0
}

# 4. Add new fields to existing fields array
Write-Host "`n3️⃣ Adding persons/personGroups fields..." -ForegroundColor $InfoColor

$newFields = @()

# Add existing fields
foreach ($field in $currentDataset.fields) {
    $newFields += $field
}

# Add assign_user if not exists
if (-not $hasAssignUser) {
    $newFields += @{
        fieldType = "persons"
        name = "assign_user"
        title = "Assigned User"
        mandatory = $false
        isArray = $false
    }
    Write-Host "   ➕ Added: assign_user (persons, single)" -ForegroundColor Green
}

# Add watcher_users if not exists
if (-not $hasWatcherUsers) {
    $newFields += @{
        fieldType = "persons"
        name = "watcher_users"
        title = "Watcher Users"
        mandatory = $false
        isArray = $true
    }
    Write-Host "   ➕ Added: watcher_users (persons, array)" -ForegroundColor Green
}

# Add signedGroups if not exists
if (-not $hasSignedGroups) {
    $newFields += @{
        fieldType = "personGroups"
        name = "signedGroups"
        title = "Signed Groups"
        mandatory = $false
        isArray = $false
    }
    Write-Host "   ➕ Added: signedGroups (personGroups, single)" -ForegroundColor Green
}

# 5. Prepare update payload
$updatePayload = @{
    description = $currentDataset.description
    category = $currentDataset.category
    forceSchema = $currentDataset.forceSchema
    logging = $currentDataset.logging
    publish_mode = $currentDataset.publish_mode
    fields = $newFields
    validations = $currentDataset.validations
    queries = $currentDataset.queries
    indexList = $currentDataset.indexList
} | ConvertTo-Json -Depth 10

# 6. Update dataset
Write-Host "`n4️⃣ Updating @tasks dataset..." -ForegroundColor $InfoColor
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/datasets/@tasks" -Method PUT -Headers $headers -Body $updatePayload -SkipCertificateCheck
    Write-Host "   ✅ Dataset updated successfully!" -ForegroundColor $SuccessColor
    Write-Host "   Total fields: $($response.fields.Count)" -ForegroundColor Gray
    
    # Show new fields
    Write-Host "`n   New persons/personGroups fields:" -ForegroundColor Cyan
    $response.fields | Where-Object { $_.fieldType -in @("persons", "personGroups") } | ForEach-Object {
        $arrayInfo = if ($_.isArray) { "array" } else { "single" }
        Write-Host "   - $($_.name) ($($_.fieldType), $arrayInfo)" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ❌ Failed to update dataset: $($_.Exception.Message)" -ForegroundColor $ErrorColor
    Write-Host $_.ErrorDetails.Message
    exit 1
}

Write-Host "`n✅ @tasks dataset updated successfully!`n" -ForegroundColor $SuccessColor
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Make sure MngKeeper is running and users/groups are synced" -ForegroundColor Gray
Write-Host "2. Create test data with persons/personGroups fields" -ForegroundColor Gray
Write-Host "3. Test GET operations with expand=true to see expansion" -ForegroundColor Gray

