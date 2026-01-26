# GitLab CI/CD SSH Key Base64 Setup

**Tarih:** 4 Ocak 2026  
**Durum:** Base64 encode yöntemi ile whitespace sorunları çözüldü

---

## 🔧 Sorun

GitLab CI/CD Variables'da multi-line SSH private key'ler whitespace karakterleri nedeniyle sorun çıkarabiliyor. Bu yüzden key'i base64 encode edip tek satır olarak saklıyoruz.

---

## ✅ Çözüm

### 1. Private Key'i Base64 Encode Etme

**Windows PowerShell:**
```powershell
$keyContent = Get-Content "$env:USERPROFILE\.ssh\gitlab_deploy_key" -Raw
$bytes = [System.Text.Encoding]::UTF8.GetBytes($keyContent)
$base64 = [Convert]::ToBase64String($bytes)
Write-Host $base64
```

**Linux/Mac:**
```bash
base64 -w 0 ~/.ssh/gitlab_deploy_key
```

**Veya tek satırda:**
```bash
cat ~/.ssh/gitlab_deploy_key | base64 -w 0
```

### 2. GitLab CI/CD Variables'a Ekleme

**Adımlar:**

1. **GitLab UI:**
   - Settings > CI/CD > Variables > Add Variable

2. **Variable Bilgileri:**
   - **Key:** `SSH_PRIVATE_KEY`
   - **Value:** Base64 encoded key (yukarıdaki komuttan çıkan tek satır)
   - **Type:** `Variable`
   - **Protected:** ✅ (işaretle)
   - **Masked:** ❌ (masked olmayabilir - çok uzun)

3. **Kaydet**

### 3. Pipeline Yapılandırması

Pipeline otomatik olarak base64 encoded key'i decode eder. Aynı zamanda decoded key formatını da destekler (geriye uyumluluk için).

**Pipeline Script:**
```yaml
before_script:
  - |
    if [ -z "$SSH_PRIVATE_KEY" ]; then
      echo "⚠️  SSH_PRIVATE_KEY not set. Skipping deployment."
      exit 0
    fi
    # Base64 decode if needed (check if it starts with -----BEGIN)
    if echo "$SSH_PRIVATE_KEY" | grep -q "^-----BEGIN"; then
      # Already decoded, use as is
      echo "$SSH_PRIVATE_KEY" | tr -d '\r' | ssh-add -
    else
      # Base64 encoded, decode first
      echo "$SSH_PRIVATE_KEY" | base64 -d | tr -d '\r' | ssh-add -
    fi
```

---

## 📝 Notlar

- Base64 encode edilmiş key tek satır olarak saklanır
- Whitespace sorunları olmaz
- Pipeline otomatik olarak decode eder
- Hem encoded hem decoded formatları desteklenir

---

## 🔍 Test

Pipeline çalıştığında:
1. `SSH_PRIVATE_KEY` variable'ını okur
2. Base64 encoded ise decode eder
3. SSH agent'a ekler
4. Deploy işlemini gerçekleştirir

---

**Son Güncelleme:** 4 Ocak 2026

