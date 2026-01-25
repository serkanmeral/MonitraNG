# Deploy Denemesi Planı

**Amaç:** İlk veya periyodik deployment denemelerinde izlenecek adımlar ve test süreçlerine göre gerekli güncellemeler.  
**Son Güncelleme:** Ocak 2026

---

## 1. Genel Akış

```
[main'e push] → [Pipeline çalışır] → [Build/Test/Docker job'ları] → [Deploy öncesi kontrol] → [deploy-services Manuel] → [Deploy sonrası doğrulama]
```

Deploy **yalnızca manuel** tetiklenir. Önce pipeline’daki build ve (tercihen) test aşamalarının tamamlanması beklenir.

---

## 2. Ön Koşullar

### 2.1 Branch ve commit

- Deploy edilecek kod **main** branch’te olmalı.
- `[skip ci]` / `[ci skip]` kullanılmamalı (pipeline’ın çalışması gerekir).

### 2.2 GitLab CI/CD değişkenleri

Aşağıdakiler **Settings → CI/CD → Variables** altında tanımlı olmalı:

| Değişken | Açıklama | Örnek |
|----------|----------|--------|
| `DEPLOY_SSH_PRIVATE_KEY` | Sunucuya SSH için private key (masked) | (key içeriği) |
| `DEPLOY_SERVER_HOST` | Sunucu IP veya hostname | `45.141.151.52` |
| `DEPLOY_SERVER_USER` | SSH kullanıcı adı | `root` |
| `DEPLOY_SERVER_PORT` | SSH port (opsiyonel) | `22` |
| `DEPLOY_SERVER_PATH` | Repo’nun sunucudaki yolu | `/root/MonitraNG` |

### 2.3 Sunucu tarafı

- Sunucuda `DEPLOY_SERVER_PATH` dizini var ve içinde **git repo** clone edilmiş olmalı.
- Aynı dizinde `ApplicationResources/mng_apps/docker-compose.production.yml` ve gerekli `.env` bulunmalı.
- Altyapı (MongoDB, RabbitMQ, Keycloak, vb.) aynı ağda çalışıyor olmalı (`mng_common_mng_network`).

---

## 3. Pipeline Tetikleme ve İzleme

### 3.1 Tetikleme

1. `main`’e merge/push yapın.
2. GitLab’da **CI/CD → Pipelines** sayfasına gidin.
3. En son pipeline’ı açın.

### 3.2 Deploy öncesi – Bakılacak job’lar (test süreçlerine göre)

Deploy’a basmadan önce aşağıdaki job’ların **başarılı** olması önerilir:

| Öncelik | Job’lar | Açıklama |
|---------|---------|----------|
| **Zorunlu** | `build-mngkeeper`, `build-mngdatagateway`, `build-mnghub`, `build-mnggateway` | Çekirdek backend’ler derlenmiş olmalı. |
| **Zorunlu** | `build-frontend` | Ana UI derlenmiş olmalı. |
| **Önerilen** | `build-mngllm`, `build-mngscheduler`, `build-mngadmin`, `build-mngnotifier` | Yeni servisler deploy edilecekse bu build’ler de yeşil olmalı. |
| **Önerilen** | `build-docker-gateway`, `build-docker-ui` | Dockerfile’ların geçerli olduğunu gösterir (deploy sunucuda yeniden build eder ama burada hata varsa orada da çıkar). |
| **Bilgi** | `test-*` | Hepsi `allow_failure: true`; kırmızı olsa bile deploy edilebilir, ama yeşil olması tercih edilir. |

**Pratik kural:** En azında **Build** ve **build-docker** stage’lerinde kırmızı olan **kritik** job yoksa deploy’a geçin. Test’ler kırmızıysa sebebini not alıp deploy’ı yine de deneyebilir veya önce test’leri düzeltebilirsiniz.

### 3.3 deploy-services job’ının görünmesi

