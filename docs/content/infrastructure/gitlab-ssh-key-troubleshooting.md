# GitLab CI/CD SSH Key Troubleshooting

**Tarih:** 4 Ocak 2026  
**Sorun:** SSH bağlantısı Permission denied veriyor

---

## 🔍 Sorun

Pipeline'da SSH key başarıyla decode ediliyor ve format doğru, ancak SSH bağlantısı "Permission denied (publickey,password)" hatası veriyor.

**Hata Mesajı:**
```
Permission denied, please try again.
Permission denied, please try again.
root@45.141.151.52: Permission denied (publickey,password).
rsync: connection unexpectedly closed (0 bytes received so far) [sender]
rsync error: unexplained error (code 255) at io.c(232) [sender=3.4.1]
```

---

## ✅ Doğrulanan

1. **SSH Key Decode:** ✅ Başarılı
2. **Key Format:** ✅ Doğru (-----BEGIN ile başlıyor)
3. **SSH Bağlantısı:** ❌ Permission denied

---

## 🔧 Çözüm Adımları

### 1. Public Key'i Kontrol Et

**Sunucuda kontrol:**
```bash
ssh root@monitrang-server 'grep gitlab-ci-deploy-key ~/.ssh/authorized_keys'
```

**Beklenen Sonuç:**
- Public key satırı görüntülenmeli
- Eğer hiçbir şey görünmüyorsa, public key eklenmemiş

### 2. Public Key'i Ekle (Gerekirse)

**Local makinede public key'i göster:**
```powershell
# Windows PowerShell
Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub"
```

**Sunucuya ekle:**
```bash
# Public key içeriğini kopyala ve şu komutu çalıştır:
ssh root@monitrang-server 'echo "<PUBLIC_KEY_CONTENT>" >> ~/.ssh/authorized_keys'
```

**Veya otomatik ekleme (local'den):**
```powershell
# Windows PowerShell
$pubKey = Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key.pub"
ssh root@monitrang-server "echo '$pubKey' >> ~/.ssh/authorized_keys"
```

### 3. İzinleri Kontrol Et

**Sunucuda:**
```bash
ssh root@monitrang-server 'ls -la ~/.ssh/authorized_keys'
# Çıktı: -rw------- (600) olmalı

ssh root@monitrang-server 'ls -ld ~/.ssh'
# Çıktı: drwx------ (700) olmalı
```

**Yanlışsa düzelt:**
```bash
ssh root@monitrang-server 'chmod 600 ~/.ssh/authorized_keys'
ssh root@monitrang-server 'chmod 700 ~/.ssh'
```

### 4. Key Eşleşmesini Test Et

**Local makineden test:**
```powershell
# Windows PowerShell
ssh -i "$env:USERPROFILE\.ssh\gitlab_deploy_key" root@monitrang-server "echo 'SSH connection successful!'"
```

**Beklenen Sonuç:**
- Password istememeli
- "SSH connection successful!" mesajı görüntülenmeli

---

## 📝 Notlar

- Public key ve private key eşleşmeli
- `authorized_keys` dosyası her satırda bir public key içermeli
- İzinler doğru olmalı (authorized_keys: 600, .ssh: 700)
- Key'in passphrase'i olmamalı (CI/CD için)

---

## 🔄 Pipeline Test

Public key eklendikten sonra:

1. Pipeline'ı tekrar çalıştır
2. `deploy-docs-to-server` job'ını manuel trigger et
3. SSH bağlantısının başarılı olduğunu kontrol et

---

**Son Güncelleme:** 4 Ocak 2026

