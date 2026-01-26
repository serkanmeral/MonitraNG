# Domain Management - Side Menu Item Verileri

Domain Management sayfasını side menu'ye eklemek için gerekli bilgiler.

## Özellikler

- **Sayfa Tipi**: `manager` (Sadece manager ve admin kullanıcılar görebilir)
- **Route**: `/apps/domain`
- **Icon**: `SettingsIcon` veya `TablerIcon` (tabler:settings)
- **Page Code**: `domain-management` (i18n için unique identifier)

## Yöntem 1: Side Menu Manager UI Üzerinden Ekleme

1. `/apps/side-menu-manager` sayfasına gidin
2. "Yeni Menu Item" butonuna tıklayın
3. Aşağıdaki değerleri girin:

### Form Alanları

| Alan | Değer |
|------|-------|
| **Item Type** | `item` |
| **Page Type** | `manager` |
| **Menu Title** | `Domain Yönetimi` |
| **Page Code** | `domain-management` |
| **Route Path** | `/apps/domain` |
| **Link Type** | `internal` |
| **Icon Type** | `tabler` |
| **Icon** | `settings` (Tabler icons: SettingsIcon) |
| **Order** | Menüdeki konumuna göre (örn: 20) |
| **Parent Menu Item** | `(Yok - Root)` veya uygun bir parent seçin |
| **Level** | Otomatik hesaplanır (parent yoksa 0) |
| **Disabled** | `false` |
| **Sub Caption** | (Opsiyonel) |

### İzinler (Permissions)

Manager sayfaları için permissions opsiyoneldir - Manager/Admin kullanıcılar otomatik olarak erişebilir. Ancak istenirse grup bazlı izinler de tanımlanabilir.

## Yöntem 2: Dataset'e Doğrudan Ekleme (JSON Format)

Eğer API üzerinden veya dataset'e direkt eklemek isterseniz, aşağıdaki JSON formatını kullanabilirsiniz:

### JSON Örneği

```json
{
  "itemType": "item",
  "pageType": "manager",
  "title": "Domain Yönetimi",
  "pageCode": "domain-management",
  "icon": "settings",
  "iconType": "tabler",
  "to": "/apps/domain",
  "type": "internal",
  "parentId": null,
  "level": 0,
  "order": 20,
  "disabled": false,
  "permissions": null
}
```

### Dataset'e Ekleme (PowerShell Script Örneği)

```powershell
# Domain Management menu item'ı oluştur
$menuItem = @{
    itemType = "item"
    pageType = "manager"
    title = "Domain Yönetimi"
    pageCode = "domain-management"
    icon = "settings"
    iconType = "tabler"
    to = "/apps/domain"
    type = "internal"
    parentId = $null
    level = 0
    order = 20
    disabled = $false
    permissions = $null
} | ConvertTo-Json -Depth 10

# Token al (varsa)
$token = Get-Content "$env:TEMP\serkan_token.txt" -ErrorAction SilentlyContinue

# API endpoint
$apiUrl = "https://localhost:5010/api/data/@side_menu"

# Menu item'ı ekle
Invoke-RestMethod -Uri $apiUrl `
    -Method POST `
    -Body $menuItem `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $token" } `
    -SkipCertificateCheck
```

## Locale (Çoklu Dil Desteği)

Menu title için i18n desteği eklemek isterseniz, `tr.json` dosyasına zaten `domain.title` eklenmiş durumda. Eğer `pageCode` kullanıyorsanız, menu title otomatik olarak `domain.title` key'inden çekilecek.

### İsteğe Bağlı: Locale Key Ekleme

Eğer menu'de `pageCode: "domain-management"` kullanıyorsanız, locale dosyasına şunu ekleyebilirsiniz (zaten mevcut):

```json
{
  "domain": {
    "title": "Domain Yönetimi",
    ...
  }
}
```

Menu rendering sırasında `pageCode` varsa, otomatik olarak ilgili locale key'inden title çekilir.

## Icon Seçenekleri

Aşağıdaki icon'lar uygun olabilir:

- `settings` (Tabler - SettingsIcon) ✓ **Önerilen**
- `server` (Tabler - ServerIcon)
- `database` (Tabler - DatabaseIcon)
- `globe` (Tabler - GlobeIcon)
- `building` (Tabler - BuildingIcon)

## Önemli Notlar

1. **Manager Yetkisi**: `pageType: "manager"` olduğu için sadece manager ve admin kullanıcılar bu menu item'ını görebilir.

2. **Order Değeri**: Mevcut menu item'ların order değerlerini kontrol ederek uygun bir değer seçin (örn: Users'tan sonra eklemek için 20-30 arası).

3. **Parent Menu Item**: Eğer bu item'ı bir parent menu item altına eklemek isterseniz (örn: "Yönetim" altına), ilgili parent'ın `__dataId` değerini `parentId` olarak kullanın.

4. **Cache Temizleme**: Menu item eklendikten sonra, menü cache'i otomatik olarak yenilenecek veya kullanıcılar sayfayı yenilediğinde yeni menu görünecek.

## Test

Menu item eklendikten sonra:

1. Manager veya Admin kullanıcı ile giriş yapın
2. Side menu'de "Domain Yönetimi" item'ını görün
3. Tıklayarak `/apps/domain` sayfasına yönlendirildiğinizi doğrulayın
