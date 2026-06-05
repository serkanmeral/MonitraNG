# mng_odak.@groups -> __dataId (Production 192.168.20.8)
# Usage: $map = & .\resolve-odak-group-ids-prod.ps1 -Names "MonitraNG Users","admins"

param(
    [string[]]$Names = @("MonitraNG Users", "admins"),
    [string]$Server = "192.168.20.8"
)

$ErrorActionPreference = "Stop"
Import-Module Posh-SSH -Force
$odakScripts = (Resolve-Path (Join-Path $PSScriptRoot "../../../../scripts/odak")).Path
. (Join-Path $odakScripts "OdakSshCommon.ps1")
Initialize-OdakSshEnvironment -Server $Server
$cred = Get-OdakSshCredential -Server $Server

$map = [ordered]@{}
$session = New-SSHSession -ComputerName $Server -Credential $cred -AcceptKey
try {
    foreach ($name in $Names) {
        $escaped = $name -replace "'", "\'"
        $js = "const g=db.getSiblingDB('mng_odak').getCollection('@groups').findOne({name:'$escaped'}); if(g) print(g.__dataId);"
        $cmd = "docker exec mongo mongosh -u admin -p admin123 --authenticationDatabase admin --quiet --eval `"$js`""
        $result = Invoke-SSHCommand -SessionId $session.SessionId -Command $cmd
        if ($result.ExitStatus -ne 0) {
            throw "SSH hata ($name): $($result.Error)"
        }
        $id = ($result.Output -split "`n" | Where-Object { $_ -match '\S' } | Select-Object -Last 1).Trim()
        if ([string]::IsNullOrEmpty($id)) {
            Write-Host "UYARI: Grup bulunamadi: $name" -ForegroundColor Yellow
        }
        else {
            $map[$name] = $id
        }
    }
    return [pscustomobject]$map
}
finally {
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
