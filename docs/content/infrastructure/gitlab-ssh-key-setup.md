# GitLab CI/CD SSH Key Yapılandırması

**Tarih:** 4 Ocak 2026  
**Durum:** SSH key oluşturuldu, GitLab CI/CD variables'a eklenmesi gerekiyor

---

## ✅ Tamamlanan İşlemler

### 1. SSH Key Oluşturuldu

**Key Detayları:**
- **Key Adı:** `gitlab_deploy_key`
- **Konum:** `~/.ssh/gitlab_deploy_key` (Windows: `C:\Users\<user>\.ssh\gitlab_deploy_key`)
- **Tip:** RSA 4096 bit
- **Comment:** `gitlab-ci-deploy-key`

### 2. Public Key Sunucuya Eklendi

Public key başarıyla sunucunun `~/.ssh/authorized_keys` dosyasına eklendi.

**Sunucu:** `root@monitrang-server`

---

## ⏳ Kalan Manuel İşlemler

### 1. GitLab CI/CD Variables'a SSH_PRIVATE_KEY Ekleme

**Adımlar:**

1. **GitLab UI'a Git:**
   - GitLab > Project > Settings > CI/CD > Variables
   - Veya: `https://gitlab.monitrang.com/root/MonitraNG/-/settings/ci_cd`

2. **Variable Ekle:**
   - **"Add Variable"** butonuna tıkla

3. **Variable Bilgileri:**
   - **Key:** `SSH_PRIVATE_KEY`
   - **Value:** Private key içeriği (aşağıda)
   - **Type:** `Variable`
   - **Protected:** ✅ (işaretle)
   - **Masked:** ✅ (işaretle - önerilir)
   - **Environment scope:** (boş bırak - tüm environment'lar için)

4. **Private Key İçeriğini Kopyala:**

Private key dosyasını açıp içeriğini kopyalayın:

**Windows PowerShell:**
```powershell
Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key"
```

**Windows CMD:**
```cmd
type %USERPROFILE%\.ssh\gitlab_deploy_key
```

**Linux/Mac:**
```bash
cat ~/.ssh/gitlab_deploy_key
```

**Önemli:** 
- Private key'in tamamını kopyalayın (BEGIN ve END satırları dahil)
- Boşluk veya ek karakter eklemeyin
- Satır sonlarını koruyun

5. **Variable'ı Kaydet:**
   - "Add variable" butonuna tıkla
   - Variable kaydedilmiş olmalı

### 2. SSH Bağlantısını Test Etme (Opsiyonel)

**Test Komutu:**

```powershell
# Windows PowerShell
ssh -i "$env:USERPROFILE\.ssh\gitlab_deploy_key" root@monitrang-server "echo '✅ SSH bağlantısı başarılı!' && hostname"
```

**Linux/Mac:**
```bash
ssh -i ~/.ssh/gitlab_deploy_key root@monitrang-server "echo '✅ SSH bağlantısı başarılı!' && hostname"
```

**Beklenen Sonuç:**
- Password istememeli
- Sunucu hostname'i görüntülenmeli
- "SSH bağlantısı başarılı!" mesajı görüntülenmeli

### 3. Pipeline'ı Test Etme

**Adımlar:**

1. **Pipeline Çalıştır:**
   - GitLab > CI/CD > Pipelines
   - Son pipeline'ı kontrol et
   - Veya yeni bir commit yaparak pipeline'ı tetikle

2. **deploy-docs-to-server Job'ını Çalıştır:**
   - Pipeline'da `deploy-docs-to-server` job'ı manuel trigger gerektirir
   - Job'ın yanındaki "Play" butonuna tıkla
   - Job çalışmalı

3. **Sonuçları Kontrol Et:**
   - Job log'larını kontrol et
   - Hata varsa, SSH_PRIVATE_KEY variable'ını kontrol et
   - Sunucuda `/var/www/docs.monitrang.com` klasörünün güncellendiğini kontrol et

---

## 🔧 Sorun Giderme

### SSH Bağlantısı Başarısız

**Kontrol Listesi:**
1. Public key sunucuya eklendi mi?
   ```bash
   ssh root@monitrang-server "grep gitlab-ci-deploy-key ~/.ssh/authorized_keys"
   ```

2. authorized_keys izinleri doğru mu?
   ```bash
   ssh root@monitrang-server "ls -la ~/.ssh/authorized_keys"
   # Çıktı: -rw------- (600) olmalı
   ```

3. SSH key dosyası izinleri doğru mu?
   ```bash
   # Windows: File properties > Security
   # Linux/Mac:
   chmod 600 ~/.ssh/gitlab_deploy_key
   ```

### Pipeline Job Başarısız

**Kontrol Listesi:**
1. SSH_PRIVATE_KEY variable'ı doğru eklenmiş mi?
   - GitLab UI > Settings > CI/CD > Variables
   - SSH_PRIVATE_KEY variable'ını kontrol et

2. Variable içeriği doğru mu?
   - BEGIN ve END satırları dahil olmalı
   - Boşluk veya ek karakter olmamalı

3. Job log'larını kontrol et:
   - "SSH_PRIVATE_KEY not set" hatası → Variable eklenmemiş
   - "Permission denied" hatası → Public key sunucuya eklenmemiş veya yanlış
   - "Connection refused" hatası → Sunucu erişilebilir değil

---

## 📝 Notlar

- SSH key sadece CI/CD için kullanılıyor
- Production sunucuya erişim için gerekli
- Private key'i güvenli tutun, asla commit etmeyin
- Variable'ı "Masked" olarak işaretleyin (güvenlik için)

---

**Son Güncelleme:** 4 Ocak 2026

