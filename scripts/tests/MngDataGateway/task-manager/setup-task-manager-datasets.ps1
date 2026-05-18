# Task Manager Datasets Setup (Faz 1)
# DG Gateway: -BaseUrl "https://localhost:5040" -UseGateway (varsayilan)
# DG direkt: -BaseUrl "http://localhost:5010" -UseGateway:$false
#
# 10 dataset: tm_projects, tm_issue_types, tm_statuses, tm_priorities, tm_field_definitions,
#             tm_labels, tm_boards, tm_sprints, tm_issues, tm_issue_comments
# Seed: issue_types, statuses, priorities, field_definitions (alan havuzu), + asagidakiler
#
# Ref: docs/content/task_manager/TASK_MANAGER_PLANNING.md

param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)
$datasetsPath = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$dataPath     = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = Get-Location }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi! get-token.ps1 ile token alin (domain claim gerekli)." -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

function Invoke-CreateDataset {
    param([string]$Name, [object]$Schema)
    $uri = "$BaseUrl$datasetsPath"
    $body = $Schema | ConvertTo-Json -Depth 20 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  $Name olusturuldu" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already exists|zaten")) {
                Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
                return $true
            }
            Write-Host "  HATA: HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host "  $responseBody" -ForegroundColor Gray }
            return $false
        } catch {
            Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  $Name olusturuldu" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already exists|zaten")) {
            Write-Host "  $Name zaten mevcut" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        return $false
    }
}

function Invoke-SeedRecord {
    param([string]$DatasetName, [hashtable]$Record, [string]$Label)
    $uri = "$BaseUrl$dataPath/$DatasetName"
    $body = $Record | ConvertTo-Json -Depth 15 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = & curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>&1 | Out-String
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]','').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count-2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  Seed: $Label" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already exists|zaten|duplicate|unique")) {
                Write-Host "  Seed (atlandi): $Label" -ForegroundColor Yellow
                return $true
            }
            Write-Host "  HATA: $Label HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host "  $responseBody" -ForegroundColor Gray }
            return $false
        } catch {
            Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    try {
        $irmParams = @{ Uri = $uri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $null = Invoke-RestMethod @irmParams
        Write-Host "  Seed: $Label" -ForegroundColor Green
        return $true
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        $errMsg = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message } else { $_.Exception.Message }
        if ($statusCode -eq 409 -or ($statusCode -eq 400 -and $errMsg -match "mevcut|already exists|zaten|duplicate|unique")) {
            Write-Host "  Seed (atlandi): $Label" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $Label $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "`nTask Manager Datasets - Olusturuluyor...`n" -ForegroundColor Cyan

# 1 tm_projects
Write-Host "1 tm_projects" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_projects"
    Description = "Task Manager - Projeler"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Proje adi"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "key"; title = "Proje kodu (PROJ)"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "persons"; name = "lead"; title = "Proje lideri"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "avatarUrl"; title = "Avatar URL"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "permissions"; title = "Yetkiler (view, edit, admin)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "selections"; title = "Havuz secimleri (oncelik, tip, alan anahtarlari)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "workflow"; title = "Durum akisi (statusIds, initial, closed, transitions)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "issueCreateLayout"; title = "Yeni gorev formu: rows, columnSections, fieldCols, sectionOrder, sectionCols, dialogMaxWidth (px), ..."; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "issueCreateForms"; title = "Coklu yeni gorev formu sablonlari (id, name, layout)"; mandatory = $false; isArray = $true },
        @{ fieldType = "text"; name = "defaultIssueCreateFormId"; title = "Varsayilan yeni gorev formu sablon id"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "issueProfileLayout"; title = "Profil (tek nesne, legacy): issueCreateLayout ile ayni JSON"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "issueProfileForms"; title = "Coklu profil sablonlari (id, name, layout)"; mandatory = $false; isArray = $true },
        @{ fieldType = "text"; name = "defaultIssueProfileFormId"; title = "Varsayilan profil sablon id"; mandatory = $false; isArray = $false },
        @{ fieldType = "bool"; name = "useKanban"; title = "Kanban kullan"; mandatory = $false; defaultValue = $true; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_key"; fields = @{ key = 1 }; unique = $true },
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "tm_projects" $schema)) { exit 1 }

# 2 tm_issue_types (tm_projects sonrasi: tm_labels icin; bagimlilik yok ama seed sirasi icin once tablolar)
Write-Host "`n2 tm_issue_types" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_issue_types"
    Description = "Task Manager - Gorev tipleri"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Tip adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "icon"; title = "Ikon"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "color"; title = "Renk (#hex)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_name"; fields = @{ name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "tm_issue_types" $schema)) { exit 1 }

