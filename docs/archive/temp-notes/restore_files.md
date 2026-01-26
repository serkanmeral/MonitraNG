# Mng.Ui Dosyalarını Geri Yükleme Komutları

## 1. Git Durumunu Kontrol Et
```bash
cd Mng.Ui
git status
```

## 2. Silinen Dosyaları Görüntüle
```bash
git ls-files --deleted
```

## 3. Tüm Silinen Dosyaları Geri Yükle
```bash
git restore .
```
veya
```bash
git checkout -- .
```

## 4. Belirli Dosyaları Geri Yükle (Eğer sadece bazılarını istiyorsanız)
```bash
git restore Mng.Ui/package.json
git restore Mng.Ui/nuxt.config.ts
git restore Mng.Ui/pages/apps/events/index.vue
git restore Mng.Ui/stores/hub.ts
git restore Mng.Ui/stores/apps/sideMenu.ts
```

## 5. Son Commit'ten Tüm Dosyaları Geri Yükle
```bash
git reset --hard HEAD
```

## 6. Icon Değişikliklerini Koruma (Eğer commit edilmişse)
```bash
# Önce mevcut değişiklikleri stash'le
git stash

# Sonra dosyaları geri yükle
git restore .

# Icon değişikliklerini geri getir
git stash pop
```

## ÖNEMLİ: 
- `git reset --hard HEAD` komutu tüm değişiklikleri siler, dikkatli kullanın!
- Önce `git status` ile durumu kontrol edin
- Icon değişikliklerini kaybetmemek için önce `git stash` yapın
