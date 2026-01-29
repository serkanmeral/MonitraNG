# MonitraNG Documentation

MkDocs tabanlı dokümantasyon sistemi.

## 🚀 Hızlı Başlangıç

### Docker ile (Python gerekmez)

Bilgisayarda Python kurulumu sorunluysa docs'u tamamen Docker içinde çalıştırabilirsiniz:

```bash
cd docs
docker compose -f docker-compose.serve.yml up --build
```

Tarayıcıda **http://localhost:6010** açın. Dosyalarda değişiklik yaptıkça sayfa otomatik yenilenir (live reload).

Durdurmak için aynı terminalde `Ctrl+C`, container'ı kaldırmak için:

```bash
docker compose -f docker-compose.serve.yml down
```

**Sadece build denemek** (statik site, nginx ile):

```bash
cd docs
docker build -t mkdocs-docs .
docker run -p 6010:80 mkdocs-docs
```

Tarayıcı: http://localhost:6010

---

### 1. Python Kurulumu (opsiyonel)

Python 3.11+ gereklidir. [Python İndir](https://www.python.org/downloads/)

### 2. Virtual Environment Oluştur

```bash
cd docs
python -m venv venv

# Windows
venv\Scripts\activate

# Linux/Mac
source venv/bin/activate
```

### 3. Dependencies Yükle

```bash
pip install -r requirements.txt
```

### 4. Dokümantasyonu Çalıştır

```bash
mkdocs serve
```

Dokümantasyon şu adreste açılacak: `http://127.0.0.1:8000`

### 5. Build (Production)

```bash
mkdocs build
```

Build edilen dosyalar `site/` klasöründe oluşacak.

## 📁 Yapı

```
docs/
├── mkdocs.yml          # MkDocs yapılandırması
├── requirements.txt    # Python dependencies
└── docs/               # Dokümantasyon kaynak dosyaları
    ├── index.md
    ├── user-guide/
    ├── api/
    ├── services/
    └── development/
```

## 🔄 Otomatik Deploy

GitHub Actions ile otomatik deploy yapılandırılmıştır:

- `main` branch'e push yapıldığında otomatik build ve deploy
- GitHub Pages'e otomatik publish
- Workflow: `.github/workflows/docs-deploy.yml`

## 📝 Dokümantasyon Ekleme

1. İlgili klasöre markdown dosyası ekle
2. `mkdocs.yml` dosyasındaki `nav` bölümüne ekle
3. Commit ve push yap

## 🔧 Yapılandırma

`mkdocs.yml` dosyasında:
- Site bilgileri
- Theme ayarları
- Navigation yapısı
- Plugin'ler

## 📚 Daha Fazla Bilgi

- [MkDocs Documentation](https://www.mkdocs.org/)
- [Material Theme](https://squidfunk.github.io/mkdocs-material/)
- [CI/CD Roadmap](ci-cd-documentation-roadmap.md)

