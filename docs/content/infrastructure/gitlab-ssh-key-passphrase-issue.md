# GitLab CI/CD SSH Key Passphrase Sorunu

**Tarih:** 4 Ocak 2026  
**Sorun:** SSH key passphrase ile korunmuş, CI/CD'de kullanılamıyor

---

## 🔍 Sorun

Pipeline'da SSH bağlantısı yapılırken şu hata alınıyor:

```
debug1: Server accepts key: /root/.ssh/deploy_key RSA SHA256:WnWrez5BuDu2w4JG3euXocMPf9LKnGGewYTu0uCgSkQ explicit
debug1: read_passphrase: can't open /dev/tty: No such device or address
Permission denied, please try again.
```

**Analiz:**
- ✅ Sunucu key'i kabul ediyor
- ✅ Key fingerprint doğru
- ❌ Key passphrase ile korunmuş gibi görünüyor
- ❌ CI/CD ortamında passphrase girilemiyor

---

## 🔧 Çözüm

### 1. Key'in Passphrase'siz Olduğundan Emin Olun

**Windows PowerShell:**
```powershell
# Key içeriğini kontrol et
Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key" | Select-String "ENCRYPTED"

# Eğer "ENCRYPTED" görünüyorsa, key passphrase ile korunmuş
# Eğer görünmüyorsa, key passphrase'siz
```

**Key Format:**
- **Passphrase'siz key:** `-----BEGIN OPENSSH PRIVATE KEY-----` ile başlar, `ENCRYPTED` kelimesi yoktur
- **Passphrase'li key:** `-----BEGIN OPENSSH PRIVATE KEY-----` ile başlar, içinde `ENCRYPTED` kelimesi vardır

### 2. Passphrase'siz Key Oluşturun

Eğer key passphrase'li ise, yeni bir key oluşturun:

**Windows PowerShell:**
```powershell
# Mevcut key'i yedekle (gerekirse)
if (Test-Path "$env:USERPROFILE\.ssh\gitlab_deploy_key") {
    Copy-Item "$env:USERPROFILE\.ssh\gitlab_deploy_key" "$env:USERPROFILE\.ssh\gitlab_deploy_key.backup"
    Copy-Item "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub" "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub.backup"
}

# Yeni passphrase'siz key oluştur
ssh-keygen -t rsa -b 4096 -C "gitlab-ci-deploy-key" -f "$env:USERPROFILE\.ssh\gitlab_deploy_key" -N ""

# Public key'i göster
Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub"
```

**Önemli:** `-N ""` parametresi passphrase'siz key oluşturur.

### 3. Public Key'i Sunucuya Ekleyin

**PowerShell ile (local'den):**
```powershell
$pubKey = Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub"
ssh root@monitrang-server "echo '$pubKey' >> ~/.ssh/authorized_keys"
```

**Veya sunucuda manuel:**
```bash
# Sunucuya bağlan
ssh root@monitrang-server

# Eski key'i authorized_keys'den kaldır (eğer varsa)
sed -i '/gitlab-ci-deploy-key/d' ~/.ssh/authorized_keys

# Yeni public key'i ekle (yukarıdaki komuttan çıkan key'i kopyala-yapıştır)
echo "ssh-rsa AAAAB3... gitlab-ci-deploy-key" >> ~/.ssh/authorized_keys

# İzinleri kontrol et
chmod 600 ~/.ssh/authorized_keys
chmod 700 ~/.ssh
```

### 4. GitLab CI/CD Variables'ı Güncelleyin

**Yeni Private Key'i Base64 Encode Edin:**

```powershell
$keyContent = Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key" -Raw
$bytes = [System.Text.Encoding]::UTF8.GetBytes($keyContent)
$base64 = [Convert]::ToBase64String($bytes)
Write-Host $base64
```

**GitLab CI/CD Variables:**
1. GitLab UI: Settings > CI/CD > Variables
2. `SSH_PRIVATE_KEY` variable'ını düzenle veya sil ve yeniden ekle
3. Yeni base64 encoded key'i yapıştır
4. Kaydet

### 5. Test Edin

**Local'den test:**
```powershell
ssh -i "$env:USERPROFILE\.ssh\gitlab_deploy_key" root@monitrang-server "echo 'SSH connection successful!'"
```

Password istememeli ve bağlantı başarılı olmalı.

**Pipeline'dan test:**
1. Pipeline'ı çalıştırın
2. `deploy-docs-to-server` job'ını manuel trigger edin
3. SSH bağlantısının başarılı olduğunu kontrol edin

---

## 📝 Notlar

- CI/CD için key'ler **her zaman** passphrase'siz olmalıdır
- Key'i oluştururken `-N ""` parametresini kullanın
- Public key'i sunucuya ekledikten sonra test edin
- GitLab CI/CD Variables'a base64 encoded key ekleyin

---

**Son Güncelleme:** 4 Ocak 2026

