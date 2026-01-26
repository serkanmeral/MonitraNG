# GitLab Pages Alternatif Yaklaşım - Artifacts'ı Direkt Serve Etmek

**Tarih:** 4 Ocak 2026  
**Durum:** GitLab CE'de Pages özelliği sınırlı veya çalışmıyor

---

## 📋 Mevcut Durum

### ❌ GitLab Pages Sorunları

1. **Deploy > Pages yok**
   - GitLab CE'de Pages özelliği sınırlı olabilir
   - Settings > General > Visibility altında Pages özelliği bulunamadı

2. **Project Public ama Pages Erişilemiyor**
   - `/root/MonitraNG/-/pages` → Sign in'e yönleniyor
   - Authentication gerekiyor

3. **Pages URL Formatı**
   - Self-hosted GitLab'da Pages URL formatı belirsiz
   - GitLab CE'de Pages özelliği farklı çalışıyor olabilir

---

## 💡 Alternatif Çözüm: Artifacts'ı Direkt Serve Etmek

### Yaklaşım

GitLab Pages yerine, pipeline artifacts'ından `public/` klasörünü alıp Nginx ile direkt static files olarak serve etmek.

**Avantajlar:**
- ✅ Daha basit ve güvenilir
- ✅ Authentication gerektirmez
- ✅ Daha hızlı (static files)
- ✅ GitLab CE/EE fark etmez
- ✅ Tam kontrol

**Dezavantajlar:**
- ⚠️ Manual deploy gerekebilir (veya script ile otomatikleştirilebilir)
- ⚠️ GitLab Pages'in otomatik deploy özelliği kullanılamaz

---

## 🔧 Uygulama Planı

### 1. Artifacts'ı İndirme

**Manuel:**
- GitLab UI: Pipeline > Job > Browse > Download artifacts
- `public/` klasörünü indir

**Otomatik (Script):**
```bash
# GitLab API ile artifacts'ı indir
curl --header "PRIVATE-TOKEN: <token>" \
  "https://gitlab.monitrang.com/api/v4/projects/root%2FMonitraNG/jobs/<job_id>/artifacts/public" \
  -o public.zip
```

### 2. Sunucuya Kopyalama

**Klasör Yapısı:**
```
/var/www/docs.monitrang.com/
├── index.html
├── assets/
├── ...
```

**Kopyalama:**
```bash
# Artifacts'ı extract et
unzip public.zip -d /var/www/docs.monitrang.com/

# Veya direkt kopyala
cp -r public/* /var/www/docs.monitrang.com/
```

### 3. Nginx Yapılandırması

**Yeni Yapılandırma:**
```nginx
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name docs.monitrang.com;

    # SSL Certificate
    ssl_certificate /etc/letsencrypt/live/monitrang.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/monitrang.com/privkey.pem;
    include /etc/nginx/ssl/ssl-params.conf;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;

    # Root directory
    root /var/www/docs.monitrang.com;
    index index.html;

    # Logging
    access_log /var/log/nginx/docs.monitrang.com-access.log;
    error_log /var/log/nginx/docs.monitrang.com-error.log;

    # Static files
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

### 4. Otomatik Deploy (Opsiyonel)

**Script Örneği:**
```bash
#!/bin/bash
# deploy-docs.sh

# GitLab API ile latest artifacts'ı indir
JOB_ID=$(curl -s --header "PRIVATE-TOKEN: $GITLAB_TOKEN" \
  "https://gitlab.monitrang.com/api/v4/projects/root%2FMonitraNG/pipelines?per_page=1" \
  | jq -r '.[0].id')

ARTIFACT_URL="https://gitlab.monitrang.com/api/v4/projects/root%2FMonitraNG/jobs/${JOB_ID}/artifacts/public"

# Artifacts'ı indir ve extract et
curl --header "PRIVATE-TOKEN: $GITLAB_TOKEN" "$ARTIFACT_URL" -o /tmp/docs-artifacts.zip
unzip -o /tmp/docs-artifacts.zip -d /var/www/docs.monitrang.com/
rm /tmp/docs-artifacts.zip

echo "✅ Documentation deployed to /var/www/docs.monitrang.com/"
```

---

## 🔄 Pipeline Entegrasyonu (Opsiyonel)

Pipeline'da artifacts'ı direkt sunucuya kopyalamak için:

```yaml
deploy-docs-to-server:
  stage: deploy-docs
  image: alpine/git
  script:
    - apk add --no-cache openssh-client
    - eval $(ssh-agent -s)
    - echo "$SSH_PRIVATE_KEY" | tr -d '\r' | ssh-add -
    - mkdir -p ~/.ssh
    - ssh-keyscan -H monitrang-server >> ~/.ssh/known_hosts
    - |
      ssh root@monitrang-server << 'EOF'
        cd /root/MonitraNG
        git pull
        cd ApplicationResources/mng_common
        docker compose exec -T nginx mkdir -p /var/www/docs.monitrang.com
        docker compose cp public/ nginx:/var/www/docs.monitrang.com/
      EOF
  only:
    - main
  when: manual
```

---

## 📝 Notlar

- Artifacts yaklaşımı GitLab CE/EE fark etmez
- Static files olduğu için çok hızlı
- Authentication gerektirmez
- Tam kontrol sağlar
- Otomatik deploy script ile kolaylaştırılabilir

---

## ✅ Sonuç

GitLab CE'de Pages özelliği sınırlı veya çalışmıyorsa, artifacts'ı direkt serve etmek daha pratik ve güvenilir bir çözümdür.

---

**Son Güncelleme:** 4 Ocak 2026

