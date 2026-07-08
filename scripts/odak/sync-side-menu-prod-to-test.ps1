<#
.SYNOPSIS
  Production (192.168.20.8) -> Test (192.168.20.20) @side_menu esitleme.
  Prod yapi + yetkiler (grup ID'leri test ortamina isimle remap) kaynak kabul edilir.

.EXAMPLE
  pwsh -File .\scripts\odak\sync-side-menu-prod-to-test.ps1 -CompareOnly
  pwsh -File .\scripts\odak\sync-side-menu-prod-to-test.ps1
  pwsh -File .\scripts\odak\sync-side-menu-prod-to-test.ps1 -WhatIf
#>
param(
    [string]$ProdServer = "192.168.20.8",
    [string]$TestServer = "192.168.20.20",
    [string]$Database = "mng_odak",
    [string]$RemoteSyncDir = "/home/odak/mongo-meta-sync",
    [switch]$CompareOnly,
    [switch]$WhatIf,
    [switch]$KeepTestOnlyItems
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$mongoUser = "admin"
$mongoPass = "admin123"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$localWork = Join-Path $env:TEMP "side-menu-prod-test-$stamp"
New-Item -ItemType Directory -Force -Path $localWork | Out-Null

function Get-SshPair {
    param([string]$Server)
    Initialize-OdakSshEnvironment -Server $Server
    $cred = Get-OdakSshCredential -Server $Server
    return @{ Session = (New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey); Cred = $cred }
}

function Invoke-Remote {
    param($Session, [string]$Command, [int]$TimeoutSec = 600)
    $r = Invoke-SSHCommand -SessionId $Session.SessionId -Command (ConvertTo-UnixShell $Command) -TimeOut $TimeoutSec
    if ($r.ExitStatus -ne 0) {
        throw "Remote failed (exit $($r.ExitStatus)): $($r.Error -join "`n")`n$($r.Output -join "`n")"
    }
    return $r.Output
}

function Export-RemoteCollectionJson {
    param($Session, [string]$Server, $Cred, [string]$Collection, [string]$LocalFile)
    $safe = ($Collection -replace '[^a-zA-Z0-9._-]', '_')
    $remotePath = "$RemoteSyncDir/export_${safe}.json"
    $escaped = $Collection.Replace("'", "'\\''")
    Invoke-Remote $Session @"
set -e
mkdir -p '$RemoteSyncDir'
docker exec mongo mongoexport -u $mongoUser -p $mongoPass --authenticationDatabase admin \
  -d $Database -c '$escaped' --jsonArray -o /tmp/export_$safe.json
docker cp mongo:/tmp/export_$safe.json '$remotePath'
docker exec mongo rm -f /tmp/export_$safe.json
wc -c < '$remotePath'
"@ | Out-Null
    Get-SCPItem -ComputerName $Server -Credential $Cred -Path $remotePath -PathType File -Destination $localWork -AcceptKey
    $downloaded = Join-Path $localWork (Split-Path $remotePath -Leaf)
    if ($downloaded -ne $LocalFile) { Move-Item -Force $downloaded $LocalFile }
}

function Build-SideMenuSummaryFromExport {
    param([string]$ExportJsonPath, [string]$OutNdjsonPath)
    $nodeScript = Join-Path $PSScriptRoot "lib/side-menu-summary.js"
    if (-not (Test-Path $nodeScript)) { throw "Node script yok: $nodeScript" }
    node $nodeScript $ExportJsonPath | Set-Content -Path $OutNdjsonPath -Encoding UTF8
}

function Read-SideMenuSummary {
    param([string]$Path)
    $items = @()
    foreach ($line in (Get-Content $Path -Encoding UTF8 | Where-Object { $_.Trim() })) {
        $items += ($line | ConvertFrom-Json)
    }
    return $items
}

function Normalize-PermissionsForCompare {
    param($Perms)
    if ($null -eq $Perms) { return $null }
    return ($Perms | ConvertTo-Json -Depth 10 -Compress)
}

function Compare-SideMenus {
    param($ProdMenu, $TestMenu)

    $prodByKey = @{}
    foreach ($d in $ProdMenu) { if ($d.key) { $prodByKey[$d.key] = $d } }
    $testByKey = @{}
    foreach ($d in $TestMenu) { if ($d.key) { $testByKey[$d.key] = $d } }

    $onlyProd = @($prodByKey.Keys | Where-Object { -not $testByKey.ContainsKey($_) })
    $onlyTest = @($testByKey.Keys | Where-Object { -not $prodByKey.ContainsKey($_) })
    $common = @($prodByKey.Keys | Where-Object { $testByKey.ContainsKey($_) })

    $permDiff = @()
    $fieldDiff = @()
    foreach ($k in $common) {
        $p = $prodByKey[$k]
        $t = $testByKey[$k]
        $pp = Normalize-PermissionsForCompare $p.permissions
        $tp = Normalize-PermissionsForCompare $t.permissions
        if ($pp -ne $tp) {
            $label = if ($p.pageCode) { $p.pageCode } elseif ($p.to) { $p.to } else { $p.header }
            $permDiff += [pscustomobject]@{ Key = $k; Label = $label; Prod = $pp; Test = $tp }
        }
        foreach ($f in @("title", "header", "to", "order", "pageType", "disabled")) {
            $pv = $p.$f; $tv = $t.$f
            if ("$pv" -ne "$tv") {
                $fieldDiff += [pscustomobject]@{ Key = $k; Field = $f; Prod = $pv; Test = $tv }
            }
        }
    }

    Write-Host "`n=== Side menu karsilastirma (prod vs test) ===" -ForegroundColor Cyan
    Write-Host "Prod kayit: $($ProdMenu.Count)  Test kayit: $($TestMenu.Count)  Ortak anahtar: $($common.Count)"
    Write-Host "Yalniz prod'da: $($onlyProd.Count)" -ForegroundColor $(if ($onlyProd.Count) { "Yellow" } else { "Green" })
    foreach ($k in ($onlyProd | Sort-Object)) { Write-Host "  + $k" -ForegroundColor Yellow }
    Write-Host "Yalniz test'te: $($onlyTest.Count)" -ForegroundColor $(if ($onlyTest.Count) { "Yellow" } else { "Green" })
    foreach ($k in ($onlyTest | Sort-Object)) { Write-Host "  - $k" -ForegroundColor Yellow }
    Write-Host "Yetki farki: $($permDiff.Count)" -ForegroundColor $(if ($permDiff.Count) { "Yellow" } else { "Green" })
    foreach ($d in ($permDiff | Select-Object -First 20)) {
        Write-Host "  $($d.Label)" -ForegroundColor Yellow
        Write-Host "    prod: $($d.Prod)" -ForegroundColor DarkGray
        Write-Host "    test: $($d.Test)" -ForegroundColor DarkGray
    }
    if ($permDiff.Count -gt 20) { Write-Host "  ... +$($permDiff.Count - 20) daha" -ForegroundColor DarkGray }
    Write-Host "Diger alan farki: $($fieldDiff.Count)" -ForegroundColor $(if ($fieldDiff.Count) { "Yellow" } else { "Green" })

    return [pscustomobject]@{
        OnlyProd       = $onlyProd
        OnlyTest       = $onlyTest
        PermDiffCount  = $permDiff.Count
        FieldDiffCount = $fieldDiff.Count
    }
}

$mergeJs = @'
const fs = require('fs');
const sourceGroups = JSON.parse(fs.readFileSync('/tmp/groups_source.json', 'utf8'));
const destGroups = JSON.parse(fs.readFileSync('/tmp/groups_dest.json', 'utf8'));
const sourceMenu = JSON.parse(fs.readFileSync('/tmp/side_menu_source.json', 'utf8'));
const dbName = process.env.SYNC_DB || 'mng_odak';
const col = db.getSiblingDB(dbName).getCollection('@side_menu');
const keepTestOnly = process.env.KEEP_TEST_ONLY === '1';
const dryRun = process.env.SYNC_DRY_RUN === '1';

const sourceGroupIdToName = {};
sourceGroups.forEach(g => { if (g.__dataId && g.name) sourceGroupIdToName[g.__dataId] = g.name; });
const destGroupNameToId = {};
destGroups.forEach(g => { if (g.__dataId && g.name) destGroupNameToId[g.name] = g.__dataId; });

function isHeader(doc) {
  return doc.itemType === 'header' || (typeof doc.header === 'string' && doc.header.length > 0);
}

function stableKey(doc) {
  if (doc.pageCode) return 'pc:' + doc.pageCode;
  if (doc.to) return 'to:' + doc.to;
  if (isHeader(doc)) return 'hdr:' + (doc.header || doc.title || doc.pageCode || '');
  if (doc.__dataId) return 'id:' + doc.__dataId;
  return null;
}

function remapPermissions(perms) {
  if (!perms || !perms.groups) return perms;
  const raw = perms.groups;
  if (Array.isArray(raw)) {
    const mapped = [];
    for (const gid of raw) {
      if (!gid) continue;
      const name = sourceGroupIdToName[gid];
      if (name && destGroupNameToId[name]) mapped.push(destGroupNameToId[name]);
    }
    return { ...perms, groups: [...new Set(mapped)] };
  }
  // Grup adi anahtarli nesne — menu filtresi isimle calisir; oldugu gibi kopyala
  return perms;
}

const destByKey = {};
const destByDataId = {};
col.find({}).forEach(d => {
  const k = stableKey(d);
  if (k) destByKey[k] = d;
  if (d.__dataId) destByDataId[d.__dataId] = d;
});
const sourceIdToDestDataId = {};

function registerDestMaps(doc) {
  const k = stableKey(doc);
  if (k) destByKey[k] = doc;
  if (doc.__dataId) destByDataId[doc.__dataId] = doc;
}

let inserted = 0, updated = 0, skipped = 0, deleted = 0;
const pendingParent = [];
const sourceKeys = new Set();

for (const doc of sourceMenu) {
  const copy = JSON.parse(JSON.stringify(doc));
  const header = isHeader(copy);
  const key = stableKey(copy);
  if (!key) { skipped++; continue; }
  sourceKeys.add(key);
  if (!header && !copy.to && !copy.pageCode) { skipped++; continue; }

  if (copy.permissions && !header) copy.permissions = remapPermissions(copy.permissions);

  const existing = destByKey[key];
  if (existing) {
    const keepId = existing._id;
    const keepDataId = existing.__dataId;
    copy._id = keepId;
    copy.__dataId = keepDataId;
    if (doc.__dataId) sourceIdToDestDataId[doc.__dataId] = keepDataId;
    if (!dryRun) {
      col.replaceOne({ _id: keepId }, copy);
      registerDestMaps(copy);
    }
    updated++;
  } else {
    if (copy._id) delete copy._id;
    if (!dryRun) {
      col.insertOne(copy);
      const insertedDoc = col.findOne(key.startsWith('pc:') ? { pageCode: copy.pageCode } : (key.startsWith('to:') ? { to: copy.to } : { header: copy.header, itemType: 'header' }));
      if (insertedDoc) {
        registerDestMaps(insertedDoc);
        if (doc.__dataId && insertedDoc.__dataId) sourceIdToDestDataId[doc.__dataId] = insertedDoc.__dataId;
      }
    } else if (doc.__dataId) {
      sourceIdToDestDataId[doc.__dataId] = doc.__dataId;
    }
    if (doc.parentId) pendingParent.push({ sourceChildId: doc.__dataId, sourceParentId: doc.parentId });
    inserted++;
  }
}

let parentFixed = 0;
if (!dryRun) {
  for (const p of pendingParent) {
    const parentDataId = sourceIdToDestDataId[p.sourceParentId];
    const childDataId = sourceIdToDestDataId[p.sourceChildId];
    if (!parentDataId || !childDataId) continue;
    const r = col.updateOne({ __dataId: childDataId }, { $set: { parentId: parentDataId } });
    if (r.modifiedCount) parentFixed++;
  }
}

if (!keepTestOnly && !dryRun) {
  for (const d of col.find({}).toArray()) {
    const k = stableKey(d);
    if (k && !sourceKeys.has(k)) {
      col.deleteOne({ _id: d._id });
      deleted++;
    }
  }
}

print(JSON.stringify({ collection: '@side_menu', inserted, updated, skipped, deleted, parentFixed, total: sourceMenu.length, dryRun }));
'@

Write-Host "=== Side menu sync: prod -> test ===" -ForegroundColor Cyan
Write-Host "Prod: $ProdServer  Test: $TestServer"

$prod = Get-SshPair $ProdServer
$test = Get-SshPair $TestServer

try {
    Invoke-Remote $prod.Session "mkdir -p '$RemoteSyncDir'" | Out-Null
    Invoke-Remote $test.Session "mkdir -p '$RemoteSyncDir'" | Out-Null

    $prodMenuFile = Join-Path $localWork "side_menu_prod.json"
    $testMenuFile = Join-Path $localWork "side_menu_test.json"
    $prodGroupsFile = Join-Path $localWork "groups_prod.json"
    $testGroupsFile = Join-Path $localWork "groups_test.json"
    $prodSummaryFile = Join-Path $localWork "side_menu_prod_summary.ndjson"
    $testSummaryFile = Join-Path $localWork "side_menu_test_summary.ndjson"

    Write-Host "Export prod @side_menu + @groups..." -ForegroundColor Cyan
    Export-RemoteCollectionJson -Session $prod.Session -Server $ProdServer -Cred $prod.Cred -Collection "@side_menu" -LocalFile $prodMenuFile
    Export-RemoteCollectionJson -Session $prod.Session -Server $ProdServer -Cred $prod.Cred -Collection "@groups" -LocalFile $prodGroupsFile
    Build-SideMenuSummaryFromExport -ExportJsonPath $prodMenuFile -OutNdjsonPath $prodSummaryFile

    Write-Host "Export test @side_menu + @groups..." -ForegroundColor Cyan
    Export-RemoteCollectionJson -Session $test.Session -Server $TestServer -Cred $test.Cred -Collection "@side_menu" -LocalFile $testMenuFile
    Export-RemoteCollectionJson -Session $test.Session -Server $TestServer -Cred $test.Cred -Collection "@groups" -LocalFile $testGroupsFile
    Build-SideMenuSummaryFromExport -ExportJsonPath $testMenuFile -OutNdjsonPath $testSummaryFile

    $prodMenuSummary = Read-SideMenuSummary $prodSummaryFile
    $testMenuSummary = Read-SideMenuSummary $testSummaryFile

    $cmp = Compare-SideMenus -ProdMenu $prodMenuSummary -TestMenu $testMenuSummary

    if ($CompareOnly) {
        Write-Host "`nCompareOnly — degisiklik uygulanmadi." -ForegroundColor Yellow
        exit 0
    }

    if ($WhatIf) {
        Write-Host "`nWhatIf — sync atlandi. Uygulamak icin -WhatIf olmadan calistirin." -ForegroundColor Yellow
        exit 0
    }

    if ($cmp.OnlyProd.Count -eq 0 -and $cmp.OnlyTest.Count -eq 0 -and $cmp.PermDiffCount -eq 0 -and $cmp.FieldDiffCount -eq 0) {
        Write-Host "`nMenu zaten esit — sync atlandi." -ForegroundColor Green
        exit 0
    }

    Write-Host "`nTest ortamina uygulaniyor..." -ForegroundColor Cyan
    $mergeJsFile = Join-Path $localWork "merge_side_menu_prod_to_test.js"
    Set-Content -Path $mergeJsFile -Value $mergeJs -Encoding UTF8

    $uploadPairs = @(
        @{ Local = $prodMenuFile; RemoteName = "side_menu_source.json" },
        @{ Local = $prodGroupsFile; RemoteName = "groups_source.json" },
        @{ Local = $testGroupsFile; RemoteName = "groups_dest.json" },
        @{ Local = $mergeJsFile; RemoteName = "merge_side_menu_prod_to_test.js"; SkipCopy = $true }
    )
    Invoke-Remote $test.Session "mkdir -p '$RemoteSyncDir'" | Out-Null
    foreach ($pair in $uploadPairs) {
        $staging = Join-Path $localWork $pair.RemoteName
        if (-not $pair.SkipCopy) {
            Copy-Item -Force $pair.Local $staging
            $uploadPath = $staging
        } else {
            $uploadPath = $pair.Local
        }
        Invoke-Remote $test.Session "rm -f '$RemoteSyncDir/$($pair.RemoteName)'" | Out-Null
        Send-OdakRemoteFile -ComputerName $TestServer -Credential $test.Cred -LocalPath $uploadPath -RemoteDestination "$RemoteSyncDir/" -AcceptKey
        Invoke-Remote $test.Session "test -f '$RemoteSyncDir/$($pair.RemoteName)'" | Out-Null
    }

    $keepFlag = if ($KeepTestOnlyItems) { "1" } else { "0" }
    $out = Invoke-Remote $test.Session @"
set -e
docker cp '$RemoteSyncDir/side_menu_source.json' mongo:/tmp/side_menu_source.json
docker cp '$RemoteSyncDir/groups_source.json' mongo:/tmp/groups_source.json
docker cp '$RemoteSyncDir/groups_dest.json' mongo:/tmp/groups_dest.json
docker cp '$RemoteSyncDir/merge_side_menu_prod_to_test.js' mongo:/tmp/merge_side_menu.js
docker exec -e SYNC_DB=$Database -e KEEP_TEST_ONLY=$keepFlag -e SYNC_DRY_RUN=0 mongo mongosh -u $mongoUser -p $mongoPass --authenticationDatabase admin --quiet /tmp/merge_side_menu.js
docker exec mongo rm -f /tmp/side_menu_source.json /tmp/groups_source.json /tmp/groups_dest.json /tmp/merge_side_menu.js
"@
    $line = ($out | Where-Object { $_ -match '^\{' }) | Select-Object -Last 1
    if ($line) {
        $r = $line | ConvertFrom-Json
        Write-Host "  @side_menu: +$($r.inserted) ~$($r.updated) -$($r.deleted) parentFixed=$($r.parentFixed) skip=$($r.skipped)" -ForegroundColor Green
    }

    Write-Host "`nDogrulama (yeniden export)..." -ForegroundColor Cyan
    $testSummaryFile2 = Join-Path $localWork "side_menu_test_after_summary.ndjson"
    Export-RemoteCollectionJson -Session $test.Session -Server $TestServer -Cred $test.Cred -Collection "@side_menu" -LocalFile (Join-Path $localWork "side_menu_test_after.json")
    Build-SideMenuSummaryFromExport -ExportJsonPath (Join-Path $localWork "side_menu_test_after.json") -OutNdjsonPath $testSummaryFile2
    $testMenuAfter = Read-SideMenuSummary $testSummaryFile2
    $cmp2 = Compare-SideMenus -ProdMenu $prodMenuSummary -TestMenu $testMenuAfter
    if ($cmp2.OnlyProd.Count -eq 0 -and $cmp2.OnlyTest.Count -eq 0 -and $cmp2.PermDiffCount -eq 0 -and $cmp2.FieldDiffCount -eq 0) {
        Write-Host "`n=== Esitleme basarili ===" -ForegroundColor Green
    } else {
        Write-Host "`n=== Uyari: bazi farklar kaldi (parentId/order gibi __dataId bagimli alanlar olabilir) ===" -ForegroundColor Yellow
    }
}
finally {
    Remove-SSHSession $prod.Session.SessionId -ErrorAction SilentlyContinue | Out-Null
    Remove-SSHSession $test.Session.SessionId -ErrorAction SilentlyContinue | Out-Null
    if (Test-Path $localWork) { Remove-Item $localWork -Recurse -Force -ErrorAction SilentlyContinue }
}
