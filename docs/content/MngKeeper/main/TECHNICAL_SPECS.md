# MngKeeper Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/keeper/api/` (ör. `https://gateway.example.com/keeper/api/auth/token`)
- **Kimlik doğrulama:** Çoğu endpoint `Authorization: Bearer <access_token>` gerektirir. Token `POST /api/auth/token` ile alınır.
- **Content-Type:** `application/json` (form-data veya belirtilen yerler hariç).

---

## 1. Auth — `api/auth`

Kimlik doğrulama, token ve şifre işlemleri.

### 1.1 Token al

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/token` |
| **Auth** | Yok (AllowAnonymous) |
| **Amaç** | Kullanıcı adı/şifre ile JWT access ve refresh token alır. |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `username` | string | Evet | Giriş adı. İsteğe bağlı: `domain@username` formatında domain bilgisi taşınabilir. | `"admin"`, `"meral@admin"` |
| `password` | string | Evet | Kullanıcı şifresi. | `"***"` |
| `domain` | string | Koşullu | Domain adı. Birden fazla domain varken zorunlu; tek domain varken veya `domain@username` kullanıldığında boş bırakılabilir. | `"meral"` |

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `accessToken` | string | JWT access token; header'da `Bearer` olarak kullanılır. |
| `refreshToken` | string | Yeni access token almak için kullanılır. |
| `tokenType` | string | Genelde `"Bearer"`. |
| `expiresIn` | number | Access token geçerlilik süresi (saniye). |
| `refreshExpiresIn` | number | Refresh token geçerlilik süresi (saniye). |

#### Hata yanıtları (4xx/5xx)

Body tipi: `{ "error": string, "errorDescription": string }`.

| HTTP | error (ör.) | Açıklama |
|------|-------------|----------|
| 400 | `invalid_request` | username/password eksik. |
| 400 | `domain_required` | Birden fazla domain var, domain verilmedi. |
| 400 | `no_domains` | Sistemde domain yok. |
| 401 | `invalid_credentials` | Yanlış kullanıcı adı veya şifre. |
| 500 | `server_error` | Sunucu hatası. |

---

### 1.2 Token yenile

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/refresh` |
| **Auth** | Yok |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `refreshToken` | string | Evet | Daha önce alınan refresh token. | — |
| `domain` | string | Evet | Token’ın ait olduğu domain adı. | `"meral"` |

#### Response (200 OK)

Token al endpoint’i ile aynı yapı: `accessToken`, `refreshToken`, `tokenType`, `expiresIn`, `refreshExpiresIn`.

#### Hata (401)

`error`: `invalid_token` — Geçersiz veya süresi dolmuş refresh token.

---

### 1.3 Token iptal (logout)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/revoke` |
| **Auth** | Yok |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `refreshToken` | string | Evet | İptal edilecek refresh token. |
| `domain` | string | Evet | Domain adı. |

#### Response (200 OK)

`{ "message": "Token revoked successfully" }`

---

### 1.4 Şifre değiştir

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/change-password` |
| **Auth** | Evet (AuthenticatedAuthorization) |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `currentPassword` | string | Evet | Mevcut şifre. |
| `newPassword` | string | Evet | Yeni şifre; şifre politikasına uygun olmalı. |

#### Response (200 OK)

`{ "message": "Password changed successfully" }`

#### Hata (400/401)

- `invalid_request`: current/new password eksik.
- `invalid_password`: Politika sağlanmıyor veya mevcut şifre hatalı.

---

### 1.5 Şifre sıfırla (token ile)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/reset-password` |
| **Auth** | Yok |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `token` | string | Evet | E-posta veya create-reset-token ile alınan sıfırlama token’ı. |
| `newPassword` | string | Evet | Yeni şifre; politika uyumlu olmalı. |

#### Response (200 OK)

`{ "message": "Password reset successfully" }`

---