- `deploy-services` job’ı, **ihtiyaç duyduğu build job’ları** bittikten sonra “manuel çalıştırılabilir” (Play ikonu) hale gelir.
- Bu job’lar: `build-mngkeeper`, `build-mnggateway`, `build-frontend` (pipeline’da `needs` ile tanımlı).
- Diğer build’ler paralel koşar; genelde aynı pipeline’da hepsi biter. Yeşil olup olmadıklarını yukarıdaki tabloya göre kontrol edin.

---

## 4. Deploy Çalıştırma

1. **CI/CD → Pipelines** → ilgili pipeline → **deploy** stage.
2. **deploy-services** satırında **Play** (▶) butonuna tıklayın.
3. Log’u takip edin. Sırayla şunlar geçer:
   - Pre-deploy backup
   - Git fetch / reset
   - Her servis için: build → container güncelleme → health check

Herhangi bir adımda **ERROR** görürseniz log’u kaydedin; gerekirse rollback (aşağıya bakın) veya manuel müdahale gerekir.

4. **(İsteğe bağlı)** Deploy bittikten sonra aynı pipeline’da **smoke-test-after-deploy** job’ının **Play** butonuna basarak sunucudaki tüm backend’lerin health kontrolünü CI üzerinden çalıştırabilirsiniz.

---

## 5. Deploy Sonrası Doğrulama

### 5.1 Pipeline’da smoke-test-after-deploy

**deploy-services** bittikten sonra aynı pipeline’da **smoke-test-after-deploy** job’ını manuel çalıştırın. Bu job sunucuya SSH ile bağlanıp her backend container’da `docker exec ... curl` ile health kontrolü yapar; çıktıda ✓/✗ ve `docker compose ps` görünür.

### 5.2 Deploy job log

- **deploy-services** log’unda en sonda “All services updated successfully!” ve “Performing final comprehensive health check…” çıktılarını kontrol edin.
- “✓ … is healthy” satırlarının sayısı beklediğiniz servis sayısına yakın olmalı.

### 5.3 Sunucuda hızlı kontrol

SSH ile sunucuya bağlanıp:

```bash
cd $DEPLOY_SERVER_PATH/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml ps
```

Tüm servisler **Up** veya **Up (healthy)** görünmeli.

### 5.4 Smoke test (lokal makineden)

Deploy’dan sonra canlı ortamı **lokal makinenizden** denemek için:

1. **PowerShell** ile proje kökünde:
   ```powershell
   .\scripts\tests\smoke-test-backend-gateway.ps1 `
     -DirectBaseUrl "https://api.monitrang.com" `
     -GatewayBaseUrl "https://api.monitrang.com" `
     -TestDirectHealth `
     -TestGatewayRoutes `
     -TestAuthFlow `
     -TestBasicScenarios `
     -SkipCertificateCheck
   ```
2. Production URL’leri kendi ortamınıza göre değiştirin (örn. `https://api.monitrang.com` → kullandığınız domain/IP).
3. Domain/kullanıcı/şifre varsayılanı script içinde; gerekirse `-DomainName`, `-Username`, `-Password` ile verin.

Tüm testler yeşil değilse, hangi endpoint’in kırmızı olduğunu not edin; Nginx rotaları, sertifika veya servis sağlığı ile ilgili olabilir.

### 5.5 Sadece health (curl ile)

Sunucuda veya erişebildiğiniz bir makineden:

```bash
# Gateway (Nginx üzerinden gerçek URL’e göre değiştirin)
curl -sk https://api.monitrang.com/health

# Keeper
curl -sk https://api.monitrang.com/keeper/api/version/short
```

Benzer şekilde diğer servislerin Nginx path’lerine göre health URL’leri denenebilir.

---

## 6. Sorun Çıkarsa

### 6.1 Log’ta “ERROR: Failed to build / update …”

- İlgili servisin Dockerfile veya bağımlılıklarını kontrol edin.
- Sunucuda `docker compose -f docker-compose.production.yml build <servis>` ile yerel build deneyin; hata mesajı daha ayrıntılı olur.

### 6.2 Health check “failed after 6 attempts”

