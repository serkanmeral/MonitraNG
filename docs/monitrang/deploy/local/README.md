# MonitraNG — Lokal Docker Desktop (geliştirme ortamı)

**Amaç:** Müşteri terminalindeki geliştirme yığınını, bu makinedeki **Docker Desktop** üzerinde tekrar çalışır hale getirmek.

**Kapsam:** Domain yapılandırması, altyapı (`mng_common`), uygulamalar (`mng_apps`), veritabanı / volume stratejisi, kullanıcı ve kimlik (Keycloak / Keeper).

**Kaynak ortam (referans):** Müşteri test / geliştirme sunucusu (`192.168.20.20` vb.) — ayrıntılar [docs/odak/proddeploy/ENVIRONMENTS.md](../../../odak/proddeploy/ENVIRONMENTS.md) ve [docs/odak/deploy/README.md](../../../odak/deploy/README.md).

**Hedef ortam:** Bu PC — Docker Desktop + lokal `ApplicationResources/mng_common` + `ApplicationResources/mng_apps`.

**Lokal URL / kullanıcı / şifre:** [../localdocker/CREDENTIALS.md](../localdocker/CREDENTIALS.md)

---

## Doküman indeksi

| Dosya | İçerik |
|-------|--------|
| [MIGRATION_PLAN.md](./MIGRATION_PLAN.md) | Taşıma planı, fazlar, kararlar, açık sorular |
| [CHECKLIST.md](./CHECKLIST.md) | Adım adım doğrulama listesi (uygulama sırasında doldurulur) |
| [INVENTORY.md](./INVENTORY.md) | Kaynak ortam envanteri (servisler, portlar, domain, secret kaynakları) |
| [DOMAIN.md](./DOMAIN.md) | Hosts / DNS, local domain, CORS, URL matrisi |
| [DATABASE.md](./DATABASE.md) | MongoDB / Postgres (Keycloak) stratejisi: boş kurulum vs dump |
| [USERS_AND_AUTH.md](./USERS_AND_AUTH.md) | Keycloak realm, admin / test kullanıcıları, Keeper domain |
| [DOCUMENT_TEMPLATES.md](./DOCUMENT_TEMPLATES.md) | Adım 4 — DI / Belge Tasarımcısı şablon taşıma |
| [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md) | VPN/RDP terminal Cursor — prompt üret / çıktı geri al |
| [remote_prompts/](./remote_prompts/) | Terminale yapıştırılacak prompt dosyaları |
| [DOCKER.md](./DOCKER.md) | Compose dosyaları, `.env`, ağ, build/up sırası |

Secret’lar ve parolalar **bu klasöre yazılmaz**. Yerel kopyalar için `.env*.local` / `local-credentials.ps1` (gitignore) kullanılır.

---

## Hızlı yönlendirme

1. Plan ve kararlar → [MIGRATION_PLAN.md](./MIGRATION_PLAN.md)
2. Kaynakta ne vardı? → [INVENTORY.md](./INVENTORY.md)
3. Çalıştırma sırası → [DOCKER.md](./DOCKER.md) + [CHECKLIST.md](./CHECKLIST.md)

---

## İlişkili mevcut dokümanlar

- Odak müşteri deploy: `docs/odak/deploy/`
- Odak ortam matrisi: `docs/odak/proddeploy/ENVIRONMENTS.md`
- Lokal compose (repo): `ApplicationResources/mng_apps/docker-compose.yml`
- Altyapı compose: `ApplicationResources/mng_common/`
