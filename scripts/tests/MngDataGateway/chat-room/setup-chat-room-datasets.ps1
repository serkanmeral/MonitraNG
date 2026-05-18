# Chat Room (cht_*) Datasets Setup — F2 oncesi DG semalari
# Gateway: -BaseUrl "https://localhost:5040" -UseGateway (varsayilan)
# DG direkt: -BaseUrl "http://localhost:5010" -UseGateway:$false
#
# - Tum dataset'ler "chat_room_datasets" dataset kategorisine baglanir (yoksa olusturulur).
# - Her dataset icin once GET ile varlik kontrolu; mevcut ise POST atlanir.
# - Token: ../auth/load-token.ps1 (Task Manager setup-task-manager-datasets.ps1 ile ayni mantik).
#
# 5 dataset: cht_topic_rooms, cht_topic_members, cht_direct_conversations, cht_group_chats, cht_messages
# Seed yok (ortam Keycloak / Keeper id gerektirir).
#
# Ref: docs/content/chat_room/CHAT_ROOM_ROADMAP.md §3.1b
#      scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1

param(
    [string]$BaseUrl = "https://localhost:5040",
    [switch]$UseGateway = $true
)
$datasetsPath   = if ($UseGateway) { "/data/api/v1/datasets" } else { "/api/v1/datasets" }
$categoriesPath = if ($UseGateway) { "/data/api/v1/dataset-categories" } else { "/api/v1/dataset-categories" }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = Get-Location }
$loadTokenScript = Join-Path $scriptPath "..\auth\load-token.ps1"

if (-not (Test-Path $loadTokenScript)) {
    Write-Host "load-token.ps1 bulunamadi! Path: $loadTokenScript" -ForegroundColor Red
    exit 1
}

$token = (& $loadTokenScript)
if (-not [string]::IsNullOrEmpty($token)) { $token = $token.Trim() }
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token alinamadi! auth/get-token.ps1 ile token alin (domain claim gerekli)." -ForegroundColor Red
    exit 1
}
# Fonksiyon icinde kapsam sorunlari olmamasi icin script-scope tasiyici
$script:__ChatRoomDgBearer = $token

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}

$useCurl = $BaseUrl.StartsWith("https://") -and (Get-Command curl.exe -ErrorAction SilentlyContinue)
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { param($a,$b,$c,$d) $true }

$ChatRoomCategoryName = "chat_room_datasets"
$ChatRoomCategoryDescription = "MonitraNG Chat Room: cht_* (DM, konu, grup, mesajlar)"

function Invoke-DgJsonGet {
    param([string]$Uri)
    $bt = $script:__ChatRoomDgBearer
    if ([string]::IsNullOrEmpty($bt)) {
        Write-Host "  [Ic hata] Bearer token bos (script scope)." -ForegroundColor Red
        return @{ StatusCode = 0; Object = $null; Raw = $null }
    }
    try {
        $iwrParams = @{
            Uri               = $Uri
            Headers           = @{ Authorization = "Bearer $bt"; Accept = "application/json" }
            UseBasicParsing   = $true
        }
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $iwrParams['SkipCertificateCheck'] = $true
        }
        $r = Invoke-WebRequest @iwrParams
        $obj = $null
        if ($r.Content) {
            try { $obj = $r.Content | ConvertFrom-Json } catch { $obj = $null }
        }
        return @{ StatusCode = [int]$r.StatusCode; Object = $obj; Raw = $null }
    } catch {
        $code = 0
        try {
            $resp = $_.Exception.Response
            if ($null -ne $resp) { $code = [int]$resp.StatusCode }
        } catch { }
        if ($code -eq 0) {
            Write-Host "  [GET] $($_.Exception.Message)" -ForegroundColor DarkYellow
        }
        return @{ StatusCode = $code; Object = $null; Raw = $null }
    }
}

function Get-CategoryListItems {
    param($listObj)
    if ($null -eq $listObj) { return @() }
    if ($listObj.items) { return @($listObj.items) }
    if ($listObj.Items) { return @($listObj.Items) }
    if ($listObj.data) { return @($listObj.data) }
    if ($listObj.Data) { return @($listObj.Data) }
    return @()
}

