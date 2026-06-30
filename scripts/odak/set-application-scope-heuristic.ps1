param(
    [string]$Server = "192.168.20.20",
    [string]$Database = "mng_odak",
    [switch]$Apply
)
$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")

Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$applyFlag = if ($Apply) { "true" } else { "false" }

$jsTemplate = @'
const DB = '__DB__';
const APPLY = __APPLY__;

function isMachineOrServiceUser(username, fn, ln) {
  const un = (username || '').trim().toLowerCase();
  if (!un) return true;
  if (un.endsWith('$')) return true;

  const patterns = [
    /^(pc|bc|ec|mc)(-\d+|-[a-z0-9_-]+)?$/,
    /^ik-pc-\d+$/,
    /^win-[a-z0-9]{5,}$/,
    /^(administrator|guest|krbtgt|exchange|terminal|wac|monitra|dummy\d*|ldap-user|erp-user|erp-destek|safetica_dlp|safetica)$/,
    /^dba\d+$/,
    /^test\.user\d+$/,
    /^test\./,
    /^(svc|service)[-_]/,
    /^talasli$/,
  ];
  if (patterns.some(p => p.test(un))) return true;

  if (/^[a-z]{2,5}-\d{2,4}$/.test(un) && !fn && !ln) return true;

  const fnL = (fn || '').trim().toLowerCase();
  if (fnL && !ln && fnL === un && /[-_]|\d/.test(un)) return true;

  return false;
}

function looksLikeRealPerson(username, fn, ln) {
  const f = (fn || '').trim();
  const l = (ln || '').trim();
  const u = (username || '').trim();
  if (f && l) return true;
  if (f && f.includes(' ')) return true;
  if (u.includes(' ') && f) return true;
  if (u.includes('.') && f && l) return true;
  if (f && l && u.includes('_')) return true;
  return false;
}

function classifyUser(u) {
  const fn = (u.firstName || '').trim();
  const ln = (u.lastName || '').trim();
  const username = (u.username || '').trim();
  if (isMachineOrServiceUser(username, fn, ln)) return false;
  return looksLikeRealPerson(username, fn, ln);
}

function isBuiltinAdGroup(name) {
  const exactBuiltins = ['Guests', 'Users', 'Administrators'];
  if (exactBuiltins.includes(name)) return true;

  const builtins = [
    /^Domain /i,
    /^Enterprise /i,
    /^Schema Admins$/i,
    /^Account Operators/i,
    /^Access Control/i,
    /^Backup Operators$/i,
    /^Allowed RODC/i,
    /^Denied RODC/i,
    /^Cert /i,
    /^Certificate Service/i,
    /^Cloneable /i,
    /^Cryptographic /i,
    /^Distributed COM Users$/i,
    /^Dns/i,
    /^Event Log/i,
    /^Group Policy/i,
    /^Hyper-V/i,
    /^IIS_/i,
    /^Incoming Forest/i,
    /^Key Admins$/i,
    /^Network Configuration/i,
    /^Performance (Log|Monitor) Users$/i,
    /^Pre-Windows/i,
    /^Print Operators$/i,
    /^Protected Users$/i,
    /^RAS and IAS/i,
    /^RDS /i,
    /^Read-only Domain/i,
    /^Remote (Desktop|Management) Users$/i,
    /^Replicator$/i,
    /^Server Operators$/i,
    /^Storage Replica/i,
    /^Terminal Server/i,
    /^Terminal-LocalAdmins$/i,
    /^Windows /i,
  ];
  return builtins.some(p => p.test(name));
}

function classifyGroup(g) {
  const name = (g.name || '').trim();
  if (!name) return false;
  if (isBuiltinAdGroup(name)) return false;

  if (name.endsWith(' Users')) return true;
  if (/^MonitraNG /i.test(name)) return true;
  if (/Yonetici Group$/i.test(name)) return true;
  if (name === 'RDP_Yetkili') return true;

  const localTeams = ['developers', 'managers', 'testers', 'viewers', 'admins', 'users', 'guests', 'g2'];
  if (localTeams.includes(name.toLowerCase())) return true;

  return false;
}

const d = db.getSiblingDB(DB);
const usersCol = d.getCollection('@users');
const groupsCol = d.getCollection('@groups');

const userRows = usersCol.find({}).toArray();
const groupRows = groupsCol.find({}).toArray();

let userShow = 0, userHide = 0, userChanged = 0;
let groupShow = 0, groupHide = 0, groupChanged = 0;

print('=== USERS (' + userRows.length + ') ===');
print('mode=' + (APPLY ? 'APPLY' : 'DRY-RUN'));
userRows.sort((a, b) => (a.username || '').localeCompare(b.username || '')).forEach(u => {
  const inc = classifyUser(u);
  if (inc) userShow++; else userHide++;
  const prev = u.includeInApplication === true;
  const changed = prev !== inc;
  if (changed) userChanged++;
  const mark = changed ? (inc ? '-> VISIBLE' : '-> HIDDEN') : (inc ? 'visible' : 'hidden');
  print(JSON.stringify({ username: u.username, firstName: u.firstName || '', lastName: u.lastName || '', includeInApplication: inc, action: mark }));
  if (APPLY) {
    usersCol.updateOne({ _id: u._id }, { $set: { includeInApplication: inc } });
  }
});

print('=== GROUPS (' + groupRows.length + ') ===');
groupRows.sort((a, b) => (a.name || '').localeCompare(b.name || '')).forEach(g => {
  const inc = classifyGroup(g);
  if (inc) groupShow++; else groupHide++;
  const prev = g.includeInApplication === true;
  const changed = prev !== inc;
  if (changed) groupChanged++;
  const mark = changed ? (inc ? '-> VISIBLE' : '-> HIDDEN') : (inc ? 'visible' : 'hidden');
  print(JSON.stringify({ name: g.name, includeInApplication: inc, action: mark }));
  if (APPLY) {
    groupsCol.updateOne({ _id: g._id }, { $set: { includeInApplication: inc } });
  }
});

print('=== SUMMARY ===');
print(JSON.stringify({
  users: { total: userRows.length, visible: userShow, hidden: userHide, changed: userChanged },
  groups: { total: groupRows.length, visible: groupShow, hidden: groupHide, changed: groupChanged },
  applied: APPLY
}));
'@

$js = $jsTemplate.Replace('__DB__', $Database).Replace('__APPLY__', $applyFlag)

$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($js))
$cmd = "echo $b64 | base64 -d | docker exec -i mongo mongosh -u admin -p admin123 --authenticationDatabase admin --quiet"

Write-Host "Odak application scope heuristic ($Database on $Server)" -ForegroundColor Cyan
Write-Host "Mode: $(if ($Apply) { 'APPLY (yaziliyor)' } else { 'DRY-RUN (onizleme)' })" -ForegroundColor Yellow

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-SSHCommand -SessionId $s.SessionId -Command $cmd -TimeOut 180
    if ($r.ExitStatus -ne 0) {
        throw "mongosh failed (exit $($r.ExitStatus)): $($r.Error)"
    }
    $r.Output | ForEach-Object { Write-Host $_ }
}
finally {
    Remove-SSHSession -SessionId $s.SessionId | Out-Null
}

if (-not $Apply) {
    Write-Host ""
    Write-Host "Onizleme tamam. Uygulamak icin: .\scripts\odak\set-application-scope-heuristic.ps1 -Apply" -ForegroundColor Green
}
