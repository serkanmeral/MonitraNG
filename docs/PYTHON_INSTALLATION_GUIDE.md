# Python ve MkDocs Kurulum Rehberi (Windows)

## Adım 1: Python Kurulumu

### Yöntem 1: Microsoft Store (Önerilen - En Kolay)

1. **Microsoft Store'u açın**
2. **"Python 3.12"** veya **"Python 3.11"** arayın
3. **Python'u yükleyin** (Microsoft tarafından sağlanan versiyon)
4. Kurulum tamamlandıktan sonra **yeni bir terminal açın**

### Yöntem 2: Python.org'dan İndirme

1. **https://www.python.org/downloads/** adresine gidin
2. **"Download Python 3.12.x"** butonuna tıklayın (veya en son stabil versiyon)
3. İndirilen `.exe` dosyasını çalıştırın
4. **ÖNEMLİ:** Kurulum sırasında **"Add Python to PATH"** seçeneğini işaretleyin!
5. **"Install Now"** butonuna tıklayın
6. Kurulum tamamlandıktan sonra **yeni bir terminal açın**

## Adım 2: Python Kurulumunu Doğrulama

Yeni bir terminal açın ve şu komutları çalıştırın:

```powershell
python --version
# veya
python3 --version
```

Çıktı şöyle olmalı: `Python 3.12.x` veya benzeri

## Adım 3: pip Kurulumunu Doğrulama

```powershell
python -m pip --version
```

Çıktı şöyle olmalı: `pip 24.x.x from ...`

## Adım 4: MkDocs ve Bağımlılıkları Yükleme

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs
python -m pip install -r requirements.txt
```

Bu komut şunları yükleyecek:
- mkdocs
- mkdocs-material
- mkdocs-swagger-ui-tag
- mkdocs-mermaid2-plugin
- mkdocs-minify-plugin
- pymdown-extensions

## Adım 5: MkDocs'i Çalıştırma

```powershell
cd c:\Serkan\iSIM\MonitraNG\docs
python -m mkdocs serve --dev-addr=127.0.0.1:6010
```

Tarayıcıda **http://127.0.0.1:6010** adresine gidin.

## Sorun Giderme

### "python komutu bulunamadı" hatası
- Yeni bir terminal açın (PATH güncellemesi için)
- Veya Python'u PATH'e manuel ekleyin

### "pip komutu bulunamadı" hatası
```powershell
python -m ensurepip --upgrade
```

### Permission hatası
```powershell
python -m pip install --user -r requirements.txt
```

## Alternatif: Chocolatey ile Kurulum

Eğer Chocolatey yüklüyse:

```powershell
choco install python
```

Sonra yeni terminal açıp yukarıdaki adımları takip edin.
