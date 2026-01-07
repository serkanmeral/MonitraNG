# GitLab Pages Otomatik Deploy Yapılandırması

**Tarih:** 4 Ocak 2026  
**Durum:** Planlama aşamasında

---

## 📋 Mevcut Durum

### ✅ Çalışan İşlemler

1. **Pipeline'da Pages Job**
   - `pages` job'ı artifacts'ı upload ediyor
   - `public/` klasörü artifacts olarak kaydediliyor

2. **Deploy Script**
   - `scripts/infrastructure/deploy-docs-from-artifacts.sh` hazır
   - Artifacts'ı `/var/www/docs.monitrang.com` klasörüne kopyalıyor

3. **Nginx Yapılandırması**
   - Static files olarak serve ediliyor
   - Volume mount çalışıyor

### ❌ Manuel İşlemler

- Artifacts'ı GitLab UI'dan download etmek gerekiyor
- SCP ile sunucuya kopyalamak gerekiyor
- Deploy script'i manuel çalıştırmak gerekiyor

---

## 🎯 Hedef

Pipeline çalıştığında artifacts'ı otomatik olarak sunucuya deploy etmek.

---

## 💡 Çözüm: Pipeline'a Deploy Job Ekleme

### Yaklaşım

Pipeline'a `deploy-docs-to-server` adında yeni bir job ekleyerek artifacts'ı otomatik olarak sunucuya deploy etmek.

**Avantajlar:**
- ✅ Manuel işlem gerektirmez
- ✅ Pipeline otomatik çalıştığında deploy edilir
- ✅ Her commit'te dokümantasyon güncel kalır
- ✅ Rollback kolay (önceki artifacts'ı kullanabilir)

---

## 🔧 Uygulama Planı

### 1. Pipeline Job Yapılandırması

**Yeni Job: `deploy-docs-to-server`**

```yaml
deploy-docs-to-server:
  stage: deploy-docs
  image: alpine/git
  dependencies:
    - pages  # pages job'ının artifacts'ını kullan
  only:
    - main  # Sadece main branch'te
  when: manual  # Opsiyonel: Manuel trigger (güvenlik için)
  before_script:
    - apk add --no-cache openssh-client rsync
    - eval $(ssh-agent -s)
    - echo "$SSH_PRIVATE_KEY" | tr -d '\r' | ssh-add -
    - mkdir -p ~/.ssh
    - ssh-keyscan -H $DEPLOY_SERVER_HOST >> ~/.ssh/known_hosts
  script:
    - |
      echo "=== Deploying Documentation to Server ==="
      # Artifacts'ı sunucuya kopyala
      rsync -avz --delete public/ $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST:/tmp/docs-public/
      # Deploy script'i çalıştır
      ssh $DEPLOY_SERVER_USER@$DEPLOY_SERVER_HOST << 'EOF'
        cd /root/MonitraNG
        chmod +x scripts/infrastructure/deploy-docs-from-artifacts.sh
        ./scripts/infrastructure/deploy-docs-from-artifacts.sh /tmp/docs-public
        # Nginx reload (gerekirse)
        cd ApplicationResources/mng_common
        docker compose exec nginx nginx -s reload
      EOF
      echo "✅ Documentation deployed successfully!"
```

### 2. CI/CD Variables

**GitLab UI > Settings > CI/CD > Variables**

Aşağıdaki değişkenleri ekleyin:

| Variable | Value | Protected | Masked |
|----------|-------|-----------|--------|
| `SSH_PRIVATE_KEY` | SSH private key (sunucuya erişim için) | ✅ | ✅ |
| `DEPLOY_SERVER_HOST` | `monitrang-server` (veya IP: `45.141.151.52`) | ✅ | ❌ |
| `DEPLOY_SERVER_USER` | `root` | ❌ | ❌ |

**SSH Private Key Oluşturma (eğer yoksa):**
```bash
# Yerel makinede
ssh-keygen -t rsa -b 4096 -f ~/.ssh/gitlab_deploy_key -N ""
# Public key'i sunucuya kopyala
ssh-copy-id -i ~/.ssh/gitlab_deploy_key.pub root@monitrang-server
# Private key'i GitLab CI/CD variables'a ekle
cat ~/.ssh/gitlab_deploy_key
```

### 3. Alternatif: GitLab API ile Artifacts Download

**Daha Güvenli Yaklaşım (SSH key gerektirmez):**

```yaml
deploy-docs-to-server:
  stage: deploy-docs
  image: alpine/curl
  dependencies:
    - pages
  only:
    - main
  when: manual
  script:
    - apk add --no-cache openssh-client unzip
    - |
      echo "=== Downloading Artifacts ==="
      # GitLab API ile artifacts'ı download et
      curl --header "PRIVATE-TOKEN: $CI_JOB_TOKEN" \
        "$CI_API_V4_URL/projects/$CI_PROJECT_ID/jobs/$CI_JOB_ID/artifacts/public.zip" \
        -o artifacts.zip
      unzip artifacts.zip -d public/
      # SSH ile deploy et
      # ... (SSH deploy script'i)
```

**Not:** Bu yaklaşım için `CI_JOB_TOKEN` yerine Personal Access Token gerekebilir.

---

## 📝 Notlar

### Güvenlik

- SSH private key'i masked ve protected olarak işaretleyin
- `when: manual` kullanarak manuel deploy yapabilirsiniz
- Production deploy için approval gerektirebilirsiniz

### Otomatik Deploy

Eğer otomatik deploy istiyorsanız:
- `when: manual` satırını kaldırın
- Veya `only: [main]` ile sadece main branch'te otomatik deploy edin

### Rollback

Önceki artifacts'a geri dönmek için:
- GitLab UI > Pipeline > Önceki pipeline > pages job > Download artifacts
- Manuel deploy script'i çalıştırın

---

## ✅ Sonuç

Pipeline'a deploy job'ı ekledikten sonra:
1. ✅ Pipeline çalışır
2. ✅ `pages` job'ı artifacts'ı upload eder
3. ✅ `deploy-docs-to-server` job'ı otomatik (veya manuel) çalışır
4. ✅ Artifacts sunucuya deploy edilir
5. ✅ docs.monitrang.com otomatik güncellenir

**Manuel işlem gerektirmez!** 🎉

---

**Son Güncelleme:** 4 Ocak 2026

