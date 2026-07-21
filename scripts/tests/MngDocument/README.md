# DI-T — Document Intelligence auth / permission test suite

**Kapsam:** T-0 fixture + T-1 permission matrisi (Faz 3 `document_intelligence` Roadmap §5)

## Çalıştırma

```powershell
cd scripts/tests/MngDocument
pwsh .\runner.ps1
# veya
pwsh .\runner.ps1 -Gateway http://localhost:5040
```

## Personae (`auth/personas.json`)

| Persona | Kullanıcı | Grup |
|:---|:---|:---|
| Admin | `odak_admin` | admins |
| EditorA / Cross | `test.user5` | developers |
| ViewerB | `test.user1` | testers |
| Outsider | `guest` | guests |

Şifre: `odak_admin` → `Admin123!`; diğerleri → `Sm123!?`  
Fixture prod’a yazılmaz; varsayılan hedef local gateway.

## Çalıştırma

| Suite | Komut |
|:---|:---|
| Hepsi | `pwsh .\runner.ps1` |
| T-1 only | `pwsh .\runner.ps1 -SkipT2` |
| T-2 only | `pwsh .\runner.ps1 -SkipT1` |

| Suite | Dosya | Son sonuç (local) |
|:---|:---|:---|
| T-1 | `suites/permissions/test-permission-matrix.ps1` | 31 PASS |
| T-2 | `suites/permissions/test-inheritance.ps1` | 12 PASS |
