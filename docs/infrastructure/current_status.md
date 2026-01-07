# Infrastructure - Güncel Durum

**Son Güncelleme:** 4 Ocak 2026  
**Konu:** GitLab CI/CD SSH Key Yapılandırması ve Dokümantasyon Deployment

---

## 📋 Son Çalışılan Konu

GitLab CI/CD pipeline'ı için `deploy-docs-to-server` job'ının SSH key yapılandırması yapılıyor. Dokümantasyon artifacts'larını production sunucusuna deploy etmek için SSH key kullanılacak.

---

## ✅ Tamamlanan İşler

### 1. SSH Key Oluşturma ve Yapılandırma
- ✅ SSH key oluşturuldu: `gitlab_deploy_key` (RSA 4096 bit, passphrase'siz)
- ✅ Public key sunucuya eklendi: `root@monitrang-server` → `~/.ssh/authorized_keys`
- ✅ Local SSH bağlantısı test edildi ve başarılı

### 2. GitLab CI/CD Variables
- ✅ `SSH_PRIVATE_KEY` variable'ı GitLab CI/CD Variables'a eklendi
- ✅ Key base64 encoded olarak saklanıyor (whitespace sorunlarını önlemek için)
- ✅ Pipeline'da base64 decode desteği eklendi

### 3. Pipeline Yapılandırması
- ✅ `deploy-docs-to-server` job'ı `.gitlab-ci.yml`'e eklendi
- ✅ Job `alpine:latest` image kullanıyor
- ✅ Key decode ve format kontrolü eklendi
- ✅ SSH key fingerprint kontrolü eklendi
- ✅ SSH connection test ve debug bilgileri eklendi

### 4. Dokümantasyon
- ✅ `docs/infrastructure/gitlab-ssh-key-setup.md` - SSH key setup rehberi
- ✅ `docs/infrastructure/gitlab-ssh-key-base64-setup.md` - Base64 encoding rehberi
- ✅ `docs/infrastructure/gitlab-ssh-key-troubleshooting.md` - Troubleshooting rehberi
- ✅ `docs/infrastructure/gitlab-ssh-key-passphrase-issue.md` - Passphrase sorunu dokümantasyonu

---

## ⏳ Devam Eden İşler

### SSH Key Yapılandırması Sorunu

**Durum:** Pipeline'da SSH bağlantısı "Permission denied" veriyor, ancak key formatı doğru ve key decode ediliyor.

**Bulgular:**
- ✅ Key decode başarılı
- ✅ Key fingerprint doğru: `SHA256:WnWrez5BuDu2w4JG3euXocMPf9LKnGGewYTu0uCgSkQ`
- ✅ Key format doğru (UNENCRYPTED, passphrase yok)
- ✅ Local'de SSH bağlantısı başarılı
- ✅ Sunucu key'i kabul ediyor (verbose log'larda görüldü)
- ❌ Pipeline'da SSH bağlantısı "Permission denied" veriyor
- ❌ SSH verbose log'larında "read_passphrase: can't open /dev/tty" hatası var

**Verbose Log Çıktısı:**
```
debug1: Server accepts key: /root/.ssh/deploy_key RSA SHA256:WnWrez5BuDu2w4JG3euXocMPf9LKnGGewYTu0uCgSkQ explicit
debug1: read_passphrase: can't open /dev/tty: No such device or address
Permission denied, please try again.
```

**Olası Nedenler:**
1. Key formatı sorunlu olabilir (OpenSSH formatı bazı durumlarda sorun çıkarabilir)
2. Key pipeline'da decode edilirken bozulmuş olabilir
3. Key dosyası yazılırken bir encoding sorunu olmuş olabilir

**Çözüm Önerileri:**
- Key'i RSA formatında (OpenSSH yerine) yeniden oluşturmayı denemek
- Key formatını kontrol etmek
- Key'in pipeline'da doğru yazıldığını doğrulamak

---

## 🔄 Sonraki Adımlar

1. **SSH Key Sorunu Çözümü:**
   - Key formatını kontrol etmek
   - Gerekirse key'i RSA formatında yeniden oluşturmak
   - Pipeline'da key'in doğru yazıldığını doğrulamak
   - SSH bağlantısını test etmek

2. **Pipeline Test:**
   - SSH bağlantısı başarılı olduktan sonra
   - `deploy-docs-to-server` job'ını test etmek
   - Dokümantasyon deployment'ını doğrulamak

3. **Dokümantasyon:**
   - Sorun çözüldükten sonra final dokümantasyonu güncellemek

---

## 📝 Önemli Notlar

- Key local'de çalışıyor, sorun pipeline'da
- Key UNENCRYPTED (passphrase yok), ama SSH passphrase soruyor
- Sunucu key'i kabul ediyor, sorun authentication aşamasında
- `ssh-agent` yaklaşımı timeout verdi, key direkt kullanılıyor (`-i` parametresi)
- Base64 encoding/decoding çalışıyor
- Key fingerprint eşleşiyor (local ve pipeline'da aynı)

---

## 🔗 İlgili Dosyalar

- `.gitlab-ci.yml` - Pipeline yapılandırması (deploy-docs-to-server job'ı)
- `docs/infrastructure/gitlab-ssh-key-setup.md` - SSH key setup rehberi
- `docs/infrastructure/gitlab-ssh-key-base64-setup.md` - Base64 encoding rehberi
- `docs/infrastructure/gitlab-ssh-key-troubleshooting.md` - Troubleshooting rehberi
- `docs/infrastructure/gitlab-ssh-key-passphrase-issue.md` - Passphrase sorunu dokümantasyonu
- `scripts/infrastructure/deploy-docs-from-artifacts.sh` - Deploy script (sunucuda)

---

**Son Güncelleme:** 4 Ocak 2026
