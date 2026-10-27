# Load external meeting people from Proje Paydaş Listesi.xlsx.
# Sheet BVA_KP -> project TCDD-BV. Sheet DDYS -> project DDYS.
# Rows whose organization is PROLINE are skipped (they are internal users).
# Title is stored in note. Same email on the project is skipped.
#
# Local: -GatewayBaseUrl https://localhost:5040
# Cafe:  -GatewayBaseUrl http://192.168.1.42:5040

param(
    [string]$GatewayBaseUrl = "https://localhost:5040",
    [string]$Domain = "proline",
    [string]$Username = "kivanc.pak",
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$SourceFile = "C:\Users\serkan.meral\Desktop\Turkcar\CONFLUENCE YEDEKLEME (2026.08.26)\Toplantı Tutanakları\Proje Paydaş Listesi (Güncel).xlsx"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")

if (-not (Test-Path -LiteralPath $SourceFile)) { throw "Dosya yok: $SourceFile" }

$py = @'
import zipfile, xml.etree.ElementTree as ET, json, sys
path, outp = sys.argv[1], sys.argv[2]
ns = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
z = zipfile.ZipFile(path)
wb = ET.fromstring(z.read("xl/workbook.xml"))
rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
rid = {rel.attrib["Id"]: rel.attrib["Target"] for rel in rels}
ss = []
root = ET.fromstring(z.read("xl/sharedStrings.xml"))
for si in root.findall("m:si", ns):
    ss.append("".join(t.text or "" for t in si.iter("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t")))
def colrow(ref):
    col = ""; row = ""
    for ch in ref:
        if ch.isalpha(): col += ch
        else: row += ch
    n = 0
    for ch in col: n = n*26 + (ord(ch.upper())-64)
    return n, int(row)
want = {"BVA_KP": "TCDD-BV", "DDYS": "DDYS"}
people = []
for sh in wb.findall("m:sheets/m:sheet", ns):
    name = sh.attrib["name"]
    if name not in want: continue
    target = rid[sh.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]]
    if not target.startswith("xl/"): target = "xl/" + target.lstrip("/")
    root = ET.fromstring(z.read(target))
    rows = {}
    for c in root.findall(".//m:c", ns):
        ref = c.attrib.get("r")
        if not ref: continue
        col, row = colrow(ref)
        t = c.attrib.get("t"); v = c.find("m:v", ns); isel = c.find("m:is", ns); val = ""
        if t == "s" and v is not None and v.text: val = ss[int(v.text)]
        elif t == "inlineStr" and isel is not None:
            val = "".join(x.text or "" for x in isel.iter("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t"))
        elif v is not None and v.text: val = v.text
        rows.setdefault(row, {})[col] = val
    for r in sorted(rows):
        if r == 1: continue
        cols = rows[r]
        org = (cols.get(3) or "").strip()
        if org.casefold() == "proline": continue
        rec = {
            "projectCode": want[name],
            "name": (cols.get(2) or "").strip(),
            "organization": org,
            "email": (cols.get(5) or "").strip(),
            "note": (cols.get(4) or "").strip(),
        }
        if rec["name"]:
            people.append(rec)
open(outp, "w", encoding="utf-8").write(json.dumps(people, ensure_ascii=False))
'@
$pyPath = Join-Path $env:TEMP "import-stakeholder-people.py"
$jsonPath = Join-Path $env:TEMP "import-stakeholder-people.json"
[System.IO.File]::WriteAllText($pyPath, $py, [System.Text.UTF8Encoding]::new($false))
python $pyPath $SourceFile $jsonPath
if ($LASTEXITCODE -ne 0) { throw "Excel okunamadi" }
$people = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
Remove-Item $pyPath, $jsonPath -Force

$loginBody = @{ username = $Username; password = $Password; domain = $Domain } | ConvertTo-Json -Compress
$loginFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($loginFile, $loginBody, [System.Text.UTF8Encoding]::new($false))
$loginOut = [System.IO.Path]::GetTempFileName()
& curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$loginFile" -o $loginOut "$GatewayBaseUrl/keeper/api/auth/token" | Out-Null
Remove-Item $loginFile -Force
$login = ([System.IO.File]::ReadAllText($loginOut).Trim().TrimStart([char]0xFEFF) | ConvertFrom-Json)
Remove-Item $loginOut -Force
if (-not $login.accessToken) { throw "Token alinamadi" }
$script:token = $login.accessToken

