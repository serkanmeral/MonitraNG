# Odak — LDAP entegrasyonu

**Odak POC** (`192.168.20.20`) — kurumsal dizin (AD → Keycloak → MngKeeper → Mongo).

**Sonraki chat:** [HANDOFF_UI.md](./HANDOFF_UI.md) (K1.6 / K5 — Mng.Ui)

---

## Hızlı başlangıç

| Ne yapıyorsunuz? | Doküman |
|------------------|---------|
| **UI (yeni chat)** | [HANDOFF_UI.md](./HANDOFF_UI.md) |
| Faz durumu | [DEVAM.md](./DEVAM.md) |
| K3 Scheduler (tamamlandı) | [HANDOFF_MNGSCHEDULER.md](./HANDOFF_MNGSCHEDULER.md), [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) |
| HTTP + Gateway (HTTPS yok) | [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md) |
| Geliştir → test → deploy | [DEV_WORKFLOW.md](./DEV_WORKFLOW.md) |
| Keeper sunucu deploy | [DEPLOY_KEEPER_LDAP.md](./DEPLOY_KEEPER_LDAP.md) |

---

## Dokümanlar

| Doküman | Konu |
|---------|------|
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | K1–K5 teknik plan |
| [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) | K3 — MngScheduler (deployed) |
| [K4_LOGIN_SYNC.md](./K4_LOGIN_SYNC.md) | Login tek kullanıcı sync |
| [P0_DIRECTORY_PRIVILEGES_TEST.md](./P0_DIRECTORY_PRIVILEGES_TEST.md) | `directoryPrivileges` + JWT |
| [POC_KEYCLOAK_LDAP.md](./POC_KEYCLOAK_LDAP.md) | K1 Keycloak AD |
| [USER_SOURCES.md](./USER_SOURCES.md) | K5 — Local vs Directory |
| [ROADMAP.md](./ROADMAP.md) | Uzun vade |

---

## Durum özeti (23 Mayıs 2026)

| Alan | Durum |
|------|--------|
| K1 Keycloak ↔ AD (odak realm) | ✅ |
| K2 directory sync endpoint | ✅ deploy |
| P0 `directoryPrivileges` + JWT | ✅ |
| K4 login sync | ✅ deploy |
| K3 MngScheduler periyodik sync | ✅ deploy + sunucu doğrulama |
| Keeper **v1.3.0** sunucu | ✅ |
| Odak ortamı | **HTTP** (HTTPS ertelendi) |
| API Gateway `/keeper/...` | ✅ |
| K1.6 / K5 UI | ⬜ sonraki chat |
| HTTPS / Nginx | ⬜ en son |

---

## Repo dışı

| Kaynak | Not |
|--------|-----|
| [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md) | Sunucu kurulum, UI `.env` |
| [../ui/README.md](../ui/README.md) | Mng.Ui notları |
| [../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md) | Portlar, HTTP URL’ler |

**AD:** `LDAP://192.168.20.3:389/DC=odak,DC=local`
