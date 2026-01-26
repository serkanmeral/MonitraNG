# Local ve Sunucu Kod Senkronizasyon Stratejisi

**Tarih:** 8 Ocak 2026  
**Durum:** ⚠️ **SENKRONİZASYON GEREKLİ**

---

## 📋 Mevcut Durum

### Local (Windows)
- **Branch:** `main`
- **Son Commit:** `360bcd9 fix(infra): fix admin UI access issues...`
- **Değişiklikler:**
  - MngKeeper kod değişiklikleri (local'de yapılan)
  - KeycloakService.cs (henüz commit edilmemiş - sunucudaki değişikliklerle çakışabilir)
  - Extensions.cs (henüz commit edilmemiş - sunucudaki değişikliklerle çakışabilir)

### Sunucu (Production)
- **Branch:** `main`
- **Son Commit:** `bd14356 feat: Enable Swagger in Production environment for MngKeeper`
- **Değişiklikler:**
  - KeycloakService.cs (Keycloak endpoint'leri `/keycloak` prefix'i ile güncellendi)
  - Extensions.cs (Swagger Production'da etkinleştirildi)
  - docker-compose.production.yml (EnableSwagger=true eklendi)

---

## 🔄 Senkronizasyon Stratejisi

### Senaryo 1: Sunucudaki Değişiklikleri Local'e Getirme (Önerilen)

**Durum:** Sunucudaki değişiklikler test edildi ve çalışıyor. Local'deki değişiklikler henüz commit edilmemiş.

**Adımlar:**

1. **Local'deki değişiklikleri yedekle:**
   ```bash
   git stash push -m "Local changes before sync"
   ```

2. **Sunucudaki commit'leri local'e getir:**
   ```bash
   git fetch origin
   git merge origin/main
   # veya
   git pull origin main
   ```

3. **Stash'lenmiş değişiklikleri geri yükle:**
   ```bash
   git stash pop
   ```

4. **Çakışmaları çöz:**
   - KeycloakService.cs: Sunucudaki değişiklikler (Keycloak endpoint'leri) öncelikli
   - Extensions.cs: Her iki değişikliği birleştir (Swagger + local değişiklikler)
   - docker-compose.production.yml: Sunucudaki değişiklikler öncelikli

5. **Test et ve commit et:**
   ```bash
   git add .
   git commit -m "fix: sync Keycloak endpoint fixes from server"
   git push origin main
   ```

---

### Senaryo 2: Local'deki Değişiklikleri Önce Commit Etme

**Durum:** Local'deki değişiklikler önemli ve önce commit edilmeli.

**Adımlar:**

1. **Local'deki değişiklikleri commit et:**
   ```bash
   git add .
   git commit -m "feat: local changes description"
   git push origin main
   ```

2. **Sunucuda pull yap:**
   ```bash
   ssh root@monitrang-server
   cd /root/MonitraNG
   git pull origin main
   ```

3. **Çakışmaları çöz:**
   - Sunucuda merge conflict'leri çöz
   - Test et
   - Commit ve push et

---

### Senaryo 3: Her İki Tarafta da Değişiklik Var (Çakışma Riski)

**Durum:** Hem local hem sunucuda aynı dosyalarda değişiklik var.

**Adımlar:**

1. **Sunucudaki değişiklikleri patch olarak export et:**
   ```bash
   ssh root@monitrang-server 'cd /root/MonitraNG && git diff HEAD > /tmp/server-changes.patch'
   scp root@monitrang-server:/tmp/server-changes.patch ./server-changes.patch
   ```

2. **Local'de patch'i incele:**
   ```bash
   git apply --check server-changes.patch
   ```

3. **Çakışma yoksa uygula:**
   ```bash
   git apply server-changes.patch
   ```

4. **Çakışma varsa manuel birleştir:**
   - KeycloakService.cs: Sunucudaki endpoint değişikliklerini al
   - Extensions.cs: Her iki değişikliği birleştir
   - docker-compose.production.yml: Sunucudaki değişiklikleri al

---

## 🎯 Önerilen Yaklaşım

**En Güvenli Yöntem:**

1. **Sunucudaki değişiklikleri önce local'e getir** (çünkü test edildi ve çalışıyor)
2. **Local'deki değişiklikleri stash'le**
3. **Sunucudaki commit'leri pull et**
4. **Stash'lenmiş değişiklikleri geri yükle ve çakışmaları çöz**
5. **Test et ve commit et**

---

## 📝 Çakışma Çözüm Rehberi

### KeycloakService.cs Çakışması

**Sunucudaki Değişiklikler (Öncelikli):**
- Tüm `/admin/realms/...` → `/keycloak/admin/realms/...`
- `/realms/master/protocol/...` → `/keycloak/realms/master/protocol/...`
- Client secret opsiyonel hale getirildi

**Çözüm:**
- Sunucudaki değişiklikleri koru
- Local'deki diğer değişiklikleri birleştir

### Extensions.cs Çakışması

**Sunucudaki Değişiklikler:**
- Swagger Production'da etkinleştirildi (`EnableSwagger` kontrolü)

**Çözüm:**
- Her iki değişikliği birleştir (Swagger + local değişiklikler)

### docker-compose.production.yml Çakışması

**Sunucudaki Değişiklikler:**
- `EnableSwagger=true` eklendi

**Çözüm:**
- Sunucudaki değişiklikleri koru

---

## ⚠️ Dikkat Edilmesi Gerekenler

1. **Yedekleme:** Senkronizasyon öncesi mutlaka yedek al
2. **Test:** Her adımdan sonra test et
3. **Commit Mesajları:** Açıklayıcı commit mesajları kullan
4. **Çakışmalar:** Çakışmaları dikkatli çöz, otomatik merge'den kaçın

---

## 🔧 Hızlı Komutlar

### Sunucudaki Değişiklikleri Local'e Getirme
```bash
# 1. Local değişiklikleri yedekle
git stash push -m "Local changes before sync"

# 2. Sunucudaki commit'leri getir
git fetch origin
git merge origin/main

# 3. Yedeklenmiş değişiklikleri geri yükle
git stash pop

# 4. Çakışmaları çöz ve test et
# ... manuel çözüm ...

# 5. Commit ve push
git add .
git commit -m "fix: sync Keycloak endpoint fixes from server"
git push origin main
```

### Sunucuda Güncelleme
```bash
ssh root@monitrang-server
cd /root/MonitraNG
git pull origin main
cd ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml build mngkeeper
docker compose -f docker-compose.production.yml up -d --force-recreate --no-deps mngkeeper
```

---

**Son Güncelleme:** 8 Ocak 2026
