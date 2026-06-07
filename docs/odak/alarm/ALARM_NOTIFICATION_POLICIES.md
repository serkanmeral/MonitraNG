# Alarm Merkezi — Bildirim politikaları

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ **AN-1–AN-5 kod hazır** — UI + seed + smoke script; manuel doğrulama: [CONTROL_CHECKLIST.md](../CONTROL_CHECKLIST.md)

**İlişkili:** [IN_APP_TOAST_PLAN.md](../notifications/IN_APP_TOAST_PLAN.md) · MO paraleli: [MO_MAIL_POLICIES.md](../notifications/MO_MAIL_POLICIES.md)

---

## 1. Karar özeti (kilitli)

| Konu | Karar |
|------|--------|
| Policy modeli | **Ayrı** alarm policy matrisi (MO `op_notification_policies` ile paralel, birleşik dataset yok) |
| Veri yeri | MngAlarm domain Mongo — `@mon_alarm_notification_policies` (kurallar gibi DG mirror değil) |
| Alıcılar | **UI'da çoklu kullanıcı seçimi** (`recipientPersonIds[]` — Keeper `mng_person_id`) |
| Kanallar | `inApp` (+ toaster, bkz. toast planı), `email` |
| Dispatch | MngAlarm lifecycle sonrası (raise/update/resolve); Workflow aksiyonları **paralel** kalır |
| E-posta | MngNotifier `send-template` (alarm `@mail_templates` anahtarları) |
| In-app | MO ile paylaşılan `op_notifications` (MO orchestrator veya ortak yazıcı) |

---

## 2. Sınır

| Bu katman | Workflow |
|-----------|----------|
| Kime, hangi kanalla, hangi şablonla **bildir** | Onay bekle, IP blokla, work item aç |

`mng.alarms` exchange'i her iki tüketiciyi de besleyebilir; sorumluluklar ayrıdır.

---

## 3. Policy şeması (`@mon_alarm_notification_policies`)

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `name` | text | evet | Görünen ad |
| `description` | text | hayır | |
| `domainId` | text | evet | Tenant kapsamı |
| `eventType` | text | evet | `AlarmRaised`, `AlarmUpdated`, `AlarmResolved` |
| `ruleId` | text | hayır | Belirli kural; boş = tüm kurallar |
| `minSeverity` | number | hayır | Alt sınır (dahil); boş = sınır yok |
| `maxSeverity` | number | hayır | Üst sınır (dahil) |
| `channels` | text[] | evet | `inApp`, `email` |
| `recipientPersonIds` | text[] | evet | **Seçilen kullanıcılar** (`@users.__dataId` / `mng_person_id`) |
| `emailTemplateKey` | text | hayır | `email` kanalı açıksa zorunlu (UI validasyonu) |
| `emailSubject` | text | hayır | Subject override (placeholder) |
| `settings` | object | hayır | `{ "pushToast": true }` — inApp için anlık toaster |
| `cooldownMinutes` | number | hayır | Aynı policy + alarm dedup penceresi (0 = kapalı) |
| `excludeAcknowledgedBy` | bool | hayır | Varsayılan `false`; ileride ack eden hariç |
| `priority` | number | hayır | Tie-break (yüksek önce) |
| `isActive` | bool | evet | |

**Kimlik:** Tüm `recipientPersonIds` değerleri Keeper person id ile aynı uzay (MO ile uyumlu).

---

## 4. Eşleştirme

`AlarmNotificationDispatchService` (yeni, MngAlarm veya paylaşılan kitaplık):

1. `isActive` ve `domainId`
2. `eventType` tam eşleşme
3. Wildcard filtreler (boş = hepsi):
   - `ruleId` ↔ alarm `ruleId`
   - `minSeverity` / `maxSeverity` ↔ alarm `severity`
4. Eşleşen **tüm** policy'ler çalışır (MO ile aynı)

**Skor (tie-break / sıralama):**

| Kriter | Skor |
|--------|------|
| `ruleId` dolu ve eşleşti | +4 |
| `minSeverity` veya `maxSeverity` dolu ve aralıkta | +2 |
| `priority` | Yüksek önce |

**Cooldown:** Son dispatch zamanı policy+alarm bazında; pencere içinde tekrar gönderme.

---

## 5. Dispatch akışı

```text
ObservationProcessor / LifecycleService
    → alarm kaydı + mng.alarms publish (mevcut)
    → AlarmNotificationDispatchService.DispatchAsync(event)
        → policy eşleştir
        → recipientPersonIds (doğrudan liste; rol çözümü yok — MVP)
        → kanal başına:
            inApp  → op_notifications + (pushToast ? Hub push : yok)
            email  → MO Notifier client veya doğrudan MngNotifier send-template
```

