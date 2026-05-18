# Config string Base64 doğrulama: decode, JSON parse, backslash ve geçersiz karakter kontrolü
param([Parameter(Mandatory)] [string] $Base64)

$ErrorActionPreference = 'Stop'

# 1) Geçersiz Base64 karakteri var mı?
$validChars = [regex]'^[A-Za-z0-9+/=-_]+$'
if ($Base64 -notmatch $validChars) {
    $bad = [char[]]$Base64 | Where-Object { $_ -notmatch '[A-Za-z0-9+/=\-_]' }
    Write-Host "HATA: Base64'te geçersiz karakter var. ASCII kodlari: $(($bad | ForEach-Object { [int][char]$_ }) -join ',')"
    exit 1
}
Write-Host "[OK] Sadece geçerli Base64 karakterleri (A-Za-z0-9+/= veya URL-safe -_)"

# 2) URL-safe ise standart Base64'e çevir
$standardB64 = $Base64.Replace('-', '+').Replace('_', '/')
# Padding (uzunluk 4'ün katı)
while ($standardB64.Length % 4 -ne 0) { $standardB64 += '=' }

# 3) Decode
try {
    $bytes = [Convert]::FromBase64String($standardB64)
} catch {
    Write-Host "HATA: Base64 decode basarisiz: $($_.Exception.Message)"
    exit 1
}
Write-Host "[OK] Base64 decode basarili. Byte uzunlugu: $($bytes.Length)"

$json = [System.Text.Encoding]::UTF8.GetString($bytes)

# 4) Decode edilen JSON'da backslash var mı?
if ($json.Contains('\')) {
    Write-Host "HATA: Decode edilen JSON icinde backslash (\) var. Bu Engine'da 'non-base 64 character' hatasina yol acabilir."
    $idx = $json.IndexOf('\')
    $snippet = $json.Substring([Math]::Max(0, $idx - 20), [Math]::Min(60, $json.Length - [Math]::Max(0, $idx - 20)))
    Write-Host "  Ornek (backslash civari): ...$snippet..."
    exit 1
}
Write-Host "[OK] Decode edilen JSON'da backslash yok"

# 5) \u002B veya \u0002B literal (6/7 karakter) var mı?
if ($json.Contains('\u002B') -or $json.Contains('\u0002B')) {
    Write-Host "UYARI: JSON icinde \u002B veya \u0002B literal dizisi var (arti escape)"
} else {
    Write-Host "[OK] JSON icinde \u002B / \u0002B literal yok"
}

# 6) JSON parse
try {
    $obj = $json | ConvertFrom-Json
} catch {
    Write-Host "HATA: JSON parse basarisiz: $($_.Exception.Message)"
    exit 1
}
Write-Host "[OK] JSON parse basarili"

# 7) Beklenen anahtarlar
$required = @('CompressPbk', 'CompressPrk', 'EngineInfo')
foreach ($key in $required) {
    if (-not $obj.PSObject.Properties[$key]) {
        Write-Host "HATA: Eksik alan: $key"
        exit 1
    }
    $len = $obj.$key.Length
    Write-Host "[OK] $key mevcut, uzunluk: $len"
}

# 8) Arti (+) karakteri değerlerde var mı (normal Base64) - bilgi
$plusCount = ([regex]::Matches($json, '\+')).Count
$slashCount = ([regex]::Matches($json, '/')).Count
Write-Host "[Bilgi] Decode edilen JSON'da + sayisi: $plusCount, / sayisi: $slashCount (beklenen, ic Base64'lerde)"

Write-Host ""
Write-Host "Sonuc: Config string GECERLI ve Engine'da parse edilebilir olmali."