# 3 tm_statuses
Write-Host "`n3 tm_statuses" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_statuses"
    Description = "Task Manager - Durumlar"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Durum adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "icon"; title = "Ikon (Tabler adi, or. CircleDotIcon)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "color"; title = "Tema rengi (primary|secondary|success|info|warning|error)"; mandatory = $false; isArray = $false }
    )
    IndexList   = @()
}
if (-not (Invoke-CreateDataset "tm_statuses" $schema)) { exit 1 }

# 4 tm_priorities
Write-Host "`n4 tm_priorities" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_priorities"
    Description = "Task Manager - Oncelikler"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Oncelik adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "icon"; title = "Ikon"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "color"; title = "Renk"; mandatory = $false; isArray = $false }
    )
    IndexList   = @()
}
if (-not (Invoke-CreateDataset "tm_priorities" $schema)) { exit 1 }

# 5 tm_labels
Write-Host "`n5 tm_labels" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_labels"
    Description = "Task Manager - Etiketler"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Etiket adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "color"; title = "Renk (#hex)"; mandatory = $false; isArray = $false },
        @{ fieldType = "relation"; name = "projectId"; title = "Proje (UI: yalnizca bu projeye bagli etiketler)"; mandatory = $false; relationDataset = "tm_projects"; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_projectId_name"; fields = @{ projectId = 1; name = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "tm_labels" $schema)) { exit 1 }

# 6 tm_boards
Write-Host "`n6 tm_boards" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_boards"
    Description = "Task Manager - Board tanimlari (Kanban/Sprint)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Board adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "projectId"; title = "Proje"; mandatory = $true; relationDataset = "tm_projects"; isArray = $false },
        @{ fieldType = "text"; name = "type"; title = "Tip (kanban|scrum)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "config"; title = "Board config (kolonlar vb.)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "issueCreateFormId"; title = "Yeni gorev formu sablon id (bos=proje varsayilani)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "issueProfileFormId"; title = "Profil tam sayfa sablon id (bos=proje varsayilani)"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_projectId"; fields = @{ projectId = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "tm_boards" $schema)) { exit 1 }

# 7 tm_sprints
Write-Host "`n7 tm_sprints" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_sprints"
    Description = "Task Manager - Sprint tanimlari (Scrum)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "name"; title = "Sprint adi"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "boardId"; title = "Board"; mandatory = $true; relationDataset = "tm_boards"; isArray = $false },
        @{ fieldType = "datetime"; name = "startDate"; title = "Baslangic"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "endDate"; title = "Bitis"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "goal"; title = "Sprint hedefi"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "state"; title = "Durum (future|active|closed)"; mandatory = $true; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_boardId_state"; fields = @{ boardId = 1; state = 1 }; unique = $false },
        @{ name = "idx_boardId_startDate"; fields = @{ boardId = 1; startDate = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "tm_sprints" $schema)) { exit 1 }

# 8 tm_field_definitions (gorev alan havuzu — salt okunur liste; proje baglama sonraki adim)
Write-Host "`n8 tm_field_definitions" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_field_definitions"
    Description = "Task Manager - Alan havuzu (tm_issues alan meta)"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "none"
    Fields      = @(
        @{ fieldType = "text"; name = "key"; title = "Alan anahtari (tm_issues)"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "label"; title = "Gorunen ad"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "fieldType"; title = "Tip (text|number|date|datetime|bool|relation|persons|group|tags|file|incremental|array...)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "scope"; title = "Kapsam (core|pool)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "cardinality"; title = "Secim (single|multi)"; mandatory = $false; isArray = $false },
        @{ fieldType = "text"; name = "optionsJson"; title = "Secenekler (JSON: min,max, maxFiles, relationDataset...)"; mandatory = $false; isArray = $false },
        @{ fieldType = "number"; name = "sortOrder"; title = "Liste sirasi"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_key"; fields = @{ key = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "tm_field_definitions" $schema)) { exit 1 }

# 9 tm_issues
Write-Host "`n9 tm_issues" -ForegroundColor Yellow
$incKey = @{
    fieldType         = "incremental"
    name              = "key"
    title             = "Gorev kodu"
    mandatory         = $true
    unique            = $true
    isArray           = $false
    incrementalOptions = @{
        format         = "{projectKey}-{0:D4}"
        startValue     = 1
        incrementStep  = 1
    }
}
$schema = @{
    Name        = "tm_issues"
    Description = "Task Manager - Gorevler"
    ForceSchema = $true
    Logging     = "self"
    PublishMode = "basic"
    Fields      = @(
        $incKey,
        @{ fieldType = "text"; name = "projectKey"; title = "Proje kodu (key uretimi)"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "projectId"; title = "Proje"; mandatory = $true; relationDataset = "tm_projects"; isArray = $false },
        @{ fieldType = "relation"; name = "issueTypeId"; title = "Gorev tipi"; mandatory = $true; relationDataset = "tm_issue_types"; isArray = $false },
        @{ fieldType = "text"; name = "title"; title = "Baslik"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "relation"; name = "statusId"; title = "Durum"; mandatory = $true; relationDataset = "tm_statuses"; isArray = $false },
        @{ fieldType = "relation"; name = "priorityId"; title = "Oncelik"; mandatory = $false; relationDataset = "tm_priorities"; isArray = $false },
        @{ fieldType = "persons"; name = "assignee"; title = "Atanan"; mandatory = $false; isArray = $false },
        @{ fieldType = "relation"; name = "epicId"; title = "Epic"; mandatory = $false; relationDataset = "tm_issues"; isArray = $false },
        @{ fieldType = "relation"; name = "sprintId"; title = "Sprint"; mandatory = $false; relationDataset = "tm_sprints"; isArray = $false },
        @{ fieldType = "relation"; name = "labels"; title = "Etiketler"; mandatory = $false; relationDataset = "tm_labels"; isArray = $true },
        @{ fieldType = "datetime"; name = "dueDate"; title = "Bitis tarihi"; mandatory = $false; isArray = $false },
        @{ fieldType = "number"; name = "storyPoints"; title = "Story points"; mandatory = $false; isArray = $false },
        @{ fieldType = "number"; name = "order"; title = "Sira (board kolonu icinde)"; mandatory = $false; isArray = $false },
        @{ fieldType = "object"; name = "__history"; title = "Alan degisiklik gunlugu (changedAt, changedBy, changes[]: field, oldValue, newValue)"; mandatory = $false; isArray = $true }
    )
    IndexList   = @(
        @{ name = "idx_projectId"; fields = @{ projectId = 1 }; unique = $false },
        @{ name = "idx_statusId"; fields = @{ statusId = 1 }; unique = $false },
        @{ name = "idx_assignee"; fields = @{ assignee = 1 }; unique = $false },
        @{ name = "idx_sprintId"; fields = @{ sprintId = 1 }; unique = $false },
        @{ name = "idx_epicId"; fields = @{ epicId = 1 }; unique = $false },
        @{ name = "idx_projectId_key"; fields = @{ projectId = 1; key = 1 }; unique = $true }
    )
}
if (-not (Invoke-CreateDataset "tm_issues" $schema)) { exit 1 }

# 10 tm_issue_comments (gorev yorumlari, yanitlar, @mention metin: @[userId])
Write-Host "`n10 tm_issue_comments" -ForegroundColor Yellow
$schema = @{
    Name        = "tm_issue_comments"
    Description = "Task Manager - Gorev yorumlari"
    ForceSchema = $true
    Logging     = "self"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "relation"; name = "issueId"; title = "Gorev"; mandatory = $true; relationDataset = "tm_issues"; isArray = $false },
        @{ fieldType = "relation"; name = "projectId"; title = "Proje"; mandatory = $true; relationDataset = "tm_projects"; isArray = $false },
        @{ fieldType = "persons"; name = "author"; title = "Yazar"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "body"; title = "Icerik (emoji, @[userId] mention)"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "parentCommentId"; title = "Ust yorum (yanit)"; mandatory = $false; relationDataset = "tm_issue_comments"; isArray = $false },
        @{ fieldType = "datetime"; name = "createdAt"; title = "Olusturma"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "updatedAt"; title = "Guncelleme"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_issueId"; fields = @{ issueId = 1 }; unique = $false },
        @{ name = "idx_projectId"; fields = @{ projectId = 1 }; unique = $false }
    )
}
if (-not (Invoke-CreateDataset "tm_issue_comments" $schema)) { exit 1 }

# --- Seed ---
Write-Host "`nSeed: tm_issue_types" -ForegroundColor Yellow
Invoke-SeedRecord "tm_issue_types" (@{ name = "Task"; icon = "task"; color = "#4A90D9"; description = "Standard task" }) "Task" | Out-Null
Invoke-SeedRecord "tm_issue_types" (@{ name = "Bug"; icon = "bug"; color = "#E5484D"; description = "Defect" }) "Bug" | Out-Null
Invoke-SeedRecord "tm_issue_types" (@{ name = "Story"; icon = "story"; color = "#49A27B"; description = "User story" }) "Story" | Out-Null
Invoke-SeedRecord "tm_issue_types" (@{ name = "Epic"; icon = "epic"; color = "#8E4EC6"; description = "Epic" }) "Epic" | Out-Null

Write-Host "`nSeed: tm_statuses" -ForegroundColor Yellow
Invoke-SeedRecord "tm_statuses" (@{ name = "To Do"; icon = "CircleDotIcon"; color = "secondary" }) "To Do" | Out-Null
Invoke-SeedRecord "tm_statuses" (@{ name = "In Progress"; icon = "ProgressIcon"; color = "info" }) "In Progress" | Out-Null
Invoke-SeedRecord "tm_statuses" (@{ name = "Done"; icon = "CircleCheckIcon"; color = "success" }) "Done" | Out-Null

Write-Host "`nSeed: tm_priorities" -ForegroundColor Yellow
Invoke-SeedRecord "tm_priorities" (@{ name = "Highest"; icon = "arrow-up"; color = "#DC2626" }) "Highest" | Out-Null
Invoke-SeedRecord "tm_priorities" (@{ name = "High"; icon = "chevron-up"; color = "#EA580C" }) "High" | Out-Null
Invoke-SeedRecord "tm_priorities" (@{ name = "Medium"; icon = "minus"; color = "#CA8A04" }) "Medium" | Out-Null
Invoke-SeedRecord "tm_priorities" (@{ name = "Low"; icon = "chevron-down"; color = "#64748B" }) "Low" | Out-Null
Invoke-SeedRecord "tm_priorities" (@{ name = "Lowest"; icon = "arrow-down"; color = "#94A3B8" }) "Lowest" | Out-Null

Write-Host "`nSeed: tm_field_definitions" -ForegroundColor Yellow
function Add-FieldDef {
    param(
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$FieldType,
        [Parameter(Mandatory = $true)][string]$Scope,
        [string]$Description = "",
        [Parameter(Mandatory = $true)][int]$SortOrder,
        [string]$Cardinality = "single",
        [string]$OptionsJson = $null
    )
    $rec = @{
        key         = $Key
        label       = $Label
        fieldType   = $FieldType
        scope       = $Scope
        description = $Description
        sortOrder   = $SortOrder
    }
    if ($Cardinality) { $rec.cardinality = $Cardinality }
    if ($null -ne $OptionsJson -and $OptionsJson -ne "") { $rec.optionsJson = $OptionsJson }
    Invoke-SeedRecord "tm_field_definitions" $rec $Key | Out-Null
}
Add-FieldDef -Key "key" -Label "Gorev kodu" -FieldType "incremental" -Scope "core" -Description "Ornek PROJ-0001; tum projelerde" -SortOrder 10 -Cardinality "single" | Out-Null
Add-FieldDef -Key "projectKey" -Label "Proje kodu (uretim)" -FieldType "text" -Scope "core" -Description "incremental key icin" -SortOrder 20 | Out-Null
Add-FieldDef -Key "projectId" -Label "Proje" -FieldType "relation" -Scope "core" -Description "tm_projects" -SortOrder 30 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_projects"}' | Out-Null
Add-FieldDef -Key "issueTypeId" -Label "Gorev tipi" -FieldType "relation" -Scope "core" -Description "tm_issue_types" -SortOrder 40 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_issue_types"}' | Out-Null
Add-FieldDef -Key "title" -Label "Baslik" -FieldType "text" -Scope "core" -Description "Zorunlu baslik" -SortOrder 50 | Out-Null
Add-FieldDef -Key "description" -Label "Aciklama" -FieldType "text" -Scope "core" -Description "Metin aciklama" -SortOrder 60 | Out-Null
Add-FieldDef -Key "statusId" -Label "Durum" -FieldType "relation" -Scope "core" -Description "tm_statuses" -SortOrder 70 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_statuses"}' | Out-Null
Add-FieldDef -Key "priorityId" -Label "Oncelik" -FieldType "relation" -Scope "pool" -Description "tm_priorities; proje baglamasi sonraki adim" -SortOrder 80 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_priorities"}' | Out-Null
Add-FieldDef -Key "assignee" -Label "Atanan" -FieldType "persons" -Scope "pool" -Description "MngKeeper person" -SortOrder 90 -Cardinality "single" | Out-Null
Add-FieldDef -Key "epicId" -Label "Epic" -FieldType "relation" -Scope "pool" -Description "tm_issues self-reference" -SortOrder 100 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_issues"}' | Out-Null
Add-FieldDef -Key "sprintId" -Label "Sprint" -FieldType "relation" -Scope "pool" -Description "tm_sprints" -SortOrder 110 -Cardinality "single" -OptionsJson '{"relationDataset":"tm_sprints"}' | Out-Null
Add-FieldDef -Key "labels" -Label "Etiketler" -FieldType "tags" -Scope "pool" -Description "tm_labels; coklu secim" -SortOrder 120 -Cardinality "multi" -OptionsJson '{"relationDataset":"tm_labels"}' | Out-Null
Add-FieldDef -Key "dueDate" -Label "Bitis tarihi" -FieldType "datetime" -Scope "pool" -Description "-" -SortOrder 130 | Out-Null
Add-FieldDef -Key "storyPoints" -Label "Story point" -FieldType "number" -Scope "pool" -Description "-" -SortOrder 140 -OptionsJson '{"min":0,"max":100}' | Out-Null
Add-FieldDef -Key "order" -Label "Sira (board)" -FieldType "number" -Scope "pool" -Description "Kanban kolon icinde" -SortOrder 150 | Out-Null

Write-Host "`nTask Manager datasets tamamlandi (10 dataset + seed)." -ForegroundColor Green
Write-Host "Oneri: 'Task Manager' dataset kategorisi olusturup tm_* dataset'leri altina alin." -ForegroundColor Gray
Write-Host ""
