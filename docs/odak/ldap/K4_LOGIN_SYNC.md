# K4 — Login tek kullanıcı sync

**Konum:** `GetTokenCommandHandler` — Keycloak `GetTokenAsync` başarılı olduktan sonra, JWT üretilmeden önce.

## Akış

1. Keycloak parola doğrulaması
2. `IKeycloakToMongoSyncService.SyncUserOnLoginAsync(domainId, username)`
3. Mongo kullanıcı yeniden yüklenir
4. `is_admin` / `is_manager` güncel `User.Groups` ile hesaplanır
5. MngKeeper JWT üretilir

## Yapılandırma

`MngKeeperSettings:DirectorySync:LoginSyncEnabled` (varsayılan `true`)

## Coordinator

Tam sync (`POST /api/directory/sync`) domain kilidini tutarken login sync **atlanır** (`code: sync_in_progress`); login yine başarılı, Mongo’daki son bilgi kullanılır.

## Test (T5)

1. `mng_odak` içinde pilot kullanıcıyı silin veya `groups` alanını Keycloak’tan farklı yapın.
2. `POST /api/auth/token` — `domain: odak`, pilot kullanıcı/parola.
3. Log: `Login directory sync applied` veya `unchanged`.
4. Mongo `@users`: gruplar Keycloak ile eşleşmeli.
5. JWT: `user_groups`, `is_admin`, `is_manager` güncel olmalı.

## İlgili kod

| Dosya | Rol |
|-------|-----|
| `KeycloakToMongoSyncService.SyncUserOnLoginAsync` | Sync + kilitleme |
| `DirectoryUserSyncComparer` | Değişiklik gerekip gerekmediği |
| `DirectoryUserFieldSets` | Yazılabilir alanlar (K2 ile aynı) |
| `GetRealmUserByUsernameAsync` | KC kullanıcı snapshot |
