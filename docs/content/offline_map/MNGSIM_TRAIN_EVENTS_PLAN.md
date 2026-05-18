# MngSim — Tren Event (MQTT) İlerleme Planı

> Polling sensörleri HTTP ile tamamlandı. Bu doküman **event sensörleri** (MQTT) için adımları tanımlar.

---

## 1) Hedef

- **Ne:** Tren bazlı anlık olaylar (yangın alarmı, hareket halinde kapı açıldı, acil fren, eşik aşım uyarıları) MQTT ile yayınlansın.
- **Kim dinler:** MngEngine, Workflow veya test için MngSim UI’da son event’ler listesi.
- **Spec referansı:** [MNGSIM_TRAIN_SIMULATION_SPEC.md](./MNGSIM_TRAIN_SIMULATION_SPEC.md) §7.2 (Event sensörleri).

---

## 2) Topic ve payload

| Öğe | Değer |
|-----|--------|
| Topic (tüm event’ler) | `mngsim/trains/events` |
| Topic (tren bazlı) | `mngsim/trains/{trainId}/events` |
| Payload | JSON; en az `trainId`, `eventType`, `timestamp` |

**Event türleri (eventType):**

| eventType | Açıklama | Ek alanlar (örnek) |
|-----------|----------|---------------------|
| `fire_alarm` | Yangın alarmı | zone, severity |
| `door_opened_while_moving` | Hareket halinde kapı açıldı | speedKmh, doorId |
| `emergency_brake` | Acil fren | — |
| `vibration_alert` | Titreşim eşiği aşıldı | vibrationMs2, threshold |
| `overheat_alert` | Aşırı ısınma | engineTempC, coolantTempC |
| `low_pressure_alert` | Düşük basınç (yağ/fren) | oilPressureBar veya brakePipePressureBar |

---

## 3) Uygulama fazları

### Faz 1 — Altyapı (önce bu)

1. **MQTT client (MngSim):**
   - NuGet: `MQTTnet` (veya benzeri).
   - Config: `TrainSim:MqttBrokerUrl` (veya mevcut `MqttBrokerUrl`). Boşsa event’ler devre dışı.
   - Servis: Broker’a bağlan, bağlıyken event publish et.

2. **Config:**
   - `appsettings` / `TrainSim`: `MqttBrokerUrl`, isteğe bağlı `EventsTopicPrefix` (varsayılan `mngsim/trains`).

3. **Publish helper:**
   - Tek bir metod: `PublishTrainEvent(trainId, eventType, payloadObject)` → JSON serileştir, `mngsim/trains/events` ve `mngsim/trains/{trainId}/events` topic’lerine gönder.

### Faz 2 — Event üretimi

4. **Eşik bazlı event’ler (otomatik):**
   - Konum/sensör döngüsünde (BuildSensors sonrası veya ayrı periyodik kontrol):
     - Motor sıcaklık > 95 °C → `overheat_alert` (bir kez, debounce).
     - Titreşim > 0.15 → `vibration_alert`.
     - Yağ basıncı < 2.5 bar → `low_pressure_alert`.
   - Debounce: Aynı tren + aynı eventType için son X saniyede gönderilmediyse gönder.

5. **Rastgele / senaryo event’leri (opsiyonel):**
   - Arka planda periyodik (örn. 60–120 sn): Rastgele bir yoldaki trene `fire_alarm` veya `door_opened_while_moving` gönder (test için).

### Faz 3 — Test ve tüketim

6. **Manuel tetikleme (API):**
   - `POST /api/trains/{trainId}/events` body: `{ "eventType": "fire_alarm", "zone": "engine", "severity": "high" }` → doğrudan MQTT’ye publish. UI’dan “Event tetikle” butonu ile test.

7. **MngSim UI’da event logu (opsiyonel):**
   - Aynı topic’e subscribe ol; son N event’i listele (Trenler veya ayrı “Event log” sayfası).

8. **MngEngine / Workflow:**
   - Bu topic’lere subscribe olup mesajı işleme; spec dışı, ayrı proje.

---

## 4) Önerilen sıra

| Sıra | Yapılacak | Çıktı |
|------|------------|--------|
| 1 | Faz 1.1–1.3: MQTTnet, config, publish servisi | Broker’a bağlanıp event publish edebilme |
| 2 | Faz 3.6: POST /api/trains/{id}/events | Manuel test; UI’dan veya curl/Postman ile event tetikleme |
| 3 | Faz 2.4: Eşik bazlı event’ler | Sensör değerleri eşiği aşınca otomatik MQTT mesajı |
| 4 | Faz 2.5 veya 3.7: Rastgele event veya UI event logu | İsteğe bağlı |

İlk adım: **Faz 1 + Faz 3.6** ile MQTT altyapısı ve manuel event tetikleme; böylece broker ve tüketen taraflar test edilebilir.

---

*Güncelleme: Polling sensörleri tamamlandı; event planı bu dokümana taşındı.*