- Container çalışıyor mu: `docker compose -f docker-compose.production.yml ps`
- Log: `docker compose -f docker-compose.production.yml logs <servis>`
- Health endpoint’i doğru mu: `DEPLOYMENT_REFERENCE.md` içindeki tabloyla karşılaştırın (path, port).

### 6.3 Rollback

- Pre-deploy backup otomatik alınır. Geri dönmek için sunucuda:
  ```bash
  cd $DEPLOY_SERVER_PATH
  ./scripts/restore-backup.sh <backup_klasör_adı>
  ```
- Backup adı, deploy log’undaki “✓ Backup created: …” satırında yazılıdır.

---

## 7. Test Süreçlerine Göre Yapılabilecek Güncellemeler

Bu plan, test süreçleriyle uyumlu ilerlemek için aşağıdaki güncellemeleri önerir.

### 7.1 Zaten yapılanlar

- **deploy-services** job’ı, kritik build job’larına `needs` ile bağlandı (`build-mngkeeper`, `build-mnggateway`, `build-frontend`). Deploy butonu bu job’lar bittikten sonra kullanılabilir.
- **smoke-test-after-deploy** job’ı eklendi: `deploy-services` tamamlandıktan sonra **manuel** çalıştırılır; sunucuya SSH ile bağlanıp tüm backend container’larında `docker exec ... curl` ile health kontrolü yapar ve `docker compose ps` çıktısını verir. Böylece deploy sonrası canlı ortam kontrolü pipeline üzerinden yapılabilir.

### 7.2 İsteğe bağlı ileride eklenebilecekler

| Güncelleme | Açıklama |
|------------|----------|
| **Kritik testler yeşil olmadan deploy’u gizleme** | Örn. `test-mngkeeper`, `test-mngdatagateway` başarısızsa deploy job’ının “skipped” veya “unavailable” olması için ek `rules` veya araya “gate” job’ı konabilir. Şu an test’ler `allow_failure: true` olduğu için pipeline kırmızı olsa bile deploy manuel çalıştırılabiliyor. |
| **Post-deploy smoke test job’ı** | ✅ Yapıldı: `smoke-test-after-deploy` job’ı sunucuda `docker exec` ile health kontrolü yapıyor; `needs: [deploy-services]`, `when: manual`. İsteğe bağlı: production’ın dış erişim URL’leri üzerinden (Nginx arkası) ayrı bir curl/smoke job’ı eklenebilir. |
| **Deploy öncesi “deploy-readiness” job’ı** | Tüm kritik build’lerin başarılı olduğunu kontrol edip özet rapor veren (ve başarısızsa fail eden) bir job; `deploy-services` buna `needs` ile bağlanır. Böylece “en az bir kritik build kırmızıysa deploy’u hiç sunma” politikası otomatikleştirilebilir. |

Bu maddeler, önce bu planla birkaç deploy denemesi yaptıktan sonra ihtiyaca göre pipeline’a eklenebilir.

---

## 8. Özet Checklist (Deploy Denemesi Öncesi)

- [ ] Kod **main**’de ve pipeline tetiklenmiş.
- [ ] **Build** stage’inde kritik job’lar (en az mngkeeper, mnggateway, frontend) yeşil.
- [ ] **build-docker** stage’inde önemli image’lar (gateway, ui) yeşil.
- [ ] GitLab CI/CD değişkenleri (özellikle `DEPLOY_*`) doğru ve maskeli key güncel.
- [ ] Sunucuda repo ve `ApplicationResources/mng_apps` yolu hazır; altyapı çalışıyor.
- [ ] **deploy-services** job’ında Play ikonu görünüyor.
- [ ] Deploy’dan sonra sunucuda `docker compose ps` ve mümkünse lokal smoke test çalıştırılacak.

Bu checklist’i her deploy denemesinde kısa bir “go/no-go” listesi gibi kullanabilirsiniz. İlk denemede özellikle **backup alındı mı** ve **rollback script’i nerede** bilgisine dikkat etmek faydalıdır.
