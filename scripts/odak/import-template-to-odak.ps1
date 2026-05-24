# Kaynak ortamdan export edilen template JSON'unu Odak sunucusuna yükler:
#   - MinIO: system/System/templates/{name}.json
#   - MongoDB mngkeeper.templates meta kaydı
#
# Ön koşul: Odak'ta MinIO "system" bucket oluşturulmuş olmalı.
#
# Kullanım:
#   .\scripts\odak\import-template-to-odak.ps1 -TemplateJsonPath "C:\path\template-content.json"
#   .\scripts\odak\import-template-to-odak.ps1 -TemplateJsonPath ".\my-template.json" -TemplateName "baseline" -Description "Kaynak ortam"
#
param(
    [Parameter(Mandatory = $true)]
    [string]$TemplateJsonPath,
    [string]$TemplateName = "",
    [string]$Description = "Imported from source environment",
    [string]$SourceDomainName = "odak",
    [string]$Server = "192.168.20.20",
    [string]$User = "odak",
    [string]$RemoteDir = "/home/odak/template-import",
    [string]$MinioAccessKey = "admin",
    [string]$MinioSecretKey = "Odak@Infra2026!",
    [string]$MongoUser = "admin",
    [string]$MongoPassword = "admin123"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force -ErrorAction Stop

if (-not (Test-Path $TemplateJsonPath)) {
    throw "JSON dosyası bulunamadı: $TemplateJsonPath"
}

$raw = Get-Content $TemplateJsonPath -Raw -Encoding UTF8
$content = $raw | ConvertFrom-Json

$name = $TemplateName
if ([string]::IsNullOrWhiteSpace($name)) {
    $name = $content.templateName
    if ([string]::IsNullOrWhiteSpace($name)) { $name = $content.TemplateName }
}
if ([string]::IsNullOrWhiteSpace($name)) {
    throw "Şablon adı bulunamadı. -TemplateName verin veya JSON içinde templateName alanı olsun."
}

$minioPath = "System/templates/$name.json"
$fileName = Split-Path $TemplateJsonPath -Leaf

Write-Host "Şablon: $name" -ForegroundColor Cyan
Write-Host "MinIO hedef: system/$minioPath" -ForegroundColor Cyan

$pass = Read-Host "SSH password for ${User}@${Server}" -AsSecureString
$cred = New-Object System.Management.Automation.PSCredential($User, $pass)

$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey

try {
    Invoke-SSHCommand -SessionId $session.SessionId -Command "mkdir -p '$RemoteDir'" | Out-Null

    Set-SCPItem -ComputerName $Server -Credential $cred -Path $TemplateJsonPath -Destination "$RemoteDir/$fileName" -AcceptKey

    $remoteJson = "$RemoteDir/$fileName"
    $escapedDesc = $Description -replace "'", "''"

    $remoteScript = @"
set -e
REMOTE_JSON='$remoteJson'
TEMPLATE_NAME='$name'
MINIO_PATH='$minioPath'
DESC='$escapedDesc'
SOURCE_DOMAIN='$SourceDomainName'

python3 << 'PYIMPORT'
import json, subprocess, sys, datetime

path = "$remoteJson"
with open(path, encoding="utf-8") as f:
    data = json.load(f)

template_name = "$name"
collections = data.get("collections") or data.get("Collections") or []
meta_collections = []
total_docs = 0
for c in collections:
    cname = c.get("collectionName") or c.get("CollectionName") or ""
    docs = c.get("documents") or c.get("Documents") or []
    dc = len(docs)
    total_docs += dc
    meta_collections.append({
        "collectionName": cname,
        "includeIndexes": True,
        "documentCount": dc
    })

# MinIO upload
subprocess.run([
    "docker", "exec", "minio", "mc", "alias", "set", "local",
    "http://localhost:9000", "$MinioAccessKey", "$MinioSecretKey"
], check=True, capture_output=True)

subprocess.run([
    "docker", "exec", "minio", "mc", "mb", "local/system", "--ignore-existing"
], check=True, capture_output=True)

subprocess.run([
    "docker", "cp", path, f"minio:/tmp/{template_name}.json"
], check=True, capture_output=True)

subprocess.run([
    "docker", "exec", "minio", "mc", "cp",
    f"/tmp/{template_name}.json", f"local/system/{ '$minioPath' if False else ''}"
], check=False)
PYIMPORT
"@

    # Fix: heredoc had a bug with minio path - use simpler bash script
    $bash = @"
set -e
REMOTE_JSON='$remoteJson'
TEMPLATE_NAME='$name'
MINIO_OBJECT='System/templates/${name}.json'
DESC='$escapedDesc'
SOURCE_DOMAIN='$SourceDomainName'

# MinIO
docker exec minio mc alias set local http://localhost:9000 '$MinioAccessKey' '$MinioSecretKey' >/dev/null
docker exec minio mc mb local/system --ignore-existing >/dev/null 2>&1 || true
docker exec minio mc stat local/system >/dev/null || { echo 'ERROR: system bucket yok. MinIO Console ile oluşturun.'; exit 1; }
docker cp "`$REMOTE_JSON" minio:/tmp/template-import.json
docker exec minio mc cp /tmp/template-import.json "local/system/`$MINIO_OBJECT"
docker exec minio mc stat "local/system/`$MINIO_OBJECT"
echo "OK MinIO: system/`$MINIO_OBJECT"

# Domain id for meta
DOMAIN_ID=`$(curl -s http://127.0.0.1:5001/api/domain | python3 -c "
import sys,json
name='$SourceDomainName'
try:
    data=json.load(sys.stdin)
    items=data if isinstance(data,list) else data.get('items',data.get('data',[]))
    for d in items:
        if d.get('domainName')==name:
            print(d.get('domainId') or d.get('id') or ''); break
except Exception:
    pass
" 2>/dev/null || echo '')

# Mongo meta via python
python3 << PYEOF
import json, subprocess, datetime

with open('$remoteJson', encoding='utf-8') as f:
    data = json.load(f)

template_name = '$name'
collections = data.get('collections') or data.get('Collections') or []
meta_collections = []
total_docs = 0
for c in collections:
    cname = c.get('collectionName') or c.get('CollectionName') or ''
    docs = c.get('documents') or c.get('Documents') or []
    dc = len(docs)
    total_docs += dc
    meta_collections.append({
        'collectionName': cname,
        'includeIndexes': True,
        'documentCount': dc
    })

domain_id = '''$DOMAIN_ID''' or 'imported'
doc = {
    'name': template_name,
    'description': '''$escapedDesc''',
    'sourceDomainId': domain_id,
    'sourceDatabaseName': 'mng_' + '''$SourceDomainName''',
    'collections': meta_collections,
    'minioObjectPath': 'System/templates/' + template_name + '.json',
    'totalDocumentCount': total_docs,
    'createdAt': datetime.datetime.utcnow(),
    'createdBy': 'import-template-to-odak'
}

existing = subprocess.run(
    ['docker', 'exec', 'mongo', 'mongosh', '-u', '$MongoUser', '-p', '$MongoPassword',
     '--authenticationDatabase', 'admin', '--quiet', '--eval',
     f'JSON.stringify(db.getSiblingDB("mngkeeper").templates.findOne({{name:"{template_name}"}}))'],
    capture_output=True, text=True
)
out = (existing.stdout or '').strip()
if out and out != 'null':
    subprocess.run(
        ['docker', 'exec', 'mongo', 'mongosh', '-u', '$MongoUser', '-p', '$MongoPassword',
         '--authenticationDatabase', 'admin', '--quiet', '--eval',
         f'db.getSiblingDB("mngkeeper").templates.deleteOne({{name:"{template_name}"}})'],
        check=True
    )
    print('Eski meta kaydı silindi')

import_script = f'''
const doc = {json.dumps(doc, default=str)};
db.getSiblingDB("mngkeeper").templates.insertOne(doc);
print("OK Mongo templates: " + doc.name);
'''
subprocess.run(
    ['docker', 'exec', '-i', 'mongo', 'mongosh', '-u', '$MongoUser', '-p', '$MongoPassword',
     '--authenticationDatabase', 'admin', '--quiet'],
    input=import_script, text=True, check=True
)
PYEOF

# Keeper API doğrulama
curl -s -o /dev/null -w 'templates_api=%{http_code}\n' http://127.0.0.1:5001/api/templates
curl -s http://127.0.0.1:5001/api/templates/'$name' | head -c 400
echo ''
"@

    $r = Invoke-SSHCommand -SessionId $session.SessionId -Command $bash -TimeOut 120
    $r.Output | ForEach-Object { Write-Host $_ }
    if ($r.Error) { $r.Error | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
    if ($r.ExitStatus -ne 0) { throw "Import failed (exit $($r.ExitStatus))" }

    Write-Host "`nImport tamamlandı. Domain UI'da '$name' şablonu seçilebilir." -ForegroundColor Green
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
