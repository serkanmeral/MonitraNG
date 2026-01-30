# Infrastructure Template Strategy

## 🎯 Senaryo

Infrastructure'ı tamamladıktan sonra, yeni uygulamalar geliştirmek için template olarak kullanmak.

---

## 🚀 Başlangıç — Netleştirilmiş Kararlar (Güncel)

**Alınan kararlar:**

| Konu | Karar |
|------|--------|
| **İlk yaklaşım** | Branch-based template ile başlanacak (hızlı başlangıç). İleride istenirse ayrı repo + GitHub Template’e geçilebilir. |
| **Branch adı** | `infrastructure-template` |
| **Kaynak** | Mevcut `main` (HEAD) — güncel infrastructure durumu template’e yansır. |
| **Branch içeriği** | Tüm repo aynen; “Dahil / Hariç” listesi yeni proje oluştururken hangi klasörlerin kullanılacağını tanımlar (branch’te dosya silinmez). |
| **Güncelleme** | Infrastructure geliştikçe `main`’deki değişiklikler periyodik olarak `infrastructure-template`’e merge/rebase edilebilir. |

**Phase 1 — Branch ile başlangıç (yapılacaklar):**

- [x] Strateji dokümanında netleştirme bölümü eklendi
- [x] `infrastructure-template` branch’i oluşturuldu
- [x] Branch `origin`’e push edildi
- [ ] (Opsiyonel) İleride ayrı repo veya GitHub Template’e geçiş yapılabilir

**Yeni proje için kullanım (branch hazır olduktan sonra):**

```bash
git clone -b infrastructure-template <repo-url> MyNewProject
cd MyNewProject
# Yeni repo için: git remote set-url origin <yeni-repo-url>
```

---

## ✅ Evet, Yapabilirsiniz!

İki ana yaklaşım var:

### Yaklaşım 1: Branch-Based Template (Basit)
### Yaklaşım 2: Separate Template Repository (Önerilen)

---

## 🔀 Yaklaşım 1: Branch-Based Template

### 1.1 Infrastructure Branch Oluşturma

**Adımlar:**

```bash
# Infrastructure'ı tamamladıktan sonra
git checkout -b infrastructure-template
git push origin infrastructure-template

# Artık bu branch template olarak kullanılabilir
```

### 1.2 Yeni Proje İçin Kullanım

**Yeni proje oluşturma:**

```bash
# Yeni proje klasörü
mkdir MyNewProject
cd MyNewProject

# Infrastructure branch'inden clone
git clone -b infrastructure-template https://github.com/serkanmeral/MonitraNG.git .

# Yeni repo oluştur (opsiyonel)
git remote remove origin
git remote add origin https://github.com/serkanmeral/MyNewProject.git
git push -u origin main
```

**Avantajlar:**
- ✅ Basit ve hızlı
- ✅ Aynı repository içinde
- ✅ Infrastructure güncellemeleri kolay

**Dezavantajlar:**
- ❌ MonitraNG projesi ile karışabilir
- ❌ Template güncellemeleri zor
- ❌ Yeni projeler için temiz başlangıç değil

---

## 🏆 Yaklaşım 2: Separate Template Repository (Önerilen)

### 2.1 Template Repository Oluşturma

**Adımlar:**

```bash
# 1. Infrastructure'ı tamamladıktan sonra
# 2. Yeni bir repository oluştur: MonitraNG-Infrastructure-Template
# 3. Infrastructure kodunu buraya kopyala

# GitHub'da yeni repo oluştur
# https://github.com/new
# Repository name: MonitraNG-Infrastructure-Template
# Description: Infrastructure template for MonitraNG-based applications
# Public veya Private (tercihinize göre)
```

**Template Repository Yapısı:**

```
MonitraNG-Infrastructure-Template/
├── MngKeeper/              # IAM Service
├── MngDataGateway/         # Generic Data Layer
├── MngHub/                 # Event Hub
├── ApplicationResources/   # Docker configs
├── docs/                   # Documentation
├── scripts/                # Deployment scripts
├── .github/workflows/      # CI/CD templates
└── README.md               # Template kullanım kılavuzu
```

### 2.2 Template README Oluşturma

**Template README örneği:**

```markdown
# MonitraNG Infrastructure Template

Bu repository, MonitraNG infrastructure'ını template olarak sağlar.

## 🚀 Yeni Proje Oluşturma

### 1. Template'den Clone

```bash
git clone https://github.com/serkanmeral/MonitraNG-Infrastructure-Template.git MyNewProject
cd MyNewProject
```

### 2. Yeni Repository Oluştur

```bash
# Eski remote'u kaldır
git remote remove origin

