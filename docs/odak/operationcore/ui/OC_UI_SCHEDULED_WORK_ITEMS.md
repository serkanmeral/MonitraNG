# Mng.Ui — Zamanlanmış work item’lar (workspace)

**Son güncelleme:** 28 Mayıs 2026  
**Durum:** SW-0…SW-4d ✅ · SW-2/3 Odak ✅ · SW-5 cron E2E · SW-6 backlog  
**Backend auth:** [SCHEDULED_WORK_ITEMS.md §4.1](../mngoperations/SCHEDULED_WORK_ITEMS.md)  
**Backend:** [SCHEDULED_WORK_ITEMS.md](../mngoperations/SCHEDULED_WORK_ITEMS.md)  
**Handoff:** [DEVAM.md](../mngoperations/DEVAM.md)  
**Kardeş (olay-tetikli):** [OC_UI_WORKSPACE_AUTOMATIONS.md](./OC_UI_WORKSPACE_AUTOMATIONS.md) · [WORKSPACE_AUTOMATION_PLANNING.md](../mngoperations/WORKSPACE_AUTOMATION_PLANNING.md)

---

## 1. Ürün kararı (özet)

Kullanıcı workspace’te **bir kez** «bakım işi şablonunu» tanımlar (board, tip, assignee, başlık, alanlar, cron). Belirlenen zamanda sistem **Yeni iş** formunu doldurup kaydetmiş gibi **otomatik** WI açar.

| Karar | Değer |
|-------|--------|
| Kapsam | Tek **workspace** |
| Board / tip | Schedule başına **sabit** |
| Assignee | **Sabit**; değişince yalnızca şablon güncellenir |
| Her tetik | **Yeni bağımsız** WI (çift oluşturma serbest) |
| Yetki | Yalnızca **manager** (domain yöneticisi) |
| İlk sürüm | Metadata + MO + Scheduler senkron + **bu sekme** |

---

## 2. Yerleşim — Workspace tanımları

Yeni üst sekme (önerilen sıra):

```text
[Genel] [Değerler ▾] [Akışlar] [Formlar] [Board'lar] [Politikalar] [Zamanlanmış işler]
```

| Öğe | Değer |
|-----|--------|
| Route anahtarı | `tab=scheduled` |
| Composable | `useOcWorkspaceDefinitionTabs.ts` — `scheduled` ekle |
| Bileşen | `OcWorkspaceDefinitionsScheduledWorkItemsTab.vue` |
| Sayfa | `pages/apps/operation-core/admin/workspace-definitions/index.vue` |

**Menü:** Workspace tanımları zaten `manager` pageType; ek menü gerekmez.

---

## 3. Sekme içeriği

### 3.1 Liste

| Sütun | İçerik |
|-------|--------|
| Ad | `name` |
| Cron / özet | İnsan okunur («Her Pazartesi 09:00») + `timezone` |
| Board | Board adı |
| Tip | İş tipi adı |
| Atanan | Kişi adı |
| Durum | `isActive` chip |
| Son çalışma | `lastRunAt` |
| Son iş | `lastWorkItemId` → profil link |
| İşlemler | Düzenle, sil, **Şimdi çalıştır** (SW-2) |

### 3.2 Editör (dialog — zamanlama sihirbazı SW-4d)

**Bileşen:** `OcScheduleTimingWizard.vue` — cron bilgisi gerektirmez.

| Sıklık | UI | Quartz örneği |
|--------|-----|----------------|
| Her X dakikada | sayı 1–59 | `0 0/2 * * * ?` |
| Her X saatte | sayı 1–23 | `0 0 0/6 * * ?` |
| Her gün saat X | saat + dakika | `0 30 9 * * ?` |
| Haftanın günleri | çoklu chip (Pzt+Sal+…) + saat | `0 0 9 ? * MON,WED,FRI` |
| Gelişmiş | ham cron | teknik |

Kısayollar: **Hafta içi**, **Hafta sonu**. Özet kutusu + canlı önizleme sağ panelde.