function Get-RecordDataId {
    param($obj)
    if ($null -eq $obj) { return $null }
    foreach ($p in @('dataId', 'DataId', '__dataId')) {
        if ($obj.$p) { return [string]$obj.$p }
    }
    return $null
}

function Test-DatasetExists {
    param([string]$DatasetName)
    $encoded = [System.Uri]::EscapeDataString($DatasetName)
    $uri = "$BaseUrl$datasetsPath/$encoded"
    $r = Invoke-DgJsonGet -Uri $uri
    if ($r.StatusCode -eq 200) {
        return $true
    }
    if ($r.StatusCode -eq 404) {
        return $false
    }
    Write-Host "  HATA: '$DatasetName' varlik kontrolu HTTP $($r.StatusCode)" -ForegroundColor Red
    return $null
}

function Ensure-ChatRoomDatasetCategoryId {
    # PS 7+: $categoriesPath? ... ternary ile karisir; ${} kullan
    $listUri = "${BaseUrl}${categoriesPath}?pageNumber=1&pageSize=100"
    Write-Host "Kategori listeleniyor: $ChatRoomCategoryName" -ForegroundColor DarkGray
    $r = Invoke-DgJsonGet -Uri $listUri
    if ($r.StatusCode -ne 200) {
        Write-Host "HATA: dataset-categories listesi alinamadi (HTTP $($r.StatusCode))" -ForegroundColor Red
        return $null
    }
    $items = Get-CategoryListItems $r.Object
    foreach ($it in $items) {
        $n = if ($it.categoryName) { $it.categoryName } else { $it.CategoryName }
        if ($n -eq $ChatRoomCategoryName) {
            $id = Get-RecordDataId $it
            if ($id) {
                Write-Host "Kategori mevcut: $ChatRoomCategoryName ($id)" -ForegroundColor Green
                return $id
            }
        }
    }

    Write-Host "Kategori yok; olusturuluyor: $ChatRoomCategoryName" -ForegroundColor Yellow
    $createUri = "$BaseUrl$categoriesPath"
    $body = (@{
            CategoryName        = $ChatRoomCategoryName
            CategoryDescription = $ChatRoomCategoryDescription
        } | ConvertTo-Json -Depth 5 -Compress)

    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = (& curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $($script:__ChatRoomDgBearer)" -H "Content-Type: application/json" -d "@$bodyFile" $createUri 2>$null | Out-String)
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]', '').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                try {
                    $created = $responseBody | ConvertFrom-Json
                    $id = Get-RecordDataId $created
                    if ($id) {
                        Write-Host "Kategori olusturuldu: $id" -ForegroundColor Green
                        return $id
                    }
                } catch { }
                Write-Host "HATA: Kategori yaniti cozulemedi" -ForegroundColor Red
                if ($responseBody) { Write-Host $responseBody -ForegroundColor Gray }
                return $null
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already|exists|zaten")) {
                $r2 = Invoke-DgJsonGet -Uri $listUri
                $items2 = Get-CategoryListItems $r2.Object
                foreach ($it in $items2) {
                    $n = if ($it.categoryName) { $it.categoryName } else { $it.CategoryName }
                    if ($n -eq $ChatRoomCategoryName) {
                        $id = Get-RecordDataId $it
                        if ($id) {
                            Write-Host "Kategori (yarismadan sonra): $id" -ForegroundColor Yellow
                            return $id
                        }
                    }
                }
            }
            Write-Host "HATA: Kategori olusturma HTTP $httpCode" -ForegroundColor Red
            if ($responseBody) { Write-Host $responseBody -ForegroundColor Gray }
            return $null
        } catch {
            Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
            return $null
        }
    }

    try {
        $irmParams = @{ Uri = $createUri; Method = "POST"; Headers = $headers; Body = $body }
        if (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }) {
            $irmParams.SkipCertificateCheck = $true
        }
        $created = Invoke-RestMethod @irmParams
        $id = Get-RecordDataId $created
        if ($id) {
            Write-Host "Kategori olusturuldu: $id" -ForegroundColor Green
            return $id
        }
    } catch {
        Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    }
    return $null
}

