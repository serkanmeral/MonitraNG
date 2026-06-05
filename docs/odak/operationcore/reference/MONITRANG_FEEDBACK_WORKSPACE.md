# MonitraNG Geri Bildirim — Workspace taslağı

**Amaç:** MonitraNG üzerinde görülen **hata** ve **öneri** kayıtlarını tek workspace’te toplamak; yalnızca **`MonitraNG Users`** grubundaki kişiler (Odak IT + developer ekibi) görür, kayıt açar ve takip eder.  
**Durum:** Prod’da seed tamamlandı (2026-06-05)  
**İlişki:** [IT_HELP_DESK_REFERENCE.md](./IT_HELP_DESK_REFERENCE.md) (sadeleştirilmiş varyant) · İkinci WS (IT iç işleri) ayrı planlanacak

---

## 1. Kapsam kararları

| Konu | Karar |
|------|--------|
| Workspace adı | **MonitraNG Geri Bildirim** |
| `workspaceType` | `service_desk` |
| Work item anahtarı | `MNG-0001` (`workItemKeyPrefix`: `MNG`) |
| Tipler | Aynı WS: **Uygulama hatası** + **Öneri / iyileştirme** |
| Yetkili grup | Yalnızca **`MonitraNG Users`** — `users` / `managers` **yok** |
| Raporlayanlar | Aynı grup (`MonitraNG Users`) |
| Öncelik | Varsayılan **Orta (P3)**; formda gizlenebilir veya salt okunur |
| Board adları | Türkçe |
| Ortam | **Yalnızca prod** (`192.168.20.8`) — test ortamında yok |

Bu workspace **şirket geneli help desk değil**; MonitraNG’yi aktif kullanan dar ekip içi geri bildirim kanalıdır.

---

## 2. Work item tipleri

| Ad | `category` | Varsayılan? | Not |
|----|------------|-------------|-----|
| Uygulama hatası | `incident` | Evet | Bug / kırık davranış |
| Öneri / iyileştirme | `service_request` | Hayır | UX, özellik, iyileştirme |

`enabledTypeIds`: yalnızca bu iki tip.

---

## 3. Durumlar (`op_states`)

Workspace’te kullanılacak global state’ler (seed’de find-or-create):

| Görünen ad | `category` | Bayraklar |
|------------|------------|-----------|
| Yeni | `open` | `isInitial`, `isStart` |
| İnceleniyor | `in_progress` | — |
| Bilgi bekleniyor | `on_hold` | — |
| Planlandı | `in_progress` | — |
| Tamamlandı | `closed` | `allowReopen` |
| Reddedildi | `closed` | `isClosed`, `isTerminal` |

---

## 4. State flow — `MonitraNG Geri Bildirim — Akış`

`initialStateId` = **Yeni**

| `transitionKey` | Geçiş | Etiket |
|-----------------|--------|--------|
| `triage` | Yeni → İnceleniyor | İncelemeye al |
| `need_info` | İnceleniyor → Bilgi bekleniyor | Bilgi iste |
| `info_provided` | Bilgi bekleniyor → İnceleniyor | Bilgi verildi |
| `plan` | İnceleniyor → Planlandı | Planla |
| `complete` | İnceleniyor / Planlandı → Tamamlandı | Tamamla |
| `reject` | İnceleniyor → Reddedildi | Reddet |
| `reopen` | Tamamlandı → İnceleniyor | Yeniden aç |

**Transition `permissions.groups`:** `MonitraNG Users` (tek grup — tüm geçişler aynı).

> **Faz 1 notu:** Grup tek olduğu için “yalnızca triyajcı” ayrımı MO transition izniyle yapılamaz. Ekip içi süreç: kayıt açan `assignee` atamaz; incelemeye alan kişi `assignee` olur. İleride assignee-bazlı kural eklenebilir (`op_rules`).

---

## 5. Öncelikler

Global `op_priorities` — workspace’te `enabledPriorityIds` dört seviye.

| Form davranışı | Ayar |
|----------------|------|
| Varsayılan | **Orta (P3)** — `fieldPolicies` `defaultValue` veya form `fieldBehaviors` |
| Görünürlük | İsteğe bağlı gizli (kullanıcı seçmez) veya seçilebilir bırakılır |

---

## 6. Pool alanları (`op_fields`)

| `key` | Etiket | `fieldType` | Zorunluluk |
|-------|--------|-------------|------------|
| `appModule` | Modül / menü | `text` | Önerilen |
| `pageUrl` | Sayfa adresi | `text` | İsteğe bağlı |
| `environment` | Ortam | `text` veya `select` | Hata: evet (`prod` / `test`) |
| `stepsToReproduce` | Yeniden üretme adımları | `text` | Hata: evet |
| `expectedBehavior` | Beklenen davranış | `text` | Hata |
| `actualBehavior` | Gerçekleşen davranış | `text` | Hata |
| `resolutionSummary` | Çözüm / karar özeti | `text` | Kapanışta (validation rule) |
| `screenshot` | Ekran görüntüsü | `file` | İsteğe bağlı |

`impact` / `urgency` çekirdek alanları bu WS’te **kullanılmayabilir** (sadeleştirme).

---

## 7. Workspace kaydı (`op_workspaces`)

```json
{
  "name": "MonitraNG Geri Bildirim",
  "workspaceType": "service_desk",
  "description": "MonitraNG hata ve öneri kayıtları — yalnızca MonitraNG Users",
  "workItemKeyPrefix": "MNG",
  "workItemKeyFormat": "{prefix}-{seq:D4}",
  "workItemSequenceStart": 1,
  "viewGroups": ["MonitraNG Users"],
  "editGroups": ["MonitraNG Users"],
  "adminGroups": ["admins"],
  "enabledTypeIds": ["<type_bug>", "<type_suggestion>"],
  "enabledFieldIds": ["<appModule>", "<pageUrl>", "<environment>", "..."]
}
```

