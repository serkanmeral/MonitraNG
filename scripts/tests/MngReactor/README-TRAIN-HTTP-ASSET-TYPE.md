# HTTP Asset Type: Tren / Rota

Bu klasör, **tren/rota konum ve sensör** HTTP JSON yanıtına uygun bir asset type ve collectible şablonu ekler.

## JSON response örneği

Engine’in toplayacağı yanıt yapısı:

```json
{
  "trainId": "T1",
  "routeId": "ANK-IST",
  "lat": 40.32261389303681,
  "lon": 31.38456681814496,
  "speed": 2191.3,
  "heading": 107.9,
  "timestamp": "2026-03-08T12:40:31.9167078Z",
  "sensors": {
    "engineTempC": 92.5,
    "oilPressureBar": 5.05,
    "coolantTempC": 91.2,
    "batteryVoltageV": 24.07,
    "brakePipePressureBar": 5.03,
    "cabTempC": 22.3,
    "vibrationMs2": 0.122,
    "doorClosed": true
  }
}
```

## Script ile ekleme

```powershell
cd scripts/tests/MngReactor
.\add-train-http-asset-type.ps1
```

**SSL hatası alırsanız** (localhost sertifikası güvenilir değil):

- Script içinde SSL bypass (callback + TLS 1.2) zaten var; yine de hata alıyorsanız **HTTP** ile deneyin:
  ```powershell
  .\add-train-http-asset-type.ps1 -BaseUrl "http://localhost:5040"
  ```
- Gateway’in HTTP dinlediği port farklıysa (örn. 5041) `-BaseUrl "http://localhost:5041"` kullanın.

**Ön koşullar:**

- `scripts/tests/MngDataGateway/auth/load-token.ps1` ile token yüklü olmalı
- `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1` çalıştırılmış olmalı (`mon_asset_type_family`, `mon_asset_types`, `mon_collectible_templates`)

Script:

1. **Collectible şablonu** ekler: "HTTP - Tren Rota Sensörleri" (`mon_collectible_templates`)
2. Gerekirse **aile** ekler: "Raylı Sistem" (`mon_asset_type_family`)
3. **Asset type** ekler: "Tren / Rota", toplama metodu **HTTP**, yukarıdaki alanlara karşılık gelen collectible’lar ile

## Collectible eşlemesi (code → JSON)

| code | Açıklama | Örnek |
|------|----------|--------|
| trainId | Tren ID | "T1" |
| routeId | Rota ID | "ANK-IST" |
| lat | Enlem | 40.32 |
| lon | Boylam | 31.38 |
| speed | Hız | 2191.3 |
| heading | Yön (derece) | 107.9 |
| timestamp | Zaman damgası | "2026-03-08T12:40:31.9167078Z" |
| sensors.engineTempC | Motor sıcaklığı (°C) | 92.5 |
| sensors.oilPressureBar | Yağ basıncı (bar) | 5.05 |
| sensors.coolantTempC | Devirdaim sıcaklığı (°C) | 91.2 |
| sensors.batteryVoltageV | Akü voltajı (V) | 24.07 |
| sensors.brakePipePressureBar | Fren borusu basıncı (bar) | 5.03 |
| sensors.cabTempC | Kabin sıcaklığı (°C) | 22.3 |
| sensors.vibrationMs2 | Titreşim (m/s²) | 0.122 |
| sensors.doorClosed | Kapı kapalı | true |

Engine HTTP collector, `code` değerini önce root key, yoksa noktalı path (`sensors.engineTempC`) olarak kullanır.

## Asset oluşturma (UI)

1. **Organizasyon** → bir item seçin → **Yeni Asset**
2. **Asset tipi:** "Tren / Rota" seçin
3. **Bağlantı bilgisi:**
   - **Base URL:** Tren API’nin base adresi (örn. `http://localhost:5000` veya simülatör adresi)
   - **Auth tipi:** Yok veya Basic (API gerektiriyorsa)

Kaydettikten sonra bu asset’i bir **Agent**’a ekleyip Engine’e atayın; Engine periyodik olarak Base URL + **/api/metrics** path’ine GET atar ve yukarıdaki JSON’u bekler.

## Endpoint path notu

Şu an Engine HTTP collector **sabit path** kullanır: `Base URL + /api/metrics`. Tren API’niz farklı bir path’te (örn. `/api/train/status` veya `/trains/T1`) dönüyorsa:

- Bu path’i sunan bir proxy kullanabilirsiniz, veya
- İleride Engine’de path’in asset/connection_info veya collectible params ile yapılandırılması eklenebilir.

## Manuel: Sadece şablon (UI’da “Şablon uygula”)

Sadece collectible şablonunu ekleyip asset type’ı UI’dan kendiniz tanımlamak isterseniz, şablonu DataGateway’e ayrı ekleyebilirsiniz; script’teki `$collectibles` ve `$templateBody` bloklarını kullanarak `mon_collectible_templates` için tek bir POST atmanız yeterli. Asset Type Tanımları sayfasında **Toplama metodu: HTTP** seçip **Şablon uygula** → "HTTP - Tren Rota Sensörleri" ile forma collectible’lar dolar.
