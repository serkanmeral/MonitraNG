# Fatih Home — Docker yığını

## Amaç

`mng_common` + `mng_apps` yığınını **`10.0.10.52`** (`debian-test`) üzerinde ayağa kaldırma. Temiz VM; Prospera yok. Aynı Fatih override dosyaları kullanılır (Keycloak hostname = IP; GitLab/Nginx/Portainer kapalı).

**UI:** http://10.0.10.52:3000  
**DomainUI:** http://10.0.10.52:3001  
**SSH:** [access.md](./access.md) — `support` (docker grubunda, NOPASSWD sudo)  
**Kimlik:** [CREDENTIALS.md](./CREDENTIALS.md)

---

## Sunucu yolları

| Ne | Path |
|----|------|
| Repo | `/home/support/MonitraNG` |
| Altyapı | `/home/support/MonitraNG/ApplicationResources/mng_common` |
| Uygulamalar | `/home/support/MonitraNG/ApplicationResources/mng_apps` |
| Clone | `git clone --depth 1 --branch main https://github.com/serkanmeral/MonitraNG.git` |

Fatih override dosyaları GitHub’da yoksa lokalden kopyalanır (`docker-compose.fatih.yml`, `docker-compose.fatih.apps.yml`, `.env.fatih.example`).

---

## Compose referansları

| Yığın | Dizin | Dosyalar | Not |
|-------|-------|----------|-----|
| Altyapı | `mng_common` | `docker-compose.yml` + **`docker-compose.fatih.yml`** | Keycloak `:8080`, hostname `10.0.10.52`; GitLab/Nginx/MkDocs/Portainer kapalı; Redis/Mosquitto host port remap |
| Uygulama | `mng_apps` | `docker-compose.production.yml` + **`docker-compose.fatih.apps.yml`** | UI `:3000`; Ollama/Collabora stub |

### mng_common başlatma

```bash
cd /home/support/MonitraNG/ApplicationResources/mng_common
# .env yoksa: cp env.example .env  &&  CREDENTIALS ile doldur
# FATIH_KEYCLOAK_HOSTNAME=10.0.10.52
docker compose -f docker-compose.yml -f docker-compose.fatih.yml up -d
docker compose -f docker-compose.yml -f docker-compose.fatih.yml ps
```

### mng_apps başlatma (common health OK sonrası)

```bash
cd /home/support/MonitraNG/ApplicationResources/mng_apps
# cp .env.fatih.example .env  &&  LICENSE + KEYCLOAK_CLIENT_SECRET
docker compose -f docker-compose.production.yml -f docker-compose.fatih.apps.yml --env-file .env up -d --build
```

---

## Önerilen sıra

```text
1. git clone (LVM gerekmedi)
2. Fatih override + .env kopyala
3. mng_common up -d → health (Mongo/Keycloak/MinIO)  [tamam 2026-08-22]
4. mng_apps build / up
5. Smoke: :3000 / :3001 / :8080/keycloak
6. DomainUI → DOMAIN.md
```

---

## Port haritası (Prospera birlikte yaşam)

| Host port | MonitraNG | Not |
|-----------|-----------|-----|
| 80 / 443 | — | **Prospera nginx**; MonitraNG nginx kapalı |
| 3000 | mngui | UI |
| 3001 | mngdomainui | DomainUI |
| 5040 | mnggateway | |
| 5001 / 5010 / 5020 | Keeper / DG / Hub | |
| 8080 | Keycloak `/keycloak` | |
| 8081 | Mongo Express | |
| 27017 | Mongo | |
| **16379** | Redis (host) | İçeride hâlâ `redis:6379`; host 6379 Prospera |
| **18001** | Redis Commander | 8001 Prospera auth |
| 5672 / 15672 | RabbitMQ | |
| 9090 / 9091 | MinIO | Prospera MinIO 9000/9001 |
| 5341 | Seq | |
| 1883 | Mosquitto MQTT | |
| **19001** | Mosquitto WS | 9001 Prospera MinIO console |
| 9200 | OpenSearch | |
| 9443 | — | Mevcut Portainer (MonitraNG Portainer kapalı) |

---

## `.env`

- Common şablon: `env.example` / `.env.fatih.example` (`FATIH_KEYCLOAK_HOSTNAME=10.0.10.52`)
- Apps şablon: `.env.fatih.example`
- Gerçek `.env`: sunucuda (gitignore)
- Değerler: [CREDENTIALS.md](./CREDENTIALS.md)

---

## Ağ

Uygulama konteynerleri external network: `mng_common_mng_network` (common up sonrası; compose proje adı `mng_common` iken `mng_network` bu isimle oluşur). Prospera ağına **bind etme**.

---

## Bilinen tuzaklar

- Önce apps, common yok → network hatası
- Base Keycloak hostname `admin.monitrang.com` — **mutlaka** `-f docker-compose.fatih.yml`
- Mailu ağı yok — fatih override placeholder network oluşturur
- Host 6379/8001/9000/9001/4000/80/443 Prospera’da — remap’siz `up` Prospera’yı kırar
- Ollama 8g / ikinci Collabora bu laptop’ta açılmamalı (stub)
- RAM 14 GiB — OpenSearch + full apps + Prospera izlenmeli

---

## Güncelleme

```bash
cd /home/support/MonitraNG
git pull origin main
# override dosyaları hâlâ untracked ise lokalden scp
cd ApplicationResources/mng_common
docker compose -f docker-compose.yml -f docker-compose.fatih.yml up -d
```

### Durum (2026-08-22)

Hedef `10.0.10.52`. Repo klonlandı. `mng_common` + `mng_apps` ayakta (UI `:3000`, DomainUI `:3001`, Gateway `:5040`, Keeper `:5001` HTTP 200). Mosquitto / Node-RED healthcheck fail (önceki ortamda da); altyapı için bloke değil. `mngworkflow-worker` ilk açılışta `workflow.resume` kuyruğu yoktu; declare + restart sonrası Up.
