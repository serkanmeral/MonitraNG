# Node-RED Mail Sender Flow

Node-RED için mail gönderme flow'u. MonitraNG mail sunucusu (`mail.monitrang.com`) üzerinden mail göndermek için kullanılır.

## 📋 Özellikler

- **Function bloğunda parametre ayarlama:** Mail parametrelerini function bloğunda kolayca ayarlayabilirsiniz
- **HTTP API endpoint:** REST API üzerinden mail gönderebilirsiniz
- **Test inject node:** Hızlı test için inject node kullanabilirsiniz
- **Gönderen:** `noreply@monitrang.com` (sabit)

## 🚀 Kurulum

### 1. Node-RED'e Flow'u Yükleme

1. Node-RED editörünü açın
2. Menüden **Import** seçeneğini seçin
3. `mail-sender-flow.json` dosyasını seçin veya içeriğini kopyalayıp yapıştırın
4. **Import** butonuna tıklayın

### 2. Gerekli Node'ları Yükleme

Flow'u kullanmak için aşağıdaki node'ların yüklü olması gerekir:

- `node-red-node-email` (Email gönderme için)

**Yükleme:**
```bash
# Node-RED dizininde
npm install node-red-node-email
```

veya Node-RED editöründe:
1. Menü → **Manage palette**
2. **Install** sekmesi
3. `node-red-node-email` arayın ve yükleyin

## 📖 Kullanım

### Yöntem 1: Function Bloğunda Parametre Ayarlama

1. Flow'da **"Mail Parametreleri"** function bloğunu açın
2. Parametreleri düzenleyin:

```javascript
// Alıcı email adresi
msg.to = "serkan.meral@outlook.com";

// Mail konusu
msg.subject = "Test Mail from Node-RED - MonitraNG";

// Mail içeriği (Plain Text)
msg.text = "Bu bir test mailidir.\n\nNode-RED flow'undan gönderilmiştir.";

// Mail içeriği (HTML formatında - opsiyonel)
msg.html = `
    <html>
        <body>
            <h2>Test Mail from Node-RED</h2>
            <p>Bu bir test mailidir.</p>
        </body>
    </html>
`;
```

3. **"Test Mail Gönder"** inject node'una tıklayın veya deploy edip tetikleyin

### Yöntem 2: HTTP API Kullanımı

Flow'u deploy ettikten sonra HTTP POST request ile mail gönderebilirsiniz:

**Endpoint:** `http://localhost:1880/mail/send`

**Request Body (JSON):**
```json
{
    "to": "serkan.meral@outlook.com",
    "subject": "Test Mail from API",
    "text": "Bu bir test mailidir.",
    "html": "<h2>Test Mail</h2><p>Bu bir test mailidir.</p>",
    "fromName": "MonitraNG"
}
```

**cURL Örneği:**
```bash
curl -X POST http://localhost:1880/mail/send \
  -H "Content-Type: application/json" \
  -d '{
    "to": "serkan.meral@outlook.com",
    "subject": "Test Mail",
    "text": "Bu bir test mailidir."
  }'
```

**PowerShell Örneği:**
```powershell
$body = @{
    to = "serkan.meral@outlook.com"
    subject = "Test Mail"
    text = "Bu bir test mailidir."
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:1880/mail/send" -Method Post -Body $body -ContentType "application/json"
```

## ⚙️ Konfigürasyon

### SMTP Ayarları

Flow'da **"SMTP Mail Sender"** email node'u şu ayarlarla yapılandırılmıştır:

- **Server:** `mail.monitrang.com`
- **Port:** `587` (Submission port - local makineden gönderim için)
- **Secure:** `false`
- **TLS:** `true` (STARTTLS kullanılıyor)
- **Authentication:** `basic` (Username/Password gerekli)
- **Username:** `noreply@monitrang.com`
- **Password:** `!2345Qawsedrf*`
- **From:** `noreply@monitrang.com` (function bloğunda ayarlanır)

**Not:** Local makineden (Windows) production sunucuya port 25 genellikle ISP'ler tarafından bloklanır. Bu yüzden port 587 ve authentication kullanıyoruz.

