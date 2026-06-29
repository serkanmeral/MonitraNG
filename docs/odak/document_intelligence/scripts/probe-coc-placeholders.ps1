# COC-STANDARD placeholder envanteri (test/prod)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$TemplateCode = "COC-STANDARD"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$isProd = $BaseUrl -match "192\.168\.20\.8"
$load = if ($isProd) {
    Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
} else {
    Join-Path $scriptDir "..\..\operationcore\scripts\load-operationcore-token.ps1"
}
$token = & $load
$h = @{ Authorization = "Bearer $($token.Trim())" }
$docs = "$BaseUrl/documents/api/v1"

$tree = Invoke-RestMethod -Uri "$docs/template-categories/tree" -Headers $h
function Find-CocCat($nodes) {
    foreach ($n in @($nodes)) {
        if ($n.name -match 'CoC|Uygunluk') { return $n.id }
        if ($n.children) { $r = Find-CocCat $n.children; if ($r) { return $r } }
    }
    return $null
}
$catId = Find-CocCat $tree
$list = Invoke-RestMethod -Uri "$docs/templates?categoryId=$catId" -Headers $h
$tpl = $list.items | Where-Object { $_.code -eq $TemplateCode } | Select-Object -First 1
if (-not $tpl) { throw "Template not found: $TemplateCode" }

Write-Host "Template: $($tpl.name) id=$($tpl.id) status=$($tpl.status)" -ForegroundColor Cyan
$detail = Invoke-RestMethod -Uri "$docs/templates/$($tpl.id)" -Headers $h
Write-Host "Saved parameters: $($detail.parameters.Count) primaryContext=$($detail.primaryContextType)"

$struct = Invoke-RestMethod -Uri "$docs/templates/$($tpl.id)/source/structure" -Headers $h
Write-Host "Scanned placeholders: $($struct.placeholders.Count)" -ForegroundColor $(if ($struct.placeholders.Count -gt 0) { 'Green' } else { 'Red' })
foreach ($w in @($struct.placeholderWarnings)) { Write-Host "WARN: $w" -ForegroundColor Yellow }
foreach ($p in @($struct.placeholders)) { Write-Host "  $($p.key) x$($p.occurrenceCount)" }

# Download DOCX and local scan hint
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$session = Invoke-RestMethod -Uri "$docs/templates/$($tpl.id)/editor-session" -Headers $h
$wopiHost = if ($isProd) { "http://192.168.20.8:5095" } else { "http://192.168.20.20:5095" }
$getUrl = "$wopiHost/wopi/files/$($tpl.id)/contents?access_token=$([Uri]::EscapeDataString($session.accessToken))"
$tmp = Join-Path $env:TEMP "coc-probe-$($tpl.id).docx"
Invoke-WebRequest -Uri $getUrl -OutFile $tmp -UseBasicParsing
Write-Host "Downloaded: $tmp ($((Get-Item $tmp).Length) bytes)" -ForegroundColor Gray

$zip = [System.IO.Compression.ZipFile]::OpenRead($tmp)
$docXml = $zip.GetEntry('word/document.xml')
$sr = New-Object System.IO.StreamReader($docXml.Open())
$xml = $sr.ReadToEnd()
$sr.Close(); $zip.Dispose()
$doubleBrace = ([regex]::Matches($xml, '\{\{')).Count
$validPh = ([regex]::Matches($xml, '\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}')).Count
Write-Host "document.xml: '{{' count=$doubleBrace valid-placeholder matches=$validPh" -ForegroundColor Gray
if ($doubleBrace -gt 0 -and $validPh -eq 0) {
    Write-Host "=> Placeholders likely SPLIT across XML runs (Collabora/Word edit). Re-type as single token." -ForegroundColor Yellow
}