# Yeni repository oluştur (GitHub'da)
# Sonra:
git remote add origin https://github.com/serkanmeral/MyNewProject.git
git push -u origin main
```

### 3. Proje İsmini Değiştir

```bash
# Tüm "MonitraNG" referanslarını yeni proje adıyla değiştir
# Namespace'leri güncelle
# README'yi güncelle
```

## 📋 Infrastructure Bileşenleri

- ✅ MngKeeper (IAM)
- ✅ MngDataGateway (Generic Data)
- ✅ MngHub (Event Hub)
- ✅ Docker Compose configs
- ✅ CI/CD templates
- ✅ Deployment scripts
```

---

## 🔧 Yaklaşım 3: GitHub Template Repository (En İyi)

### 3.1 Template Repository Olarak İşaretleme

**GitHub'da:**
1. Repository Settings'e git
2. "Template repository" seçeneğini aktifleştir
3. Artık "Use this template" butonu görünecek

**Avantajlar:**
- ✅ GitHub'ın native template özelliği
- ✅ "Use this template" butonu ile kolay kullanım
- ✅ Template güncellemeleri ayrı takip edilir
- ✅ Yeni projeler için temiz başlangıç

---

## 📋 Önerilen Strateji

### Seçenek 1: GitHub Template Repository (En İyi) ✅

**Adımlar:**

1. **Infrastructure'ı tamamlayın**
2. **Yeni repository oluşturun:**
   - `MonitraNG-Infrastructure-Template`
   - Veya `monitrang-infrastructure-starter`

3. **Template olarak işaretleyin:**
   - GitHub Settings > Template repository ✅

4. **Yeni proje için:**
   - GitHub'da "Use this template" butonuna tıklayın
   - Yeni repository oluşturun
   - Clone edin ve geliştirmeye başlayın

**Avantajlar:**
- ✅ GitHub'ın native özelliği
- ✅ Kolay kullanım
- ✅ Template güncellemeleri ayrı
- ✅ Yeni projeler için temiz başlangıç

---

### Seçenek 2: Branch-Based (Hızlı)

**Adımlar:**

```bash
# Infrastructure branch oluştur
git checkout -b infrastructure-template
git push origin infrastructure-template

# Yeni proje için
git clone -b infrastructure-template <repo-url> MyNewProject
```

**Avantajlar:**
- ✅ Hızlı
- ✅ Aynı repository içinde

**Dezavantajlar:**
- ❌ Template güncellemeleri zor
- ❌ Yeni projeler için temiz başlangıç değil

---

## 🎯 Önerilen Yol Haritası

### Phase 1: Infrastructure Tamamlama
- [ ] Infrastructure bileşenlerini tamamla
- [ ] Test et
- [ ] Dokümantasyonu tamamla

### Phase 2: Template Oluşturma
- [ ] Yeni repository oluştur: `MonitraNG-Infrastructure-Template`
- [ ] Infrastructure kodunu kopyala
- [ ] Template README oluştur
- [ ] GitHub'da "Template repository" olarak işaretle

### Phase 3: Template Kullanımı
- [ ] Yeni proje için "Use this template" kullan
- [ ] Proje ismini değiştir
- [ ] Namespace'leri güncelle
- [ ] Geliştirmeye başla

---

## 📝 Template İçeriği

### Dahil Edilecekler

**Infrastructure Services:**
- ✅ MngKeeper (IAM)
- ✅ MngDataGateway (Generic Data)
- ✅ MngHub (Event Hub)
- ⏳ MngScheduler (gelecekte)

**Supporting Services:**
- ✅ Docker Compose configs
- ✅ Environment examples
- ✅ Deployment scripts

**DevOps:**
- ✅ CI/CD workflow templates
- ✅ Deployment scripts
- ✅ MkDocs yapılandırması

**Documentation:**
- ✅ Infrastructure overview
- ✅ Setup guide
- ✅ Architecture docs

### Hariç Tutulacaklar

**Business Logic:**
- ❌ MngReactor (business logic)
- ❌ MngEngine (data collection)
- ❌ Spesifik uygulamalar

**Test Data:**
- ❌ Test dataset'leri
- ❌ Test script'leri (opsiyonel - template'de kalabilir)

---

## 🔄 Template Güncellemeleri

### Senaryo: Infrastructure'a Yeni Özellik Eklendi

**Seçenek 1: Template Repository Güncelleme**

```bash
# Template repository'de
cd MonitraNG-Infrastructure-Template
git pull origin main  # Ana projeden güncellemeleri al
# Gerekli değişiklikleri yap
git commit -m "feat: Add new infrastructure feature"
git push
```

**Seçenek 2: Yeni Projeler İçin**