| Bölüm | Alanlar |
|-------|---------|
| Genel | Ad, açıklama, aktif |
| Zamanlama | Cron (metin + doğrulama) veya basit builder (haftanın günü + saat); **timezone** seçici |
| Hedef | Board (workspace board listesi), iş tipi (`ocListWorkItemTypesForWorkspace`) |
| Şablon | Başlık, açıklama, assignee (person picker), öncelik, isteğe bağlı `fields` (create ile uyumlu alan seti) |
| Önizleme | «Bir sonraki 3 çalışma» (cron parser, client-side) |

**Kayıt (bugün):** Yalnızca DG `op_work_item_schedules` — MngScheduler job **henüz oluşturulmaz** (SW-3).

**Runtime (SW-2/3):** Cron tetik → MngScheduler → **Keeper token** → MO `from-origin` → WI. Kimlik: teknik kullanıcı adı/şifre Scheduler konfigünde; [SCHEDULED_WORK_ITEMS §4.1](../mngoperations/SCHEDULED_WORK_ITEMS.md).

### 3.3 Yetki (UI)

- Sekme yalnızca manager için görünür (`auth` / `isManager` — Keeper claim).  
- `users` grubu: sekme gizli veya salt okunur uyarı.

---

## 4. Kullanıcı deneyimi notları

- Manuel **Yeni iş** ile aynı sonuç; fark yalnızca tetikleyici (zaman).  
- Oluşan WI’lar normal board/kanban’da görünür; «otomatik» köken profilde/activity’de `scheduler` olarak işaretlenir.  
- Schedule silinince **geçmiş WI’lar kalır**.  
- Assignee güncellemesi **ileriye dönük** şablona uygulanır.

---

## 5. Servis / tipler (öneri)

`Mng.Ui/types/apps/operationCore.ts`:

```ts
export interface OpWorkItemSchedule {
  __dataId: string;
  workspaceId: string;
  name: string;
  description?: string;
  isActive: boolean;
  cronExpression: string;
  timezone: string;
  boardId: string;
  typeId: string;
  assignee: string;
  priorityId?: string;
  title: string;
  // ...
  schedulerJobId?: string;
  lastRunAt?: string;
  lastWorkItemId?: string;
}
```

`operationCoreService.ts`:

- `ocListSchedulesForWorkspace(wsId)`
- `ocCreateWorkItemSchedule(wsId, body)`
- `ocUpdateWorkItemSchedule(id, body)`
- `ocDeleteWorkItemSchedule(id)`
- `ocRunWorkItemScheduleNow(id)` — manuel tetik (manager)

---

## 6. Uygulama sırası (UI)

| Adım | İş |
|------|-----|
| SW-0 | Dataset + tipler |
| SW-4a | Sekme iskelet + liste (DG CRUD) |
| SW-4b | Editör + person/board/type seçicileri (mevcut OC bileşenleri) |
| SW-3 | Kayıtta Scheduler senkron (hata mesajı UI’da) |
| SW-4c | Son çalışma / son WI link, «Şimdi çalıştır» |

---

## 7. i18n anahtarları (öneri)

`operationCore.workspaceDefinitions.tabs.scheduled`  
`operationCore.workspaceDefinitions.scheduled.*` — liste, form, cron, managerOnly, runNow, lastRun, …

---

## 8. Test checklist

### 8.0 Önkoşullar (404 hatası)

**Belirti:** `GET .../data/op_work_item_schedules?...` → **404 Not Found**

**Neden:** Dataset şeması Odak DG’de henüz oluşturulmamış. UI hazır; SW-0 deploy gerekir.

**Çözüm** (repo kökünden, PowerShell):

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets.ps1
```

Script mevcut dataset’leri atlar; yalnızca eksik olanları (ör. `op_work_item_schedules`) ekler. Sonra tarayıcıyı yenileyin.

**Doğrulama:**

```powershell
$token = .\docs\odak\operationcore\scripts\load-operationcore-token.ps1
curl.exe -s -H "Authorization: Bearer $token" `
  "http://192.168.20.20:5040/data/api/v1/data/op_work_item_schedules?limit=1"
```

