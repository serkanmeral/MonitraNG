# Lokal Docker Desktop — erişim bilgileri

**Kapsam:** Bu PC’deki Docker Desktop yığını (`mng_common`, `mng_apps`, `mng_others`).  
**Doğrulama:** Çalışan konteyner env’leri (`docker inspect`) — 2026-07-11.  
**Kaynak dosyalar:**

| Yığın | Dosya |
|-------|--------|
| Altyapı | `ApplicationResources/mng_common/docker-compose.yml`, `env.example` |
| Uygulamalar | `ApplicationResources/mng_apps/docker-compose.yml` |
| GIS / Sim | `ApplicationResources/mng_others/docker-compose.yml` |

> Yalnızca **lokal geliştirme**. Production / müşteri secret’ları buraya yazılmaz.  
> `mng_common/.env` bu makinede yoktu; değerler compose’a verilen / volume’da kalıcı env’den okundu (çoğu `admin123` / `redis123` kalıbı — `mng_apps/docker-compose.yml` ile uyumlu).

## Dokümanlar

| Dosya | İçerik |
|-------|--------|
| [CREDENTIALS.md](./CREDENTIALS.md) | URL + kullanıcı + şifre tabloları |
| [PORTS.md](./PORTS.md) | Host port özeti |

Taşıma planı (domain vb.): [../deploy/local/](../deploy/local/).

**Lokal Docker URL / şifreler:** [../localdocker/CREDENTIALS.md](../localdocker/CREDENTIALS.md)