**Context (e-posta / inbox):** `alarm.id`, `alarm.severity`, `rule.name`, `rule.id`, `event.type`, `event.timestamp`, `domain.displayName`, `recipient.displayName` (per-user mail ileride).

---

## 6. API (MngAlarm)

| Metot | Yol | Not |
|-------|-----|-----|
| GET | `/api/v1/notification-policies` | Liste, filtre: `domainId`, `isActive` |
| GET | `/api/v1/notification-policies/{id}` | |
| POST | `/api/v1/notification-policies` | |
| PUT | `/api/v1/notification-policies/{id}` | |
| DELETE | `/api/v1/notification-policies/{id}` | |

UI proxy: mevcut `Mng.Ui/server/api/alarm/[...path].ts` → gateway.

---

## 7. UI — Alarm Center

**Yer:** `/apps/alarm-center/notification-policies` (veya Rules yanında sekme **Bildirim politikaları**)

**Explorer + Dialog** (MO `OcWorkspaceMailPoliciesExplorer` deseni):

| Sütun / alan | Bileşen |
|--------------|---------|
| Ad | text |
| Olay | select: Raised / Updated / Resolved |
| Kural | combobox (opsiyonel, boş = tümü) |
| Severity | min / max number |
| Alıcılar | **çoklu kullanıcı seçici** (Keeper arama; `recipientPersonIds`) |
| Kanallar | checkbox: Uygulama içi, E-posta |
| Uygulama içi alt | “Anlık toaster göster” (`settings.pushToast`) |
| E-posta şablonu | combobox `@mail_templates` (email seçiliyse zorunlu) |
| Subject override | text |
| Aktif | switch |

**Kullanıcı seçici:** MO person alanlarındaki Keeper lookup ile aynı kaynak; çoklu seçim (`v-autocomplete` chips veya paylaşılan `OcPersonMultiSelect`).

---

## 8. E-posta şablonları

Alarm için `@mail_templates` seed (`notifier_mail_templates_seed.json`):

- `alarm-raised`
- `alarm-resolved`

Placeholder'lar: `{{alarm.id}}`, `{{alarm.severity}}`, `{{rule.name}}`, `{{event.timestamp}}`, `{{recipient.displayName}}`.

---

## 8b. Odak demo seed

| Dosya | Açıklama |
|-------|----------|
| [datasets/alarm_notification_policies_seed.json](./datasets/alarm_notification_policies_seed.json) | 3 örnek politika tanımı |
| [scripts/seed-alarm-notification-policies.ps1](./scripts/seed-alarm-notification-policies.ps1) | Idempotent seed (`alarm-raised` / `alarm-resolved` mail şablonları dahil) |
| [scripts/alarm_notification_policies_seed_result.json](./scripts/alarm_notification_policies_seed_result.json) | Son çalıştırma — oluşan policy id'leri |

```powershell
.\docs\odak\alarm\scripts\seed-alarm-notification-policies.ps1
```

---

## 9. Implementasyon fazları

| Faz | İçerik |
|-----|--------|
| **AN-1** | Mongo koleksiyon + CRUD API + unit smoke |
| **AN-2** | Dispatch service (policy match + email + inApp yazımı) |
| **AN-3** | UI explorer + dialog + kullanıcı multi-select |
| **AN-4** | Mail template seed + E2E smoke (raised → inbox + mail) |
| **AN-5** | Hub toaster entegrasyonu ([IN_APP_TOAST_PLAN.md](../notifications/IN_APP_TOAST_PLAN.md) F1–F2) |

**Ön koşul toast için:** ✅ Hub user room + global toast ([IN_APP_TOAST_PLAN.md](../notifications/IN_APP_TOAST_PLAN.md) T1–T3).

**Araya planlanan:** RabbitMQ diagnostics oturumu — dispatch öncesi/sonrası kuyruk sağlığı değerlendirmesi ([PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md) §4 `RMQ-DIAG`).

---

## 10. Açık (ileri faz)

- Rol tabanlı alıcı (`oncall`, `ruleSubscribers`) — şimdilik **yalnızca explicit kullanıcı listesi**
- `excludeAcknowledgedBy` tam davranışı
- Policy başına domain geneli asset/tag filtresi
- DG mirror (Faz 2 alarm mirror ile birlikte değerlendirilir)
