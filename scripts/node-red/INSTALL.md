# Node-RED E-mail Node Kurulumu

Node-RED'de mail göndermek için `node-red-node-email` package'ını kurmanız gerekiyor.

## 📦 Kurulum Yöntemleri

### Yöntem 1: Node-RED Editörü Üzerinden (Önerilen)

1. Node-RED editörünü açın (genellikle `http://localhost:1880`)
2. Sağ üst köşedeki **☰** (menü) butonuna tıklayın
3. **Manage palette** seçeneğini seçin
4. **Install** sekmesine gidin
5. Arama kutusuna `node-red-node-email` yazın
6. **node-red-node-email** paketini bulun ve **install** butonuna tıklayın
7. Kurulum tamamlandıktan sonra Node-RED'i yeniden başlatın (gerekirse)

### Yöntem 2: Komut Satırından

Node-RED'in kurulu olduğu dizinde (genellikle `~/.node-red` veya proje dizini):

```bash
npm install node-red-node-email
```

**Windows PowerShell:**
```powershell
cd $env:USERPROFILE\.node-red
npm install node-red-node-email
```

**Linux/Mac:**
```bash
cd ~/.node-red
npm install node-red-node-email
```

### Yöntem 3: package.json ile (Proje Bazlı)

Eğer Node-RED'i bir proje içinde kullanıyorsanız:

1. Proje dizininizde `package.json` dosyası oluşturun (yoksa):

```json
{
  "name": "monitrang-node-red",
  "version": "1.0.0",
  "description": "MonitraNG Node-RED flows",
  "dependencies": {
    "node-red": "^3.0.0",
    "node-red-node-email": "^1.15.0"
  }
}
```

2. Dependencies'leri yükleyin:

```bash
npm install
```

## ✅ Kurulum Kontrolü

Kurulum başarılı olduysa:

1. Node-RED editörünü açın
2. Sol paneldeki node listesinde **"e-mail"** node'unu arayın
3. Node'u bulabiliyorsanız kurulum başarılıdır ✅

## 🔄 Node-RED'i Yeniden Başlatma

Kurulumdan sonra Node-RED'i yeniden başlatmanız gerekebilir:

**Windows (Service olarak çalışıyorsa):**
```powershell
# Service'i yeniden başlat
Restart-Service node-red
```

**Linux (Systemd service):**
```bash
sudo systemctl restart node-red
```

**Manuel çalıştırıyorsanız:**
- Terminal'de `Ctrl+C` ile durdurun
- Tekrar `node-red` komutu ile başlatın

## 📝 Alternatif: node-red-contrib-sendgrid (Eğer SendGrid kullanıyorsanız)

Eğer SendGrid gibi bir email servisi kullanıyorsanız:

```bash
npm install node-red-contrib-sendgrid
```

Ancak bizim durumumuzda `node-red-node-email` yeterli çünkü doğrudan SMTP kullanıyoruz.

## 🐛 Sorun Giderme

### "Cannot find module" hatası

1. Node-RED'in doğru dizininde olduğunuzdan emin olun
2. `npm install` komutunu Node-RED'in kurulu olduğu dizinde çalıştırın
3. Node-RED'i yeniden başlatın

### Node görünmüyor

1. Node-RED editörünü yenileyin (F5)
2. Node-RED'i yeniden başlatın
3. Browser cache'ini temizleyin

### Permission hatası

**Linux/Mac:**
```bash
sudo npm install -g node-red-node-email
```

**Windows:**
- PowerShell'i "Run as Administrator" olarak açın

## 📚 Daha Fazla Bilgi

- [node-red-node-email GitHub](https://github.com/node-red/node-red-nodes/tree/master/node_modules/node-red-node-email)
- [Node-RED Node Installation Guide](https://nodered.org/docs/user-guide/runtime/adding-nodes)

---

**Son Güncelleme:** 3 Ocak 2026

