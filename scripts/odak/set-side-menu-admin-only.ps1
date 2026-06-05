param(
    [string]$Server = "192.168.20.8",
    [string[]]$DataIds = @(),
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
. (Join-Path $PSScriptRoot "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

# POC / ornek menuler + onceki onerideki opsiyoneller (sadece prod)
if (-not $DataIds -or $DataIds.Count -eq 0) {
    $DataIds = @(
        "2d639fcb-4ae1-42c0-9615-871d2e5883b6", # Ornekler header
        "8fa228c1-aa8a-4992-ab22-f46dc92a16ee", # Kitap Tanimlamalari
        "d9e65964-db22-4690-a97a-48953e41157c", # Kitap Turleri
        "948abbb1-3c24-4098-97fd-6b9ddb453082", # Kitap Yayincilar
        "85540f4d-bac2-4688-a962-743a7194c43c", # Kitaplar
        "e3cb7672-a4ed-419e-bef1-eeb03d4c2a35", # Dashboard 1
        "4038f827-a128-482a-af92-fc0dc13eb8ce", # Dashboard 2
        "b0b1dc40-6ce7-4d56-b5db-c5d1c2a3cc52", # Sohbet Odasi
        "561fac3f-0979-4ac6-ba5c-966f4b8716b5", # Monitoring Ayarlar
        # Hazir olmayan merkezler (prod)
        "d94af9d7-22bf-4511-9bdf-0413d9329b40",
        "a635b12f-f675-4266-b09f-246fbc616c48",
        "a584e005-b17f-4343-9c0a-fd90bc5d7678",
        "c57ddca2-310b-4006-8ff6-f6f0ca4df821",
        "860b7b1c-d457-4337-a509-d405b2a9c53a",
        "fe219146-eab7-47c7-85c3-8b0df92c6365",
        "b32856e3-7ebb-42fe-a2ff-daffc97bc4ca",
        "e8f87cab-204d-4b81-83c6-aab4d29863de",
        "5b0a4ba7-245b-4195-a639-6f0ee0532134",
        "5a81aec0-9c5d-4367-b5ee-c1c99b9b3986",
        "0e2be527-fe1d-40ec-8d8d-5f848bbac79f",
        # Management > Arayuz + Data (prod - manager gormesin)
        "5576629f-b34c-4bbd-89db-0f54c319c470",
        "a44d5717-2697-458c-a807-18cec9d79e7f",
        "ee539ee7-d5af-494f-8683-0c1ba105c137",
        "ad6fc4d7-0cbf-4f2e-b03f-3f905210975b",
        "6932a6b3-b14c-458d-a747-1a848fd434ed",
        "43d1751c-8745-431b-9075-262139001237",
        "6fc23e2d-72de-4337-8da6-fd1f5eb19660",
        "7d5f6af5-e5c9-434a-a7d1-5b0fb0ef8f49"
    )
}

$idJson = ($DataIds | ForEach-Object { "`"$_`"" }) -join ","
$dry = if ($WhatIf) { "true" } else { "false" }

$remote = ConvertTo-UnixShell @"
docker exec mongo mongosh -u admin -p admin123 --authenticationDatabase admin --quiet --eval '
const ids = [$idJson];
const col = db.getSiblingDB("mng_odak").getCollection("@side_menu");
const dry = $dry;
ids.forEach(id => {
  const doc = col.findOne({ __dataId: id });
  if (!doc) { print("MISSING:", id); return; }
  const label = doc.header || doc.title || id;
  print((dry ? "WOULD_SET " : "SET ") + label + " | " + (doc.pageType||"?") + " -> admin");
  if (!dry) col.updateOne({ __dataId: id }, { `$set: { pageType: "admin" } });
});
print("done");
'
"@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
$r = Invoke-SSHCommand -SessionId $s.SessionId -Command $remote -TimeOut 60
$r.Output | ForEach-Object { Write-Host $_ }
if ($r.ExitStatus -ne 0) { exit 1 }
Remove-SSHSession $s.SessionId
