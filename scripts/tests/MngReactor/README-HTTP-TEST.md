# HTTP Collector Test — Node-RED + Collectible Şablonu

Bu klasör, HTTP Collector'ı test etmek için Node-RED flow ve collectible şablonu içerir.

---

## 1. Node-RED Flow Kurulumu

### 1.1 Flow'u İçe Aktar

1. Node-RED editörünü açın (örn. http://localhost:1880)
2. Menü → **Import** → **select a file to import**
3. `NodeRed_HttpTestFlow.json` dosyasını seçin
4. **Import** ile flow'u ekleyin

### 1.2 Deploy

- Sağ üst **Deploy** butonuna tıklayın

### 1.3 Test

Tarayıcı veya curl ile test edin:

```bash
curl http://localhost:1880/api/metrics
```

Örnek yanıt:
```json
{
  "timestamp": "2025-02-13T12:00:00.000Z",
  "sensors": {
    "temperature": 27.3,
    "humidity": 65.2,
    "pressure": 1013.25
  },
  "storage": {
    "disk": {
      "usagePercent": 72.5,
      "freeGB": 125.3
    }
  },
  "status": "healthy"
}
```

---

## 2. Collectible Şablonu Ekleme

### 2.1 Ön koşullar

- `setup-monitoring-datasets.ps1` çalıştırılmış olmalı (mon_collectible_templates var)
- Token yüklenmiş olmalı: `..\..\MngDataGateway\auth\load-token.ps1`

### 2.2 Script çalıştırma

```powershell
cd scripts/tests/MngReactor
.\add-http-test-template.ps1
```

Bu script `mon_collectible_templates` dataset'ine **"HTTP - Node-RED Test"** şablonunu ekler.

---

## 3. Asset Type ve Asset Oluşturma

### 3.1 Asset Type

1. **Asset Tür Tanımları** → **Tipler** → **Yeni tip**
2. Aile: Bir aile seçin veya "API" gibi yeni aile oluşturun
3. Toplama metodu: **HTTP**
4. **Şablon uygula** → "HTTP - Node-RED Test" seçin
5. Kaydet

### 3.2 Asset

1. **Organizasyon** → Item seç → **Yeni Asset**
2. Asset tipi: Oluşturduğunuz HTTP tipi
3. **Base URL:** `http://localhost:1880` (Node-RED varsayılan port)
4. **Auth tipi:** None için boş bırakın (test flow auth gerektirmez)  
   - *Not: Şu an UI'da "Auth tipi" seçili olabilir; Basic veya Bearer Token kullanmıyorsanız Base URL yeterli olabilir. Auth alanları zorunlu değilse boş bırakılabilir.*
5. Kaydet

---

## 4. Response ↔ Collectible Eşlemesi

| Collectible code   | JSON Path                    | Örnek değer |
|--------------------|------------------------------|-------------|
| temperature        | $.sensors.temperature        | 27.3        |
| humidity           | $.sensors.humidity           | 65.2        |
| pressure           | $.sensors.pressure           | 1013.25     |
| disk_usage_percent | $.storage.disk.usagePercent  | 72.5        |
| disk_free_gb       | $.storage.disk.freeGB        | 125.3       |
| status             | $.status                     | "healthy"   |

---

## 5. Node-RED Port Notu

Node-RED varsayılan portu **1880**. Flow'daki endpoint tam URL:

```
http://localhost:1880/api/metrics
```

Docker veya farklı port kullanıyorsanız Base URL'i buna göre güncelleyin.
