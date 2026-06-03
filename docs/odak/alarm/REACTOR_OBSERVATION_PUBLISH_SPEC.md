# MngReactor → `monitra.observations` Native Publish Sözleşmesi

**Amaç:** Geçici `MetricObservationBridgeConsumer` (MngAlarm Worker) kapatılabilsin; metrik ingest doğrudan alarm motorunun dinlediği exchange'e publish edilsin.

**Durum:** ✅ **C6 tamam (3 Haz 2026).** Odak: bridge kapalı (`ReactorBridge__Enabled=false`); MngReactor native publish açık. E2E: `test-reactor-observation-e2e.ps1`, `test-observation-native-e2e.ps1` PASS.

---

## Exchange / routing

| Alan | Değer |
|------|--------|
| Exchange | `monitra.observations` (topic, durable) |
| Routing key | `{domainId}.metric.{collectibleCode}` |
| Örnek | `6a0f8fc43d6ba5d774ee37c1.metric.cpu_usage` |

Bridge ile aynı desen: `MetricObservationMapper.BuildRoutingKey` (`MngAlarm/Core/MngAlarm.Application/Observations/MetricObservationMapper.cs`).

---

## Payload (flat — tercih edilen)

Mevcut `monitoring.metric.inserted.#` ingest DTO'su ile uyumlu:

```json
{
  "domainName": "odak",
  "domainId": "6a0f8fc43d6ba5d774ee37c1",
  "collectibleCode": "cpu_usage",
  "value": 95.5,
  "timestamp": "2026-06-03T10:00:00Z",
  "assetId": "asset-1",
  "engineId": "engine-1"
}
```

**Zorunlu alanlar:** `domainName` (veya `domain`), `collectibleCode`, `value` (number veya sayısal string).

**Opsiyonel:** `domainId`, `timestamp` (ISO-8601 veya epoch ms), `assetId`, `itemId`, `agentId`, `engineId`, `unit`.

---

## Payload (nested meta — mon_metrics uyumu)

```json
{
  "domainName": "odak",
  "value": 95.5,
  "timestamp": "2026-06-03T10:00:00Z",
  "meta": {
    "domain": "odak",
    "collectibleCode": "cpu_usage",
    "assetId": "asset-1",
    "engineId": "engine-1"
  }
}
```

Mapper her iki şekli de destekler — bkz. `MngAlarm.Tests/MetricObservationMapperTests`.

---

## MngReactor implementasyon checklist

> **Detaylı handoff:** [REACTOR_NATIVE_PUBLISH_HANDOFF.md](./REACTOR_NATIVE_PUBLISH_HANDOFF.md) (Faz R1–R5, DoD, rollback)

1. Metric persist sonrası (veya ingest pipeline çıkışında) `monitra.observations` exchange declare.
2. Her metrik için yukarıdaki zarfı publish et (`persistent=true`, `contentType=application/json`).
3. `domainId` JWT/tenant context'ten; yoksa `domainName` fallback (Alarm tarafı aynı).
4. Feature flag: `ObservationPublish__Enabled` — Odak'ta bridge ile kademeli geçiş.
5. Bridge kapatma: MngAlarm `ReactorBridge__Enabled=false` + Reactor native açık → E2E yeşil.

---

## Doğrulama

| Script | Ne test eder |
|--------|----------------|
| `scripts/odak/test-metric-bridge-e2e.ps1` | Metrik → observation → (opsiyonel) alarm |
| `scripts/odak/test-observation-native-e2e.ps1` | **Native:** flat DTO → `monitra.observations` (bridge bypass simülasyonu) |
| `scripts/odak/test-alarm-lifecycle-e2e.ps1` | Observation ingest → alarm lifecycle → workflow |

Native publish sonrası bridge kapalıyken aynı scriptler geçmeli.

**Odak E2E (3 Haz 2026):** `test-observation-native-e2e.ps1` PASS — flat DTO → `monitra.observations` → alarm updated.

**Not:** E2E scriptleri RabbitMQ parolasını sunucu `mng_apps/.env` (`RABBITMQ_PASSWORD`) üzerinden okur — `Invoke-OdakRabbitMqPublish` (`OdakSshCommon.ps1`).

---

## İlgili kod (MonitraNG)

- Bridge: `MngAlarm.Worker` → `MetricObservationBridgeConsumer`
- Mapper: `MngAlarm.Application/Observations/MetricObservationMapper.cs`
- Alarm ingest: `ObservationProcessor` → `mng.alarms`
