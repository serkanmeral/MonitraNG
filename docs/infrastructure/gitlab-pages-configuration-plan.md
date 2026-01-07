# GitLab Pages Yapılandırma Planı

**Tarih:** 4 Ocak 2026  
**Domain:** `docs.monitrang.com`  
**Durum:** Planlama aşamasında

---

## 🎯 Hedef

`docs.monitrang.com` domain'i üzerinden GitLab Pages dokümantasyonunu erişilebilir hale getirmek.

---

## 📋 Mevcut Durum

### ✅ Hazır Olanlar

1. **DNS Kaydı**
   - ✅ `docs.monitrang.com` → `45.141.151.52` (DNS kaydı mevcut)

2. **Nginx Yapılandırması**
   - ✅ `docs.monitrang.com` için server block mevcut
   - ✅ SSL sertifikası yapılandırılabilir

3. **GitLab Pages**
   - ✅ Pages aktif (`gitlab_pages['enable'] = true`)
   - ✅ Pipeline'da `pages` job'ı çalışıyor

4. **MkDocs**
   - ✅ `mkdocs.yml` mevcut
   - ⚠️ `site_url`: GitHub Pages URL'i (güncellenmeli)

---

## 🔧 Yapılacaklar

### Phase 1: Nginx Yapılandırması

**Dosya:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf`

**Yapılacak:**
- `docs.monitrang.com` için GitLab Pages'e proxy yapılandırması
- GitLab Pages URL formatı: `http://gitlab:80/root/MonitraNG/-/pages`
- SSL sertifikası yapılandırması (Let's Encrypt)

**Yapılandırma:**
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

    # GitLab Pages Proxy
    location / {
        proxy_pass http://gitlab:80/root/MonitraNG/-/pages/;
        proxy_http_version 1.1;
        
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
```

**Not:** GitLab Pages'in gerçek URL formatını doğrulamak gerekebilir.

---

### Phase 2: MkDocs Yapılandırması

**Dosya:** `docs/mkdocs.yml`

**Yapılacak:**
- `site_url` güncelleme: `https://docs.monitrang.com`
- `repo_url` güncellenebilir (GitLab URL'i)

**Güncelleme:**
```yaml
site_url: https://docs.monitrang.com
repo_url: https://gitlab.monitrang.com/root/MonitraNG
edit_uri: edit/main/docs/
```

---

### Phase 3: GitLab Pages Yapılandırması (Gerekirse)

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Yapılacak:**
- `pages_external_url` güncellenebilir (şu an `http://localhost`)
- Domain için gerekirse `https://docs.monitrang.com` kullanılabilir

**Not:** Self-hosted GitLab'da Pages genellikle GitLab UI üzerinden erişilir, custom domain için Nginx proxy yeterli olabilir.

---

### Phase 4: Test ve Doğrulama

1. Nginx yapılandırmasını test et
2. Nginx container'ı yeniden başlat
3. `docs.monitrang.com` erişilebilirliğini test et
4. Dokümantasyon içeriğini kontrol et
5. Link'lerin çalışıp çalışmadığını kontrol et

---

## 📝 Notlar

- GitLab Pages self-hosted GitLab'da `/root/MonitraNG/-/pages` formatında erişilir
- Nginx reverse proxy ile custom domain kullanılabilir
- SSL sertifikası Let's Encrypt ile yapılandırılmalı
- `site_url` doğru ayarlanmalı (MkDocs relative link'ler için)

---

## 🎯 Sonraki Adımlar

1. ✅ DNS kaydı kontrol edildi
2. ⏳ Nginx yapılandırması güncellenecek
3. ⏳ MkDocs site_url güncellenecek
4. ⏳ Test ve doğrulama

---

**Son Güncelleme:** 4 Ocak 2026

