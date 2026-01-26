# MkDocs Kullanım Rehberi

## Önemli Not

Sisteminizde **LibreOffice'in Python'u** PATH'te öncelikli olduğu için, **Microsoft Store Python'unu** doğrudan kullanmanız gerekiyor.

## MkDocs'i Çalıştırma

### Yöntem 1: PowerShell Script (Önerilen)

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs
.\run_mkdocs.ps1
```

### Yöntem 2: Tam Path ile

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\python.exe" -m mkdocs serve --dev-addr=127.0.0.1:6010
```

### Yöntem 3: Alias Oluşturma (Kalıcı Çözüm)

PowerShell profil dosyanıza şunu ekleyin:

```powershell
# PowerShell profil dosyasını aç
notepad $PROFILE

# Şu satırı ekle:
Set-Alias python "$env:LOCALAPPDATA\Microsoft\WindowsApps\python.exe"
```

Sonra yeni terminal açıp:

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs
python -m mkdocs serve --dev-addr=127.0.0.1:6010
```

## Tarayıcıda Görüntüleme

MkDocs başladıktan sonra tarayıcıda şu adrese gidin:
**http://127.0.0.1:6010**

## Durdurma

Terminal'de `Ctrl+C` tuşlarına basın.
