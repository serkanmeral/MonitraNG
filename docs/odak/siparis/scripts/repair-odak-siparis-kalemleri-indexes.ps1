# MongoDB — odak_siparis_kalemleri index onarimi (Odak test/prod)
# - Eski idx_parent_line (parentWorkItemId+lineNo) kaldirilir
# - parentPackageId+lineNo index non-unique olarak tutulur (ayni kalem no serbest)
# - DG @datasets schema indexList.unique bayragi senkronize edilir
#
# Usage:
#   .\repair-odak-siparis-kalemleri-indexes.ps1
#   .\repair-odak-siparis-kalemleri-indexes.ps1 -Server 192.168.20.8 -DryRun

param(
    [string]$Server = "192.168.20.20",
    [string]$MongoDatabase = "mng_odak",
    [string]$Collection = "odak_siparis_kalemleri",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
$odakScripts = (Resolve-Path (Join-Path $PSScriptRoot "../../../../scripts/odak")).Path
. (Join-Path $odakScripts "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$mongoJs = @"
const dbName = '$MongoDatabase';
const collName = '$Collection';
const dbx = db.getSiblingDB(dbName);
const coll = dbx.getCollection(collName);
const before = coll.getIndexes();
print('=== BEFORE ===');
printjson(before);

const dryRun = '$($DryRun.IsPresent)' === 'True';

const toDrop = [];
before.forEach(function(idx) {
  if (idx.name === '_id_') return;
  const k = idx.key || {};
  if (k.parentWorkItemId !== undefined) {
    toDrop.push(idx.name);
  }
  // Drop unique parentPackageId+lineNo so we can recreate non-unique
  if (idx.name === 'idx_parent_line' && k.parentPackageId !== undefined && idx.unique === true) {
    if (toDrop.indexOf(idx.name) < 0) toDrop.push(idx.name);
  }
  // Also drop legacy idx_parent_line without parentPackageId
  if (idx.name === 'idx_parent_line' && k.parentPackageId === undefined) {
    if (toDrop.indexOf(idx.name) < 0) toDrop.push(idx.name);
  }
});

toDrop.forEach(function(name) {
  print('DROP ' + name);
  if (!dryRun) coll.dropIndex(name);
});

const afterDrop = coll.getIndexes();
const parentLineIdx = afterDrop.find(function(i) {
  return i.name === 'idx_parent_line' && i.key && i.key.parentPackageId === 1 && i.key.lineNo === 1;
});

if (!parentLineIdx) {
  print('CREATE idx_parent_line { parentPackageId:1, lineNo:1 } non-unique');
  if (!dryRun) {
    coll.createIndex({ parentPackageId: 1, lineNo: 1 }, { unique: false, name: 'idx_parent_line' });
  }
} else if (parentLineIdx.unique === true) {
  print('WARN: unique idx_parent_line still present after drop attempt');
} else {
  print('OK: idx_parent_line already non-unique');
}

// Sync DG dataset schema metadata to parentPackageId + non-unique line index
const datasets = dbx.getCollection('@datasets');
const ds = datasets.findOne({ name: collName });
const desiredIndexList = [
  { name: 'idx_parent_line', fields: { parentPackageId: 1, lineNo: 1 }, unique: false },
  { name: 'idx_parentPackageId', fields: { parentPackageId: 1 }, unique: false },
  { name: 'idx_customerPoNo', fields: { customerPoNo: 1 }, unique: false },
  { name: 'idx_customerProjectNo', fields: { customerProjectNo: 1 }, unique: false },
  { name: 'idx_legacyLineId', fields: { legacyLineId: 1 }, unique: true }
];
if (ds) {
  print('SCHEMA: replace @datasets indexList (parentPackageId + non-unique idx_parent_line)');
  if (!dryRun) {
    datasets.updateOne({ _id: ds._id }, { `$set: { indexList: desiredIndexList } });
  }
} else {
  print('SCHEMA: @datasets row not found');
}

print('=== AFTER ===');
printjson(coll.getIndexes());
"@

Write-Host "`n=== repair-odak-siparis-kalemleri-indexes ===" -ForegroundColor Cyan
Write-Host "Server: $Server  DB: $MongoDatabase  Collection: $Collection  DryRun: $DryRun`n" -ForegroundColor Gray

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $result = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
    if ($result.ExitStatus -ne 0) {
        Write-Host $result.Output -ForegroundColor Red
        Write-Host $result.Error -ForegroundColor Red
        throw "mongosh exit $($result.ExitStatus)"
    }
    Write-Host ($result.Output -join "`n")
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

Write-Host "`nIndex onarimi tamamlandi." -ForegroundColor Green