### 1.6 Sıfırlama token’ı oluştur (Admin)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/auth/create-reset-token` |
| **Auth** | Evet (AdminAuthorization) |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama | Varsayılan |
|----------|-----|---------|----------|------------|
| `username` | string | Evet | Kullanıcı adı. | — |
| `domain` | string | Evet | Domain adı. | — |
| `expirationHours` | number | Hayır | Token geçerlilik süresi (saat). | `1` |

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `token` | string | Oluşturulan sıfırlama token’ı. |
| `expiresAt` | string (ISO 8601) | Son kullanım tarihi. |
| `userId` | string | Kullanıcı ID. |
| `username` | string | Kullanıcı adı. |

---

## 2. Domain — `api/domain`

Domain CRUD ve domain veritabanı koleksiyonları.

### 2.1 Domain oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/domain` |
| **Auth** | Uygulama ayarına bağlı (ör. Admin) |

#### Request body (CreateDomainCommand)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek / Varsayılan |
|----------|-----|---------|----------|--------------------|
| `domainName` | string | Evet | Tekil domain kodu (realm/db adı). | `"acme"` |
| `displayName` | string | Evet | Görünen ad. | `"Acme Şirketi"` |
| `adminEmail` | string | Evet | İlk admin e-posta. | `"admin@acme.com"` |
| `adminPassword` | string | Evet | İlk admin şifresi. | — |
| `settings` | object | Hayır | Domain ayarları. | Aşağıda |
| `relatedPersonPhone` | string | Hayır | İletişim telefonu. | — |
| `relatedPersonEmail` | string | Hayır | İletişim e-posta. | — |
| `logo` | string | Hayır | Logo (örn. base64). | — |
| `logoUrl` | string | Hayır | Logo URL. | — |
| `initialDataTemplateName` / `templateName` | string | Hayır | İlk veri şablonu adı. | — |

**settings** alt alanları:

| Alan adı | Tip | Zorunlu | Açıklama | Varsayılan |
|----------|-----|---------|----------|------------|
| `maxUsers` | number | Hayır | Maksimum kullanıcı. | `100` |
| `maxAssets` | number | Hayır | Maksimum varlık. | `1000` |
| `enableMqtt` | boolean | Hayır | MQTT açık mı. | `true` |
| `mqttSettings` | object | Hayır | brokerHost, brokerPort, username, password, topicPrefix. | — |
| `customSettings` | object | Hayır | Serbest ayar çiftleri. | `{}` |

#### Response (201 Created)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `domainId` | string | Oluşturulan domain ID. |
| `domainName` | string | Domain adı. |
| `databaseName` | string | Oluşturulan veritabanı adı. |
| `adminUsername` | string | İlk admin kullanıcı adı. |
| `adminEmail` | string | İlk admin e-posta. |
| `createdAt` | string (ISO 8601) | Oluşturulma zamanı. |
| `isSuccess` | boolean | İşlem başarılı mı. |
| `errorMessage` | string | Hata mesajı (başarısızsa). |
| `message` | string | İsteğe bağlı mesaj. |
| `failedStep` | string | Hata hangi adımda oluştu (varsa). |

#### Hata (400 / 409)

- 400: Geçersiz veri.
- 409: Aynı isimde domain zaten var.

---

### 2.2 Domain listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/domain` |
| **Auth** | Uygulama ayarına bağlı |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Örnek |
|-----------|-----|---------|----------|--------|
| `status` | string (enum) | Hayır | Filtre: Pending, Active, Suspended, Expired, Deleted, Failed. | `Active` |

#### Response (200 OK)

Domain entity dizisi. Her öğe örneğin: `id`, `name`, `displayName`, `databaseName`, `realmName`, `storageBucket`, `storageQuota`, `storageUsed`, `status`, `settings`, `createdAt`, `updatedAt`, `relatedPersonPhone`, `logo`, `logoUrl`, `licenseInfo`.

---

### 2.3 Domain getir (ID)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/domain/{id}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | string | Evet | Domain ObjectId. |

#### Response (200 OK)

Tek bir Domain entity. 404: Bulunamadı.

