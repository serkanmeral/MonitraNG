# MonitraNG Alarm & Rule Engine — Plan v1

## Doküman Durumu

* Durum: Planlama (taslak)
* Versiyon: 1.0
* Kapsam: Platform geneli tespit / alarm üretim katmanı
* Konumlandırma: Major Roadmap **§4.2 Alarm & Rule Engine**'in somutlaştırılması
* Bağımlılık / ilişki:

  * `docs/odak/operationcore/major_plan.md` §4.2, §4.4, §5.3, §8 (vizyon)
  * `docs/content/monitoring_plans/MONITORING_DATA_PRODUCTION.md` (mon_metrics modeli)
  * `docs/content/monitoring_plans/MONITORING_WORKFLOW.md` (IFTTT — superseded; §13)
  * `docs/odak/monitoring/SIEM_PLANNING.md` §7, §12.1 (SIEM korelasyon = bu motorun bir kural ailesi)
  * `docs/odak/workflow/Workflow Backend Implementation Plan v1.md` §12 (alarm → workflow seam)

---

# 1. Amaç ve Sınır

Alarm & Rule Engine, MonitraNG'nin **tespit/algılama** katmanıdır. Girdi olarak metrik/olay/sinyal akışlarını tüketir, kuralları değerlendirir ve **alarm üretir**.

**Net sınır (workflow `planing.md` §2 ile uyumlu):**

> Alarm üretimi ve doğrulama bu motorun sorumluluğundadır. Workflow Engine alarm üretmez; yalnızca üretilen alarmlara **tepki verir** (onay, aksiyon, remediation).

