# Otomatik Versiyon Artırma Sistemi

Bu sistem, git push yapılmadan önce değişiklik yapılan servislerin versiyon numaralarını otomatik olarak artırır.

## Nasıl Çalışır?

### Pre-Push Hook

Git'in `pre-push` hook'u, her `git push` komutundan önce otomatik olarak çalışır ve:

1. Değişen dosyaları tespit eder (HEAD ile origin/main karşılaştırması)
2. Hangi servislerin değiştiğini belirler
3. Değişen servislerin versiyon numaralarını **PATCH** seviyesinde artırır (örn: 1.0.0 → 1.0.1)
4. Güncellenen versiyon dosyalarını **otomatik olarak commit** eder (`chore: bump versions for changed services`); bu commit aynı push ile birlikte gönderilir

### Versiyon Artırma Türleri

Script şu anda **PATCH** seviyesinde artırma yapar:
- **PATCH** (1.0.0 → 1.0.1): Bug fix, küçük güncellemeler
- **MINOR** (1.0.0 → 1.1.0): Yeni özellikler (backward compatible)
- **MAJOR** (1.0.0 → 2.0.0): Breaking changes

## Kullanım

### Otomatik (Önerilen)

Sadece normal git push yapın:

```bash
git push origin main
```

Hook otomatik olarak çalışır ve değişen servislerin versiyonlarını artırır.

### Manuel Çalıştırma

Script'i manuel olarak çalıştırmak için:

```powershell
# Dry-run (değişiklik yapmadan ne yapacağını göster)
.\scripts\bump-versions.ps1 -BumpType patch -DryRun

# Patch bump (1.0.0 → 1.0.1) — değişiklikleri siz commit edersiniz
.\scripts\bump-versions.ps1 -BumpType patch

# Patch bump + otomatik commit (hook ile aynı davranış)
.\scripts\bump-versions.ps1 -BumpType patch -AutoCommit

# Minor bump (1.0.0 → 1.1.0)
.\scripts\bump-versions.ps1 -BumpType minor

# Major bump (1.0.0 → 2.0.0)
.\scripts\bump-versions.ps1 -BumpType major
```

## Desteklenen Servisler

### Backend Servisleri (.csproj)
- MngAdmin
- MngScheduler
- MngNotifier
- MngLLM
- MngGateway
- MngDataGateway
- MngKeeper
- MngHub
- MngReactor

### WebUI Uygulamaları (package.json)
- Mng.Ui
- MngDomainUI

## Versiyon Formatı

### Backend (.csproj)
```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

### WebUI (package.json)
```json
{
  "version": "1.0.0"
}
```

## Hook Kurulumu

Hook repoda takip edilmez; yeni klonlarda veya ekip arkadaşlarında kurulum için:

```bash
# Git Bash / Linux / macOS
cp scripts/hooks/pre-push .git/hooks/pre-push
chmod +x .git/hooks/pre-push
```

Windows'ta PowerShell ile doğrudan hook kullanıyorsanız, `scripts/hooks/pre-push.ps1` dosyasını `.git/hooks/pre-push.ps1` olarak kopyalayın. Şablonlar **-AutoCommit** ile çalışır; bump sonrası değişen proje/package dosyaları otomatik commit edilir.

## Hook'u Devre Dışı Bırakma

Hook'u geçici olarak atlamak için:

```bash
git push --no-verify origin main
```

## Sorun Giderme

### Hook Çalışmıyor

1. Hook dosyasının çalıştırılabilir olduğundan emin olun:
   ```bash
   # Git Bash'te
   chmod +x .git/hooks/pre-push
   ```

2. Hook'un doğru konumda olduğunu kontrol edin: `.git/hooks/pre-push`. Yoksa veya eskiyse, `scripts/hooks/` içindeki şablonları buraya kopyalayın (yukarıdaki "Hook Kurulumu" bölümüne bakın).

### Script Hatası

Eğer script hata verirse, hook size devam edip etmek istediğinizi sorar. İsterseniz `--no-verify` ile atlayabilirsiniz.

### Yanlış Servis Algılanıyor

Script, dosya yoluna göre servis algılar. Eğer bir servis yanlış algılanıyorsa, `scripts/bump-versions.ps1` dosyasındaki `$Services` hash tablosunu kontrol edin.

## Notlar

- Hook sadece **değişiklik olan servislerin** versiyonunu artırır
- Hook **-AutoCommit** ile çalışır: versiyon güncellemeleri aynı push öncesi otomatik commit edilir
- Manuel çalıştırmada `-AutoCommit` kullanmazsanız, commit'i siz yaparsınız
- `docs/`, `scripts/tests/` gibi klasörlerdeki değişiklikler servis versiyonlarını etkilemez