function Invoke-Api([string]$Method, [string]$Url, [string]$BodyFile = "") {
    $out = [System.IO.Path]::GetTempFileName()
    $args = @("-s", "-k", "-X", $Method, "-w", "%{http_code}", "-o", $out, "-H", "Authorization: Bearer $script:token", "--max-time", "60")
    if ($BodyFile) { $args += @("-H", "Content-Type: application/json; charset=utf-8", "--data-binary", "@$BodyFile") }
    $code = (& curl.exe @args $Url)
    $text = ""
    if (Test-Path -LiteralPath $out) {
        $text = [System.IO.File]::ReadAllText($out)
        Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue
    }
    return @{ Code = "$code".Trim(); Body = $text }
}

function ConvertTo-JsonString([string]$Value) {
    if ($null -eq $Value) { return "null" }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        $code = [int]$ch
        switch ($code) {
            8 { [void]$sb.Append('\b'); break }
            9 { [void]$sb.Append('\t'); break }
            10 { [void]$sb.Append('\n'); break }
            12 { [void]$sb.Append('\f'); break }
            13 { [void]$sb.Append('\r'); break }
            34 { [void]$sb.Append('\"'); break }
            92 { [void]$sb.Append('\\'); break }
            default {
                if ($code -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', $code) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

$projRes = Invoke-Api GET "$GatewayBaseUrl/operations/api/v1/projects"
if ($projRes.Code -ne "200") { throw "projeler HTTP $($projRes.Code)" }
$projects = @{}
foreach ($p in @($projRes.Body | ConvertFrom-Json)) { $projects[$p.code] = $p.id }

$created = 0
$skipped = 0
$failed = @()
foreach ($code in @("TCDD-BV", "DDYS")) {
    $projectId = $projects[$code]
    if (-not $projectId) { throw "Proje yok: $code" }
    $list = Invoke-Api GET "$GatewayBaseUrl/operations/api/v1/projects/$projectId/meeting-people"
    if ($list.Code -ne "200") { throw "kisiler HTTP $($list.Code) $code" }
    $existingEmails = @{}
    $existingNames = @{}
    foreach ($row in @($list.Body | ConvertFrom-Json)) {
        if ($row.email) { $existingEmails[$row.email.ToLowerInvariant()] = $true }
        if ($row.name) { $existingNames[$row.name.ToLowerInvariant()] = $true }
    }
    $batch = @($people | Where-Object { $_.projectCode -eq $code })
    Write-Host "$code mevcut=$($existingNames.Count) yazilacak=$($batch.Count)"
    foreach ($person in $batch) {
        $emailKey = ""
        if ($person.email) { $emailKey = $person.email.ToLowerInvariant() }
        $nameKey = $person.name.ToLowerInvariant()
        if (($emailKey -and $existingEmails.ContainsKey($emailKey)) -or $existingNames.ContainsKey($nameKey)) {
            $skipped++
            continue
        }
        $emailJson = "null"
        $noteJson = "null"
        $orgJson = "null"
        if ($person.email) { $emailJson = ConvertTo-JsonString $person.email }
        if ($person.note) { $noteJson = ConvertTo-JsonString $person.note }
        if ($person.organization) { $orgJson = ConvertTo-JsonString $person.organization }
        $json = '{"name":' + (ConvertTo-JsonString $person.name) + ',"organization":' + $orgJson + ',"email":' + $emailJson + ',"note":' + $noteJson + '}'
        $bodyFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($bodyFile, $json, [System.Text.UTF8Encoding]::new($false))
        $res = Invoke-Api POST "$GatewayBaseUrl/operations/api/v1/projects/$projectId/meeting-people" $bodyFile
        Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
        if ($res.Code -eq "201" -or $res.Code -eq "200") {
            $created++
            if ($emailKey) { $existingEmails[$emailKey] = $true }
            $existingNames[$nameKey] = $true
        }
        else {
            $snippet = $res.Body
            if ($snippet.Length -gt 180) { $snippet = $snippet.Substring(0, 180) }
            $failed += "$code $($person.name) HTTP $($res.Code) $snippet"
            Write-Host "HATA $code $($person.name) $($res.Code)"
        }
    }
}
Write-Host "BITTI yazilan=$created atlanan=$skipped hata=$($failed.Count)"
foreach ($item in $failed) { Write-Host "KALAN $item" }
Write-Host "IMPORT_DONE"
if ($failed.Count -gt 0) { exit 2 }
exit 0