Bu motor **aksiyon almaz** (firewall blok, mail, vb. workflow'un işi). Yalnızca "ne oldu, anlamlı mı?" sorusunu yanıtlar ve sonucu alarm olarak yayınlar.

**SIEM ile ilişki:** SIEM korelasyonu (brute-force, port-scan) bu motorun **bir kural ailesidir**, ayrı bir ürün değil. Aynı motor IT metrik eşikleri, endüstriyel anomaliler ve AI sinyallerini de işler.

---

# 2. Platform Konumu — Katmanlı Mimari

```text
[MngEngine]  toplama (SSH/WMI/SNMP/HTTP/syslog/OPC-UA/MQTT...)
     ↓
[MngReactor] normalize → kalıcılık + paralel RabbitMQ publish
     ├──→ mon_metrics  (MongoDB Time Series, sayısal telemetri)
     └──→ sec_events   (güvenlik/log olayları)
     ↓ (RabbitMQ stream)
╔══════════════════════════════════════════════════════════╗
║  KATMAN 1 — ALARM & RULE ENGINE  (bu doküman)              ║
║  • threshold / composite / stateful                        ║
║  • correlation (window + sequence + cooldown)              ║
║  • scheduled validation                                    ║
║  • anomaly / predictive (AI skorlarını sinyal olarak alır) ║
║  → ALARM üretir (raise / update / resolve)                 ║
╚══════════════════════════════════════════════════════════╝
   ↑ besleme                            ↓ alarm event (RabbitMQ)
[AI Scorer servisleri]        ╔════════════════════════════════╗
 anomaly detection,           ║  KATMAN 2 — WORKFLOW ENGINE     ║
 predictive maintenance  →    ║  (orkestrasyon)                 ║
 (offline/on-prem;            ║  Event Trigger (alarm) →        ║
  çıktı = skor/sinyal event)  ║  Approval / Action / Delay /    ║
                              ║  auto-remediation / WorkItem    ║
                              ╚════════════════════════════════╝
```

Üç temiz seam, hepsi RabbitMQ üzerinden gevşek bağlı:
1. **Reactor → normalize akış** (metrik/olay stream)
2. **Alarm Engine → alarm event** (Workflow tüketir)
3. **Workflow → aksiyon** (modüllere)

---

# 3. Girdi Akışları — Birleşik Gözlem (Observation)

Motor heterojen kaynakları tek bir mantıksal "observation" soyutlamasıyla ele alır:

| Kaynak | Tip | Kaynak koleksiyon / event | Örnek |
|---|---|---|---|
| Metrik | sayısal/zaman serisi | `mon_metrics` insert event | cpu_usage=95 |
| Güvenlik/log olayı | yapılandırılmış olay | `sec_events` insert event | login_failed |
| AI sinyali | skor/tahmin | scorer servis event | anomalyScore=0.97 |
| Harici event | platform event | mevcut exchange'ler | DocumentUploaded |

**Birleşik observation alanları (mantıksal):** `domain`, `timestamp`, `kind` (metric/event/signal), `key` (collectibleCode / event.action / signal adı), `value`, `dimensions` (assetId/itemId/srcIp/actor...), `raw`.

> Reactor publish'i (MONITORING_WORKFLOW.md §9): her metrik yazılırken RabbitMQ'ya da basılır. Bu motor o akışı tüketir.

---

# 4. Alarm Tipleri (Major §4.2)

| Tip | Açıklama | State gerektirir mi |
|---|---|---|
| **Threshold** | Tek gözlem eşiği (cpu_usage > 90) | Hayır (anlık) |
| **Composite** | Birden fazla koşulun AND/OR'u | Hayır (anlık) |
| **Stateful** | "N süre boyunca eşik üstünde kalırsa" (sürekli ihlal) | Evet |
| **Correlation** | Pencere içi sayım / sequence / gruplama (5 dk'da 10 başarısız login) | Evet |
| **Scheduled validation** | Periyodik kontrol (cron) — "her gün X dokümanı kontrol et" | Kısmi |
| **Anomaly (AI)** | AI skoru bir eşiği aşınca | Hayır (skor event'i tetikler) |
| **Predictive (AI)** | Arıza tahmini sinyali eşiği aşınca | Hayır |

**Önemli:** Threshold/Composite/Anomaly/Predictive **anlık** (stateless) değerlendirilir. Stateful/Correlation **kayan pencere + state** gerektirir — runtime'ın asıl zorluğu burası (§6).

---

# 5. Kural Modeli (`mon_alarm_rules`)

SIEM `sec_rules` modeli (SIEM_PLANNING.md §7) platform geneline genelleştirilir.

| Alan | Açıklama |
|---|---|
| `name` / `description` | Kural kimliği |
| `enabled` | Aktif/pasif |
| `type` | threshold / composite / stateful / correlation / scheduled / anomaly / predictive |
| `severity` | Üretilecek alarm ciddiyeti (0–10) |
| `scope` | Hedef: asset / assets / assetType / item / all / source.type |
| `match` | Gözlem filtresi (kind, key, dimensions, event.action...) |
| `expression` | (opsiyonel) **Jint** ifadesi — workflow ile **ortak motor** |
| `groupBy` | Pencere içi gruplama anahtarı (`actor.user`, `network.srcIp`, `assetId`) |
| `window` | Zaman penceresi (ör. `5m`) — stateful/correlation için |
| `threshold` | Pencere içi / anlık eşik |
| `sequence` | (opsiyonel) sıralı olay zinciri (failure* → success) |
| `for` | Stateful: "şu süre boyunca koşul sürerse" |
| `cooldownMinutes` | Aynı grup için tekrar tetikleme bekleme |
| `dependencies` | (opsiyonel) bağımlı asset/koşul (gürültü azaltma) |
| `dedupKey` | Alarm tekilleştirme anahtarı şablonu |

**Örnek — IT stateful (CPU):**
```json
{
  "name": "CPU sürekli yüksek",
  "type": "stateful",
  "match": { "kind": "metric", "key": "cpu_usage" },
  "scope": "assetType",
  "expression": "value > 90",
  "for": "10m",
  "severity": 6,
  "cooldownMinutes": 30
}
```

**Örnek — SIEM correlation (brute force):**
```json
{
  "name": "Brute force - başarısız login",
  "type": "correlation",
  "match": { "kind": "event", "event.category": "authentication", "event.outcome": "failure" },
  "groupBy": ["actor.user", "network.srcIp"],
  "window": "5m",
  "threshold": 10,
  "severity": 7,
  "cooldownMinutes": 15
}
```

**Örnek — anomaly (AI sinyali):**
```json
{
  "name": "Davranışsal anomali",
  "type": "anomaly",
  "match": { "kind": "signal", "key": "anomaly_score", "scope": "assetId" },
  "expression": "value > 0.95",
  "severity": 5
}
```

`mon_alarm_rules` MngDataGateway üzerinden (düşük frekanslı tanım verisi) tutulur; motor cache'ler.

---

# 6. Stateful / Streaming Runtime (Asıl Farklılaşma)

Bu motor, Workflow Engine'in **per-instance stateless** modelinden farklı olarak **stream + state** modelidir. Tasarım:

* **Pencere/state:** Her (kural, groupBy anahtarı) için bellekte kayan-pencere sayaç/durum tutulur; periyodik **checkpoint** ile Mongo'ya yazılır (restart kurtarma).
* **State locality için partitioning:** Belirli bir groupBy anahtarı her zaman aynı worker'a düşmeli → RabbitMQ **consistent-hash exchange** veya `(domain, groupByHash)` ile partition'lı tüketim. (Workflow'daki "herhangi bir worker" modelinden farklı.)
* **Pencere tipi:** Sliding (correlation) ve "for/sürekli" (stateful). Tumbling opsiyonel.
* **Geç gelen veri:** `@timestamp` (olay anı) vs `ingestedAt` (varış) ayrımı; pencere değerlendirmesi event-time bazlı, sınırlı tolerans.
* **Cooldown / flapping:** Aynı dedupKey için cooldown; resolve/raise titremesini (flap) bastırma.
* **Yüksek hacim:** Firewall gibi yoğun kaynaklarda MongoDB yeterli olmayabilir → arama/stream-optimize store (ör. OpenSearch) değerlendirmesi açık karar (SIEM §12.4 ile aynı).

> Threshold/anomaly gibi anlık kurallar state tutmaz; doğrudan değerlendirilip alarm üretir. State maliyeti yalnızca stateful/correlation için ödenir.

---

# 7. Alarm Modeli (`mon_alarms`)

Üretilen alarm, yaşam döngüsü olan bir varlıktır (sadece event değil).

| Alan | Açıklama |
|---|---|
| `__dataId` | Alarm kimliği |
| `domainId` | Tenant |
| `ruleId` | Tetikleyen kural |
| `dedupKey` | Tekilleştirme (aynı sorun tek alarm) |
| `severity` | Ciddiyet |
| `status` | `active` / `acknowledged` / `resolved` / `suppressed` |
| `firstSeenAt` / `lastSeenAt` | İlk ve son görülme |
| `count` | Tekrar sayısı (dedup altında) |
| `context` | Tetikleyen gözlem(ler), groupBy değerleri, asset/item referansları |
| `correlationId` | İzleme / workflow korelasyonu |
| `acknowledgedBy` / `resolvedAt` | Yaşam döngüsü meta |

**Yaşam döngüsü:** `raise` (yeni) → `update` (dedup altında tekrar) → `resolve` (koşul düştü / TTL) / `acknowledge` (operatör) / `suppress` (bakım penceresi).

---

# 8. Alarm Event Yayını → Workflow Seam

Alarm yaşam döngüsü olayları RabbitMQ'ya basılır; Workflow Engine Event Trigger ile tüketir (Workflow Plan §11.2, §12).

* **Exchange:** `mng.alarms` (topic, durable)
* **Routing key:** `{domainId}.alarm.{raised|updated|resolved}.{severity}`
* **Event şeması:** `{ domainId, eventType: "AlarmRaised", alarmId, ruleId, severity, dedupKey, context, correlationId }`

Workflow tarafında bu, `eventType = AlarmRaised` trigger'ına bağlanır; `filterExpression` (Jint) ile `severity`/`context` filtrelenir. **Ortak ifade dili** sayesinde kural `expression`'ları ile workflow `filterExpression`'ları aynı motoru kullanır.

---

# 9. AI Entegrasyonu (Scorer Modeli)

AI (anomaly detection, predictive maintenance — major §5.3, §8) **motorun içinde değil, ayrı skorlayıcı servis(ler)dedir** (offline/on-prem, major §3.4).

```text
mon_metrics akışı → [AI Scorer] → skor/sinyal event (kind=signal)
                                        ↓
                            Alarm Engine (anomaly/predictive kuralı)
                                        ↓
                                     ALARM
```

* Scorer çıktısı bir **observation (signal)** olarak akışa girer; normal bir kural (`type: anomaly`) bunu eşiğe vurup alarma çevirir.
* Böylece AI, mimariyi karmaşıklaştırmadan **tek besleme noktasından** girer; model değişse bile motor değişmez.
* Offline çalışma: model servisi kurum içinde; internet bağımlılığı yok.

---

# 10. Depolama & Dataset Özeti

| Veri | Konum | Erişim |
|---|---|---|
| `mon_metrics` | MongoDB Time Series, `mng_{domain}` | Reactor doğrudan yazar; motor okur/stream tüketir |
| `sec_events` | MongoDB, `mng_{domain}` | Reactor yazar; motor stream tüketir |
| `mon_alarm_rules` | DG dataset | Motor cache'ler (düşük frekans) |
| `mon_alarms` | DG dataset veya doğrudan Mongo | Motor yazar (yaşam döngüsü); UI okur |
| Stateful checkpoint | Mongo (motor DB) | Motor yazar/restore eder |

---

# 11. Çapraz Kesen Konular

* **Multi-tenancy:** Tüm kural/alarm/state `domainId` ile izole; routing key'lerde `{domainId}` (platform deseni).
* **Suppression / bakım penceresi:** Asset bakımdayken alarm bastırma (status `suppressed`).
* **Dependency bazlı gürültü azaltma:** Üst bağımlılık (switch down) varken alt alarmları bastırma.
* **Dedup & cooldown:** dedupKey + cooldownMinutes ile alarm fırtınası önleme.
* **Audit & correlation:** Her alarm correlationId taşır; workflow ve loglarla ilişkilendirilir.
* **Güvenlik:** Kural ifadeleri Jint sandbox (timeout + limit); hassas alanlar maskeleme.

---

# 12. Servis Sınırı

* **Karar önerisi:** Ayrı stateful servis — çalışma adı **`MngAlarm`** (veya `MngCorrelator`). Workflow ve Reactor'dan **ayrı** ölçeklenir; state locality için partition'lı.
* **Neden Reactor'a gömülmesin:** Reactor ingest hızı kritik; stateful pencere yönetimi onu yavaşlatır ve ölçek profili farklı.
* **Neden Workflow'a gömülmesin:** Workflow per-instance/stateless; stream+state ona ait değil (Workflow Plan §12.1).
* Teknoloji standartları (mimari §8 ile uyum): .NET 9, Onion, MediatR, Serilog, Asp.Versioning, HealthController, RabbitMQ.

---

# 13. IFTTT (MONITORING_WORKFLOW.md) Supersession

Mevcut IFTTT planı (tek-metrik threshold → aksiyon) iki parçaya bölünür:

| IFTTT parçası | Yeni yeri |
|---|---|
| Koşul (threshold/AND-OR, cooldown) | **Alarm Engine** (threshold/composite kuralı) |
| Aksiyon (notification/http/email/ui) | **Workflow Engine** (action node) |

`MONITORING_WORKFLOW.md` "superseded" olarak işaretlenir; §12'deki "Node-RED tarzı görsel workflow" hedefi = `docs/odak/workflow` engine'i ile karşılanır.

---

# 14. Fazlar (Monitoring & SIEM yol haritalarıyla hizalı)

| Faz | Odak | Çıktı |
|---|---|---|
| **0** | Bu doküman + birleşik observation soyutlaması + kural/alarm modeli | Plan, şema kararları |
| **1** | Anlık kurallar: threshold / composite + alarm yaşam döngüsü + alarm event yayını | mon_metrics threshold → alarm → workflow uçtan uca |
| **2** | Stateful + correlation runtime (window/sequence/cooldown, partitioning, checkpoint) | SIEM U1/U2/U4 + IT stateful alarmları |
| **3** | Scheduled validation + suppression/dependency + dedup gelişmiş | Gürültü kontrollü üretim |
| **4** | AI scorer entegrasyonu (anomaly/predictive sinyal kuralları) | Anomaly/predictive alarmları |

---

# 15. Açık Kararlar

1. **Servis adı:** `MngAlarm` mı `MngCorrelator` mı? (öneri: `MngAlarm` — kapsam SIEM'den geniş)
2. **State store:** Bellek + Mongo checkpoint yeterli mi, yoksa yüksek hacim için stream/arama-optimize store (OpenSearch)? (SIEM §12.4 ile birleşik karar)
3. **`mon_alarms` erişimi:** DG dataset mi, doğrudan Mongo mu? (UI okuması DG; yazma frekansı düşük → DG olabilir)
4. **Reactor publish kapsamı:** Sadece metrik mi, yoksa sec_events ve signal'lar da aynı publish deseniyle mi? (birleşik observation için tutarlılık gerekir)
5. **Scheduled validation:** MngScheduler'a mı yaslanacak (workflow ile aynı desen)?
6. **Partitioning mekanizması:** RabbitMQ consistent-hash exchange mi, uygulama seviyesinde mi?

---

# 16. Sonuç

Alarm & Rule Engine, Major Roadmap §4.2'nin somut karşılığıdır ve MonitraNG'nin **tespit katmanını** tek çatı altında toplar: IT metrikleri, SIEM korelasyonu, endüstriyel anomaliler ve AI sinyalleri aynı kural/alarm modelinde birleşir. Üretilen alarmlar Workflow Engine tarafından orkestre edilir. Bu ayrım (tespit ↔ orkestrasyon) hem `planing.md` §2 ile hem major plan ile tam uyumludur.
