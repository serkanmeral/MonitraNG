# TEST-only: SEED-PMO process map demo. Do not run against prod.
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$ProjectId = "027bdf17-6741-4001-835f-9c1412c42f21"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = $PSScriptRoot
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"))) {
    $parent = Split-Path $repoRoot -Parent
    if (-not $parent -or $parent -eq $repoRoot) { break }
    $repoRoot = $parent
}
if (-not (Test-Path (Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"))) {
    $repoRoot = "C:\Users\monitra\Dev\MonitraNG\MonitraNG"
}
$load = Join-Path $repoRoot "docs\odak\operationcore\scripts\load-operationcore-token.ps1"

$token = (& $load -AutoRefresh).ToString().Trim()
$headers = @{ Authorization = "Bearer $token"; Accept = "application/json" }
$ops = "$Gateway/operations/api/v1"
$doc = "$Gateway/documents/api/v1/resources"

function Invoke-Json {
    param(
        [string]$Uri,
        [string]$Method = "GET",
        $Body = $null,
        [int[]]$Expect = @(200, 201, 204)
    )
    $status = 0
    $p = @{
        Uri                  = $Uri
        Method               = $Method
        Headers              = $headers
        TimeoutSec           = 90
        SkipCertificateCheck = $true
        SkipHttpErrorCheck   = $true
        StatusCodeVariable   = "status"
    }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $p.ContentType = "application/json; charset=utf-8"
        $p.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
    }
    $result = Invoke-RestMethod @p
    if ($Expect -notcontains [int]$status) {
        $err = $null
        try { $err = $result | ConvertTo-Json -Compress -Depth 8 } catch { $err = [string]$result }
        throw "HTTP $status $Method $Uri : $err"
    }
    return $result
}

function Find-Wbs($items, [string]$code) {
    $hit = @($items) | Where-Object { $_.wbsCode -eq $code } | Select-Object -First 1
    if (-not $hit) { throw "WBS $code bulunamadi." }
    return $hit
}

function Find-Folder([string]$parentId, [string[]]$names) {
    $kids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($parentId))&limit=100"
    foreach ($name in $names) {
        $found = @($kids.items) | Where-Object {
            $_.type -eq "folder" -and (($_.name -eq $name) -or ($_.title -eq $name))
        } | Select-Object -First 1
        if ($found) { return [string]$found.id }
    }
    return $null
}

function Ensure-Drawio([string]$parentId, [string]$name, [string]$xml) {
    $kids = Invoke-Json "$doc/children?parentId=$([uri]::EscapeDataString($parentId))&limit=100"
    $found = @($kids.items) | Where-Object {
        (($_.name -eq $name) -or ($_.title -eq $name) -or ($_.originalFileName -eq $name))
    } | Select-Object -First 1
    if ($found) { return [string]$found.id }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($xml)
    $created = Invoke-Json "$doc/file" "POST" @{
        parentId         = $parentId
        name             = $name
        originalFileName = $name
        mimeType         = "application/vnd.jgraph.mxfile"
        extension        = "drawio"
        kind             = "diagram"
        content          = [Convert]::ToBase64String($bytes)
    }
    return [string]$created.id
}

$sahaV1Xml = @'
<mxfile host="app.diagrams.net" agent="SEED-PMO" version="22.1.0">
  <diagram id="saha-v1" name="Saha kabul v1">
    <mxGraphModel dx="800" dy="400" grid="1" gridSize="10" page="1" pageWidth="600" pageHeight="300">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="n1" value="Saha teslim" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="40" y="80" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="n2" value="Kabul" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="280" y="80" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="e1" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n1" target="n2">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
'@

$sahaXml = @'
<mxfile host="app.diagrams.net" agent="SEED-PMO" version="22.1.0">
  <diagram id="saha-kabul" name="Saha kabul">
    <mxGraphModel dx="1000" dy="600" grid="1" gridSize="10" page="1" pageWidth="800" pageHeight="400">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="n1" value="Teslimat geldi" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="40" y="140" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="n2" value="Saha kontrol" style="rhombus;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="260" y="125" width="140" height="90" as="geometry"/>
        </mxCell>
        <mxCell id="n3" value="Kabul" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="500" y="40" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="n4" value="NCR / red" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f8cecc;strokeColor=#b85450;" vertex="1" parent="1">
          <mxGeometry x="500" y="240" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="e1" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n1" target="n2">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
        <mxCell id="e2" value="uygun" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n2" target="n3">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
        <mxCell id="e3" value="uygunsuz" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n2" target="n4">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
'@

