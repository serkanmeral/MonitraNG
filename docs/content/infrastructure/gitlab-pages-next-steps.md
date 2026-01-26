# GitLab Pages Yapılandırma - Sonraki Adımlar

**Tarih:** 4 Ocak 2026  
**Durum:** Yapılandırma güncellemeleri tamamlandı, sunucuya deploy bekleniyor

---

## ✅ Tamamlanan İşler

1. **Nginx Yapılandırması Güncellendi**
   - Dosya: `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf`
   - Değişiklik: `proxy_pass` GitLab Pages URL'ine güncellendi
   - Önceki: `http://gitlab:80`
   - Yeni: `http://gitlab:80/root/MonitraNG/-/pages/`

2. **MkDocs Yapılandırması Güncellendi**
   - Dosya: `docs/mkdocs.yml`
   - Değişiklikler:
     - `site_url`: `https://docs.monitrang.com`
     - `repo_url`: `https://gitlab.monitrang.com/root/MonitraNG`
     - `repo_name`: `root/MonitraNG`

---

## 🔧 Mevcut Sorun

**docs.monitrang.com GitLab ana sayfasına yönleniyor**

**Neden:**
- Eski Nginx yapılandırmasında `proxy_pass http://gitlab:80` kullanılıyordu
- Bu, tüm istekleri GitLab ana sayfasına yönlendiriyordu
- GitLab Pages için doğru path (`/root/MonitraNG/-/pages/`) eksikti

**Çözüm:**
- Yeni yapılandırmada `proxy_pass http://gitlab:80/root/MonitraNG/-/pages/` kullanılıyor
- Bu, istekleri direkt GitLab Pages'e yönlendirecek

---

## 📋 Sonraki Adımlar

### 1. Sunucuya Dosya Kopyalama

**Seçenek A: Git Commit/Push (Önerilen)**
```bash
# Yerel
git add ApplicationResources/mng_common/nginx/conf.d/monitrang.conf docs/mkdocs.yml
git commit -m "feat: GitLab Pages yapılandırması - docs.monitrang.com"
git push

# Sunucuda
cd /root/MonitraNG
git pull
```

**Seçenek B: Doğrudan Kopyalama**
```bash
# Nginx yapılandırmasını sunucuya kopyala
scp ApplicationResources/mng_common/nginx/conf.d/monitrang.conf root@monitrang-server:/root/MonitraNG/ApplicationResources/mng_common/nginx/conf.d/
```

### 2. Nginx Container'ını Yeniden Başlatma

```bash
# Sunucuda
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose restart nginx

# Veya config test edip reload
docker exec nginx nginx -t
docker exec nginx nginx -s reload
```

### 3. Test ve Doğrulama

**Yapılacak Testler:**
1. ✅ Nginx yapılandırması geçerli mi?
   ```bash
   docker exec nginx nginx -t
   ```

2. ✅ docs.monitrang.com erişilebilir mi?
   ```bash
   curl -I https://docs.monitrang.com
   ```

3. ✅ GitLab Pages içeriği görüntüleniyor mu?
   ```bash
   curl -L https://docs.monitrang.com | head -20
   ```

4. ✅ SSL sertifikası çalışıyor mu?
   ```bash
   curl -vI https://docs.monitrang.com 2>&1 | grep -i ssl
   ```

**Beklenen Sonuç:**
- docs.monitrang.com GitLab Pages dokümantasyonunu göstermeli
- GitLab ana sayfasına yönlenmemeli
- SSL sertifikası geçerli olmalı

---

## 🔍 Sorun Giderme

### Sorun: docs.monitrang.com hala GitLab ana sayfasına yönleniyor

**Kontrol Listesi:**
1. Nginx yapılandırması sunucuya kopyalandı mı?
2. Nginx container'ı yeniden başlatıldı mı?
3. Nginx yapılandırması geçerli mi? (`nginx -t`)
4. GitLab Pages deploy edildi mi? (Pipeline'da pages job başarılı mı?)
5. GitLab Pages URL'i doğru mu? (`http://gitlab:80/root/MonitraNG/-/pages`)

**Olası Çözümler:**
- Nginx cache'ini temizle
- GitLab Pages'in gerçek URL formatını kontrol et
- Nginx log'larını kontrol et: `docker logs nginx | tail -50`

### Sorun: 404 veya 502 hatası

**Kontrol Listesi:**
1. GitLab Pages deploy edildi mi?
2. GitLab container'ı çalışıyor mu?
3. Network bağlantısı var mı? (`docker network inspect mng_network`)
4. Container name doğru mu? (`gitlab`)

**Olası Çözümler:**
- GitLab Pages URL formatını doğrula
- GitLab container'ını kontrol et: `docker logs gitlab | tail -50`
- Network bağlantısını kontrol et

---

## 📝 Notlar

- GitLab Pages self-hosted GitLab'da `/root/MonitraNG/-/pages` formatında erişilir
- Nginx reverse proxy ile custom domain kullanılabilir
- SSL sertifikası Let's Encrypt ile yapılandırılmış (monitrang.com wildcard)
- Pipeline'da `pages` job'ı başarılı olmalı (artifacts upload)

---

**Son Güncelleme:** 4 Ocak 2026