function Invoke-CreateDataset {
    param([string]$Name, [object]$Schema)
    $uri = "$BaseUrl$datasetsPath"
    $body = $Schema | ConvertTo-Json -Depth 20 -Compress
    if ($useCurl) {
        try {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            $body | Out-File -FilePath $bodyFile -Encoding utf8 -NoNewline
            $output = (& curl.exe -s -k -w "`n%{http_code}" -X POST -H "Authorization: Bearer $($script:__ChatRoomDgBearer)" -H "Content-Type: application/json" -d "@$bodyFile" $uri 2>$null | Out-String)
            Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
            $lines = ($output.Trim() -split "`n")
            $httpCode = if ($lines.Count -ge 1) { ($lines[-1] -replace '[^\d]', '').Trim() } else { "" }
            $responseBody = if ($lines.Count -gt 1) { ($lines[0..($lines.Count - 2)] -join "`n").Trim() } else { "" }
            if ($httpCode -eq "200" -or $httpCode -eq "201") {
                Write-Host "  $Name olusturuldu" -ForegroundColor Green
                return $true
            }
            if ($httpCode -eq "409" -or ($httpCode -eq "400" -and $responseBody -match "mevcut|already exists|zaten")) {
                Write-Host "  $Name zaten mevcut (POST)" -ForegroundColor Yellow
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
            Write-Host "  $Name zaten mevcut (POST)" -ForegroundColor Yellow
            return $true
        }
        Write-Host "  HATA: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host "  $($_.ErrorDetails.Message)" -ForegroundColor Gray }
        return $false
    }
}

function Invoke-EnsureChatDataset {
    param([string]$Name, [hashtable]$Schema, [string]$CategoryId)
    $exists = Test-DatasetExists -DatasetName $Name
    if ($null -eq $exists) { return $false }
    if ($exists) {
        Write-Host "  $Name zaten mevcut (GET, atlandi)" -ForegroundColor Yellow
        return $true
    }
    $Schema['Category'] = $CategoryId
    return Invoke-CreateDataset -Name $Name -Schema $Schema
}

Write-Host "`nChat Room (cht_*) Datasets - Kategori + dataset'ler`n" -ForegroundColor Cyan

$categoryId = Ensure-ChatRoomDatasetCategoryId
if ([string]::IsNullOrEmpty($categoryId)) {
    Write-Host "Kategori id alinamadi, cikiliyor." -ForegroundColor Red
    exit 1
}

# 1 cht_topic_rooms (kok + yan dal: parentTopicRoomId)
Write-Host "`n1 cht_topic_rooms" -ForegroundColor Yellow
$schema = @{
    Name        = "cht_topic_rooms"
    Description = "Chat Room - Konu / yan dal odalari"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "text"; name = "title"; title = "Baslik"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "description"; title = "Aciklama"; mandatory = $false; isArray = $false },
        @{ fieldType = "persons"; name = "ownerPersonId"; title = "Konuyu / dali acan (yonetici)"; mandatory = $true; isArray = $false },
        @{ fieldType = "relation"; name = "parentTopicRoomId"; title = "Ust konu (yan dal)"; mandatory = $false; relationDataset = "cht_topic_rooms"; isArray = $false },
        @{ fieldType = "bool"; name = "archived"; title = "Arsivlendi"; mandatory = $false; defaultValue = $false; isArray = $false },
        @{ fieldType = "datetime"; name = "createdAt"; title = "Olusturma"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "updatedAt"; title = "Guncelleme"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_parentTopicRoomId"; fields = @{ parentTopicRoomId = 1 }; unique = $false },
        @{ name = "idx_ownerPersonId"; fields = @{ ownerPersonId = 1 }; unique = $false }
    )
}
if (-not (Invoke-EnsureChatDataset -Name "cht_topic_rooms" -Schema $schema -CategoryId $categoryId)) { exit 1 }

