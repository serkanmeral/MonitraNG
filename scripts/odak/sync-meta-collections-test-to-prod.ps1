<#
.SYNOPSIS
  Test (192.168.20.20) -> Production (192.168.20.8) secili Mongo meta collection merge.
  DROP yok; prod-only kayitlar korunur; workspace / is verisi collection'larina dokunmaz.

.EXAMPLE
  pwsh -File .\scripts\odak\sync-meta-collections-test-to-prod.ps1
#>
param(
    [string]$SourceServer = "192.168.20.20",
    [string]$DestServer = "192.168.20.8",
    [string]$Database = "mng_odak",
    [string]$RemoteSyncDir = "/home/odak/mongo-meta-sync",
    [switch]$WhatIf,
    [switch]$SkipSideMenu,
    [switch]$SkipOpCatalogs,
    [switch]$SideMenuOnly
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

$mongoUser = "admin"
$mongoPass = "admin123"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$localWork = Join-Path $env:TEMP "mongo-meta-sync-$stamp"
New-Item -ItemType Directory -Force -Path $localWork | Out-Null

$MergeByName = @(
    @{ Collection = "@datasets"; Field = "name" },
    @{ Collection = "@dataset_categories"; Field = "name" },
    @{ Collection = "@automated_forms"; Field = "formCode" },
    @{ Collection = "@widget_categories"; Field = "name" },
    @{ Collection = "@widget_templates"; Field = "templateId" },
    @{ Collection = "@widgets"; Field = "name" },
    @{ Collection = "@dashboards"; Field = "name" },
    @{ Collection = "@mail_layouts"; Field = "name" },
    @{ Collection = "@mail_templates"; Field = "name" },
    @{ Collection = "@notification_templates"; Field = "name" }
)
$MergeOpCatalogs = @(
    "op_fields", "op_forms", "op_states", "op_priorities", "op_work_item_types",
    "op_state_flows", "op_sla_policies", "op_tags", "op_notification_policies", "op_rules"
)

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
    param($Session, [string]$Collection, [string]$RemotePath)
    $safe = ($Collection -replace '[^a-zA-Z0-9._-]', '_')
    $containerPath = "/tmp/sync_export_$safe.json"
    $escaped = $Collection.Replace("'", "'\\''")
    Invoke-Remote $Session @"
set -e
docker exec mongo mongoexport -u $mongoUser -p $mongoPass --authenticationDatabase admin \
  -d $Database -c '$escaped' --jsonArray -o $containerPath
docker cp mongo:$containerPath '$RemotePath'
docker exec mongo rm -f $containerPath
wc -c < '$RemotePath'
"@ | Out-Null
}

function Import-RemoteCollectionMerge {
    param($Dst, [string]$Collection, [string]$RemotePath, [string]$UpsertField, [switch]$InsertOnly)
    $safe = ($Collection -replace '[^a-zA-Z0-9._-]', '_')
    $containerPath = "/tmp/sync_import_$safe.json"
    $escaped = $Collection.Replace("'", "\\'")
    $insertOnlyJs = if ($InsertOnly) { "true" } else { "false" }

    $mergeJs = @"
const fs = require('fs');
const docs = JSON.parse(fs.readFileSync('$containerPath', 'utf8'));
const col = db.getSiblingDB('$Database').getCollection('$escaped');
const field = '$UpsertField';
const insertOnly = $insertOnlyJs;
let inserted = 0, updated = 0, skipped = 0;
for (const doc of docs) {
  const key = doc[field];
  if (key === undefined || key === null || key === '') { skipped++; continue; }
  const filter = {}; filter[field] = key;
  const existing = col.findOne(filter);
  if (existing) {
    if (insertOnly) { skipped++; continue; }
    const keepId = existing._id;
    doc._id = keepId;
    if (doc.__dataId && existing.__dataId) doc.__dataId = existing.__dataId;
    col.replaceOne({ _id: keepId }, doc);
    updated++;
  } else {
    delete doc._id;
    col.insertOne(doc);
    inserted++;
  }
}
print(JSON.stringify({ collection: '$escaped', inserted, updated, skipped, total: docs.length }));
"@

    $localJs = Join-Path $localWork "merge_$safe.js"
    Set-Content -Path $localJs -Value $mergeJs -Encoding UTF8
    $remoteJs = "$RemoteSyncDir/merge_${safe}.js"
    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath $localJs -RemoteDestination $remoteJs -AcceptKey

    return Invoke-Remote $Dst.Session @"
set -e
docker cp '$RemotePath' mongo:$containerPath
docker cp '$remoteJs' mongo:/tmp/merge.js
docker exec mongo mongosh -u $mongoUser -p $mongoPass --authenticationDatabase admin --quiet /tmp/merge.js
docker exec mongo rm -f $containerPath /tmp/merge.js
"@
}

