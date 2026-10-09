# ORNEK-PMO hakedis demo. Cafe proline. Uretim Odak ve monitrang.com reddedilir.
# Bütçe satırına bağlanmaz; eldeki plan/gerçekleşen tutarlar durur.
#
#   pwsh .\docs\odak\project_management\scripts\seed-progress.ps1 -Gateway http://192.168.1.42:5040
param(
    [string]$Gateway = "http://192.168.1.42:5040",
    [string]$ProjectCode = "ORNEK-PMO",
    [string]$TokenFile = "$env:TEMP\proline_hakedis_token.txt"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if ($Gateway -match "192\.168\.20\.8" -or $Gateway -match "monitrang\.com") {
    throw "Uretim Odak ve monitrang.com icin calistirilmaz."
}
if (-not (Test-Path $TokenFile)) { throw "Token yok: $TokenFile" }
$token = (Get-Content $TokenFile -Raw).Trim()
if (-not $token) { throw "Token bos." }

$headers = @{ Authorization = "Bearer $token"; Accept = "application/json" }
$ops = "$Gateway/operations/api/v1"

function Invoke-Ops {
    param(
        [string]$Method = "GET",
        [string]$Path,
        $Body = $null,
        [int[]]$Expect = @(200, 201, 204)
    )
    $status = 0
    $p = @{
        Uri                  = "$ops$Path"
        Method               = $Method
        Headers              = $headers
        TimeoutSec           = 90
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $p.ContentType = "application/json; charset=utf-8"
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 8 -Compress))
    }
    $result = Invoke-RestMethod @p
    if ($Expect -notcontains [int]$status) {
        $err = $null
        try { $err = $result | ConvertTo-Json -Compress -Depth 6 } catch { $err = [string]$result }
        throw "HTTP $status $Method $Path : $err"
    }
    return $result
}

function Day([string]$ymd) { return "${ymd}T00:00:00.000Z" }

$projects = @(Invoke-Ops -Path "/projects")
$project = $projects | Where-Object { $_.code -eq $ProjectCode } | Select-Object -First 1
if (-not $project) { throw "Proje yok: $ProjectCode" }
$projectId = [string]$project.id
Write-Host "Hakedis seed  $ProjectCode  $projectId" -ForegroundColor Cyan

$detail = Invoke-Ops -Path "/projects/$projectId"
$wbs = @($detail.wbs)
$gates = @($detail.stageGates)
function Wbs([string]$code) {
    $hit = $wbs | Where-Object { $_.wbsCode -eq $code } | Select-Object -First 1
    if (-not $hit) { throw "WBS $code yok." }
    return [string]$hit.id
}
$gatePassed = [string](($gates | Where-Object { $_.status -eq "passed" } | Select-Object -First 1).id)
$gateOpen = [string](($gates | Where-Object { $_.status -eq "open" } | Select-Object -First 1).id)
if (-not $gatePassed -or -not $gateOpen) { throw "Gecmis ve acik kapi bekleniyor." }

$docs = @(Invoke-Ops -Path "/projects/$projectId/documents")
function Doc([string]$name) {
    $hit = $docs | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if (-not $hit) { throw "Belge yok: $name" }
    return [string]$hit.id
}
$docKickoff = Doc "Kick-off tutanağı"
$docPlan = Doc "Proje planı"
$docStatus = Doc "Durum özeti"
$docDelivery = Doc "Teslimat listesi"

Invoke-Ops -Method PUT -Path "/projects/$projectId/progress/terms" -Body @{
    baseAmount         = 1000000
    currency           = "TRY"
    penaltyCapPercent  = 35
} | Out-Null

function Get-Progress { return Invoke-Ops -Path "/projects/$projectId/progress" }

function Ensure-Slice([string]$name, $body) {
    $pack = Get-Progress
    $found = @($pack.slices) | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($found) {
        Write-Host "  dilim var  $name" -ForegroundColor DarkGray
        return $found
    }
    $created = Invoke-Ops -Method POST -Path "/projects/$projectId/progress/slices" -Body $body
    Write-Host "  dilim  $name" -ForegroundColor Yellow
    return $created
}