---

### 2.4 Domain getir (ad)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/domain/name/{name}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `name` | string | Evet | Domain adı. |

#### Response (200 OK)

Tek Domain entity. 404: Bulunamadı.

---

### 2.5 Domain güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/domain/{id}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | string | Evet | Domain ID. |

#### Request body

Gönderilen Domain nesnesinden güncellenen alanlar: `displayName`, `settings`, `relatedPersonPhone`, `logo`, `logoUrl`. Diğer alanlar path/mevcut kayıttan alınır.

#### Response (200 OK)

Güncellenmiş Domain entity. 404: Domain yok.

---

### 2.6 Domain sil (soft delete)

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/domain/{id}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | string | Evet | Domain ID. |

#### Response (204 No Content)

Başarılı. Status `Deleted` yapılır. 404: Domain yok.

---

### 2.7 Domain koleksiyonları

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/domain/{id}/collections` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | string | Evet | Domain ID. |

#### Response (200 OK)

`Array<{ name: string, documentCount: number, hasIndexes: boolean }>`. 404: Domain bulunamadı.

---

## 3. User — `api/user`

Kullanıcı CRUD, gruplar, foto ve şifre sıfırlama talebi. Tüm endpoint’ler Manager veya Admin yetkisi (JWT’den domain bağlamı) gerektirir.

### 3.1 Kullanıcı oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/user` |
| **Auth** | ManagerAuthorization |

#### Request body (CreateUserCommand)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `username` | string | Evet | Benzersiz kullanıcı adı (domain içinde). | `"j.doe"` |
| `email` | string | Evet | E-posta adresi. | `"j.doe@example.com"` |
| `password` | string | Hayır | Şifre; yoksa sıfırlama ile verilebilir. | — |
| `firstName` | string | Evet | Ad. | `"Jane"` |
| `lastName` | string | Evet | Soyad. | `"Doe"` |
| `title` | string | Hayır | Ünvan. | — |
| `department` | string | Hayır | Departman. | — |
| `gender` | number (enum) | Hayır | 0=NotSpecified, 1=Male, 2=Female. | `0` |
| `phoneNumber` | string | Hayır | Telefon. | — |
| `photoUrl` | string | Hayır | Foto URL. | — |
| `groupIds` | string[] | Hayır | Atanacak grup ID’leri. | `[]` |
| `isActive` | boolean | Hayır | Aktif mi. | `true` |
| `customData` | object | Hayır | DataGateway’e sync edilen ek veri. | — |

#### Response (201 Created)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `userId` | string | Yeni kullanıcı ID. |
| `username`, `email`, `firstName`, `lastName`, `title`, `department`, `gender`, `phoneNumber`, `photoUrl` | — | Girilen değerler. |
| `isActive` | boolean | — |
| `createdAt` | string (ISO 8601) | — |
| `isSuccess` | boolean | — |
| `errorMessage` | string | Hata durumunda. |

---

### 3.2 Tek kullanıcı getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/user/{userId}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Kullanıcı ID. |

#### Response (200 OK)

`{ "user": UserDto | null, "isSuccess": boolean, "errorMessage": string | null }`

**UserDto** alanları: `userId`, `username`, `email`, `firstName`, `lastName`, `title`, `department`, `gender`, `phoneNumber`, `photoUrl`, `isActive`, `createdAt`, `updatedAt`, `createdBy`, `updatedBy`, `groups` (string[]), `permissions` (string[]).

404: Kullanıcı bulunamadığında `isSuccess: false` ile body döner.

---

### 3.3 Kullanıcı listele (sayfalı)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/user` |
| **Auth** | ManagerAuthorization |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `page` | number | Hayır | Sayfa numarası. | `1` |
| `pageSize` | number | Hayır | Sayfa boyutu. | `10` |
| `searchTerm` | string | Hayır | Ad/soyad/email/username’de arama. | — |
| `isActive` | boolean | Hayır | Sadece aktif/pasif. | — |
| `sortBy` | string | Hayır | Sıralama alanı. | — |
| `sortOrder` | string | Hayır | `"asc"` veya `"desc"`. | — |

