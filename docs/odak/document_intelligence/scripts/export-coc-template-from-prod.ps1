# Prod COC-STANDARD DOCX'i WOPI uzerinden indirir (Collabora'daki guncel icerik).
#
# Kullanim:
#   .\export-coc-template-from-prod.ps1
#   .\export-coc-template-from-prod.ps1 -OutputDocx ..\sample\ODK-COC-prod-current.docx

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [string]$WopiHost = "http://192.168.20.8:5095",
    [string]$TemplateCode = "COC-STANDARD",
    [string]$Token = "",
    [string]$OutputDocx = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
if ([string]::IsNullOrWhiteSpace($OutputDocx)) {
    $OutputDocx = Join-Path $repoRoot "docs/odak/document_intelligence/sample/ODK-COC-prod-current.docx"
}

$tokenFile = Join-Path $env:TEMP 'operationcore_dg_token_prod.txt'
$token = $Token
if ([string]::IsNullOrEmpty($token) -and (Test-Path $tokenFile)) {
    $token = (Get-Content $tokenFile -Raw).Trim()
}
if ([string]::IsNullOrEmpty($token)) {
    $loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"
    if (Test-Path $loadTokenScript) { $token = & $loadTokenScript }
}
if ([string]::IsNullOrEmpty($token)) { throw "Token yok." }

$headers = @{ Authorization = "Bearer $($token.Trim())" }

function Get-AllCategories {
    param([object[]]$Nodes, [string]$Prefix = '')
    $all = @()
    foreach ($n in $Nodes) {
        $path = if ($Prefix) { "$Prefix / $($n.name)" } else { [string]$n.name }
        $all += [pscustomobject]@{ id = $n.id; path = $path }
        if ($n.children) { $all += Get-AllCategories -Nodes @($n.children) -Prefix $path }
    }
    return $all
}

$tree = Invoke-RestMethod "$BaseUrl/documents/api/v1/template-categories/tree" -Headers $headers
$cat = (Get-AllCategories -Nodes @($tree) | Where-Object { $_.path -like '*CoC*' -or $_.path -like '*Uygunluk*' } | Select-Object -First 1)
if (-not $cat) { throw "CoC kategorisi bulunamadi" }

$list = Invoke-RestMethod "$BaseUrl/documents/api/v1/templates?categoryId=$($cat.id)" -Headers $headers
$tpl = $list.items | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Sablon bulunamadi: $TemplateCode" }

$session = Invoke-RestMethod "$BaseUrl/documents/api/v1/templates/$($tpl.id)/editor-session" -Headers $headers
$getUrl = "$WopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
$bytes = Invoke-WebRequest -Uri $getUrl -Method GET -UseBasicParsing
[IO.File]::WriteAllBytes($OutputDocx, $bytes.Content)
Write-Host "OK export: $OutputDocx ($($bytes.Content.Length) bytes) id=$($tpl.id)" -ForegroundColor Green
