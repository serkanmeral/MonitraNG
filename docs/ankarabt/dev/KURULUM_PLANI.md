# app01 kurulum planı

**Durum:** Analiz tamam, kurulum başlamadı  
**Son güncelleme:** 2026-10-20  
**Hedef:** `app01` (`192.168.1.252`) üzerinde MonitraNG  
**Erişim:** [../server/access.md](../server/access.md)  
**Envanter:** [ANALIZ.md](./ANALIZ.md)

Uzak `portal.ankarabt.com` yığınına dokunulmaz. Repodaki `docker-compose.ankarabt.yml` ve `docker-compose.ankarabt.apps.yml` o host içindir; bu VM’de kullanılmaz.

## 1. Başarı kriterleri

- [ ] VM belleği en az 16 GiB (Ollama hariç)
- [ ] `appadmin` Docker soketine parolasız erişiyor
- [ ] Docker verisi ve repo `/opt` altında
- [ ] `mng_common` ayakta (Mongo, Redis, RabbitMQ, Keycloak, MinIO). GitLab ve Nginx kapalı
- [ ] `mng_apps` ayakta. Ollama kapalı
- [ ] Tarayıcıdan `http://192.168.1.252:3000` ve `:3001`
- [ ] DomainUI ile tenant oluşturulmuş
- [ ] Secret’lar yalnızca sunucudaki `.env` dosyasında
- [ ] SMTP, mailcow (`192.168.1.253`) üzerinden doğrulanmış

## 2. Fazlar

| Faz | Konu | Çıkış |
|-----|------|--------|
| 0 | Açık kararlar | Aşağıdaki tablo kapanır |
| 1 | Hyper-V kaynak | RAM 16 GiB. Disk ancak imajlar `/opt`’u doldurursa |
| 2 | Docker | Engine + Compose. Veri kökü `/opt/docker`. `appadmin` `docker` grubunda |
| 3 | Repo | `/opt/MonitraNG`, `main` |
| 4 | Yerel compose ve `.env` | `app01` override. Portal hostname yok |
| 5 | `mng_common` | Altyapı sağlıklı. GitLab profili verilmez |
| 6 | `mng_apps` | UI ve DomainUI HTTP 200. Ollama yok |
| 7 | Tenant | DomainUI. Ad, Faz 0’da kilitlenir |
| 8 | Posta | Mailcow SMTP. Gönderen adresi ve port ölçülerek yazılır |
| 9 | Smoke | Login, Keeper, DataGateway |

## 3. Açık kararlar

Kurulum komutuna geçmeden bunlar netleşir.

| Konu | Öneri | Neden kilitli değil |
|------|--------|---------------------|
| Erişim adresi | `http://192.168.1.252:3000` | LAN DNS, olmayan `*.ankarabt.com` adını `192.168.1.1` yapıyor |
| Tenant (backend adı) | `ankarabt` | Mongo `mng_ankarabt`, realm `ankarabt`, bucket `mng-ankarabt` olur. Görünen ad ayrıca seçilir |
| Bellek | 16 GiB, Ollama kapalı | 5,3 GiB yetmez. Ölçü Hyper-V host’ta verilir |
| OpenSearch | Açık, heap 1 GiB | Güvenlik olayları buna yazıyor. 16 GiB içinde kalır |
| Collabora ve draw.io | İlk kurulumda açık | Document servisi buna bağlı. Bellek artınca sığar |
| GitLab, Nginx, MkDocs | Kapalı | `app01` uygulama sunucusu. Portal override’ındaki `ankarabt-disabled` profili bu iş için doğru |
| Ollama / MngLLM | Kapalı | 4 GiB limit bu VM’yi bitirir |
| AD | İlk kurulumda kapalı | DC duruyor. Kullanıcılar önce Keycloak’ta. LDAP ayrı iş |
| SMTP | `192.168.1.253` | Örnek `.env` içindeki `172.17.0.1:25` portal hostunun kendi MTA’sı içindir |

