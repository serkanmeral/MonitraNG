# Odak kurulum dokümantasyonu

Uzak sunucu (`192.168.20.20`) — MonitraNG Odak / POC ortamı.

## Ana rehber

**Tüm kurulum ve çalışma akışı:** [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)

Yeni chat’te geliştirmeye başlamadan önce orayı okuyun.

---

## Yol haritası

| Sıra | Konu | Doküman | Durum |
|------|------|---------|--------|
| 1 | Sunucu, Docker | [KURULUM.md](./KURULUM.md) | Tamamlandı |
| 2 | Altyapı (mng_common) | [MNG_COMMON_ODAK.md](./MNG_COMMON_ODAK.md) | Tamamlandı |
| 2b | IT — altyapı erişim | [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md) | Teslim |
| 3 | Uygulamalar (mng_apps) | [MNG_APPS_ODAK.md](./MNG_APPS_ODAK.md) | Tamamlandı |
| 3a | Deploy (git push’suz) | [MNG_APPS_ODAK_DEPLOY.md](./MNG_APPS_ODAK_DEPLOY.md) | Hazır |
| 3b | IT — uygulama erişim | [MNG_APPS_ODAK_MUSTERI_ERISIM.md](./MNG_APPS_ODAK_MUSTERI_ERISIM.md) | Teslim |
| 4 | Domain + initial data | [../domain/README.md](../domain/README.md) | Tamamlandı |

---

## Domain

- [../domain/README.md](../domain/README.md) — domain dokümantasyon indeksi
- [../domain/DOMAIN_OLUSTURMA.md](../domain/DOMAIN_OLUSTURMA.md) — MngDomainUI ile oluşturma

---

## Compose dosyaları (repo)

| Ortam | Dizin | Dosyalar |
|-------|-------|----------|
| Altyapı | `ApplicationResources/mng_common/` | `docker-compose.yml` + `docker-compose.odak.yml`, `.env.odak.example` |
| Uygulama | `ApplicationResources/mng_apps/` | `docker-compose.production.yml` + `docker-compose.odak.yml`, `.env.odak.example` |
