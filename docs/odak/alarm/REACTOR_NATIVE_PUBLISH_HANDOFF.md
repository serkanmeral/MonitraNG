# MngReactor — Native Observation Publish Handoff

**Hedef repo:** MngReactor (MonitraNG dışı)  
**MonitraNG tüketici:** hazır ✅ (3 Haz 2026)  
**Sözleşme:** [REACTOR_OBSERVATION_PUBLISH_SPEC.md](./REACTOR_OBSERVATION_PUBLISH_SPEC.md)

Bu doküman implementasyon ekibi için **sıralı, işaretlenebilir** checklist'tir.

---

## Özet

Metrik ingest başarılı olduktan sonra, mevcut `mng.topics` / `monitoring.metric.inserted.{domain}` publish'e **ek olarak** (şimdilik kaldırmadan) `monitra.observations` exchange'ine native publish eklenecek. MngAlarm Worker'daki geçici bridge (`MetricObservationBridgeConsumer`) kapatılabilecek.

```text
[IngestProcessing] metric persist OK
    ├─ (mevcut) IMetricPublisher → mng.topics / monitoring.metric.inserted.{domain}
    └─ (yeni)   IObservationPublisher → monitra.observations / {domainId}.metric.{collectibleCode}
                      ↓
              alarm.observation.inbound → MngAlarm ObservationConsumer
```

---

## MonitraNG tarafı (referans — değiştirmeyin)

| Bileşen | Dosya |
|---------|--------|
| Flat DTO mapper | `MngAlarm/Core/MngAlarm.Application/Observations/MetricObservationMapper.cs` |
| Queue consumer | `MngAlarm/Infrastructure/.../Messaging/ObservationConsumer.cs` |
| Bridge (kapatılacak) | `MetricObservationBridgeConsumer.cs` |
| Routing pattern (queue bind) | `*.metric.*` → `alarm.observation.inbound` |
| Bridge env | `MngAlarmSettings__Engine__ReactorBridge__Enabled` |

**Mapper'ın kabul ettiği minimum JSON:**

```json
{
  "domainName": "odak",
  "domainId": "6a0f8fc43d6ba5d774ee37c1",
  "collectibleCode": "cpu_usage",
  "value": 95.5
}
```

