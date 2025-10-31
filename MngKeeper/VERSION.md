# Versiyonlama Stratejisi

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
1.1.0 → Yeni özellik (refresh token)
2.0.0 → Breaking change (API değişikliği)
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

Her projede versiyonu tanımlayın:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
</PropertyGroup>
```

### 2. Git Tags

Her release için git tag oluşturun:

```bash
# Tag oluşturma
git tag -a v1.0.0 -m "Release v1.0.0 - Initial production release"

# Tag'ları push etme
git push origin v1.0.0

# Tüm tag'ları push etme
git push --tags
```

### 3. CHANGELOG.md

Her versiyon için değişiklikleri kaydedin:

```markdown
# Changelog

## [1.1.0] - 2025-10-31

### Added
- Refresh token support
- Swagger + Scalar UI
- Seq logging integration

### Changed
- GetToken endpoint iyileştirildi
- Test controller'lar kaldırıldı

### Fixed
- Schema ID conflict düzeltildi
```

## Mevcut Versiyon

**v1.0.0** - Initial Release (31 Ekim 2025)

