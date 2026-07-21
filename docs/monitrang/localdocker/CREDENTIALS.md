# Lokal Docker — adres, kullanıcı, şifre

**Son doğrulama:** 2026-07-11 (çalışan konteyner env)  
**Ortam:** Docker Desktop · localhost

Şifreler değiştiyse: `docker inspect <container> --format '{{range .Config.Env}}{{println .}}{{end}}'` veya ilgili `.env`.

---

## 1. Altyapı (`mng_common`)

| Uygulama | URL / endpoint | Kullanıcı | Şifre | Not |
|----------|----------------|-----------|-------|-----|
| **Keycloak** Admin | http://localhost:8080/keycloak/admin/master/console/ | `admin` | `admin123` | Path: `/keycloak`. DomainUI master login aynı. |
| **PostgreSQL** (Keycloak DB) | host’tan port yok (yalnızca Docker ağı) | `keycloak` | `keycloak123` | DB: `keycloak`. pgAdmin ile ekleyebilirsiniz. |
| **MongoDB** | `localhost:27017` | `admin` | `admin123` | authSource: `admin` |
| **Mongo Express** | http://localhost:8081 | — | — | Basic auth **kapalı** |
| **Redis** | `localhost:6379` | — | `redis123` | `requirepass` |
| **Redis Commander** | http://localhost:8001 | — | — | Redis şifresi compose’tan |
| **RabbitMQ** AMQP | `localhost:5672` | `admin` | `admin123` | |
| **RabbitMQ** Management | http://localhost:15672 | `admin` | `admin123` | |
| **MinIO** API | http://localhost:9090 | `admin` | `admin123` | S3 API |
| **MinIO** Console | http://localhost:9091 | `admin` | `admin123` | Web UI |
| **Seq** | http://localhost:5341 | `admin` | `admin123` | İlk kurulum admin şifresi |
| **Portainer** | http://localhost:9000 | _(ilk kurulumda sizin seçtiğiniz)_ | _(volume’da)_ | Compose’ta sabit user yok |
| **Node-RED** | http://localhost:1880 | `admin` | _(env `NODE_RED_PASSWORD`)_ | Inspect’te net değilse UI’dan kontrol |
| **Mosquitto** MQTT | `localhost:1883` | `monitrang` | `!2345qawsedrf` | [MOSQUITTO_CREDENTIALS](../../content/infrastructure/MOSQUITTO_CREDENTIALS.md) |
| **MkDocs** | http://localhost:6010 | — | — | Dokümantasyon sitesi |
| **Ollama** | http://localhost:11434 | — | — | API |
| **Local registry** | `localhost:5000` | — | — | Docker image registry |

### Keycloak — DomainUI / Keeper notları

| Amaç | Değer |
|------|--------|
| Master admin (DomainUI girişi) | `admin` / `admin123` |
| Keeper’ın Keycloak admin şifresi (compose) | `admin123` |
| Yeni domain admin varsayılan şifre (Keeper setting) | `Admin123!` |

---

## 2. MonitraNG uygulamaları (`mng_apps`)

| Uygulama | URL | Auth | Not |
|----------|-----|------|-----|
| **Mng UI** | http://localhost:4000 | Domain kullanıcı (Keycloak realm) | Host 4000→80. Import kullanıcılar: `Sm123!?` · `odak_admin` / `Admin123!` |
| **Mng Domain UI** | http://localhost:3001/domain/ | Keycloak **master** `admin` / `admin123` | |
| **API Gateway** | http://localhost:5040 | Bearer token | HTTPS alternatif: `:5443` |
| **MngKeeper** | http://localhost:5001 | | Doğrudan API |
| **MngDataGateway** | http://localhost:5010 | | |
| **MngHub** | http://localhost:5020 | | |
| **MngReactor** | http://localhost:5003 | | |
| **MngLLM** | http://localhost:5030 | | |
| **MngNotifier** | http://localhost:5070 | | |
| **MngAdmin** | http://localhost:5080 | | |
| **MngWorkflow** | http://localhost:5085 | | |
| **MngScheduler** | http://localhost:5090 | | |

Uygulama konteynerlerinin Mongo/Redis/Rabbit/MinIO bağlantıları `mng_apps/docker-compose.yml` içinde çoğunlukla `admin` / `admin123` ve Redis `redis123` ile sabitlenmiş.

---

## 3. GIS / diğer (`mng_others`)

| Uygulama | URL / endpoint | Kullanıcı | Şifre | Not |
|----------|----------------|-----------|-------|-----|
| **PostGIS** | `localhost:5433` | `gisuser` | `gispass` | DB: `gis` |
| **GeoServer** | http://localhost:8082/geoserver | `admin` | `admin` | |
| **pgAdmin** | http://localhost:5051 | `admin@example.com` | `admin` | |
| **MngSim** | http://localhost:6061 | — | — | Health: `/api/health` |

---

## 4. Hızlı kopyala (sık kullanılanlar)

```text
Keycloak master:  http://localhost:8080/keycloak/admin/master/console/   admin / admin123
Domain UI:        http://localhost:3001/domain/                          admin / admin123
Mongo:            mongodb://admin:admin123@localhost:27017/?authSource=admin
Mongo Express:    http://localhost:8081                                  (auth yok)
RabbitMQ UI:      http://localhost:15672                                 admin / admin123
MinIO Console:    http://localhost:9091                                  admin / admin123
Redis:            localhost:6379                                         password redis123
Portainer:        http://localhost:9000                                  (kendi admin)
Seq:              http://localhost:5341                                  admin / admin123
GeoServer:        http://localhost:8082/geoserver                        admin / admin
pgAdmin:          http://localhost:5051                                  admin@example.com / admin
PostGIS:          localhost:5433                                         gisuser / gispass / db gis
```

---

## 5. Güncelleme

Env değişince bu tabloyu güncelleyin veya:

```powershell
docker inspect keycloak mongo rabbitmq minio redis postgres --format "{{.Name}}`n{{range .Config.Env}}{{println .}}{{end}}"
```
