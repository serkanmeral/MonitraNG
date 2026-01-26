# GitLab Pages URL Formatı Sorunu

**Tarih:** 4 Ocak 2026  
**Durum:** Pages deploy başarılı, ancak URL formatı yanlış

---

## 📋 Mevcut Durum

### ✅ Pages Job Başarılı

**Log'dan:**
```
✅ GitLab Pages deployed successfully!
Pages URL: https://root.gitlab.io/monitrang
```

**Sorun:**
- Log'daki URL (`https://root.gitlab.io/monitrang`) GitLab.com formatı
- Self-hosted GitLab için yanlış format

### ⚠️ Erişim Sorunu

**Test Sonuçları:**
- `http://gitlab:80/root/MonitraNG/-/pages/` → Sign in'e yönleniyor
- `http://gitlab:80/root/MonitraNG/-/pages/main` → Sign in'e yönleniyor
- Authentication gerekiyor

---

## 🔍 Sorun Analizi

### GitLab Pages Self-Hosted Yapılandırması

**docker-compose.yml'de:**
```yaml
pages_external_url 'http://localhost'
gitlab_pages['enable'] = true
gitlab_pages['external_http'] = ['0.0.0.0:8090']
pages_nginx['enable'] = true
```

**Sorun:**
- GitLab Pages URL formatı `/root/MonitraNG/-/pages` authentication gerektiriyor
- Public erişim için project veya Pages ayarlarında public erişim açık olmalı

---

## 💡 Çözüm Önerileri

### 1. GitLab Pages Public Erişimini Kontrol Et

**GitLab UI'da:**
1. Project > Settings > General > Visibility
   - Project'i public yap VEYA
   - Pages için public erişim açık olmalı

2. Deploy > Pages
   - Pages URL'ini kontrol et
   - Gerçek Pages URL'i burada görüntülenir

### 2. GitLab Pages URL Formatını Doğrula

**Olası URL Formatları:**
- `http://gitlab-host/namespace/project/-/pages` (Private projects için auth gerekir)
- `http://pages-host/namespace/project` (Ayrı Pages service varsa)
- `http://gitlab-host/namespace/project` (Public Pages için)

**Not:** GitLab Pages self-hosted'ta genellikle:
- Private projects için authentication gerekir
- Public projects için `/namespace/project/-/pages` formatında erişilebilir olmalı

### 3. Nginx Yapılandırmasını Güncelle

**Şu anki yapılandırma:**
```nginx
location / {
    proxy_pass http://gitlab:80/root/MonitraNG/-/pages/;
    ...
}
```

**Alternatif yaklaşımlar:**
1. Project'i public yap ve Pages URL'ini doğrula
2. GitLab Pages service'i ayrı çalıştır (eğer gerekirse)
3. GitLab API ile Pages URL'ini al

---

## 🔧 Kontrol Edilecekler

### 1. Project Visibility
- [ ] Project public mi?
- [ ] Pages public erişime açık mı?

### 2. GitLab Pages Ayarları
- [ ] GitLab UI: Deploy > Pages
- [ ] Pages URL'i nedir?
- [ ] Pages erişilebilir mi?

### 3. GitLab Pages Service
- [ ] GitLab Pages service çalışıyor mu?
- [ ] Pages log'larını kontrol et

---

## 📝 Notlar

- GitLab Pages self-hosted'ta genellikle private projects için authentication gerekir
- Public erişim için project'in public olması veya Pages'in public erişime açık olması gerekir
- Log'daki URL formatı (`https://root.gitlab.io/monitrang`) GitLab.com formatı, self-hosted için geçerli değil
- Gerçek Pages URL'ini GitLab UI'dan kontrol etmek en doğru yöntem

---

**Son Güncelleme:** 4 Ocak 2026