#### Response (200 OK)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `users` | UserDto[] | Sayfa verisi. |
| `totalCount` | number | Toplam kayıt. |
| `page` | number | Mevcut sayfa. |
| `pageSize` | number | Sayfa boyutu. |
| `totalPages` | number | Toplam sayfa. |
| `isSuccess` | boolean | — |
| `errorMessage` | string | Hata varsa. |

---

### 3.4 Kullanıcı güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/user/{userId}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Güncellenecek kullanıcı ID. |

#### Request body (UpdateUserCommand)

CreateUserCommand ile aynı alan seti; `userId` path’ten gelir. `groupIds`: `null` = değiştirme, `[]` = tümünü kaldır, dolu dizi = bu ID’leri ata. `customData`: DataGateway’e sync edilir.

#### Response (200 OK)

`userId`, `username`, `email`, `firstName`, `lastName`, `title`, `department`, `gender`, `phoneNumber`, `photoUrl`, `groupIds`, `isActive`, `updatedAt`, `isSuccess`, `errorMessage`.

---

### 3.5 Kullanıcı sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/user/{userId}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Silinecek kullanıcı ID. |

#### Response (204 No Content)

Başarılı. 400: Hata gövdesi (ör. `isSuccess: false`, `errorMessage`).

---

### 3.6 Kullanıcıyı gruba ekle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/user/{userId}/groups/{groupId}` |
| **Auth** | ManagerAuthorization (domain JWT’den) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Kullanıcı ID. |
| `groupId` | string | Evet | Grup ID. |

#### Response (200 OK)

`AddUserToGroupResponse` (örn. `isSuccess`, `errorMessage`). 400: Domain bilgisi veya işlem hatası.

---

### 3.7 Kullanıcıyı gruptan çıkar

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/user/{userId}/groups/{groupId}` |
| **Auth** | ManagerAuthorization |

Path ve davranış yukarıdakinin tersi; response tipi `RemoveUserFromGroupResponse`.

---

### 3.8 Kullanıcı fotoğrafı yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/user/{userId}/photo` |
| **Content-Type** | `multipart/form-data` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Kullanıcı ID. |

#### Request body (form)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `file` | file | Evet | JPEG/PNG/WebP; en fazla 5MB. |

#### Response (200 OK)

`{ "photoUrl": string, "url": string, "fileUrl": string }` — Örn. `/keeper/api/user/{userId}/photo`

---

### 3.9 Kullanıcı fotoğrafı getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/user/{userId}/photo` veya `/api/user/{userId}/photo.{ext}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Kullanıcı ID. |
| `ext` | string | Hayır | Uzantı (örn. jpg, png). |

Yanıt: image bytes (Content-Type: image/jpeg vb.). 404: Foto yok.

---

### 3.10 Kullanıcı fotoğrafı sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/user/{userId}/photo` |
| **Auth** | ManagerAuthorization |

#### Response (200 OK)

`{ "message": "Photo removed successfully." }`

---

### 3.11 Şifre sıfırlama talebi (e-posta ile)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/user/{userId}/request-password-reset` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `userId` | string | Evet | Kullanıcı ID. |

Kullanıcının e-posta adresine sıfırlama linki gönderilir (MngNotifier).

#### Response (200 OK)

`{ "isSuccess": true, "message": "Password reset email sent successfully.", "expiresAt": string }`

404: Kullanıcı yok. 400: E-posta adresi yok.

---