function Sync-CollectionByName {
    param($Src, $Dst, [string]$Collection, [string]$MergeField = "name", [switch]$InsertOnly)
    $safe = ($Collection -replace '[^a-zA-Z0-9._-]', '_')
    $remoteSrc = "$RemoteSyncDir/src_${safe}.json"
    $localFile = Join-Path $localWork "${safe}.json"
    $remoteDst = "$RemoteSyncDir/${safe}.json"

    Write-Host "[$Collection] export test..." -ForegroundColor Cyan
    Export-RemoteCollectionJson -Session $Src.Session -Collection $Collection -RemotePath $remoteSrc

    Get-SCPItem -ComputerName $SourceServer -Credential $Src.Cred -Path $remoteSrc -PathType File -Destination $localWork -AcceptKey
    $downloaded = Join-Path $localWork (Split-Path $remoteSrc -Leaf)
    if ($downloaded -ne $localFile) { Move-Item -Force $downloaded $localFile }

    $bytes = (Get-Item $localFile).Length
    Write-Host "  file: $([math]::Round($bytes/1KB, 1)) KB" -ForegroundColor DarkGray

    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath $localFile -RemoteDestination $remoteDst -AcceptKey

    $mode = if ($InsertOnly) { "insert-only" } else { "merge" }
    Write-Host "  import prod ($mode by name)..." -ForegroundColor Cyan
    if ($InsertOnly) {
        $out = Import-RemoteCollectionMerge -Dst $Dst -Collection $Collection -RemotePath $remoteDst -UpsertField $MergeField -InsertOnly
    } else {
        $out = Import-RemoteCollectionMerge -Dst $Dst -Collection $Collection -RemotePath $remoteDst -UpsertField $MergeField
    }
    $line = ($out | Where-Object { $_ -match '^\{' }) | Select-Object -Last 1
    if ($line) { return ($line | ConvertFrom-Json) }
    return [pscustomobject]@{ collection = $Collection; inserted = "?"; updated = "?"; skipped = 0; total = "?" }
}

