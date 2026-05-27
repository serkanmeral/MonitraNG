# IT Help Desk — Referans Workspace Taslağı

**Amaç:** Operation Core'un **help desk / IT destek** senaryosunu, operasyonel süreçlerden (SOC, bakım) bağımsız ama **aynı platform** üzerinde göstermek.  
**Kaynak:** [operationcore_phase1.md](../operationcore_phase1.md), [OPERATION_CORE_IMPLEMENTATION_PLAN.md](../OPERATION_CORE_IMPLEMENTATION_PLAN.md) — TM/eski UI sayfaları **referans değil**.

**Seed:** [../scripts/seed-operation-core-helpdesk-reference.ps1](../scripts/seed-operation-core-helpdesk-reference.ps1)

---

## 1. Konumlandırma

| Katman | Help desk örneği |
|--------|------------------|
| **Sistem tanımlaması** | Global state/öncelik/tip/alan havuzu — semantik + görsel |
| **Workspace tanımlaması** | `IT Destek` workspace — hangi tipler aktif, akış, SLA, board |
| **Runtime (MO)** | Transition, permission, SLA hesabı, profil aksiyonları |

Aynı tenant'ta paralel workspace'ler:

```text
IT Destek          → help desk (bu doküman)
OC Demo Workspace  → geliştirme demosu (mevcut seed)
SOC / Bakım        → operasyonel süreç (ileride ayrı referans)
```

---

## 2. Work Item tip kategorileri (plan kararı)

Fikir plan metinlerinde henüz ayrı bir bölüm olarak yazılmamıştı; **şemada** `op_work_item_types.category` (zorunlu, indeksli) vardı.  
Bu referans, kategorileri **sabit enum** olarak tanımlar (Faz 1 UI: select; Faz 2: tenant özelleştirme değerlendirilir).

| `category` | Açıklama | Örnek tipler |
|------------|----------|--------------|
| `incident` | Kesinti / olay | Donanım arızası, Uygulama hatası |
| `service_request` | Standart hizmet talebi | Erişim talebi, Yazılım kurulumu |
| `problem` | Kök neden analizi | Tekrarlayan olay |
| `change` | Değişiklik yönetimi | Planlı bakım, Sürüm yükseltme |
| `task` | Genel iş / dahili görev | Dokümantasyon, Kontrol listesi |
| `operational` | IT dışı operasyon (SOC/NOC vb.) | Alarm triage, Runbook adımı |

**UI gruplama:** Tip seçicide ve admin listesinde `category` → bölüm başlığı.  
**Raporlama / SLA:** Workspace + `type.category` kırılımı.  
**Not:** `op_states.category` (`open` / `in_progress` / `closed`) **farklı** bir eksen — state'in yaşam döngüsü anlamı; tip kategorisi değildir.

Detay: [operationcore_phase1.md §8.4](../operationcore_phase1.md#84-work-item-tip-kategorileri)

---

## 3. Global katalog — Durumlar (`op_states`)

Help desk akışı için önerilen **global** state'ler (display name seed'de Türkçe):

| Ad | `category` | Bayraklar | Renk (öneri) |
|----|------------|-----------|--------------|
| Yeni | `open` | `isInitial`, `isStart` | `info` |
| Atandı | `in_progress` | — | `primary` |
| İşlemde | `in_progress` | — | `warning` |
| Müşteri bekleniyor | `on_hold` | — | `secondary` |
| Çözüldü | `closed` | `allowReopen` | `success` |
| Kapalı | `closed` | `isClosed`, `isTerminal` | `secondary` |

Akış sırası **state kaydında değil**; workspace `op_state_flows` içinde tanımlanır.

---

## 4. Global katalog — Öncelikler (`op_priorities`)

| Ad | `level` | `sortOrder` | Renk |
|----|---------|-------------|------|
| Kritik (P1) | 1 | 10 | `error` |
| Yüksek (P2) | 2 | 20 | `warning` |
| Orta (P3) | 3 | 30 | `info` |
| Düşük (P4) | 4 | 40 | `secondary` |

`level` sayısal — escalation ve SLA eşlemesi için (major plan).

---

## 5. Global katalog — Tipler (`op_work_item_types`)

`workspaceId` **boş** = global tip; workspace'te `enabledTypeIds` ile seçilir.