### 3.12 Kullanıcı dışa aktar

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/user/export` |
| **Auth** | ManagerAuthorization |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `format` | string | Hayır | `csv`, `xlsx`, `json`. | `csv` |
| `searchTerm` | string | Hayır | Arama. | — |
| `isActive` | boolean | Hayır | Filtre. | — |
| `sortBy` | string | Hayır | Sıralama alanı. | — |
| `sortOrder` | string | Hayır | `asc` / `desc`. | — |

#### Response (200 OK)

Dosya içeriği; `Content-Disposition` ile dosya adı. Content-Type: format’a göre (örn. text/csv, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, application/json).

---

## 4. Group — `api/group`

Grup CRUD ve dışa aktarma. Manager/Admin (JWT domain bağlamı) gerekir.

### 4.1 Grup oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/group` |
| **Auth** | ManagerAuthorization |

#### Request body (CreateGroupCommand)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `name` | string | Evet | Grup adı. | `"Satış"` |
| `description` | string | Hayır | Açıklama. | — |
| `permissions` | string[] | Hayır | İzin listesi. | `[]` |
| `isActive` | boolean | Hayır | Aktif mi. | `true` |
| `customData` | object | Hayır | DataGateway’e sync edilen veri. | — |

#### Response (201 Created)

| Alan adı | Tip | Açıklama |
|----------|-----|----------|
| `groupId` | string | Yeni grup ID. |
| `name`, `description`, `permissions`, `isActive` | — | Girilen değerler. |
| `createdAt` | string (ISO 8601) | — |
| `isSuccess` | boolean | — |
| `errorMessage` | string | Hata varsa. |

---

### 4.2 Grup listele (sayfalı)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/group` |
| **Auth** | ManagerAuthorization |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `page` | number | Hayır | Sayfa. | `1` |
| `pageSize` | number | Hayır | Sayfa boyutu. | `10` |
| `searchTerm` | string | Hayır | Arama. | — |
| `isActive` | boolean | Hayır | Filtre. | — |

#### Response (200 OK)

`{ "groups": GetGroupsResponseDto[], "totalCount", "page", "pageSize", "totalPages", "isSuccess", "errorMessage" }`

---

### 4.3 Tek grup getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/group/{groupId}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `groupId` | string | Evet | Grup ID. |

#### Response (200 OK)

`{ "group": GetGroupResponseDto | null, "isSuccess", "errorMessage" }`. 404: Grup yok.

---

### 4.4 Grup güncelle

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/group/{groupId}` |
| **Auth** | ManagerAuthorization |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `groupId` | string | Evet | Güncellenecek grup ID. |

#### Request body (UpdateGroupCommand)

`name`, `description`, `permissions`, `isActive`, `customData` (CreateGroup ile uyumlu). `groupId` path’te verilir.

#### Response (200 OK)

`groupId`, `name`, `description`, `permissions`, `isActive`, `updatedAt`, `isSuccess`, `errorMessage`.

---

### 4.5 Grup sil

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/group/{groupId}` |
| **Auth** | ManagerAuthorization (domain JWT) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `groupId` | string | Evet | Silinecek grup ID. |

#### Response (204 No Content)

Başarılı. 400: Domain veya işlem hatası.

---

### 4.6 Grup dışa aktar

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/group/export` |
| **Auth** | ManagerAuthorization |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `format` | string | Hayır | `csv`, `xlsx`, `json`. | `csv` |
| `searchTerm` | string | Hayır | Arama. | — |
| `isActive` | boolean | Hayır | Filtre. | — |

#### Response (200 OK)

Dosya stream; format’a göre Content-Type ve dosya adı.

---

## 5. License — `api/license`

Lisans yükleme, doğrulama, operasyon kontrolü ve domain bazlı lisans bilgisi. yetkilendirme JWT middleware ile yönetilir.

### 5.1 Lisans dosyası yükle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/license/upload` |
| **Content-Type** | `multipart/form-data` |

#### Request (form)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |
| `licenseFile` | file | Evet | Lisans dosyası. |

#### Response (200 OK)

`{ "message": "License uploaded successfully", "domainName": string }`

---

### 5.2 Lisans doğrula

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/license/validate` |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Response (200 OK)

`LicenseValidationResult`: `isValid`, `isExpired`, `licenseType`, `expiresAt`, `expirationBehavior`, `errorMessage`, `licenseFeatures`.

---

### 5.3 Operasyon izni kontrolü

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/license/check-operation` |

