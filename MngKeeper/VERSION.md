# MngKeeper Versiyonlama

## Mevcut Versiyon: v1.0.0

## Semantic Versioning (SemVer)

Format: `MAJOR.MINOR.PATCH`

### Versiyon Numarası Kuralları

- **MAJOR** (1.x.x): Breaking changes, API değişiklikleri
- **MINOR** (x.1.x): Yeni özellikler (backward compatible)
- **PATCH** (x.x.1): Bug fixes, küçük güncellemeler

### Örnek

```
1.0.0 → İlk production release
1.0.1 → Bug fix
1.1.0 → Yeni özellik (user export)
2.0.0 → Breaking change (API v2)
```

## Pre-release Versiyonları

```
1.0.0-alpha.1  → İlk geliştirme
1.0.0-beta.1   → Test aşaması
1.0.0-rc.1     → Release candidate
1.0.0          → Stable release
```

## Versiyon Yönetimi

### 1. Assembly Version (.csproj)

Tüm projelerde aynı versiyon:
- MngKeeper.Api
- MngKeeper.Application
- MngKeeper.Domain
- MngKeeper.Infrastructure
- MngKeeper.Persistence

### 2. Git Tags

Format: `mngkeeper-vX.Y.Z`

```bash
git tag -a mngkeeper-v1.0.0 -m "Release description"
git push origin mngkeeper-v1.0.0
```

### 3. Version Script Kullanımı

```powershell
# Mevcut versiyonu göster
.\version.ps1 current

# Patch bump (1.0.0 → 1.0.1)
.\version.ps1 bump-patch

# Minor bump (1.0.0 → 1.1.0)
.\version.ps1 bump-minor

# Major bump (1.0.0 → 2.0.0)
.\version.ps1 bump-major

# Manuel set
.\version.ps1 set 1.2.3

# Git tag oluştur
.\version.ps1 tag
```

### 4. Otomatik Release

```powershell
# Bug fix release
.\release.ps1 -BumpType patch -Message "Fixed authentication bug"

# Feature release
.\release.ps1 -BumpType minor -Message "Added user export feature"

# Breaking release
.\release.ps1 -BumpType major -Message "API v2.0 - Breaking changes"
```

## Version History

| Version | Date | Git Tag | Description |
|---------|------|---------|-------------|
| 1.0.0 | 2025-10-31 | mngkeeper-v1.0.0 | Initial production release |

