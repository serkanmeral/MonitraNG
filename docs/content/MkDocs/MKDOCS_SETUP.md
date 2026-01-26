# MkDocs Kurulum ve Kullanım Rehberi

## Kurulum

### Python ve pip Kurulumu
MkDocs Python ile çalışır. Python 3.8+ gereklidir.

### MkDocs ve Bağımlılıkları Yükleme

```bash
cd docs
pip install -r requirements.txt
```

veya doğrudan:

```bash
pip install mkdocs mkdocs-material mkdocs-swagger-ui-tag mkdocs-minify-plugin
```

## MkDocs'i Çalıştırma

### Development Server (Local Preview)

```bash
cd docs
mkdocs serve
```

Tarayıcıda `http://127.0.0.1:8000` adresinde görüntülenir.

### Build (Static Site Oluşturma)

```bash
cd docs
mkdocs build
```

Çıktı `docs/site/` klasörüne oluşturulur.

## Yeni Dokümantasyon Ekleme

1. **Dosya Oluştur:** `docs/content/Mng.Ui/guides/chatbot/datasets/` altına `.md` dosyası ekle
2. **Navigation Güncelle:** `docs/mkdocs.yml` dosyasındaki `nav:` bölümüne ekle
3. **Test Et:** `mkdocs serve` ile kontrol et

## Dataset Dokümantasyonları

Dataset dokümantasyonları şu konumda:
- `docs/content/Mng.Ui/guides/chatbot/datasets/`

Navigation'da şu şekilde görünür:
- Services → Mng.Ui → Dataset Guides

## Sorun Giderme

### MkDocs Bulunamadı Hatası
```bash
python -m pip install --upgrade pip
pip install mkdocs mkdocs-material
```

### Port Zaten Kullanılıyor
```bash
mkdocs serve -a 127.0.0.1:8001
```

### YAML Syntax Hatası
`mkdocs.yml` dosyasındaki YAML syntax'ını kontrol edin. Online YAML validator kullanabilirsiniz.
