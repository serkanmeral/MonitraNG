# MongoDB — odak_musteri_kisileri idx_legacyContactId sparse unique onarimi
# Sorun: non-sparse unique index, legacyContactId olmayan kayitlarda null'i tek deger sayiyor;
#        UI'dan ikinci kisi eklenince "legacyContactId benzersiz olmali" hatasi olusuyor.
# Cozum: index'i unique+sparse olarak yeniden olustur; @datasets indexList'e sparse ekle.
#
# Usage:
#   .\repair-odak-musteri-kisileri-legacy-index.ps1
#   .\repair-odak-musteri-kisileri-legacy-index.ps1 -Server 192.168.20.8
#   .\repair-odak-musteri-kisileri-legacy-index.ps1 -Server 192.168.20.8 -DryRun

param(
    [string]$Server = "192.168.20.20",
    [string]$MongoDatabase = "mng_odak",
    [string]$Collection = "odak_musteri_kisileri",
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
const indexName = 'idx_legacyContactId';

const existing = before.find(function(i) { return i.name === indexName; });
if (existing) {
  const isSparse = existing.sparse === true;
  const isUnique = existing.unique === true;
  print('FOUND ' + indexName + ' unique=' + isUnique + ' sparse=' + isSparse);
  if (isUnique && isSparse) {
    print('OK: index already unique+sparse');
  } else {
    print('DROP ' + indexName);
    if (!dryRun) coll.dropIndex(indexName);
    print('CREATE ' + indexName + ' { legacyContactId:1 } unique+sparse');
    if (!dryRun) {
      coll.createIndex(
        { legacyContactId: 1 },
        { unique: true, sparse: true, name: indexName }
      );
    }
  }
} else {
  print('CREATE ' + indexName + ' { legacyContactId:1 } unique+sparse');
  if (!dryRun) {
    coll.createIndex(
      { legacyContactId: 1 },
      { unique: true, sparse: true, name: indexName }
    );
  }
}

const datasets = dbx.getCollection('@datasets');
const ds = datasets.findOne({ name: collName });
if (ds) {
  const indexList = Array.isArray(ds.indexList) ? ds.indexList.slice() : [];
  let updated = false;
  for (let i = 0; i < indexList.length; i++) {
    if (indexList[i] && indexList[i].name === indexName) {
      indexList[i] = {
        name: indexName,
        fields: { legacyContactId: 1 },
        unique: true,
        sparse: true
      };
      updated = true;
      break;
    }
  }
  if (!updated) {
    indexList.push({
      name: indexName,
      fields: { legacyContactId: 1 },
      unique: true,
      sparse: true
    });
  }
  print('SCHEMA: set @datasets.indexList sparse=true for ' + indexName);
  if (!dryRun) {
    datasets.updateOne({ _id: ds._id }, { `$set: { indexList: indexList } });
  }
} else {
  print('SCHEMA: @datasets row not found for ' + collName);
}

print('=== AFTER ===');
printjson(coll.getIndexes());
"@

Write-Host "`n=== repair-odak-musteri-kisileri-legacy-index ===" -ForegroundColor Cyan
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
