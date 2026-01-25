# MonitraNG Deployment Referans Rehberi

**Amaç:** Deployment ve CI/CD için tek, güncel referans dokümanı.  
**Son Güncelleme:** Ocak 2026  
**Versiyon:** 1.0

---

## 1. Servis Listesi ve Sırası

Tüm uygulama servisleri, deployment sırası ve health endpoint’leri aşağıdaki gibidir. Rolling update bu sırayla yapılır.

| Sıra | Servis        | Container   | Varsayılan port (iç) | Health endpoint              | Açıklama                          |
|------|---------------|-------------|----------------------|------------------------------|-----------------------------------|
| 1    | MngKeeper     | mngkeeper   | 5001                 | `https://...:5001/api/version/short` | IAM, Domain, Auth                 |
| 2    | MngDataGateway| mngdatagateway | 5010              | `https://...:5010/api/v1/health`     | Veri katmanı, dataset, formlar    |
| 3    | MngHub        | mnghub      | 5020                 | `http://...:5020/health`             | Event Hub, mesajlaşma             |
| 4    | MngLLM        | mngllm      | 5030                 | `http://...:5030/health`             | LLM, çeviri, chatbot              |
| 5    | MngScheduler  | mngscheduler| 5090                 | `http://...:5090/api/v1/health`      | Zamanlanmış işler (Quartz)        |
| 6    | MngAdmin      | mngadmin    | 5080                 | `http://...:5080/api/v1/health`      | Backup, admin işlemleri           |
| 7    | MngNotifier   | mngnotifier | 5070                 | `http://...:5070/api/v1/health`      | E-posta, bildirimler              |
| 8    | MngGateway    | mnggateway  | 5000 (HTTP), 5443 (HTTPS) | `http://...:5000/health` / `https://...:5443/health` | API Gateway (Ocelot)   |
| 9    | MngUI         | mngui       | 80 (container)       | `http://.../` (Nginx root)           | Ana SPA (Nuxt)                    |
| 10   | MngDomainUI   | mngdomainui | 3000 (container)     | `http://...:3000/domain/api/health`  | Domain tarafı UI (Nuxt)           |

Production’da erişim genelde Nginx reverse proxy üzerinden olur; portlar host’a açılmayabilir. Deploy script’inde yeni backend’ler için health check `docker exec <container> curl ...` ile yapılır.

---

## 2. CI/CD Pipeline Yapısı

### 2.1 Stage’ler (sırayla)

| Stage           | Açıklama                                                |
|-----------------|---------------------------------------------------------|
| test-setup      | Ortam kontrolü, dizin yapısı                            |
| build           | .NET ve frontend build                                  |
| test            | Birim testleri (allow_failure kullanılıyor)             |
| build-docker    | Docker image build (Runner’da)                          |
| openapi-extract | OpenAPI spec üretimi                                    |
| validate-docs   | Dokümantasyon lint / link kontrolü                      |
| deploy-docs     | MkDocs build, GitLab Pages deploy                       |
| deploy          | Production’a manuel deploy (deploy-services)            |

### 2.2 Build job’ları

- **Backend (.NET):**  
  `build-mngkeeper`, `build-mngdatagateway`, `build-mnghub`, `build-mnggateway`,  
  `build-mngllm`, `build-mngscheduler`, `build-mngadmin`, `build-mngnotifier`
- **Frontend:**  
  `build-frontend` (Mng.Ui), `build-frontend-domainui` (MngDomainUI)

### 2.3 Test job’ları

Tüm backend’ler için aynı isimlerle test job’ları vardır (`test-mngkeeper`, `test-mngllm`, vb.).  
Hepsi `allow_failure: true`; test projesi olmayan çözümler “0 test” ile geçer.

### 2.4 Docker build job’ları

- `build-docker-ui`, `build-docker-domainui`, `build-docker-gateway`
- `build-docker-mngllm`, `build-docker-mngscheduler`, `build-docker-mngadmin`, `build-docker-mngnotifier`

Image’lar Runner’da build edilir; production deploy’da sunucuda `docker compose -f docker-compose.production.yml build` ile yeniden build yapılır (kaynak repodan).

---

## 3. Deployment Akışı (deploy-services)

1. GitLab CI’da `deploy-services` job’ı **manuel** tetiklenir (sadece `main`). Bu job, `needs: [build-mngkeeper, build-mnggateway, build-frontend]` ile ancak bu build’ler bittikten sonra “Play” alır.
2. Sunucuda:
   - `DEPLOY_SERVER_PATH` dizinine geçilir, pre-deploy backup alınır.
   - `git fetch` / `git reset --hard origin/main`.
   - `ApplicationResources/mng_apps` altında `docker-compose.production.yml` kullanılır.
3. Rolling update sırası (yukarıdaki tablodaki 1–10 sıra):
   - Her servis için:  
     `docker compose -f docker-compose.production.yml build <servis>`  
     `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate <servis>`  
   - Bekleme ve health check (yeni backend’lerde `docker exec <container> curl ...`).