```bash
# Yeni projeler template'den oluşturulduğu için
# Otomatik olarak güncel template'i kullanır
```

---

## 🚀 Yeni Proje Oluşturma Senaryosu

### Senaryo: IoT Monitoring Application

**1. Template'den Yeni Proje Oluştur:**

```bash
# GitHub'da "Use this template" butonuna tıkla
# Repository name: IoT-Monitoring-App
# Create repository
```

**2. Clone ve Setup:**

```bash
git clone https://github.com/serkanmeral/IoT-Monitoring-App.git
cd IoT-Monitoring-App

# Proje ismini değiştir
# Tüm "MonitraNG" referanslarını "IoT-Monitoring-App" ile değiştir
```

**3. Yeni Servis Ekle:**

```bash
# Infrastructure'ı kullanarak yeni servis ekle
# MngKeeper, MngDataGateway, MngHub zaten hazır
# Sadece business logic'i ekle
```

**4. Geliştirmeye Başla:**

```bash
# Infrastructure hazır, direkt business logic'e geç
# Authentication: MngKeeper
# Data Layer: MngDataGateway
# Events: MngHub
```

---

## 📊 Karşılaştırma

| Yaklaşım | Avantajlar | Dezavantajlar | Öneri |
|----------|------------|---------------|-------|
| **GitHub Template** | ✅ Native özellik<br>✅ Kolay kullanım<br>✅ Temiz başlangıç | - | 🏆 **En İyi** |
| **Separate Repo** | ✅ Ayrı takip<br>✅ Template güncellemeleri kolay | ❌ Ekstra repo | ✅ **İyi** |
| **Branch-Based** | ✅ Hızlı<br>✅ Basit | ❌ Karışıklık riski<br>❌ Güncelleme zor | ⚠️ **Hızlı çözüm** |

---

## 🎯 Öneri

### GitHub Template Repository Kullanın ✅

**Neden?**
1. ✅ GitHub'ın native özelliği
2. ✅ "Use this template" butonu ile kolay
3. ✅ Template güncellemeleri ayrı takip edilir
4. ✅ Yeni projeler için temiz başlangıç
5. ✅ Best practice

**Adımlar:**
1. Infrastructure'ı tamamlayın
2. Yeni repository oluşturun: `MonitraNG-Infrastructure-Template`
3. Infrastructure kodunu kopyalayın
4. Template README ekleyin
5. GitHub'da "Template repository" olarak işaretleyin
6. Yeni projeler için "Use this template" kullanın

---

## 📝 Template README Örneği

```markdown
# MonitraNG Infrastructure Template

Bu repository, MonitraNG infrastructure'ını template olarak sağlar.

## 🎯 Ne İçerir?

- ✅ **MngKeeper** - Identity & Access Management
- ✅ **MngDataGateway** - Generic Data Layer
- ✅ **MngHub** - Event Hub & Real-time
- ✅ **Docker Compose** - Infrastructure services
- ✅ **CI/CD Templates** - GitHub Actions workflows
- ✅ **Deployment Scripts** - Production deployment

## 🚀 Yeni Proje Oluşturma

1. "Use this template" butonuna tıklayın
2. Yeni repository oluşturun
3. Clone edin
4. Proje ismini değiştirin
5. Geliştirmeye başlayın!

## 📚 Dokümantasyon

Detaylı dokümantasyon için `docs/` klasörüne bakın.
```

---

## 🔄 Workflow

### Infrastructure Geliştirme

```
MonitraNG (Ana Proje)
    ↓
Infrastructure geliştir
    ↓
Test et
    ↓
Template Repository'ye kopyala
    ↓
Template güncelle
```

### Yeni Proje Oluşturma

```
GitHub Template Repository
    ↓
"Use this template" butonuna tıkla
    ↓
Yeni repository oluştur
    ↓
Clone ve geliştirmeye başla
```

---

## 📋 Checklist

### Infrastructure Tamamlama
- [ ] Tüm infrastructure bileşenleri tamamlandı
- [ ] Test edildi
- [ ] Dokümantasyon tamamlandı

### Template Oluşturma
- [ ] Yeni repository oluşturuldu
- [ ] Infrastructure kopyalandı
- [ ] Template README eklendi
- [ ] GitHub'da "Template repository" işaretlendi

### Yeni Proje İçin
- [ ] Template'den yeni proje oluşturuldu
- [ ] Proje ismi değiştirildi
- [ ] Namespace'ler güncellendi
- [ ] Geliştirmeye başlandı

---

**Sonuç:** Evet, infrastructure'ı template olarak kullanabilirsiniz! GitHub Template Repository yaklaşımını öneriyorum.

