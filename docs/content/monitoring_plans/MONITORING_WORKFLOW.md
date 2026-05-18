# MngWorkflow Planı

Bu doküman, **MngWorkflow** uygulamasının planını tanımlar. Workflow, belirli koşullara göre aksiyon tetikleyen bir sistemdir. İlk sürümde **monitoring** senaryoları hedeflenir; ileride diğer alanlara genişletilebilir.

Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## 1. Amaç

- **Koşul–Aksiyon:** Belirli asset(ler)den gelen metrik değeri koşula uyduğunda (aralık dışı, threshold üstü/altı) aksiyon çalıştırma
- **Aksiyonlar:** Bildirim, HTTP endpoint, mail, UI uyarısı
- **Genişletilebilirlik:** İleride monitoring dışı dataset'ler için de kullanılabilir

---

## 2. Yaklaşım: IFTTT (If This Then That)

MngWorkflow, **IFTTT** paradigmasına uyumludur: *"Eğer X olursa Y yap"*.

| Parça | MngWorkflow karşılığı |
|-------|------------------------|
| **IF (This)** | Koşul: metrik değeri belirlenen kritere uyduğunda (örn. CPU > 90, bellek aralık dışı) |
| **THEN (That)** | Aksiyon: bildirim, HTTP çağrısı, e-posta, UI uyarısı vb. |

Bu model, kullanıcıların akıllı servis bağlantılarında alışık olduğu basit ve anlaşılır mantığı korur; Workflow tanımları da bu yapıya göre modellenir.

---

## 3. Veri Akışı

```
[Engine] → Ingest → [Reactor] → MongoDB (mon_metrics)
                        │
                        └──→ RabbitMQ (paralel publish)
                                       │
                                       v
                                [MngWorkflow] → Queue'dan consume
                                             → Koşul kontrolü
                                             → Eşleşen workflow → Aksiyon
```

- **Reactor:** Her metrik MongoDB'ye yazılırken **paralel** olarak RabbitMQ'ya da publish eder.
- **Workflow:** Queue'yu dinler; mesajı okur, işler, ACK eder — mesaj kuyruktan kaldırılır. Okunana kadar kuyrukta kalır.

---

## 4. RabbitMQ Yapısı

**DG publish mode benzeri** — MngDataGateway'in dataset `publish_mode: true` olduğunda kullandığı yapıya benzer.

| Öğe | Açıklama |
|-----|----------|
| **Exchange** | Topic exchange. Örn. `monitra.monitoring.events` veya mevcut `mng.topics` ile uyumlu |
| **Routing key** | Örn. `monitoring.metric.inserted.{domain}` — domain bazlı filtreleme |
| **Queue** | Workflow kendi queue'sunu oluşturur, exchange'e bind eder |
| **Mesaj davranışı** | Mesaj Workflow tarafından okunup ACK edilene kadar kuyrukta kalır. ACK sonrası kaldırılır |
| **Durable** | Evet — exchange ve queue kalıcı |

**Mesaj formatı:** Metrik dokümanı — `domain`, `assetId`, `itemId`, `agentId`, `engineId`, `collectibleCode`, `value`, `unit`, `timestamp` (mon_metrics ile uyumlu).

---

## 5. Workflow Tanımı (Dataset)

UI'da uygun bir dataset içinde workflow tanımları tutulur. Örn. `mon_workflows`.

| Alan | Açıklama |
|------|----------|
| name | Workflow adı |
| description | Opsiyonel açıklama |
| scope | `asset` \| `assets` \| `all` — tek asset, seçili asset listesi, tüm asset'ler |
| assetIds | scope=assets ise asset __dataId listesi |
| collectibleCode | Hangi metrik (örn. cpu_usage, memory_used) |
| condition | Koşul tanımı (threshold, aralık, AND/OR) |
| actions | Tetiklenecek aksiyon listesi |
| enabled | Aktif/pasif |
| cooldownMinutes | Aynı koşul için tekrar tetikleme bekleme süresi (dakika) |

---

## 6. Koşul Yapısı

| Bileşen | Açıklama |
|---------|----------|
| **Operatörler** | `gt`, `lt`, `gte`, `lte`, `between`, `outside`, `eq` |
| **AND/OR** | Birden fazla koşul birleştirilebilir |
| **JS validasyon (ileride)** | DG dataset field validasyonlarına benzer; `value`, `assetId` vb. ile script. Sandbox gerekir. |

**Örnek koşul:**
```json
{
  "type": "or",
  "conditions": [
    { "operator": "gt", "value": 90 },
    { "operator": "lt", "value": 5 }
  ]
}
```
→ Değer 90'dan büyük VEYA 5'ten küçükse tetikle.

---

## 7. Aksiyon Tipleri

| Aksiyon | Hedef |
|---------|-------|
| **notification** | MngNotifier — bildirim gönder |
| **http** | Webhook — belirtilen URL'ye POST |
| **email** | SMTP — e-posta gönder |
| **ui_alert** | MngHub push veya alert tablosu — UI'da uyarı göster |

Aksiyonlar önceden tanımlanabilir (reusable action config); workflow tanımında aksiyon ID veya inline config referans edilir.

---

## 8. MngWorkflow Backend

- **Ayrı backend** — .NET 9, standalone uygulama
- **Cache:** Workflow tanımlarını DG dataset'ten okur; kendi cache'ini güncel tutar (dataset değişince veya periyodik refresh)
- **Queue consumer:** RabbitMQ'dan mesaj alır, koşul kontrolü yapar, eşleşen workflow varsa aksiyon çalıştırır
- **Bağımlılık:** RabbitMQ, MngDataGateway (workflow tanımları), MngNotifier/HTTP/SMTP (aksiyonlar)

---

## 9. Reactor Değişikliği

Reactor, ingest sırasında her metrik MongoDB'ye yazıldıktan sonra **paralel** olarak RabbitMQ'ya publish edecek. Bu, mevcut ingest akışına ek bir adım.

---

## 10. Öncelik Sırası

1. **Reactor:** Ingest + RabbitMQ publish
2. **Workflow backend:** Queue consumer, basit koşul motoru, cache
3. **Dataset:** mon_workflows şeması, UI
4. **Aksiyonlar:** Notification, HTTP önce; Email, UI alert sonra
5. **İleride:** JS validasyon, AND/OR karmaşık koşullar

---

## 11. Açık Kararlar

1. **Exchange/routing key:** Kesin isimlendirme — `monitra.monitoring.events` mi, `mng.topics` + `monitoring.metric.#` mi?
2. **Cache güncelleme:** DG webhook mu, periyodik refresh mi, UI kayıt sonrası API mi?
3. **Aksiyon tanımları:** Ayrı dataset (mon_workflow_actions) mi, workflow içinde inline mı?

---

## 12. İlerideki Plan: Node-RED Tarzı Görsel Workflow

İleride, IFTTT tarzı basit koşul–aksiyon modelinin ötesinde **Node-RED benzeri, kendimize özel bir görsel workflow** desteği düşünülebilir:

- **Görsel düzenleyici:** Düğümler (node) ve bağlantılar ile akış tasarlama
- **Özel bloklar:** Metrik okuma, koşul, dönüşüm, aksiyon gibi MonitraNG’e özgü bileşenler
- **Bambaşka bir iş:** Ayrı UI, çalışma zamanı motoru ve sürüm yönetimi gerektirir

Bu, şu anki planın kapsamı dışındadır; gelecekte değerlendirilebilecek bir hedef olarak kayıt altına alınır.

---

## 13. Referanslar

- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md)
- [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md)