function Sync-SideMenu {
    param($Src, $Dst)
    Write-Host "[@side_menu] export + custom merge..." -ForegroundColor Cyan
    $remoteMenu = "$RemoteSyncDir/side_menu_test.json"
    $remoteTestGroups = "$RemoteSyncDir/groups_test.json"
    $remoteProdGroups = "$RemoteSyncDir/groups_prod.json"

    Export-RemoteCollectionJson -Session $Src.Session -Collection "@side_menu" -RemotePath $remoteMenu
    Export-RemoteCollectionJson -Session $Src.Session -Collection "@groups" -RemotePath $remoteTestGroups
    Export-RemoteCollectionJson -Session $Dst.Session -Collection "@groups" -RemotePath $remoteProdGroups

    foreach ($pair in @(
        @{ Remote = $remoteMenu; Local = "side_menu_test.json" },
        @{ Remote = $remoteTestGroups; Local = "groups_test.json" },
        @{ Remote = $remoteProdGroups; Local = "groups_prod.json" }
    )) {
        Get-SCPItem -ComputerName $(if ($pair.Local -eq "groups_prod.json") { $DestServer } else { $SourceServer }) `
            -Credential $(if ($pair.Local -eq "groups_prod.json") { $Dst.Cred } else { $Src.Cred }) `
            -Path $pair.Remote -PathType File -Destination $localWork -AcceptKey
        $dl = Join-Path $localWork (Split-Path $pair.Remote -Leaf)
        $target = Join-Path $localWork $pair.Local
        if ($dl -ne $target) { Move-Item -Force $dl $target }
    }

    $mergeJs = @'
const fs = require('fs');
const testGroups = JSON.parse(fs.readFileSync('/tmp/groups_test.json', 'utf8'));
const prodGroups = JSON.parse(fs.readFileSync('/tmp/groups_prod.json', 'utf8'));
const testMenu = JSON.parse(fs.readFileSync('/tmp/side_menu_test.json', 'utf8'));
const dbName = process.env.SYNC_DB || 'mng_odak';
const col = db.getSiblingDB(dbName).getCollection('@side_menu');

const testGroupIdToName = {};
testGroups.forEach(g => { if (g.__dataId && g.name) testGroupIdToName[g.__dataId] = g.name; });
const prodGroupNameToId = {};
prodGroups.forEach(g => { if (g.__dataId && g.name) prodGroupNameToId[g.name] = g.__dataId; });

function remapPermissions(perms) {
  if (!perms || !perms.groups) return perms;
  const raw = perms.groups;
  const list = Array.isArray(raw) ? raw : (typeof raw === 'object' ? Object.values(raw) : [raw]);
  const mapped = [];
  for (const gid of list) {
    if (!gid) continue;
    const name = testGroupIdToName[gid];
    if (name && prodGroupNameToId[name]) mapped.push(prodGroupNameToId[name]);
  }
  return { ...perms, groups: [...new Set(mapped)] };
}

const prodByTo = {};
const prodByPageCode = {};
const prodByDataId = {};
col.find({}).forEach(d => {
  if (d.to) prodByTo[d.to] = d;
  if (d.pageCode) prodByPageCode[d.pageCode] = d;
  if (d.__dataId) prodByDataId[d.__dataId] = d;
});
const testIdToProdDataId = {};
testMenu.forEach(d => { if (d.__dataId) testIdToProdDataId[d.__dataId] = d.__dataId; });

function isHeader(doc) {
  return doc.itemType === 'header' || (typeof doc.header === 'string' && doc.header.length > 0);
}

function findExisting(copy) {
  if (copy.__dataId && prodByDataId[copy.__dataId]) return prodByDataId[copy.__dataId];
  if (copy.pageCode && prodByPageCode[copy.pageCode]) return prodByPageCode[copy.pageCode];
  if (copy.to && prodByTo[copy.to]) return prodByTo[copy.to];
  return null;
}

function registerProdMaps(doc) {
  if (doc.to) prodByTo[doc.to] = doc;
  if (doc.pageCode) prodByPageCode[doc.pageCode] = doc;
  if (doc.__dataId) prodByDataId[doc.__dataId] = doc;
}

let inserted = 0, updated = 0, skipped = 0;
const pendingParent = [];

for (const doc of testMenu) {
  const copy = JSON.parse(JSON.stringify(doc));
  const header = isHeader(copy);
  if (!header && !copy.to) { skipped++; continue; }
  if (copy.permissions && !header) copy.permissions = remapPermissions(copy.permissions);

  const existing = findExisting(copy);
  if (existing) {
    const keepId = existing._id;
    const keepDataId = existing.__dataId;
    const keepPerms = existing.permissions;
    copy._id = keepId;
    copy.__dataId = keepDataId;
    if (!header && keepPerms && keepPerms.groups && (Array.isArray(keepPerms.groups) ? keepPerms.groups.length : Object.keys(keepPerms.groups || {}).length)) {
      copy.permissions = keepPerms;
    }
    col.replaceOne({ _id: keepId }, copy);
    registerProdMaps(copy);
    if (doc.__dataId) testIdToProdDataId[doc.__dataId] = keepDataId;
    updated++;
  } else {
    if (copy._id) delete copy._id;
    col.insertOne(copy);
    const filter = copy.pageCode ? { pageCode: copy.pageCode } : (copy.to ? { to: copy.to } : { header: copy.header, itemType: 'header' });
    const insertedDoc = col.findOne(filter);
    if (insertedDoc) {
      registerProdMaps(insertedDoc);
      if (doc.__dataId && insertedDoc.__dataId) testIdToProdDataId[doc.__dataId] = insertedDoc.__dataId;
    }
    if (doc.parentId) pendingParent.push({ testChildId: doc.__dataId, testParentId: doc.parentId });
    inserted++;
  }
}

let parentFixed = 0;
for (const p of pendingParent) {
  const parentDataId = testIdToProdDataId[p.testParentId];
  const childDataId = testIdToProdDataId[p.testChildId];
  if (!parentDataId || !childDataId) continue;
  const r = col.updateOne({ __dataId: childDataId }, { $set: { parentId: parentDataId } });
  if (r.modifiedCount) parentFixed++;
}

print(JSON.stringify({ collection: '@side_menu', inserted, updated, skipped, parentFixed, total: testMenu.length }));
'@

    $localJs = Join-Path $localWork "merge_side_menu.js"
    Set-Content -Path $localJs -Value $mergeJs -Encoding UTF8

    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath (Join-Path $localWork "side_menu_test.json") -RemoteDestination "$RemoteSyncDir/side_menu_test.json" -AcceptKey
    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath (Join-Path $localWork "groups_test.json") -RemoteDestination "$RemoteSyncDir/groups_test.json" -AcceptKey
    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath (Join-Path $localWork "groups_prod.json") -RemoteDestination "$RemoteSyncDir/groups_prod.json" -AcceptKey
    Send-OdakRemoteFile -ComputerName $DestServer -Credential $Dst.Cred -LocalPath $localJs -RemoteDestination "$RemoteSyncDir/merge_side_menu.js" -AcceptKey

    $out = Invoke-Remote $Dst.Session @"
set -e
docker cp '$RemoteSyncDir/side_menu_test.json' mongo:/tmp/side_menu_test.json
docker cp '$RemoteSyncDir/groups_test.json' mongo:/tmp/groups_test.json
docker cp '$RemoteSyncDir/groups_prod.json' mongo:/tmp/groups_prod.json
docker cp '$RemoteSyncDir/merge_side_menu.js' mongo:/tmp/merge_side_menu.js
docker exec -e SYNC_DB=$Database mongo mongosh -u $mongoUser -p $mongoPass --authenticationDatabase admin --quiet /tmp/merge_side_menu.js
docker exec mongo rm -f /tmp/side_menu_test.json /tmp/groups_test.json /tmp/groups_prod.json /tmp/merge_side_menu.js
"@
    $line = ($out | Where-Object { $_ -match '^\{' }) | Select-Object -Last 1
    return ($line | ConvertFrom-Json)
}

