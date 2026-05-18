# Engine-Reactor Status, Hata Raporlama ve Ingest Şifreleme Spesifikasyonu

Bu doküman, Engine'in Reactor'a periyodik durum bildirimi, toplama hatalarının raporlanması ve ingest verisinin şifrelenmesi konularını tanımlar.

---

## 1. Genel Bakış

| Konu | Açıklama | Karar |
|------|----------|-------|
| **Status (Heartbeat)** | Engine'in belirli periyotlarda Reactor'a sağlık bildirmesi | Tek endpoint, periyodik |
| **Hata Raporlama** | Asset bağlantı/toplama hatalarının Reactor ve MngUI'da görünmesi | Status payload içinde, periyodik (hibrit C) |
| **Ingest Şifreleme** | Metrik verisinin GZip + AES ile gönderilmesi | Config string'deki CompressPbk/CompressPrk ile |

---

## 2. Engine Status Endpoint

### 2.1 Endpoint

```
POST /api/v1/engine/status
Authorization: Bearer {token}
Content-Type: application/json
```

### 2.2 İstek Gövdesi

```json
{
  "engineId": "string",
  "domain": "string",
  "timestamp": "2025-02-15T12:00:00Z",
  "health": "ok | degraded | error",
  "errors": [
    {
      "assetId": "string",
      "agentId": "string",
      "errorCode": "connection_timeout | auth_failed | ssh_error | snmp_error | wmi_error | unknown",
      "message": "string",
      "occurredAt": "2025-02-15T11:58:00Z"
    }
  ],
  "queueDepth": 0,
  "assetCount": 5
}
```

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| engineId | Evet | Engine kimliği |
| domain | Evet | Tenant domain |
| timestamp | Evet | Rapor zamanı (UTC) |
| health | Evet | `ok`, `degraded`, `error` |
| errors | Hayır | Son N toplama hatası (örn. son 50); boş array olabilir |
| queueDepth | Hayır | Kuyruktaki batch sayısı |
| assetCount | Hayır | Config'teki asset sayısı |
| hostAddress | Hayır | Engine'in çalıştığı makinenin IP adresi |

### 2.3 Reactor Tarafı İşlem

- `mon_engines` collection'da `engineId` ile ilgili kaydı bul
- `lastSeenAt` = `timestamp` ile güncelle
- `lastErrors` = `errors` ile güncelle (veya mevcut alan: `lastErrors` array olarak sakla)
- Yanıt: `{ "success": true }` veya uygun hata

### 2.4 mon_engines Alanları (Status güncellemesi)

| Alan | Tip | Açıklama |
|------|-----|----------|
| lastSeenAt | datetime | Son görülme zamanı |
| health | text | ok, degraded, error |
| hostAddress | text | Engine'in çalıştığı makinenin IP adresi (son bilinen) |
| lastErrors | array | Son toplama hataları; her öğe `{ assetId, agentId, errorCode, message, occurredAt }` |

`lastErrors` son 50–100 hata ile sınırlandırılabilir; yeni gelen `errors` mevcut liste üzerine yazılır veya birleştirilir.

---

## 3. Engine Tarafı: Status Job ve Hata Buffer

### 3.1 EngineStatusJob

- **Periyot:** `appsettings.json` → `MngEngine:EngineStatusJob:CronExpression` (Quartz cron, varsayılan `0 */2 * * * ?` = her 2 dakika). Env: `MngEngine__EngineStatusJob__CronExpression`
- **Görev:** Hata buffer'ını topla, status payload oluştur (hostAddress dahil), POST /api/v1/engine/status gönder
- **Health hesaplama:** `errors` boşsa `ok`, hata varsa `degraded` veya `error` (kritik hata sayısına göre)

### 3.2 Hata Buffer (IEngineErrorBuffer)

- **Arayüz:** `IEngineErrorBuffer` – `Add(assetId, agentId, errorCode, message)`, `TakeRecent(count)`, `Clear()`
- **Tutarlılık:** Thread-safe (ConcurrentQueue veya lock)
- **Kapasite:** Son 50–100 hata; dolunca en eskiler atılır
- **Kullanım:** CollectorJob catch bloğunda `_errorBuffer.Add(...)` çağrılır

### 3.3 Hata Kodları

| Kod | Açıklama |
|-----|----------|
| connection_timeout | Bağlantı zaman aşımı |
| auth_failed | Kimlik doğrulama hatası |
| ssh_error | SSH toplama hatası |
| snmp_error | SNMP toplama hatası |
| wmi_error | WMI toplama hatası |
| unknown | Diğer hatalar |

---

## 4. Ingest Şifreleme (GZip + AES)

### 4.1 Genel Akış

```
Engine: IngestMetricsRequest (JSON)
  → JSON serialize
  → GZip compress
  → AES encrypt (CompressPbk, CompressPrk - config'ten)
  → Base64 encode
  → POST body + header: X-Payload-Format: encrypted
```

```
Reactor: Gelen istek
  → X-Payload-Format: encrypted ise
    → Base64 decode
    → AES decrypt
    → GZip decompress
    → JSON deserialize
    → Mevcut IngestProcessing akışı
  → Değilse: mevcut plain JSON akışı (geriye uyumluluk)
```

### 4.2 Engine Tarafı

- Config'ten `CompressPbk`, `CompressPrk` (EngineInfo decrypt sonrası mevcut)
- `ICryptProcessing.Compress` + AES encrypt (config string ile aynı anahtar yapısı)
- Content-Type: `application/octet-stream` veya `text/plain` (Base64 string)
- Header: `X-Payload-Format: encrypted` veya `X-Ingest-Encoded: gzip-aes`

### 4.3 Reactor Tarafı

- Ingest controller: Header kontrolü; şifreliyse decrypt pipeline
- `IngestDecryptKey`, `IngestEncryptKey` (ComressPrk, CompressPbk) ile decrypt
- Decompress → IngestMetricsRequest → mevcut ProcessAsync

### 4.4 Geriye Uyumluluk

- Header yoksa veya `plain` ise: mevcut JSON body işlenir
- Bu sayede eski Engine sürümleri plain JSON göndermeye devam edebilir

---

## 5. Uygulama Sırası

1. **Reactor: Engine Status endpoint** – POST /api/v1/engine/status, mon_engines güncelleme
2. **Engine: Hata buffer + CollectorJob entegrasyonu** – Hataları buffer'a ekle
3. **Engine: EngineStatusJob** – Periyodik status gönderimi
4. **Reactor: mon_engines.lastErrors** – Schema/field ekleme (DataGateway). `setup-monitoring-datasets.ps1` yeni kurulumlarda `lastErrors` alanını ekler. Mevcut ortamlarda DG üzerinden dataset güncellemesi veya manuel alan ekleme gerekebilir.
5. **Ingest: GZip + AES** – Engine IngestClient, Reactor IngestController decrypt pipeline
6. **MngUI: lastErrors gösterimi** – Engine/asset detay sayfalarında (sonraki faz)

---

## 6. Referanslar

- [MONITORING_ENGINE_ARCHITECTURE](../../../monitoring_plans/MONITORING_ENGINE_ARCHITECTURE.md)
- [MngEngine ROADMAP](../main/ROADMAP.md)
- Config string şifreleme: ConfigStringProcessing, ConfigService (CompressPbk, CompressPrk)
