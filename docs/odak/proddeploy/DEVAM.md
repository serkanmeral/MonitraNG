# DEVAM — Production deploy (Kaldığımız yer)

**Son güncelleme:** 20 Haziran 2026  
**Durum:** ✅ Kod deploy + domain + meta sync + **legacy iş verisi migrasyonu (çekirdek)** — ⏳ PO PDF + UI UAT

> **İlke:** Test (`192.168.20.20`) ile production (`192.168.20.8`) tamamen bağımsız → [INDEPENDENCE.md](./INDEPENDENCE.md)  
> **Yeni chat:** Bu dosya + [LEGACY_DATA_MIGRATION_PROD.md](./LEGACY_DATA_MIGRATION_PROD.md)

---

## 1. Tek cümlede durum

Production’da **mng_apps ayakta**, dış erişim **`https://mng.odaksavunma.com`**, Odak Sipariş **iş verisi** (823 paket, 2757 kalem, 87 müşteri, NCR/CAPA/sevkiyat) legacy SQL dump’tan DG’ye aktarıldı. **PO PDF’lerin çoğu** yerel uploads eksik olduğu için henüz yüklenmedi.

---

## 2. Faz özeti

| Faz | Konu | Durum |
|-----|------|--------|
| P0–P3 | mng_common + mng_apps deploy, Keycloak, domain/CORS | ✅ |
| P4 | Meta sync (`@datasets`, `@side_menu`, …) test → prod | ✅ |
| **P5** | **Legacy iş verisi migrasyonu (SQL dump → prod DG)** | ✅ çekirdek |
| P5b | Meta `$date` onarımı (DG LIST/CREATE engeli) | ✅ |
| P6 | PO PDF tam migrasyon (`uploads` sync) | ⏳ |
| P7 | Sipariş hub UAT (prod URL) | ⏳ |

---

## 3. Production veri özeti (`mng_odak`)

| Veri | Prod sayı |
|------|-----------|
| `odak_musteriler` | 87 |
| `odak_is_paketleri` | 823 |
| `odak_siparis_kalemleri` | 2757 |
| `odak_ncr` / `odak_capa` | 499 / 25 |
| `odak_sevkiyatlar` | 622 |
| PO PDF yüklü paket | 6 |

Detay, sorunlar, komutlar: **[LEGACY_DATA_MIGRATION_PROD.md](./LEGACY_DATA_MIGRATION_PROD.md)**

---

## 4. Sonraki oturum — sıra

1. UI: https://mng.odaksavunma.com/apps/odak-siparis/packages  
2. PO PDF: `sync-legacy-from-server.ps1` + `migrate-legacy-po-pdf-to-dg.ps1 -All` (bkz. LEGACY doc §6)  
3. İsteğe bağlı: verify script hedef sayılarını 824/2767 yap  

---

## 5. Agent / yeni chat talimatı

Kullanıcı *“prod deploy’a / migrasyona devam”* dediğinde:

1. [DEVAM.md](./DEVAM.md) + [LEGACY_DATA_MIGRATION_PROD.md](./LEGACY_DATA_MIGRATION_PROD.md) oku.  
2. Migrasyon API: **`http://192.168.20.8:5040`** (public URL POST 405 verir).  
3. Token: `$env:MNG_OC_USE_PROD_TOKEN=1` + `get-operationcore-token-prod.ps1`.  
4. Test Mongo → prod dump **kullanma** (iş verisi için).  
5. `op_*` workspace verisine dokunma.

---

## 6. İlgili dosyalar

| Dosya | Rol |
|-------|-----|
| [LEGACY_DATA_MIGRATION_PROD.md](./LEGACY_DATA_MIGRATION_PROD.md) | **Legacy veri migrasyonu (bu oturum)** |
| [README.md](./README.md) | proddeploy indeks |
| [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) | Günlük prod deploy |
| [AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md) | Agent kuralları |
| `docs/odak/siparis/scripts/migrate-legacy-full-to-prod.ps1` | Tam migrasyon orchestrator |