`domainId` yoksa `domainName` kullanılır (Odak E2E'de ikisi de `"odak"`).

---

## Faz R1 — Publisher altyapısı (MngReactor)

| # | Görev | Kabul |
|---|-------|-------|
| R1.1 | `IObservationPublisher` interface: `PublishAsync(domainId, domainName, collectibleCode, value, dimensions?, timestamp?, ct)` | Derleme |
| R1.2 | RabbitMQ impl: exchange `monitra.observations`, type **topic**, **durable** | Exchange declare idempotent |
| R1.3 | Routing key: `{domainId}.metric.{collectibleCode}` | `MetricObservationMapper.BuildRoutingKey` ile aynı |
| R1.4 | Mesaj: **flat JSON** (nested `meta` opsiyonel — spec §Payload) | `contentType=application/json`, `deliveryMode=persistent` |
| R1.5 | Config: `MngReactorSettings:ObservationPublish:Enabled` (default `false`) | appsettings + env override |
| R1.6 | Config: mevcut RabbitMQ connection ayarlarını yeniden kullan | Ayrı broker yok |

**Önerilen dosya konumları (MngReactor repo):**

```text
Core/.../Observations/IObservationPublisher.cs
Infrastructure/.../Messaging/ObservationPublisher.cs
Infrastructure/.../Messaging/ObservationTopologyBootstrap.cs   // exchange declare
```

---

## Faz R2 — Ingest hook

| # | Görev | Kabul |
|---|-------|-------|
| R2.1 | `IngestProcessing` (veya metric persist çıkışı): her **başarılı** metrik yazımından sonra publisher çağrısı | Flag kapalıyken sıfır publish |
| R2.2 | `domainId` tenant context'ten; yoksa `domainName` | Alarm ile uyumlu fallback |
| R2.3 | Dimensions: `assetId`, `itemId`, `agentId`, `engineId`, `unit` — ingest DTO'da varsa flat root'a yaz | Mapper dimensions'a kopyalar |
| R2.4 | `timestamp`: ingest zamanı veya metrik zaman damgası (ISO-8601 UTC) | Yoksa consumer `UtcNow` kullanır |
| R2.5 | **`mng.topics` metrik publish'e dokunma** | MngHub / mevcut tüketiciler bozulmaz (ROADMAP_TODAY §2) |

---

## Faz R3 — Unit test (MngReactor)

| # | Test | Beklenen |
|---|------|----------|
| R3.1 | Flat payload serialize | Zorunlu alanlar: domainName, collectibleCode, value |
| R3.2 | Routing key üretimi | `abc.metric.cpu_usage` |
| R3.3 | Flag kapalı | Publisher no-op |
| R3.4 | Nested meta payload (opsiyonel) | `meta.collectibleCode` + root `value` |

MonitraNG referans testleri: `MngAlarm.Tests/MetricObservationMapperTests.cs`, `ObservationIngressParserTests.cs`

---

## Faz R4 — Odak kademeli geçiş

### Adım 1: Dual-run (bridge açık + native açık)

```yaml
# mng_apps compose — mngalarm-worker
MngAlarmSettings__Engine__ReactorBridge__Enabled=true

# mngreactor (yeni)
MngReactorSettings__ObservationPublish__Enabled=true
```

**Risk:** Aynı metrik iki kez işlenebilir (bridge + native). Geçiş penceresi kısa tutulmalı veya bridge geçici kapatılıp sadece native E2E ile doğrulanmalı.

**Önerilen sıra:**

1. Native **kapalı** → `test-observation-native-e2e.ps1` PASS (simülasyon — zaten yeşil)
2. Reactor deploy, native **açık**, bridge **açık** → metrik ingest smoke (duplicate alarm olabilir — kısa test)
3. Bridge **kapat** → aşağıdaki E2E

### Adım 2: Bridge kapalı — asıl kabul

```yaml
MngAlarmSettings__Engine__ReactorBridge__Enabled=false
MngReactorSettings__ObservationPublish__Enabled=true
```

**Odak deploy (MonitraNG):**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -Command @"
Set-Location 'C:\...\MonitraNG'
& .\scripts\odak\sync-odak-source.ps1 -Paths @('MngAlarm','ApplicationResources/mng_apps')
& .\scripts\odak\deploy-odak-apps.ps1 -Services mngalarm-worker -NoCache
"@
```

Reactor deploy ayrı pipeline (MngReactor repo).

---

## Faz R5 — Odak E2E (MonitraNG scriptleri)

| Script | Bridge kapalı + native açık |
|--------|----------------------------|
| `scripts/odak/test-observation-native-e2e.ps1` | Simülasyon — her zaman geçmeli |
| Gerçek ingest E2E (Reactor repo'ya eklenecek) | Engine → Reactor ingest → alarm raised/updated |
| `scripts/odak/test-alarm-lifecycle-e2e.ps1` | Dev ingest yolu — regresyon |

**Native simülasyon (MonitraNG):** RabbitMQ'ya flat DTO publish — bridge bypass. Parola: sunucu `mng_apps/.env` → `Invoke-OdakRabbitMqPublish`.

**Gerçek Reactor E2E:** `scripts/odak/test-reactor-observation-e2e.ps1` (Reactor stub ise SKIP; `-FailIfSkipped` ile zorunlu kılınabilir)

```text
1) cpu_usage threshold rule (gateway API)
2) Reactor ingest API — tek metrik batch, value=97
3) 10 sn bekle
4) dev ingest follow-up veya GET alarms — raised/updated ≥ 1
5) mngalarm-worker log: "Observation consumer" — bridge log YOK
```

---

## Rollback

| Durum | Aksiyon |
|-------|---------|
| Native publish hatalı | `ObservationPublish__Enabled=false` |
| Alarm tüketmiyor | `ReactorBridge__Enabled=true` (MonitraNG worker) |
| Duplicate alarm | Bridge kapat, native düzelt |

---

## Definition of Done

- [x] R1–R3 MngReactor PR merge
- [x] Odak: `ReactorBridge__Enabled=false`, native `Enabled=true`
- [x] `test-observation-native-e2e.ps1` PASS
- [x] Reactor ingest → alarm lifecycle (raised veya updated) PASS — `test-reactor-observation-e2e.ps1`
- [x] `mngalarm-worker` log: bridge disabled, observation consumer active
- [x] MonitraNG `docs/odak/alarm/DEVAM.md` güncelle (bridge kapatıldı notu)

---

## İlgili dokümanlar

- [REACTOR_OBSERVATION_PUBLISH_SPEC.md](./REACTOR_OBSERVATION_PUBLISH_SPEC.md)
- [DEVAM.md](./DEVAM.md)
- [MONITORING_REACTOR_ARCHITECTURE.md](../../content/monitoring_plans/MONITORING_REACTOR_ARCHITECTURE.md)
