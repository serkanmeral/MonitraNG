# MngOperations — Workspace e-posta politikaları

**Son güncelleme:** 7 Haziran 2026  
**Durum:** Kararlandı — `op_notification_policies` genişletmesi + matris UI (Faz 2–3)

---

## 1. Amaç

Workspace bazında **hangi state geçişinde**, **hangi kişilere**, **hangi template** ile e-posta gönderileceğini tanımlamak. Tetikleme MO'da; render ve SMTP MngNotifier'da.

---

## 2. Dataset: `op_notification_policies` (genişletilmiş)

### Mevcut alanlar (korunur)

| Alan | Açıklama |
|------|----------|
| `workspaceId` | Zorunlu workspace kapsamı |
| `boardId` | Opsiyonel board filtresi |
| `typeId` | Opsiyonel work item tipi |
| `eventType` | `WorkItemCreated`, `WorkItemTransitioned`, … |
| `channels` | `inApp`, `email` |
| `recipients` | Alıcı tanımları (aşağıda) |
| `emailTemplateKey` | `@mail_templates.templateKey` |
| `notificationTemplateKey` | In-app tip anahtarı |
| `excludeActor` | Geçişi yapan kişiyi hariç tut |
| `priority` | Çakışma sıralaması |
| `isActive` | Aktif/pasif |

### Yeni alanlar (eklenecek)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `transitionKey` | text | Belirli geçiş (`resolve`, `escalate`); boş = tüm geçişler |
| `fromStateId` | relation → `op_states` | Kenar filtresi (opsiyonel) |
| `toStateId` | relation → `op_states` | Kenar filtresi (opsiyonel) |
| `emailSubject` | text | Subject override; boş = template `subject`; placeholder destekli |

---

## 3. `recipients` sözdizimi

| Değer | Kaynak |
|-------|--------|
| `assignee` | Çekirdek alan (`mng_person_id`) |
| `reporter` | Çekirdek alan |
| `watchers` | Dizi alan |
| `actor` | Geçişi yapan (`MngPersonId`) |
| `field:<fieldKey>` | Pool `person` veya `persons` alanı |

**Kimlik uzayı:** Tüm person referansları **MngKeeper `User.Id`** = DG `@users.__dataId` = claim `mng_person_id`.

**E-posta çözümü:** MO `IPersonDirectory` → Keeper `User/by-ids` → `PersonDisplayDto.Email`.  
**Kural:** E-posta tanımsız veya boş kullanıcılar **atlanır**; yalnızca geçerli adreslere gönderilir. `EmailDomainSuffix` ile sahte adres üretimi **kullanılmaz**.

**Odak test notu (LDAP):** Çoğu LDAP kullanıcısının Keeper'da `email` alanı boştur. Mail smoke / manuel test için assignee olarak e-postası dolu kullanıcı kullanın — örn. `serkan.meral@outlook.com` (`datasets/odak_mail_test_assignee.json`). Policy `recipients: ["assignee"]` ise work item'da assignee bu kişi olmalıdır.

---

## 4. Policy eşleştirme skoru

En spesifik policy kazanır:

| Kriter | Skor |
|--------|------|
| `transitionKey` eşleşmesi | +4 |
| `fromStateId` + `toStateId` | +3 |
| `typeId` | +2 |
| `boardId` | +1 |
| `priority` | Tie-break (yüksek önce) |

---

## 5. Matris UI (hedef)

Workspace tanımları → **Mail Policies** sekmesi:

| Geçiş | Alıcılar | Template | Subject override | Kanal |
|-------|----------|----------|------------------|-------|
| `resolve` → Resolved | `assignee`, `field:requester` | `work-item-transitioned` | *(boş)* | email |
| `escalate` | `watchers` | `work-item-transitioned` | `[ACİL] {{workItem.key}}` | email + inApp |

---

## 6. MO → Notifier çağrısı (geçiş anı)

```text
WorkItemCommandService.TransitionAsync (başarılı)
  → DispatchWorkItemEventAsync(WorkItemTransitioned)
    → PolicyMatches (+ transitionKey, from/to state)
    → channels içinde "email":
        → ResolveRecipientEmailsAsync (Keeper, email filtresi)
        → BuildMailContextAsync (state adları katalogdan)
        → Domain branding (Keeper Domain.logoUrl, displayName)
        → POST /api/v1/notifications/send-template
```

### Subject önceliği

1. Policy `emailSubject` (placeholder'lı) → Notifier'da render
2. Request `subject` (doğrudan override)
3. Template `subject` (Notifier render)

---

## 7. İlk canlı senaryo

| Alan | Değer |
|------|-------|
| Event | `WorkItemTransitioned` |
| Template | `work-item-transitioned` |
| Alıcılar | `assignee` (+ isteğe bağlı `field:…`) |
| Context | `workItem`, `transition`, `actor`, `domain`, `workspace`, `event` |

Seed: [datasets/notifier_mail_templates_seed.json](./datasets/notifier_mail_templates_seed.json)

---

## 8. Bilinçli sınırlar (Faz 1 MO)

| Konu | Faz 1 | Sonra |
|------|-------|-------|
| Person **grup** → mail | Hayır | Grup üyelerine toplu |
| Kişi başına `{{recipient.*}}` | Tek mail, tüm `to` | Alıcı başına ayrı gönderim |
| Per-domain SMTP | Global (Odak) | Multi-tenant |

---

## 9. MO kod değişiklikleri (Faz 1 backlog)

- [ ] `PersonDisplayDto.Email` + Keeper client map
- [ ] `NotificationRecipientResolver` → `field:` + `GetPersonRefId` + persons dizisi
- [ ] `ResolveRecipientEmailsAsync` — email yoksa atla
- [ ] `PolicyMatches` → `transitionKey`, `fromStateId`, `toStateId`
- [ ] `NotificationOrchestrator` → `SendTemplateAsync` (`/mail` yerine)
- [ ] `MailContextBuilder` — transition state adları, domain branding
- [ ] `emailTemplateKey` ve `emailSubject` policy'den okunacak

İlgili: [DEVAM.md](./DEVAM.md) · [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md)