**JWT:** `user_groups` içinde tam ad **`MonitraNG Users`** olmalı (LDAP/Keycloak sync — Keeper claim).

---

## 8. Form — `MonitraNG Geri Bildirim — Yeni kayıt`

**Bölüm: Kayıt bilgileri**

| Alan | Davranış |
|------|----------|
| `title` | Zorunlu |
| `description` | Zorunlu |
| `typeId` | Zorunlu; varsayılan Uygulama hatası |
| `priorityId` | Varsayılan **Orta (P3)** — görünür, salt okunur (`defaultValues`) |
| `assignee` | Görünür; kullanıcı seçer (triage ataması) |
| `labels` | Görünür; katalog **`op_tags`** (workspace etiketleri); varsayılan ön-dolum yok |
| `appModule`, `pageUrl`, `environment` | Önerilen / isteğe bağlı |
| `screenshot` | İsteğe bağlı |
| `resolutionSummary` | İsteğe bağlı (oluşturma); **Tamamla** geçişinde zorunlu (dialog + validation rule) |

**Etiketler (`op_tags`):** MonitraNG · UI / Arayüz · API / Veri · Yetki · Performans · Prod

---

## 9. Board’lar

| Board adı | `viewType` | Hedef |
|-----------|------------|--------|
| **Geri bildirim gönder** | `list` veya form odaklı | Yeni kayıt + kendi açtıklarım (ileride filtre) |
| **İnceleme kuyruğu** | `list` | Tüm açık kayıtlar; kolonlar duruma göre |

Her iki board `viewGroups` / `editGroups`: **`MonitraNG Users`**.

Kanban (opsiyonel, Faz 1.1): **Geri bildirim — Kanban**

---

## 9.1 Özet pano (`op_dashboards`)

**Ad:** `MNG Geri Bildirim — Özet pano` · `isDefault: true` · `scope: workspace`

Workspace hub’da board seçildiğinde **Pano** sekmesinde görünür (inline dashboard).

| Satır | Widget | Tip | Açıklama |
|-------|--------|-----|----------|
| 1 | Yeni geri bildirim | summaryCard | `wi_by_workspace_and_state` → Yeni |
| 1 | İnceleniyor | summaryCard | İnceleme kuyruğu |
| 1 | Bilgi bekleniyor | summaryCard | Bekleyen bilgi talepleri |
| 1 | Planlandı | summaryCard | Planlanmış işler |
| 2 | Yeni kayıtlar — tip dağılımı | chart (donut) | `groupBy: typeId` (hata / öneri) |
| 2 | İnceleme kuyruğu — öncelik | chart (bar) | `groupBy: priorityId` |
| 3 | Son yeni geri bildirimler | list (8) | Yeni durumdaki kayıtlar |
| 3 | Bana atanan açık kayıtlar | list (8) | `wi_assigned_open` + `{{currentUser}}` |

SLA widget’ları yok (bu workspace’te SLA politikası kullanılmıyor).

---

## 10. Kurallar (`op_rules`)

| Kural | Tetikleyici | Koşul |
|-------|-------------|--------|
| Çözüm özeti zorunlu | `WorkItemTransition` → `complete` | `resolutionSummary` boş olmasın |

---

## 11. Bildirim (opsiyonel)

| Olay | Alıcı |
|------|--------|
| `WorkItemCreated` | `MonitraNG Users` (policy — e-posta/UI) |

---

## 12. Seed planı

| Dosya | Rol |
|-------|-----|
| `scripts/seed-operation-core-monitrang-feedback.ps1` | DG kayıtları (helpdesk seed fork) |
| `scripts/operationcore-monitrang-feedback-seed.json` | Oluşan ID özeti |

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets-prod.ps1   # ilk kurulum
.\docs\odak\operationcore\scripts\seed-operation-core-monitrang-feedback.ps1
```

**Ön koşul:** Prod’da `setup-operation-core-datasets-prod.ps1` çalışmış olmalı. Grup ID’leri `resolve-odak-group-ids-prod.ps1` ile `mng_odak.@groups` üzerinden çözülür (`personGroups` alanı grup adı değil `__dataId` bekler).

**Seed özeti:** [operationcore-monitrang-feedback-seed.json](../scripts/operationcore-monitrang-feedback-seed.json)

---

## 13. Doğrulama checklist

- [x] Workspace `viewGroups` / `editGroups` → **MonitraNG Users** (`6a1072ac69221f812f97b29b`)
- [ ] `MonitraNG Users` dışındaki kullanıcı workspace’i **görmez** (UI doğrulama)
- [ ] Grup üyesi **Uygulama hatası** ve **Öneri** kaydı açar → `MNG-0001`
- [ ] Varsayılan öncelik **Orta**
- [ ] İnceleme kuyruğu board’unda kayıt listelenir
- [ ] `complete_from_review` / `complete_from_planned` geçişinde `resolutionSummary` zorunlu
- [ ] Prod menü: Operasyon Merkezi → workspace seçimi
- [ ] Workspace hub → **Pano**: özet kartları ve listeler doluyor

---

## 14. Sonraki workspace (ertelendi)

**IT genel help desk** — ayrı WS: [IT_HELP_DESK_WORKSPACE.md](./IT_HELP_DESK_WORKSPACE.md) (`users` talep açar, `MonitraNG Users` agent kuyruğu).