### Parametreler

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `to` | string | ✅ | Alıcı email adresi |
| `subject` | string | ✅ | Mail konusu |
| `text` | string | ⚠️ | Mail içeriği (plain text) - `html` yoksa zorunlu |
| `html` | string | ⚠️ | Mail içeriği (HTML formatında) - `text` yoksa zorunlu |
| `from` | string | ❌ | Gönderen adresi (varsayılan: `noreply@monitrang.com`) |
| `fromName` | string | ❌ | Gönderen adı (varsayılan: `MonitraNG`) |

## 🔧 Özelleştirme

### Function Bloğunda Dinamik Parametreler

Function bloğunda parametreleri dinamik olarak ayarlayabilirsiniz:

```javascript
// Mesaj payload'ından parametreleri al
const params = msg.payload;

msg.to = params.to || "serkan.meral@outlook.com";
msg.subject = params.subject || "Default Subject";
msg.text = params.text || "";
msg.html = params.html || null;

return msg;
```

### Çoklu Alıcı

Birden fazla alıcıya mail göndermek için:

```javascript
// Alıcı listesi
const recipients = [
    "serkan.meral@outlook.com",
    "admin@monitrang.com"
];

// Her alıcı için ayrı mesaj oluştur
const messages = recipients.map(to => {
    return {
        to: to,
        subject: "Test Mail",
        text: "Bu bir test mailidir."
    };
});

// Çoklu mesaj gönder
return messages.map(msg => ({...msg, payload: msg}));
```

## 🐛 Sorun Giderme

### Mail Gönderilemiyor

1. **SMTP bağlantı kontrolü:**
   ```bash
   telnet mail.monitrang.com 25
   ```

2. **Node-RED log'larını kontrol edin:**
   - Node-RED editöründe **Debug** sekmesini açın
   - Hata mesajlarını kontrol edin

3. **Email node konfigürasyonunu kontrol edin:**
   - Server: `mail.monitrang.com`
   - Port: `25`
   - Secure: `false`

### Port 25 Erişim Sorunu

**Sorun:** Local makineden production sunucuya port 25'e bağlanamıyorsunuz (`ECONNREFUSED` hatası)

**Neden:** 
- ISP'ler genellikle port 25'i bloklar (spam önleme)
- Firewall kuralları port 25'i engelleyebilir
- Local makineden production sunucusuna direkt port 25 bağlantısı güvenlik nedeniyle kısıtlanmış olabilir

**Çözüm:** 
Flow zaten port 587 ve authentication kullanacak şekilde yapılandırılmış. Eğer hala sorun yaşıyorsanız:

1. **Port 587 kontrolü:**
   ```bash
   telnet mail.monitrang.com 587
   ```

2. **Firewall kontrolü:**
   - Windows Firewall'da port 587'nin açık olduğundan emin olun
   - Antivirus yazılımı port 587'yi engelliyor olabilir

3. **Alternatif: Production sunucusunda Node-RED kullanın:**
   - Production sunucusunda Node-RED çalıştırın
   - Oradan port 25 kullanarak mail gönderin (authentication gerekmez)

## 📝 Örnekler

### Basit Text Mail

```javascript
msg.to = "serkan.meral@outlook.com";
msg.subject = "Basit Mail";
msg.text = "Bu basit bir text mailidir.";
return msg;
```

### HTML Mail

```javascript
msg.to = "serkan.meral@outlook.com";
msg.subject = "HTML Mail";
msg.html = `
    <html>
        <body style="font-family: Arial, sans-serif;">
            <h2 style="color: #0066cc;">Merhaba!</h2>
            <p>Bu bir HTML mailidir.</p>
            <p><strong>MonitraNG</strong> tarafından gönderilmiştir.</p>
        </body>
    </html>
`;
return msg;
```

### Template ile Mail

```javascript
const template = `
    Merhaba {{name}},
    
    {{message}}
    
    İyi günler,
    MonitraNG Ekibi
`;

msg.to = "serkan.meral@outlook.com";
msg.subject = "Template Mail";
msg.text = template
    .replace("{{name}}", "Serkan")
    .replace("{{message}}", "Bu bir template mailidir.");

return msg;
```

## 🔗 İlgili Dosyalar

- `mail-sender-flow.json` - Node-RED flow dosyası
- `docs/infrastructure/mail-server-setup.md` - Mail sunucusu dokümantasyonu

---

**Son Güncelleme:** 3 Ocak 2026

