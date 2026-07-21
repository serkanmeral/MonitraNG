# Kaynak ortam envanteri

Müşteri geliştirme / test ortamından lokal’e taşırken referans alınacak envanter. Değerler planlama sırasında doldurulur; **parola yazılmaz**.

## Kaynak

| Alan | Değer |
|------|--------|
| Rol | Müşteri test / geliştirme (günlük geliştirme yapılan yer) |
| Host / IP | _örn. 192.168.20.20_ |
| SSH kullanıcı | _örn. odak_ |
| Repo yolu (sunucu) | _örn. /home/odak/MonitraNG_ |
| mng_common yolu | _örn. /home/odak/mng_common_ |
| Compose override | `docker-compose.odak.yml` (test) |

Referans: [docs/odak/proddeploy/ENVIRONMENTS.md](../../../odak/proddeploy/ENVIRONMENTS.md)

## Hedef (bu PC)

| Alan | Değer |
|------|--------|
| OS | Windows + Docker Desktop |
| Repo | `C:\Serkan\iSIM\MonitraNG` (veya güncel workspace) |
| mng_common | `ApplicationResources/mng_common` |
| mng_apps | `ApplicationResources/mng_apps` |

## Servis / port matrisi (kaynak → lokal)

| Servis | Kaynak URL / port | Lokal hedef (TBD) |
|--------|-------------------|-------------------|
| MngUI | :3000 | |
| MngDomainUI | :3001 | |
| API Gateway | :5040 | |
| Keycloak | :8080 | |
| MngKeeper | :5001 | |
| MongoDB | :27017 | |
| … | | |

## Domain / tenant

| Alan | Kaynak | Lokal karar |
|------|--------|-------------|
| Domain adı | | |
| Mongo DB adı | | |
| Keycloak realm | | |

## Secret / config kaynakları (konum, değer değil)

| Ne | Kaynak konum | Lokal karşılık |
|----|--------------|----------------|
| mng_apps `.env` | sunucu `.../mng_apps/.env` | lokal `.env` (gitignore) |
| mng_common `.env` | sunucu `.../mng_common/.env` | lokal `.env` |
| Keycloak admin | | |
| Mongo admin | | |

## Notlar

_Planlama oturumunda doldurulacak._
