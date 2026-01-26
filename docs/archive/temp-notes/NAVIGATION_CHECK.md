# MkDocs Navigation Kontrol Listesi

## Dosya Konumları

Tüm dataset dokümantasyonları şu konumda olmalı:
- `docs/content/Mng.Ui/guides/chatbot/datasets/`

## Navigation Yapısı

Navigation'da şu şekilde görünmeli:
```
Services
  └── Mng.Ui
      ├── Docker Deployment
      ├── Gateway Integration
      └── Dataset Guides
          ├── Overview
          ├── Creating Dataset
          ├── Field Types (9 field type)
          ├── Validations (3 validation type)
          ├── Indexes (4 index guide)
          └── Examples (Books Dataset)
```

## Kontrol Adımları

1. **MkDocs'i çalıştırın:**
   ```bash
   cd docs
   mkdocs serve
   ```

2. **Tarayıcıda kontrol edin:**
   - `http://127.0.0.1:8000` adresine gidin
   - Sol menüden: **Services** → **Mng.Ui** → **Dataset Guides**

3. **Eğer görünmüyorsa:**
   - MkDocs log'larını kontrol edin (hata mesajları)
   - Dosya yollarının doğru olduğundan emin olun
   - `mkdocs.yml` syntax'ını kontrol edin

## Dosya Listesi

Toplam 19 dosya olmalı:
- 1 index.md (Overview)
- 1 creating-dataset.md
- 9 field-types/*.md
- 3 validations/*.md
- 4 indexes/*.md
- 1 examples/books-dataset.md