$pmoXml = @'
<mxfile host="app.diagrams.net" agent="SEED-PMO" version="22.1.0">
  <diagram id="pmo-ritim" name="PMO ritim">
    <mxGraphModel dx="800" dy="400" grid="1" gridSize="10" page="1" pageWidth="700" pageHeight="280">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="n1" value="Haftalik PMO" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="40" y="80" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="n2" value="Tutanak" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="260" y="80" width="140" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="n3" value="Aksiyon / OC isi" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="480" y="80" width="160" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="e1" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n1" target="n2">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
        <mxCell id="e2" style="edgeStyle=orthogonalEdgeStyle;endArrow=block;endFill=1;" edge="1" parent="1" source="n2" target="n3">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
'@

Write-Host "SEED-PMO process map seed  project=$ProjectId" -ForegroundColor Cyan

$detail = Invoke-Json "$ops/projects/$ProjectId"
$wbs = @($detail.wbs)
$project = $detail.project
if (-not $project) { $project = $detail }
if ($wbs.Count -eq 0) { throw "WBS bos." }
$wbs11 = Find-Wbs $wbs "1.1"
$wbs32 = Find-Wbs $wbs "3.2"
$hubId = [string]$project.diFolderId
if (-not $hubId) { throw "Proje diFolderId yok." }

$folderId = Find-Folder $hubId @("Yüklemeler", "Yuklemeler", "Wiki")
if (-not $folderId) { throw "Kutuphane klasoru yok (Yuklemeler/Wiki)." }
Write-Host "Library folder=$folderId  WBS 1.1=$($wbs11.id)  3.2=$($wbs32.id)"

$sahaV1Id = Ensure-Drawio $folderId "Saha kabul akisi v1 (DEMO).drawio" $sahaV1Xml
$sahaId = Ensure-Drawio $folderId "Saha kabul akisi (DEMO).drawio" $sahaXml
$pmoId = Ensure-Drawio $folderId "PMO ritim proseduru (DEMO).drawio" $pmoXml
Write-Host "Docs v1=$sahaV1Id current=$sahaId pmo=$pmoId"

$pack = Invoke-Json "$ops/projects/$ProjectId/process-maps"
$existing = @($pack.items)
$existingNames = @{}
foreach ($row in $existing) {
    $existingNames[([string]$row.name).Trim().ToLowerInvariant()] = $true
}

$seeds = @(
    @{
        name       = "Saha kabul akisi v1 (DEMO)"
        kind       = "workflow"
        resourceId = $sahaV1Id
        wbsId      = $wbs32.id
        status     = "superseded"
        note       = "Kontrol adimi yoktu; v2 resmi yapildi."
    }
    @{
        name       = "Saha kabul akisi (DEMO)"
        kind       = "workflow"
        resourceId = $sahaId
        wbsId      = $wbs32.id
        status     = "current"
        note       = $null
    }
    @{
        name       = "PMO ritim proseduru (DEMO)"
        kind       = "procedure"
        resourceId = $pmoId
        wbsId      = $wbs11.id
        status     = "draft"
        note       = $null
    }
    @{
        name       = "Organizasyon semasi (DEMO)"
        kind       = "org"
        resourceId = $null
        wbsId      = $null
        status     = "draft"
        note       = $null
    }
)

foreach ($seed in $seeds) {
    $key = $seed.name.ToLowerInvariant()
    if ($existingNames.ContainsKey($key)) {
        Write-Host "SKIP $($seed.name)" -ForegroundColor Yellow
        continue
    }
    $row = Invoke-Json "$ops/projects/$ProjectId/process-maps" "POST" $seed
    Write-Host ("CREATE {0} status={1} open={2} incomplete={3} current={4}" -f $row.name, $row.status, $row.open, $row.incomplete, $row.current)
}

$after = Invoke-Json "$ops/projects/$ProjectId/process-maps"
$status = Invoke-Json "$ops/projects/$ProjectId/status"
Write-Host ""
Write-Host ("processMaps={0} open={1} incomplete={2} current={3}" -f @($after.items).Count, $after.openCount, $after.incompleteCount, $after.currentCount)
Write-Host ("status counts openProcessMap={0} incompleteProcessMap={1} currentProcessMap={2}" -f $status.counts.openProcessMap, $status.counts.incompleteProcessMap, $status.counts.currentProcessMap)
$flagWbs = @($status.items) | Where-Object {
    @($_.flags) -contains "openProcessMap" -or
    @($_.flags) -contains "incompleteProcessMap"
}
foreach ($row in $flagWbs) {
    Write-Host ("WBS {0} {1} flags={2}" -f $row.wbsCode, $row.name, (@($row.flags) -join ","))
}
Write-Host "SEED process map OK" -ForegroundColor Green
