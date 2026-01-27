# Dokümantasyon Deployment Kontrol Listesi

**Tarih:** 27 Ocak 2026  
**Durum:** ✅ Timeout ve performans optimizasyonları tamamlandı

---

## ✅ Tamamlanan İşlemler

### 1. Timeout Ayarları
- ✅ `deploy-docs` job timeout: `15m` → `60m`
- ✅ `deploy-docs-preview` job timeout: `60m` eklendi
- ✅ `deploy-docs-to-server` job timeout: `15m` → `30m`

### 2. Lokal Docker Performans Optimizasyonu
- ✅ `Dockerfile.serve`: Pip cache desteği eklendi
- ✅ `docker-compose.serve.yml`: Pip cache volume eklendi
- ✅ Beklenen etki: İlk build sonrası 30+ dk → ~5 dk

### 3. Sunucu Temizliği
- ✅ Eski `mkdocs` container'ı durduruldu ve kaldırıldı
- ✅ Nginx static root yapılandırması mevcut ve çalışıyor

---

## 📋 Yapılması Gerekenler

### 1. GitLab CI/CD Variables Kontrolü ✅ TAMAMLANDI

SSH key'ler GitLab CI/CD Variables'da tanımlı:
- ✅ `DEPLOY_SSH_PRIVATE_KEY` (öncelikli) - **MEVCUT**
- ✅ `SSH_PRIVATE_KEY` (alternatif) - **MEVCUT**

**Durum:** Her iki SSH key de GitLab'da mevcut, deployment için hazır!

**SSH Key Özellikleri:**
- ✅ Passphrase olmamalı (CI/CD'de passphrase prompt çalışmaz)
- ✅ Private key formatında olmalı (BEGIN/END RSA/OPENSSH PRIVATE KEY)
- ✅ Sunucuda `~/.ssh/authorized_keys` içinde public key olmalı

**SSH Key Test:**
```bash
# Lokal makineden test
ssh -i ~/.ssh/deploy_key root@monitrang-server "echo 'SSH connection successful'"
```

---

### 2. İlk Deployment Testi

**Manuel Test:**
1. GitLab'da pipeline'ı tetikleyin (main branch'e push veya manual trigger)
2. `deploy-docs` job'unun başarılı olduğunu kontrol edin
3. `deploy-docs-to-server` job'unu **manuel olarak** tetikleyin (manual trigger)
4. Job loglarını kontrol edin:
   - SSH bağlantısı başarılı mı?
   - Artifacts kopyalandı mı?
   - Sunucuda dosyalar doğru yere kopyalandı mı?

**Sunucuda Kontrol:**
```bash
ssh root@monitrang-server "ls -la /var/www/docs.monitrang.com | head -10"
```

**Tarayıcıda Test:**
- `https://docs.monitrang.com` adresini açın
- Dokümantasyonun doğru görüntülendiğini kontrol edin

---

### 3. Otomatik Deployment (Opsiyonel)

Şu anda `deploy-docs-to-server` job'u **manuel trigger** ile çalışıyor (`when: manual`).

**Otomatik deployment için:**
`.gitlab-ci.yml` dosyasında `deploy-docs-to-server` job'unun `rules` bölümünü güncelleyin:

```yaml
rules:
  - if: $CI_COMMIT_MESSAGE =~ /\[skip ci\]|\[ci skip\]/i
    when: never
  - if: $CI_COMMIT_BRANCH == "main"
    when: on_success  # Manuel yerine otomatik
```

**Not:** Otomatik deployment için SSH key'in kesinlikle tanımlı olması gerekir.

---

## 🔍 Sorun Giderme

### Problem: SSH Connection Failed

**Kontrol Listesi:**
1. ✅ SSH key GitLab CI/CD Variables'da tanımlı mı?
2. ✅ SSH key passphrase'siz mi?
3. ✅ Public key sunucuda `~/.ssh/authorized_keys` içinde mi?
4. ✅ Sunucu erişilebilir mi? (`ping monitrang-server`)

**Çözüm:**
```bash
# Sunucuda public key kontrolü
ssh root@monitrang-server "cat ~/.ssh/authorized_keys | grep 'deploy'"

# SSH key format kontrolü
ssh-keygen -l -f ~/.ssh/deploy_key
```

---

### Problem: Timeout Hatası

**Kontrol:**
- `deploy-docs` job timeout: 60m (yeterli)
- `deploy-docs-to-server` job timeout: 30m (yeterli)

**Eğer hala timeout alıyorsanız:**
- Network bağlantısını kontrol edin
- Pip install süresini loglardan kontrol edin
- Cache'in düzgün çalıştığını doğrulayın

---

### Problem: Artifacts Bulunamadı

**Kontrol:**
- `deploy-docs` job'u başarılı mı?
- `docs/site/` klasörü oluştu mu?
- Artifacts expire süresi geçmedi mi? (1 gün)

**Çözüm:**
```bash
# Pipeline loglarında kontrol
# deploy-docs job'unun artifacts bölümüne bakın
```

---

## 📊 Deployment Akışı

```
1. Git Push (main branch)
   ↓
2. CI/CD Pipeline Tetiklenir
   ↓
3. deploy-docs Job (60m timeout)
   - Pip install (cache ile hızlı)
   - MkDocs build
   - Artifacts: docs/site/
   ↓
4. deploy-docs-to-server Job (30m timeout, manual)
   - SSH bağlantısı
   - rsync ile sunucuya kopyalama
   - /var/www/docs.monitrang.com dizinine yerleştirme
   ↓
5. Nginx Static Serve
   - https://docs.monitrang.com
```

---

## 🎯 Sonraki Adımlar

1. ✅ SSH key'ler GitLab CI/CD Variables'da mevcut
2. ⏳ İlk deployment'ı manuel olarak test edin (aşağıdaki adımları takip edin)
3. ⏳ Başarılı olduktan sonra otomatik deployment'a geçin (opsiyonel)
4. ⏳ Lokal Docker build'i test edin (cache ile hızlı olmalı)

---

**Not:** Bu checklist, dokümantasyon deployment sürecinin tamamlanması için gereken tüm adımları içerir.