| Ad | `category` | Varsayılan flow (workspace'te) |
|----|------------|--------------------------------|
| Olay (Incident) | `incident` | IT Help Desk — Standard Flow |
| Hizmet talebi | `service_request` | aynı |
| Problem kaydı | `problem` | aynı |
| Erişim talebi | `service_request` | aynı |

İkon/renk: incident → `AlertCircleIcon` / `error`; service_request → `TicketIcon` / `info`; problem → `BugIcon` / `warning`.

---

## 6. Global katalog — Alan havuzu (`op_fields`)

Help desk **pool** alanları (`scope: pool`); değerler `op_work_items.extraFields` içinde ([phase1 §8.5](../operationcore_phase1.md)).

**Core alanlar** (`title`, `stateId`, `assignee`, `impact`, `urgency` …) şema kolonlarıdır; `op_fields` kaydı yok.

| `key` | `fieldType` | category |
|-------|-------------|----------|
| `requestCategory` | `text` | classification |
| `affectedUser` | `persons` | assignment |
| `affectedAsset` | `text` | technical |
| `resolutionSummary` | `text` | resolution |

`impact` / `urgency` → **core** (üst seviye); pool duplicate kullanılmaz.

---

## 7. Workspace — `IT Destek`

```json
{
  "name": "IT Destek",
  "workspaceType": "service_desk",
  "description": "Kurumsal IT help desk — olay, hizmet talebi ve problem yönetimi",
  "workItemKeyPrefix": "HD",
  "workItemKeyFormat": "{prefix}-{seq:D4}",
  "workItemSequenceStart": 1,
  "enabledTypeIds": ["<incident>", "<service_request>", "<problem>", "<access_request>"],
  "enabledFieldIds": ["<affectedUser>", "<affectedAsset>", "<requestCategory>", "<resolutionSummary>"]
}
```

---

## 8. State flow — `IT Help Desk — Standard Flow`

| transitionKey | from → to | Etiket |
|---------------|-----------|--------|
| `assign` | Yeni → Atandı | Ata |
| `start_work` | Atandı → İşlemde | İşleme al |
| `start_from_new` | Yeni → İşlemde | Doğrudan işle |
| `wait_customer` | İşlemde → Müşteri bekleniyor | Müşteriden yanıt bekle |
| `resume` | Müşteri bekleniyor → İşlemde | Devam et |
| `resolve` | İşlemde → Çözüldü | Çöz |
| `close` | Çözüldü → Kapalı | Kapat |
| `reopen` | Çözüldü → Atandı | Yeniden aç |
| `reopen_closed` | Kapalı → Atandı | Yeniden aç (izin varsa) |

`initialStateId` = **Yeni**.  
Kapatma öncesi `resolutionSummary` veya `description` zorunluluğu → `op_rules` (validation, `transitionKey: resolve`).

---

## 9. Board, form, profil (özet)

| Varlık | Not |
|--------|-----|
| **Board** `IT Destek — Kuyruk` | `viewType: list` (agent kuyruğu); kolonlar state flow sırasına göre |
| **Board** `IT Destek — Kanban` | `viewType: kanban`; aynı flow |
| **Form** | Zorunlu: `title`, `typeId`, `priorityId`; core: `impact`, `urgency`; pool: `requestCategory`, `affectedUser`, `affectedAsset` |
| **Profile** | SLA paneli açık; aksiyonlar flow transitionKey ile; `typeId` readonly |
| **SLA** | Incident P1: yanıt 15 dk / çözüm 4 saat (demo değerleri; Faz 2 working hours) |

---

## 10. Operasyonel süreç ile fark

| Help desk | Operasyon (SOC vb.) |
|-----------|------------------------|
| Tip kategorisi: `incident`, `service_request` | `operational`, `change` |
| Akış: müşteri bekleme, çöz/kapat | Akış: triage, eskalasyon, runbook |
| SLA: yanıt/çözüm | SLA: müdahale/restore |
| Alan: etkilenen kullanıcı | Alan: kaynak alarm, asset, lokasyon |

**Aynı motor** — farklı workspace profili.

---

## 11. Seed ve doğrulama

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\seed-operation-core-helpdesk-reference.ps1
```

Çıktı özeti `operationcore-helpdesk-seed.json` dosyasına yazılır.  
UI: Operasyon Merkezi → **IT Destek** workspace → board aç.

---

## 12. Sonraki adımlar (ürün)

- [ ] Sistem tanımlaması UI: tip listesinde `category` gruplama
- [ ] Workspace tanımlaması UI: `enabledTypeIds`, flow editor
- [ ] Self-service portal (major plan Faz 2+)
- [ ] E-posta / chat'ten ticket açma entegrasyonu