Kayıt listesi boş dönebilir; **404 değil 200** olması yeterli.

### 8.1 Manuel UI testi (Zamanlanmış işler sekmesi)

| # | Adım | Beklenen |
|---|------|----------|
| 1 | **Manager** hesabıyla giriş (`isManager`) | Sayfa açılır; normal user → `/unauthorized` |
| 2 | Workspace tanımları: demo workspace seç | `f414462a-cd9e-427e-87e8-3cdff0502325` (Odak demo) |
| 3 | URL: `?tab=scheduled` veya **Zamanlanmış işler** sekmesi | Liste yüklenir; hata bandı yok |
| 4 | **Yeni zamanlama** → haftalık Pazartesi 09:00, board + tip + assignee + başlık | Kayıt başarılı; listede görünür |
| 5 | Sayfayı yenile | Kayıt DG’den geri gelir |
| 6 | Düzenle → assignee değiştir → kaydet | Liste güncellenir |
| 7 | **Şimdi çalıştır** | SW-2 deploy sonrası → WI oluşur; `lastRunAt` / `lastWorkItemId` güncellenir |
| 8 | Sil | Kayıt kalkar |

**Demo URL örneği:**

```text
/apps/operation-core/admin/workspace-definitions?workspaceId=f414462a-cd9e-427e-87e8-3cdff0502325&tab=scheduled
```

**Ön veri:** En az bir **board** ve **iş tipi** (Değerler sekmesinde etkin) olmalı; aksi halde hedef alanları boş kalır.

### 8.2 Politikalar ve Kurallar (karşılaştırmalı smoke)

Aynı workspace’te — dataset deploy **gerekmez** (`fieldPolicies` workspace settings içinde; `op_rules` zaten deploy edilmiş olmalı):

| Sekme | URL `tab=` | Hızlı test |
|-------|------------|------------|
| Politikalar | `policies` | Alan seç → Görünürlük ekle → koşullu → kaydet → özet listede |
| Kurallar | `rules` | Kural ekle → validation → geçiş scope → kaydet → düzenle → pasifleştir |

Detay: [OC_UI_WORKSPACE_POLICIES.md §7](./OC_UI_WORKSPACE_POLICIES.md), [OC_UI_RULES_FAZ1.md §6](./OC_UI_RULES_FAZ1.md).

### 8.3 E2E (SW-2/3 sonrası)

1. Manager ile sekme görünür; normal user görmez.  
2. Schedule kaydet → DG’de kayıt + Scheduler’da job.  
3. Cron tetiklenince board’da yeni WI; assignee/board/tip şablona uygun.  
4. Assignee değiştir → kayıt güncellenir; **eski WI assignee değişmez**.  
5. Pasif schedule → job `isActive=false`, yeni WI yok.  
6. İki tetik ardışık → **iki ayrı** WI.

---

## 9. SW-6 — Admin Scheduled Jobs sayfası (backlog)

Workspace **Zamanlanmış işler** sekmesi yalnızca **tek workspace** schedule'larını yönetir. Platform admin'in ihtiyacı farklı:

| | Workspace sekmesi (SW-4) | Admin explorer (SW-6) |
|--|--------------------------|------------------------|
| Kapsam | Bir workspace'in `op_work_item_schedules` | Tüm system + domain user job'lar |
| Yetki | Domain manager | Platform/domain admin |
| Manuel run | «Şimdi çalıştır» → MO `/execute` | Aynı + system job trigger |
| Veri kaynağı | DG `op_work_item_schedules` | `mngkeeper.@scheduled_jobs` + `mng_* .@scheduled_jobs` |

**Kullanıcı notu (28 May):** Job listesini görmek için artık veritabanına bakmak istemiyoruz — admin UI'da birleşik liste + isteğe bağlı manuel run.

Backend detay: [SCHEDULED_WORK_ITEMS §4.5](../mngoperations/SCHEDULED_WORK_ITEMS.md).

---

*Operasyonel runtime board: [OC_UI_PHASE1_PLAN.md](./OC_UI_PHASE1_PLAN.md) Sprint 2.*