## 4. Faz notları

### Faz 1 — Hyper-V

Host `192.168.1.250`. `ubuntu-vg` dolu; kök ve `/opt` yaklaşık 73 GiB. Bellek artışı yeni disk gerektirmez. Disk ancak Docker imajları `/opt`’u doldurursa eklenir.

### Faz 2 — Docker

Ubuntu 26.04 (`resolute`). Docker CE deposunda bu kod adı yayınlanmamış olabilir. Kurulum anında depo kod adı kontrol edilir; yoksa dağıtımın `docker.io` paketi kullanılır.

`appadmin` için `sudo` parola ister. Docker grubuna alındıktan sonra yeni SSH oturumunda `docker ps` parolasız çalışmalıdır.

Veri kökü `/opt/docker`. Repo ile aynı diskte kalır; kök dosya sistemi imajla dolmaz.

### Faz 4 — Compose

Yeni dosyalar, portal dosyalarının kopyası değildir:

- `ApplicationResources/mng_common/docker-compose.ankarabt.local.yml`
- `ApplicationResources/mng_apps/docker-compose.ankarabt.local.apps.yml`
- Sunucuda üretilen `.env` (repoya girmez)

Hostname ve CORS `192.168.1.252` olur. Keycloak `KC_HOSTNAME` bu IP’yi kullanır. `MNG_KEEPER_UI_BASE_URL` ve `GATEWAY_URL` aynı adrestir.

Çalıştırma şekli, diğer ortamlarla aynı kalır:

```bash
cd /opt/MonitraNG/ApplicationResources/mng_common
docker compose -f docker-compose.yml -f docker-compose.ankarabt.local.yml up -d

cd /opt/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.ankarabt.local.apps.yml --env-file .env up -d --build
```

`MNGKEEPER_LICENSE_MASTER_KEY` ve `KEYCLOAK_CLIENT_SECRET` sunucuda üretilir.

### Faz 5 ve 6 — Yığın

Sıra: önce `mng_common`, ağ `mng_common_mng_network` ayakta, sonra uygulamalar. GitLab profili verilmez. Ollama servisi yerel override’da stub veya kapalı olur; 4 GiB limit konmaz.

Sağlık: Mongo ping, Keycloak HTTP, MinIO live, UI `:3000`, DomainUI `:3001`.

### Faz 8 — Posta

Mailcow `192.168.1.253` üzerinde. Hangi SMTP portunun açık olduğu (25, 587, 465) kurulum sırasında ölçülür. Gönderen kimliği mailcow’da açılmış bir hesap olur. Portal örneğindeki `noreply@ankarabt.local` bu VM için geçerli sayılmaz.

## 5. Riskler

| Risk | Etki | Önlem |
|------|------|--------|
| 5,3 GiB RAM ile yığını kaldırmak | OOM, konteyner yeniden başlar | Faz 1 bitmeden Faz 5 yok |
| Portal compose’unu bu VM’de kullanmak | Arayüz `portal.ankarabt.com` adresine kilitlenir | Ayrı local override |
| `*.ankarabt.com` DNS’i | Tarayıcı ağ geçidine gider | IP ile yayın |
| Ubuntu 26.04 Docker deposu | `apt` kod adı bulamaz | Kurulumda depo veya `docker.io` |
| Secret’ların commit edilmesi | Parolalar repoda kalır | Yalnızca sunucu `.env` |
| AD’yi ilk güne almak | Keycloak realm kurulumu uzar | Faz 7’den sonraya |

## 6. Bu planda yapılmayanlar

- `portal.ankarabt.com` üzerinde değişiklik
- Hyper-V host veya DC yapılandırması (Faz 0 kararı çıkmadan)
- Ollama modeli indirmek
- Mailcow içinde hesap açmak (Faz 8 ölçümüne kadar)
- Repoya gerçek `.env` koymak
