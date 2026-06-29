# Repair Extended JSON {$date: "..."} in meta collections (sync-meta side effect)
# Symptom: DG LIST/CREATE fails with "Invalid element: '$date'"
#
# Usage:
#   .\repair-dataset-createinfo-dates.ps1 -Server 192.168.20.8

param(
    [string]$Server = "192.168.20.8",
    [string]$MongoDatabase = "mng_odak",
    [string[]]$Collections = @("@datasets", "@automated_forms", "@dataset_categories", "@side_menu"),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
$odakScripts = (Resolve-Path (Join-Path $PSScriptRoot "../../../../scripts/odak")).Path
. (Join-Path $odakScripts "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$collJson = ($Collections | ConvertTo-Json -Compress)
$mongoJs = @'
const dbName = 'DBNAME_PLACEHOLDER';
const collNames = COLLJSON_PLACEHOLDER;
const dryRun = DRYRUN_PLACEHOLDER;

function hasExtendedDate(obj) {
  if (obj === null || obj === undefined) return false;
  if (typeof obj !== 'object') return false;
  if (obj instanceof Date) return false;
  if (obj['$date']) return true;
  if (Array.isArray(obj)) return obj.some(hasExtendedDate);
  return Object.keys(obj).some(function(k) { return hasExtendedDate(obj[k]); });
}

function fixDates(obj) {
  if (obj === null || obj === undefined) return obj;
  if (obj instanceof Date) return obj;
  if (typeof obj === 'object' && obj._bsontype === 'ObjectId') return obj;
  if (Array.isArray(obj)) return obj.map(fixDates);
  if (typeof obj === 'object') {
    if (obj['$date']) return new Date(obj['$date']);
    const out = {};
    Object.keys(obj).forEach(function(k) { out[k] = fixDates(obj[k]); });
    return out;
  }
  return obj;
}

const dbx = db.getSiblingDB(dbName);
let fixedDocs = 0;
collNames.forEach(function(collName) {
  dbx.getCollection(collName).find({}).forEach(function(doc) {
    if (!hasExtendedDate(doc)) return;
    fixedDocs++;
    const label = doc.name || doc.pageCode || doc.code || String(doc._id);
    print('FIX ' + collName + ' ' + label);
    if (!dryRun) {
      const fixed = fixDates(doc);
      fixed._id = doc._id;
      dbx.getCollection(collName).replaceOne({ _id: doc._id }, fixed);
    }
  });
});
print('fixedDocs=' + fixedDocs + ' dryRun=' + dryRun);
'@
$mongoJs = $mongoJs.Replace('DBNAME_PLACEHOLDER', $MongoDatabase)
$mongoJs = $mongoJs.Replace('COLLJSON_PLACEHOLDER', $collJson)
$mongoJs = $mongoJs.Replace('DRYRUN_PLACEHOLDER', $(if ($DryRun) { 'true' } else { 'false' }))

Write-Host "`n=== repair-dataset-createinfo-dates ===" -ForegroundColor Cyan
Write-Host "Server: $Server  DB: $MongoDatabase  DryRun: $DryRun`n" -ForegroundColor Gray

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $result = Invoke-OdakMongoJsonEval -SshSession $session -JavaScript $mongoJs
    if ($result.ExitStatus -ne 0) {
        Write-Host ($result.Output -join "`n") -ForegroundColor Red
        Write-Host ($result.Error -join "`n") -ForegroundColor Red
        throw "mongosh exit $($result.ExitStatus)"
    }
    Write-Host ($result.Output -join "`n")
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}

Write-Host "`nTamamlandi." -ForegroundColor Green