#### Request body

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `domainName` | string | Evet | Domain adı. | `"meral"` |
| `operation` | string (enum) | Evet | `TokenGeneration`, `CrudOperation`, `GetOperation`. | `TokenGeneration` |

#### Response (200 OK)

`{ "isAllowed": boolean, "domainName": string, "operation": string }`

---

### 5.4 Gerçek lisans oluştur (domain bazlı)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/license/{domainName}/create-real` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Request body (CreateRealLicenseRequest)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `expiresAt` | string (ISO 8601) | Evet | Bitiş tarihi (gelecek). |
| `expirationBehavior` | object | Evet | blockTokenGeneration, blockCrudOperations, blockGetOperations, allowReadOnly, customMessage. |
| `licenseFeatures` | object | Evet | maxUsers, maxDomains, maxStorageGB, enableAdvancedFeatures, supportLevel, countActiveUsersOnly, activeUserDefinition. |
| `customerInfo` | object | Hayır | customerName, customerId, contactEmail, contactPhone. |
| `metadata` | object | Hayır | purchaseDate, invoiceNumber, salesRep. |

#### Response (200 OK)

`{ "message": "Real license created successfully", "domainName": string, "expiresAt": string }`

---

### 5.5 Trial lisansı yeniden oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/license/{domainName}/recreate-trial` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `days` | number | Hayır | Trial süresi (gün). | `15` |

#### Response (200 OK)

`{ "message": "Trial license recreated successfully", "domainName": string, "expiresAt": string }`

---

### 5.6 Lisans dosyası indir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/license/{domainName}/download` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `type` | string | Hayır | `"real"` veya `"trial"`. | `"real"` |

#### Response (200 OK)

Dosya içeriği (application/octet-stream); dosya adı örn. `license-real-{domainName}.enc`. 404: İlgili tipte lisans yok.

---

### 5.7 Aktif kullanıcı sayısı

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/license/{domainName}/user-count` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Response (200 OK)

`{ "domainName": string, "activeUserCount": number, "maxUsers": number | null, "canCreateUser": boolean }`

---

### 5.8 Lisans bilgisi getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/license/{domainName}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainName` | string | Evet | Domain adı. |

#### Response (200 OK)

`LicenseInfoResponse`: `domainName`, `licenseType` (Trial/Real), `isValid`, `isExpired`, `expiresAt`, `issuedAt`, `issuedBy`, `expirationBehavior`, `licenseFeatures`, `customerInfo`, `metadata`. 404: Domain için lisans yok.

---

## 6. Admin — `api/admin`

Keycloak realm mapper yapılandırması.

### 6.1 Realm mapper’ları yapılandır

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/admin/realms/{realmName}/configure-mappers` |
| **Auth** | AllowAnonymous (ortama göre kısıtlanabilir) |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `realmName` | string | Evet | Keycloak realm adı (genelde domain adı). |

admin-cli client’a `user_groups`, `isAdmin`, `domain_name`, `domain_id` claim mapper’ları eklenir.

#### Response (200 OK)

`{ "realmName": string, "mappersAdded": string[], "message": string }`

#### Hata (400)

- `authentication_failed`: Master realm token alınamadı.
- `client_not_found`: admin-cli bulunamadı.

---

## 7. Sync — `api/sync`

MngKeeper → DataGateway kullanıcı/grup senkronizasyonu. Domain JWT gerekir.

### 7.1 Kullanıcıları senkronize et

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/sync/users` |

Domain, token claim’den alınır.

#### Response (200 OK)

`DataGatewaySyncResult`: `totalCount`, `createdCount`, `updatedCount`, `errorCount`, `errors` (string[]), `isSuccess`, `message`.

---

### 7.2 Grupları senkronize et

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/sync/groups` |

Aynı response yapısı (DataGatewaySyncResult).

---

### 7.3 Tümünü senkronize et (kullanıcı + grup)

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/sync/all` |

