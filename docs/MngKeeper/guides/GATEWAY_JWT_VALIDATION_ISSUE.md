# Gateway JWT Validation Sorunu

## Sorun

Gateway üzerinden MngKeeper endpoint'lerine erişimde 401 (Unauthorized) hatası alınıyor. Token alınabiliyor ancak diğer endpoint'ler 401 veriyor.

## Sebep

Multi-tenant sistemde her domain'in kendi Keycloak realm'i var:
- Token issuer: `http://keycloak:8080/realms/meral` (domain realm)
- Gateway beklenen: `http://keycloak:8080/realms/monitra` (master realm)

JWT Bearer middleware, `Authority` ayarlandığında belirtilen realm'in metadata endpoint'inden signing key'i almaya çalışıyor. Ancak token farklı bir realm'den geldiği için validation başarısız oluyor.

## Geçici Çözüm

Şu anda gateway'de JWT validation şu şekilde yapılandırılmış:
- `ValidateIssuer = false` - Multi-realm desteği için
- `ValidateAudience = false` - Multi-realm desteği için
- `ValidateIssuerSigningKey = false` - Geçici olarak devre dışı
- `Authority` kaldırıldı - Multi-realm desteği için

**Not:** Bu yapılandırma güvenlik açısından ideal değildir. Production için multi-realm signing key validation implementasyonu gereklidir.

## Kalıcı Çözüm (TODO)

1. **Dynamic Signing Key Validation**: Token'ın issuer'ından (realm'den) signing key'i dinamik olarak almak
2. **Custom JWT Validator**: Multi-realm desteği için özel JWT validator middleware'i yazmak
3. **Token Realm Detection**: Token'dan realm bilgisini çıkarıp, o realm'in metadata endpoint'inden signing key'i almak

## Şu Anki Durum

- ✅ Token alma çalışıyor: `https://localhost:5040/keeper/api/auth/token`
- ❌ Diğer endpoint'ler 401 veriyor (JWT validation sorunu)

## Önerilen Geçici Çözüm

Gateway'de JWT validation'ı şimdilik bypass edip, validation'ı MngKeeper'da yapmak. Gateway sadece routing yapar, authentication MngKeeper'da yapılır.