# 2 cht_topic_members (yalniz kok konu uyeleri)
Write-Host "`n2 cht_topic_members" -ForegroundColor Yellow
$schema = @{
    Name        = "cht_topic_members"
    Description = "Chat Room - Kok konu uyeleri"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "relation"; name = "topicRoomId"; title = "Kok konu odasi"; mandatory = $true; relationDataset = "cht_topic_rooms"; isArray = $false },
        @{ fieldType = "persons"; name = "memberPersonId"; title = "Uye"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "role"; title = "Rol (member)"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "joinedAt"; title = "Katilim"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_topicRoomId"; fields = @{ topicRoomId = 1 }; unique = $false },
        @{ name = "idx_topic_member_unique"; fields = @{ topicRoomId = 1; memberPersonId = 1 }; unique = $true }
    )
}
if (-not (Invoke-EnsureChatDataset -Name "cht_topic_members" -Schema $schema -CategoryId $categoryId)) { exit 1 }

# 3 cht_direct_conversations
Write-Host "`n3 cht_direct_conversations" -ForegroundColor Yellow
$schema = @{
    Name        = "cht_direct_conversations"
    Description = "Chat Room - Birebir konusmalar"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "text"; name = "canonicalKey"; title = "Iki katilimcinin sirali birlesik anahtari"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "participantAId"; title = "Katilimci A (Keeper / sub id)"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "participantBId"; title = "Katilimci B"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "lastMessageAt"; title = "Son mesaj"; mandatory = $false; isArray = $false },
        @{ fieldType = "datetime"; name = "createdAt"; title = "Olusturma"; mandatory = $true; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_canonicalKey"; fields = @{ canonicalKey = 1 }; unique = $true }
    )
}
if (-not (Invoke-EnsureChatDataset -Name "cht_direct_conversations" -Schema $schema -CategoryId $categoryId)) { exit 1 }

# 4 cht_group_chats (Keycloak grup eslemesi)
Write-Host "`n4 cht_group_chats" -ForegroundColor Yellow
$schema = @{
    Name        = "cht_group_chats"
    Description = "Chat Room - Keycloak grup sohbet odasi"
    ForceSchema = $true
    Logging     = "none"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "text"; name = "keycloakGroupId"; title = "Keycloak grup id"; mandatory = $true; unique = $true; isArray = $false },
        @{ fieldType = "text"; name = "displayNameCache"; title = "Grup adi (Keeper onbellek)"; mandatory = $false; isArray = $false },
        @{ fieldType = "datetime"; name = "createdAt"; title = "Olusturma"; mandatory = $true; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_keycloakGroupId"; fields = @{ keycloakGroupId = 1 }; unique = $true }
    )
}
if (-not (Invoke-EnsureChatDataset -Name "cht_group_chats" -Schema $schema -CategoryId $categoryId)) { exit 1 }

# 5 cht_messages
Write-Host "`n5 cht_messages" -ForegroundColor Yellow
$schema = @{
    Name        = "cht_messages"
    Description = "Chat Room - Mesajlar (direct|topic|group)"
    ForceSchema = $true
    Logging     = "self"
    PublishMode = "basic"
    Fields      = @(
        @{ fieldType = "text"; name = "roomKind"; title = "direct | topic | group"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "roomRecordId"; title = "Hedef kayit __dataId"; mandatory = $true; isArray = $false },
        @{ fieldType = "text"; name = "body"; title = "Metin (@[userId] mention)"; mandatory = $true; isArray = $false },
        @{ fieldType = "object"; name = "mentions"; title = "Mention meta (liste veya TM ile uyumlu)"; mandatory = $false; isArray = $false },
        @{ fieldType = "persons"; name = "authorPersonId"; title = "Yazar"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "createdAt"; title = "Olusturma"; mandatory = $true; isArray = $false },
        @{ fieldType = "datetime"; name = "updatedAt"; title = "Guncelleme"; mandatory = $false; isArray = $false }
    )
    IndexList   = @(
        @{ name = "idx_room_timeline"; fields = @{ roomKind = 1; roomRecordId = 1; createdAt = 1 }; unique = $false },
        @{ name = "idx_authorPersonId"; fields = @{ authorPersonId = 1 }; unique = $false }
    )
}
if (-not (Invoke-EnsureChatDataset -Name "cht_messages" -Schema $schema -CategoryId $categoryId)) { exit 1 }

Write-Host "`nChat Room dataset'leri tamam (kategori: $ChatRoomCategoryName).`n" -ForegroundColor Cyan
