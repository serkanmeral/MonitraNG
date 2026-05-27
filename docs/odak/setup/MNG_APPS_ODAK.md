# Odak — mng_apps kurulum planı

**Ön koşul:** [mng_common Odak altyapısı](./MNG_COMMON_ODAK.md) çalışıyor olmalı (`192.168.20.20`, `/home/odak/mng_common`).  
**Sunucu dizini (hedef):** `/home/odak/MonitraNG/ApplicationResources/mng_apps`  
**Compose:** `docker-compose.production.yml` + `docker-compose.odak.yml`  
**Durum:** Kuruldu ve doğrulandı — özet: [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)

---

## mng_common durumu (tamamlandı)

| Kontrol | Sonuç |
|---------|--------|
| 12 altyapı konteyneri | Çalışıyor |
| Keycloak Admin UI | http://192.168.20.20:8080/keycloak/admin/master/console/ |
| `mng_common_mng_network` | Oluştu (mng_common compose ile) |
| Müşteri IT dokümanı | [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md) |

---

## Odak farkı (production’a göre)

| Konu | Production | Odak |
|------|------------|------|
| Nginx | Var | Yok |
| Host portları | Kapalı | Açık (`docker-compose.odak.yml`) |
| Ollama | İsteğe bağlı | **Kapalı** (`odak-disabled`) |
| Gateway scheme | https | http |
| JWT Authority | `${KEYCLOAK_BASE_URL}/realms/monitra` | `http://keycloak:8080/keycloak/realms/monitra` |
| CORS / UI URL | `app.monitrang.com` | `http://192.168.20.20:3000` |

---

## Dahil uygulama servisleri

| Servis | Host portu | Not |
|--------|------------|-----|
| mnggateway | 5040 | API girişi |
| mngkeeper | 5001 | |
| mngdatagateway | 5010 | |
| ~~mngreactor~~ | — | **Kapalı** (repo’da Dockerfile / kaynak yok) |
| mnghub | 5020 | SignalR |
| ~~mngllm~~ | — | **Kapalı** (Ollama bağımlılığı; RAM) |
| mngscheduler | 5090 | |
| mngworkflow | 5085 | |
| **mngoperations** | **5086** | Operation Core orchestration |
| mngadmin | 5080 | |
| mngnotifier | 5070 | SMTP sunucuda yoksa mail çalışmaz |
| mngui | 3000 | Ana UI |
| mngdomainui | 3001 | Domain UI (`/domain/`) |

---

## Kurulum öncesi: Keycloak

`mng-keeper-admin` client ve `monitra` realm production ile uyumlu olmalı. İlk kurulumda:

1. Keycloak admin → http://192.168.20.20:8080/keycloak/admin/master/console/
2. `monitra` realm yoksa oluşturun (veya mevcut export/import prosedürünüzü uygulayın).
3. Client `mng-keeper-admin` (confidential) oluşturup **Client secret** alın → `.env` içinde `KEYCLOAK_CLIENT_SECRET`.

Realm/client hazır değilse MngKeeper domain oluşturma ve Gateway JWT doğrulaması hata verir.

---

## Dağıtım (git push şart değil)

Geliştirme PC’deki workspace’ten sunucuya aktarım: **[MNG_APPS_ODAK_DEPLOY.md](./MNG_APPS_ODAK_DEPLOY.md)**

```powershell
# Repo kökünden (ilk kurulum)
.\scripts\odak\sync-odak-source.ps1 -Full
# Sunucuda bir kez: .env oluşturun (aşağıdaki adım 2)
.\scripts\odak\deploy-odak-apps.ps1 -FullBuild
```

Sunucuda `git clone` / `git pull` **zorunlu değildir**; push zamanınız deploy’dan bağımsızdır.

---

## Sunucuda adımlar (ilk kurulum)

### 1. Kaynak dizini

`sync-odak-source.ps1 -Full` sonrası: `/home/odak/MonitraNG/...` (build context’ler burada).

### 2. Ortam dosyası

```bash
cp .env.odak.example .env
nano .env
```

Mutlaka doldurun:

- `KEYCLOAK_CLIENT_SECRET`
- `MNGKEEPER_LICENSE_MASTER_KEY` (en az 32 karakter)

Diğer parolalar `.env.odak.example` içinde mng_common ile hizalıdır.

### 3. Ağ kontrolü

```bash
docker network inspect mng_common_mng_network >/dev/null \
  || echo "Önce mng_common Odak compose çalıştırın"
```

### 4. Build ve başlatma

PC’den: `.\scripts\odak\deploy-odak-apps.ps1 -FullBuild`  
veya sunucuda:

```bash
cd ~/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --build
```

### 5. Doğrulama

```bash
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml ps
curl -s -o /dev/null -w "gateway=%{http_code}\n" http://127.0.0.1:5040/health
curl -s -o /dev/null -w "keeper=%{http_code}\n" http://127.0.0.1:5001/health
curl -s -o /dev/null -w "ui=%{http_code}\n" http://127.0.0.1:3000/
```

---

## Kaynak notu

| | |
|--|--|
| RAM | mng_common ~8–10 GiB + uygulamalar ~4–6 GiB; **Ollama kapalı** |
| Sunucu | 15 GiB — sıkı; build sırasında diğer servisleri izleyin |
| SMTP | Odak’ta Mailu yok; `SMTP_HOST=172.17.0.1` denenebilir, mail opsiyonel |

---

## İlgili dokümanlar

- [MNG_APPS_ODAK_DEPLOY.md](./MNG_APPS_ODAK_DEPLOY.md) — **deploy stratejisi** (sync, tekrar deploy, git’siz)
- [../domain/DOMAIN_OLUSTURMA.md](../domain/DOMAIN_OLUSTURMA.md) — domain oluşturma adımları
- [MNG_APPS_ODAK_MUSTERI_ERISIM.md](./MNG_APPS_ODAK_MUSTERI_ERISIM.md) — port ve URL tablosu (IT teslimi)
- [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./MNG_COMMON_ODAK_MUSTERI_ERISIM.md) — altyapı parolaları
- [SUNUCU_COMPOSE_VE_ENV_REHBERI.md](../../../ApplicationResources/mng_apps/SUNUCU_COMPOSE_VE_ENV_REHBERI.md) — production env açıklamaları
- [KURULUM.md](./KURULUM.md) — genel Odak yol haritası
