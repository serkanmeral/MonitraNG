# Production sunucu — ilk kurulum

**Hedef:** `192.168.20.8` — müşteri production makinesi  
**İlke:** Test sunucudan (`192.168.20.20`) **hiçbir veri, volume veya `.env` kopyalanmaz**. Production, kendi `mng_common` + Keycloak + MongoDB + … bileşenlerini **sıfırdan bu makinede** ayağa kaldırır. Bkz. [INDEPENDENCE.md](./INDEPENDENCE.md).

**Prosedür referansı (yalnızca adımlar için, veri paylaşımı yok):** [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md), [../setup/KURULUM.md](../setup/KURULUM.md)

Bu checklist, production makinesinde **henüz** tam yığın yokken izlenir. Tamamlandıktan sonra günlük işler [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) ile yapılır.

---

## 1. Sunucu hazırlığı

- [ ] SSH: `odak@192.168.20.8` (parola: [SERVER_ACCESS.md](./SERVER_ACCESS.md))
- [ ] OS: Debian (veya test ile uyumlu Linux), Docker kurulu
- [ ] **`odak` → `sudo` ve `docker` grubu** (zorunlu — 4 Haziran 2026 kontrol: prod’da henüz yok; IT’den isteyin)
- [ ] Docker yoksa: `pwsh -File scripts/odak/setup-docker-odak-prod.ps1` (sudo gerekir) veya IT kurulumu — bkz. [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md)
- [ ] Kaynak: yeterli CPU/RAM (test: 8 CPU / 15 GiB referans)
- [ ] Geliştirme PC → sunucu: port `22` + uygulama portları (3000, 5040, 8080, …) erişilebilir

```bash
docker --version
docker compose version
groups   # docker içermeli
```

---

## 2. Dizin yapısı

```bash
# odak kullanıcısı ile
mkdir -p /home/odak/mng_common
mkdir -p /home/odak/MonitraNG
```

---

## 3. mng_common (altyapı)

### 3.1 Dosyaları sunucuya alma

**Seçenek A — sync (PC):**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-mng-common-prod.ps1
```

**Seçenek B — manuel:** `ApplicationResources/mng_common` içeriğini `/home/odak/mng_common` altına kopyalayın.

### 3.2 Ortam dosyası

```bash
cd /home/odak/mng_common
cp .env.odak.example .env
# veya env.example → .env (kurulum rehberine göre)
nano .env
```

**Production’a özel:**

```bash
# .env içinde (veya compose override)
ODAK_KEYCLOAK_HOSTNAME=192.168.20.8
```

Detay: [../setup/MNG_COMMON_ODAK.md](../setup/MNG_COMMON_ODAK.md).

### 3.3 Başlatma (production’un kendi altyapısı)

```bash
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.prod.yml --env-file .env up -d
docker network inspect mng_common_mng_network
```

Bu adım **yalnızca `192.168.20.8` üzerinde** yeni volume’lar oluşturur; test sunucuya (`20.20`) dokunmaz.

- [ ] Keycloak: http://192.168.20.8:8080/keycloak/
- [ ] Mongo, Redis, RabbitMQ, MinIO ayakta (hepsi bu host’ta)

---

## 4. Uygulama kaynağı (`MonitraNG`)

```powershell
# Geliştirme PC — repo kökü
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Full -Server 192.168.20.8
```

- [ ] `/home/odak/MonitraNG/ApplicationResources/mng_apps/docker-compose.production.yml` mevcut

---

## 5. mng_apps `.env` (production IP)

```bash
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
cp .env.odak.prod.example .env   # test .env.odak.example DEĞİL
```

Veya PC’den: `bootstrap-odak-prod.ps1`. İç servisler (`mongo`, `keycloak`, …) docker ağ adlarıdır — test IP’si **yazılmaz**. Şablon: [env.prod.server.example](./env.prod.server.example).

**Zorunlu secret’lar (production Keycloak’ta yeni):**

- [ ] `KEYCLOAK_CLIENT_SECRET` — realm `monitra`, client `mng-keeper-admin`
- [ ] `MNGKEEPER_LICENSE_MASTER_KEY` — ≥32 karakter, güçlü rastgele

Keycloak adımları: [../domain/DOMAIN_OLUSTURMA_KAYIT.md](../domain/DOMAIN_OLUSTURMA_KAYIT.md) (URL’leri `20.8` ile).

---

## 6. İlk tam uygulama deploy

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Server 192.168.20.8
```

Uzun sürer (tüm servisler build). Alternatif sunucuda elle:

```bash
cd ~/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env up -d --build
```

---

## 7. Domain ve veri (müşteri kararı)

- [ ] Keycloak `monitra` realm + `mng-keeper-admin` client
- [ ] Domain oluşturma (UI veya API): [../domain/DOMAIN_OLUSTURMA.md](../domain/DOMAIN_OLUSTURMA.md)
- [ ] `initial_data` import (gerekirse): `scripts/odak/import-template-to-odak.ps1` — **production için script’e `-Server 192.168.20.8` eklenmeli** (şu an varsayılan test IP; çalıştırmadan önce script parametrelerini kontrol edin)

Test ortamındaki domain verisini production’a **otomatik kopyalamayın**; bilinçli migration gerekir.

---

## 8. Doğrulama

| Kontrol | URL / komut |
|---------|----------------|
| Gateway health | http://192.168.20.8:5040/health → 200 |
| UI | http://192.168.20.8:3000/ → 200 |
| Keeper (gateway) | http://192.168.20.8:5040/keeper/api/version/short |
| Domain UI | http://192.168.20.8:3001/domain/ |

```bash
curl -s -o /dev/null -w "gateway=%{http_code}\n" http://127.0.0.1:5040/health
curl -s -o /dev/null -w "ui=%{http_code}\n" http://127.0.0.1:3000/
```

---

## 9. İlk kurulum sonrası

- Günlük deploy: [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md)
- Test ortamında çalışmaya devam: [../deploy/README.md](../deploy/README.md)
- Müşteri erişim dokümanı (IT): [../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md) — IP’yi `192.168.20.8` olarak güncelleyerek teslim edin