Ensure-Slice "Kick-off" @{
    name = "Kick-off"; kind = "percent"; percent = 10; cadence = "once"
    gateId = $gatePassed; wbsId = (Wbs "1.1"); anchorDate = (Day "2026-09-01")
    note = "Kapsam kapisi gecti."
} | Out-Null
Ensure-Slice "Proje planı" @{
    name = "Proje planı"; kind = "percent"; percent = 20; cadence = "once"
    wbsId = (Wbs "2.1"); anchorDate = (Day "2026-09-15")
} | Out-Null
Ensure-Slice "Yürütme" @{
    name = "Yürütme"; kind = "percent"; percent = 40; cadence = "installments"
    installmentCount = 4; intervalMonths = 3
    wbsId = (Wbs "3.1"); anchorDate = (Day "2026-10-01")
    note = "Uc ayda bir. Kapi bagli degil."
} | Out-Null
Ensure-Slice "Kapanış" @{
    name = "Kapanış"; kind = "percent"; percent = 30; cadence = "once"
    gateId = $gateOpen; wbsId = (Wbs "4.2"); anchorDate = (Day "2026-12-15")
    note = "Kapi acikken kabul olmaz."
} | Out-Null
Ensure-Slice "Ek danışmanlık" @{
    name = "Ek danışmanlık"; kind = "unit"; unitPrice = 25000
    wbsId = (Wbs "3.2"); note = "Yuzdeye girmez."
} | Out-Null

$steps = @("draft", "submitted", "accepted", "paid")

function Set-Claim([string]$sliceName, [int]$sequence, [string]$target, $fields) {
    $pack = Get-Progress
    $slice = @($pack.slices) | Where-Object { $_.name -eq $sliceName } | Select-Object -First 1
    if (-not $slice) { throw "Dilim yok: $sliceName" }
    $claim = @($pack.claims) | Where-Object { $_.sliceId -eq $slice.id -and [int]$_.sequence -eq $sequence } | Select-Object -First 1
    if (-not $claim) { throw "Donem yok: $sliceName #$sequence" }
    $current = [string]$claim.status
    $from = [array]::IndexOf($steps, $current)
    $to = [array]::IndexOf($steps, $target)
    if ($from -lt 0 -or $to -lt 0) { throw "Durum yok: $current -> $target" }
    if ($from -gt $to) {
        Write-Host "  donem atlandi  $sliceName #$sequence ($current)" -ForegroundColor DarkGray
        return
    }
    $body = @{}
    foreach ($k in $fields.Keys) { $body[$k] = $fields[$k] }
    for ($i = $from; $i -le $to; $i++) {
        $body.status = $steps[$i]
        Invoke-Ops -Method PUT -Path "/progress/claims/$($claim.id)" -Body $body | Out-Null
    }
    Write-Host "  donem  $sliceName #$sequence -> $target" -ForegroundColor Green
}

Set-Claim "Kick-off" 1 "paid" @{
    periodLabel = "Kick-off"; claimedAmount = 100000; acceptedAmount = 100000
    deduction = 5000; adjustmentAmount = 0; quantity = 0
    resourceIds = @($docKickoff); note = "Kesinti 5.000."
}
Set-Claim "Proje planı" 1 "submitted" @{
    periodLabel = "Proje planı"; claimedAmount = 200000; acceptedAmount = 200000
    deduction = 0; adjustmentAmount = 0; quantity = 0
    resourceIds = @($docPlan); note = "Sunuldu, henuz kabul degil."
}
Set-Claim "Yürütme" 1 "submitted" @{
    periodLabel = "Yürütme 1/4"; claimedAmount = 100000; acceptedAmount = 100000
    deduction = 0; adjustmentAmount = 0; quantity = 0
    resourceIds = @($docStatus)
}
Set-Claim "Kapanış" 1 "submitted" @{
    periodLabel = "Kapanış"; claimedAmount = 300000; acceptedAmount = 300000
    deduction = 0; adjustmentAmount = 0; quantity = 0
    resourceIds = @($docDelivery); note = "Kapi acik. Kabul bekler."
}
Set-Claim "Ek danışmanlık" 1 "draft" @{
    periodLabel = "Ek danışmanlık"; claimedAmount = 50000; acceptedAmount = 0
    deduction = 0; adjustmentAmount = 0; quantity = 2
    resourceIds = @()
}

$done = Get-Progress
Write-Host ("Taban {0} {1}  kabul/odenen net {2}  kesinti {3}  dilim {4}  donem {5}" -f `
    $done.terms.baseAmount, $done.terms.currency, $done.paidNet, $done.deducted, @($done.slices).Count, @($done.claims).Count) -ForegroundColor Cyan