Aynı response yapısı; kullanıcı ve grup sayıları birleşik.

---

## 8. Templates — `api/templates`

Domain şablonları (liste, ada göre getir, domain’e göre, oluştur, güncelle, sil).

### 8.1 Tüm şablonları listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/templates` |

#### Response (200 OK)

`TemplateResponseDto[]`. Her öğe: `id`, `name`, `description`, `contentType`, `sourceDomainId`, `createdAt`, `updatedAt` vb.

---

### 8.2 Şablon getir (ada göre)

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/templates/{name}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `name` | string | Evet | Şablon adı. |

#### Response (200 OK)

Tek `TemplateResponseDto`. 404: Şablon yok.

---

### 8.3 Domain’e göre şablonlar

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/templates/domain/{domainId}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `domainId` | string | Evet | Kaynak domain ID. |

#### Response (200 OK)

`TemplateResponseDto[]`.

---

### 8.4 Şablon oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/templates` |

#### Request body

CreateTemplateDto: `name`, `description`, `contentType`, `content`, `sourceDomainId` vb. (uygulama DTO’suna göre doldurulur).

#### Response (201 Created)

`TemplateResponseDto`. 400: Geçersiz veri.

---

### 8.5 Şablon güncelle / sil

Path’ler ve method’lar uygulama kodunda `TemplatesController` içinde tanımlıdır; gerekirse Swagger/OpenAPI çıktısı ile tam path ve body tipleri netleştirilir.

---

## 9. System Locales — `api/system/locales`

Sistem dil dosyaları (MinIO system bucket).

### 9.1 Locale dosyası getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/system/locales/{locale}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `locale` | string | Evet | Dil kodu (örn. `tr`, `en`, `ar`). |

#### Response (200 OK)

JSON nesnesi (dil çevirileri). 404: Dosya yok.

---

### 9.2 Locale dosyası güncelle / oluştur

| Özellik | Değer |
|--------|--------|
| **Method** | `PUT` |
| **Path** | `/api/system/locales/{locale}` |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `locale` | string | Evet | Dil kodu. |

#### Request body

Geçerli bir JSON nesnesi (çeviri anahtar-değerleri).

#### Response (200 OK)

Güncellenen içeriğin onayı veya mesaj. 400: Geçersiz JSON.

---

## 10. Health — `Health`

Sağlık kontrolü.

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/Health` (kök, controller adı) |
| **Auth** | Yok |

#### Response (200 OK)

Genelde boş veya kısa “OK” benzeri metin.

---

## 11. Version — `api/version`

Uygulama sürüm bilgisi.

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/version` |
| **Auth** | Yok |

#### Response (200 OK)

Sürüm string’i veya küçük bir version objesi (uygulama implementasyonuna göre).

---

## Örnek istekler

### Token al

```http
POST /keeper/api/auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "your-password",
  "domain": "meral"
}
```

### Domain listele (sadece aktifler)

```http
GET /keeper/api/domain?status=Active
Authorization: Bearer <access_token>
```

### Kullanıcı listele (sayfa 2, 20’şer)

```http
GET /keeper/api/user?page=2&pageSize=20&sortBy=createdAt&sortOrder=desc
Authorization: Bearer <access_token>
```

### Lisans doğrula

```http
POST /keeper/api/license/validate
Content-Type: application/json

{
  "domainName": "meral"
}
```

---

## Ek notlar

- **Gender enum:** `0` = NotSpecified, `1` = Male, `2` = Female.
- **DomainStatus:** Pending, Active, Suspended, Expired, Deleted, Failed.
- **LicenseType:** Trial, Real.
- **LicenseOperation:** TokenGeneration, CrudOperation, GetOperation.
- Swagger UI (yerel/geliştirme): `http://localhost:5001/api-docs` veya gateway üzerinden ilgili path.
- Yeni endpoint veya alan eklendiğinde bu spec güncellenmelidir (DOCUMENTATION_STANDARDS §3.6).
