param([string]$Server = "192.168.20.8")

Import-Module Posh-SSH -Force
$odakScripts = (Resolve-Path (Join-Path $PSScriptRoot "../../../../scripts/odak")).Path
. (Join-Path $odakScripts "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$mongoJs = @'
const dbx = db.getSiblingDB("mng_odak");
print("musteriler=" + dbx.odak_musteriler.countDocuments({}));
print("paketler=" + dbx.odak_is_paketleri.countDocuments({}));
print("kalemler=" + dbx.odak_siparis_kalemleri.countDocuments({}));
print("ncr=" + dbx.odak_ncr.countDocuments({}));
print("capa=" + dbx.odak_capa.countDocuments({}));
print("sevkiyat=" + dbx.odak_sevkiyatlar.countDocuments({}));
dbx.odak_musteriler.find({ unvan: /[ıİşğüöçŞĞÜÖÇ]/ }).limit(5).forEach(function(d){ print("UNVAN: " + d.unvan); });
dbx.odak_siparis_kalemleri.find({ description: /[ıİ]/ }).limit(3).forEach(function(d){ print("DESC: " + d.description.substring(0,100)); });
'@

$s = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    $r = Invoke-OdakMongoJsonEval -SshSession $s -JavaScript $mongoJs
    Write-Host ($r.Output -join "`n")
}
finally {
    Remove-SSHSession -SessionId $s.SessionId | Out-Null
}