4. Sonunda `docker compose ps` ve tüm servisler için “final” health check yapılır.

Health check başarısız olsa bile deployment kesilmez; uyarı verilir ve bir sonraki servise geçilir.

5. **(İsteğe bağlı)** Deploy bittikten sonra **smoke-test-after-deploy** job’ı (aynı stage, `needs: [deploy-services]`, `when: manual`) ile sunucuda `docker exec` ile tüm backend health’leri tekrar kontrol edilebilir. Detay: `DEPLOYMENT_TRIAL_PLAN.md`.

---

## 4. docker-compose.production.yml Konumu ve Servisler

- **Dosya:**  
  `ApplicationResources/mng_apps/docker-compose.production.yml`
- **Network:**  
  `mng_common_mng_network` (external).
- **Context:**  
  `../../<ProjeKlasörü>` (ör. `../../MngLLM`, `../../MngAdmin`).
- **Image isimleri:**  
  `mngkeeper`, `mngdatagateway`, `mnghub`, `mngllm`, `mngscheduler`, `mngadmin`, `mngnotifier`, `mnggateway`, `mngui`, `mngdomainui`  
  Tag: `${VERSION:-latest}`.

Yeni eklenen dört serviste varsayılan port ve health path’ler:

- **mngllm:** 5030, `curl -f http://localhost:5030/health`
- **mngscheduler:** 5090, `curl -f http://localhost:5090/api/v1/health`
- **mngadmin:** 5080, `curl -f http://localhost:5080/api/v1/health`
- **mngnotifier:** 5070, `curl -f http://localhost:5070/api/v1/health`

---

## 5. Yeni Servisler İçin Ortak Env / Bağımlılıklar

| Servis       | Önemli env / bağımlılıklar (özet) |
|-------------|------------------------------------|
| mngllm      | `OLLAMA_BASE_URL`, `MNGKEEPER_URL`, `MNGDATAGATEWAY_URL` |
| mngscheduler| `MONGO_CONNECTION_STRING`, `RABBITMQ_*`, `MNGKEEPER_URL`, `MNGDATAGATEWAY_URL` |
| mngadmin    | `MONGO_CONNECTION_STRING`, `RABBITMQ_*`, `MINIO_*`, `MNGKEEPER_URL` |
| mngnotifier | `RABBITMQ_*`, Mail (SMTP) ayarları |

Detay için ilgili `appsettings.json` ve `env.example` kullanılmalı.

---

## 6. İlgili Dosyalar

| Konu            | Dosya / konum |
|-----------------|----------------|
| Pipeline        | `.gitlab-ci.yml` |
| Production compose | `ApplicationResources/mng_apps/docker-compose.production.yml` |
| Pre-deploy backup | `scripts/backup-pre-deploy.sh` |
| Restore         | `scripts/restore-backup.sh` |
| Kapsamlı CI/CD  | `docs/content/cicd/CICD_DEPLOYMENT_COMPLETE_GUIDE.md` |
| Deployment roadmap | `docs/deployment/DEPLOYMENT_ROADMAP.md` |
| Health check özeti | `docs/content/cicd/HEALTH_CHECK_STATUS.md` |

---

## 7. Yeni Bir Backend Servisi Eklerken Checklist

1. **Kod / çözüm**
   - Solution: `Build/<ServisAdı>/<ServisAdı>.sln`
   - Dockerfile: `Presentation/<ServisAdı>.Api/Dockerfile`
   - Health endpoint (tercihen `/health` veya `/api/v1/health`).

2. **Pipeline**
   - Build: `build-<küçük-servis>` (stage: build).
   - `dotnet restore` + `dotnet build`.
   - İsteğe bağlı: `test-<küçük-servis>` (stage: test, `allow_failure: true`).
   - Docker: `build-docker-<küçük-servis>` (stage: build-docker).

3. **docker-compose.production.yml**
   - Servis bloku: build context, image, env, network, `depends_on`, `healthcheck`, `deploy.resources`.

4. **deploy-services**
   - Rolling update bloğu: build → up -d --no-deps --force-recreate → sleep → health check (`docker exec ... curl`).
   - Sıra: Mevcut backend’lerden sonra, MngGateway’den önce.
   - Final health check listesine yeni servisi ekle.

5. **Dokümantasyon**
   - Bu dosyadaki “Servis listesi” ve “Yeni servisler için env” tablolarını güncelle.

---

**Bu doküman,** pipeline’daki deploy adımları ve `docker-compose.production.yml` ile uyumlu tek referans olarak kullanılabilir. Detaylı kurulum / sorun giderme için `CICD_DEPLOYMENT_COMPLETE_GUIDE.md` ve `DEPLOYMENT_ROADMAP.md` esas alınmalıdır.
