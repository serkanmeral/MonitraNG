# Test Verisi Stratejisi — Mng.Ui

**Son güncelleme:** 9 Haziran 2026  
**İlgili:** [TESTPROCS_PLAN.md](./TESTPROCS_PLAN.md) · [PAGE_CATALOG.md](./PAGE_CATALOG.md)

---

## 1. Amaç

UI testlerinin her koşuda **aynı başlangıç durumuna** sahip olması:

- Öngörülebilir login ve yetki
- Modül bazlı minimum dataset
- Idempotent seed (tekrar koşum güvenli)

---

## 2. Persona tanımları

| Persona | Kullanıcı adı (öneri) | Rol | Smoke | Flow |
|---------|----------------------|-----|-------|------|
| **admin** | `test-ui-admin` | `is_admin: true` | ✅ | ✅ |
| **manager** | `test-ui-manager` | `is_manager: true` | nightly | permission |
| **user** | `test-ui-user` | normal grup üyeliği | nightly | kısıtlı view |

### Yetki test senaryoları

| Senaryo | Persona | Route | Beklenen |
|---------|---------|-------|----------|
| Admin tam erişim | admin | `/apps/operation-core/admin/definitions` | 200, sayfa render |
| Manager admin engeli | manager | `/apps/operation-core/admin/definitions` | `/unauthorized` |
| User grup view | user | menu'de izinli route | 200 |
| User grup yok | user | menu'de izinsiz route | `/unauthorized` |

Middleware kaynakları:
- `Mng.Ui/middleware/auth.global.js`
- `Mng.Ui/middleware/menu-permission.global.ts`

---

## 3. Test domain stratejisi

### Seçenek A — Ayrı test domain (önerilen)

```
Domain adı: test-ui-odak
Prefix:     test-ui-
```

**Artıları:** Production/demo veriyi kirletmez; seed sil-yeniden oluştur güvenli  
**Eksileri:** İlk kurulum bir kez daha uzun

### Seçenek B — Mevcut Odak demo domain

Mevcut `operationcore-demo-seed.json` ve widget seed'leri kullanılır.

**Artıları:** Hızlı başlangıç  
**Eksileri:** Paralel test / veri çakışması riski

**Karar (taslak):** Faz 1'de **Seçenek B** (hız); Faz 3'te **Seçenek A**'ya geçiş.

---

## 4. Mevcut seed kaynakları

| Modül | Script / dosya | Konum |
|-------|----------------|-------|
| Operation Core | `operationcore-demo-seed.json`, setup script'leri | `docs/odak/operationcore/scripts/` |
| Widgets | widget seed script'leri | `docs/odak/widgets/scripts/` |
| Alarm | `seed-alarm-notification-policies.ps1` | `docs/odak/alarm/scripts/` |
| Document Intelligence | `seed-monitrang-tutorials.ps1` | `docs/odak/document_intelligence/scripts/` |
| Domain (Keeper) | `test-domain-*.json` | `ApplicationResources/test_data/mng_keeper/` |
| Token | `get-operationcore-token.ps1`, `load-operationcore-token.ps1` | `docs/odak/operationcore/scripts/` |

---

## 5. Planlanan birleşik seed script

**Dosya (henüz yazılmadı):** `docs/odak/testprocs/scripts/seed-ui-test-env.ps1`

```powershell
# Taslak kullanım (plan)
# .\docs\odak\testprocs\scripts\seed-ui-test-env.ps1
# .\docs\odak\testprocs\scripts\seed-ui-test-env.ps1 -Modules widgets,operation_core
# .\docs\odak\testprocs\scripts\seed-ui-test-env.ps1 -PersonasOnly
```

### Script adımları (tasarım)

1. Keeper: test persona kullanıcıları (admin, manager, user) — yoksa oluştur
2. Side menu: `@pages` minimum menü kayıtları (P0 route'lar)
3. Modül seed'leri (parametre ile):
   - `-Modules operation_core` → mevcut OC setup script'lerini çağır
   - `-Modules widgets` → widget dataset + örnek widget'lar
4. Çıktı: `docs/odak/testprocs/fixtures/test-env.json` (id'ler, token hint)

---

## 6. Playwright auth fixture

**Dosya (plan):** `Mng.Ui/e2e/fixtures/auth.ts`

```typescript
// Taslak — E2E_TOOLING.md ile birlikte uygulanacak
// adminStorageState → playwright/.auth/admin.json
// baseURL: process.env.UI_BASE_URL ?? 'http://192.168.20.20:3000'
```

Ortam değişkenleri:

| Değişken | Varsayılan | Açıklama |
|----------|------------|----------|
| `UI_BASE_URL` | `http://192.168.20.20:3000` | Nuxt dev/preview |
| `TEST_ADMIN_USER` | `test-ui-admin` | Login kullanıcı |
| `TEST_ADMIN_PASSWORD` | (secret / .env.test) | CI secret |
| `GATEWAY_BASE_URL` | `http://192.168.20.20:5040` | API seed script'leri |

**Güvenlik:** Şifreler repoya yazılmaz; `.env.test` gitignore + CI secrets.

---

## 7. Sentetik veri

UI'da mevcut `chance` paketi tema mock'larında kullanılıyor (`Mng.Ui/_mockApis/`).

İş modülü testlerinde:

- **Sabit fixture** tercih edilir (deterministik assertion)
- Sentetik veri yalnızca yük/stress veya çok satırlı tablo testlerinde

---

## 8. Test ortamı gereksinimleri

Minimum çalışan servisler (modüle göre değişir):

| Servis | P0 smoke | Not |
|--------|----------|-----|
| Mng.Ui (Nuxt) | ✅ | `:3000` veya preview |
| MngGateway | ✅ | `:5040` |
| MngKeeper | ✅ | Auth |
| MngDataGateway | ✅ | Dataset CRUD |
| MngOperations | OC modülü | OC testleri |
| MngDocument | DI modülü | DI testleri |
| Keycloak / auth | ✅ | Token |

Referans kurulum: [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)

---

## 9. Veri temizleme

| Strateji | Ne zaman |
|----------|----------|
| Seed overwrite | Her nightly öncesi (test domain) |
| CRUD test cleanup | Spec `afterEach` — oluşturulan kayıtları sil |
| Snapshot DB | İleride docker volume reset (opsiyonel) |

---

## 10. Sonraki adım

1. `seed-ui-test-env.ps1` taslağını yaz (Faz 1)
2. Persona kullanıcı adlarını Odak Keeper ile doğrula
3. `test-env.json` çıktı formatını Playwright fixture'a bağla