Write-Host "=== Meta collection merge: test -> prod ===" -ForegroundColor Cyan
Write-Host "Kaynak: $SourceServer  Hedef: $DestServer"
if ($WhatIf) { Write-Host "WhatIf" -ForegroundColor Yellow; exit 0 }

$src = Get-SshPair $SourceServer
$dst = Get-SshPair $DestServer
Invoke-Remote $src.Session "mkdir -p '$RemoteSyncDir'" | Out-Null
Invoke-Remote $dst.Session "mkdir -p '$RemoteSyncDir'" | Out-Null

try {
    if (-not $SideMenuOnly) {
        foreach ($item in $MergeByName) {
            $r = Sync-CollectionByName -Src $src -Dst $dst -Collection $item.Collection -MergeField $item.Field
            Write-Host "  $($r.collection): +$($r.inserted) ~$($r.updated) skip=$($r.skipped)" -ForegroundColor Green
        }
        if (-not $SkipOpCatalogs) {
            foreach ($col in $MergeOpCatalogs) {
                Sync-CollectionByName -Src $src -Dst $dst -Collection $col -InsertOnly | ForEach-Object {
                    Write-Host "  $($_.collection): inserted=$($_.inserted) skipped=$($_.skipped)" -ForegroundColor Green
                }
            }
        }
    }
    if (-not $SkipSideMenu -or $SideMenuOnly) {
        $r = Sync-SideMenu -Src $src -Dst $dst
        Write-Host "  @side_menu: inserted=$($r.inserted) updated=$($r.updated) parentFixed=$($r.parentFixed)" -ForegroundColor Green
    }
}
finally {
    Remove-SSHSession $src.Session.SessionId | Out-Null
    Remove-SSHSession $dst.Session.SessionId | Out-Null
    if (Test-Path $localWork) { Remove-Item $localWork -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Host "`n=== Tamamlandi ===" -ForegroundColor Green
