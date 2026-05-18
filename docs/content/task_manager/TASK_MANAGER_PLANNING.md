# Jira Benzeri Task Manager — Planlama Belgesi

**Tarih:** 25 Şubat 2026  
**Son güncelleme:** 27 Nisan 2026 — **Task Manager’da ara:** bu tarihte yapılan işler ve devam noktası aşağıda **El değiştirme** + **§1.1** / **§1.3** ile kayıt altına alındı. Önceki özet: 23 Nisan görev yorumları ilk sürüm; öncesinde yeni görev formu / layout; **Breadcrumb §12** / **§10.12** hâlâ backlog’ta.

**Backend:** MngDataGateway (DG) Datasets + **MngWorkflow** (ayrı servis, `projectKey` doğrulama pipeline; MngTaskManager yok)  
**Frontend:** Mng.Ui (Nuxt 3 + Vue 3)

### El değiştirme — ara sonrası devam (27 Nisan 2026)

| Konu | Not |
|------|-----|
| **Ara notu** | Task Manager geliştirmesine **geçici olarak ara verildi**. Tekrar açıldığında bu tablo + **§1.3** satırlarından devam edilebilir. |
| **27 Nisan — tamamlanan (kod)** | **Yorum yazar adı:** DG `persons` genişlemesi `@users.__dataId` ile eşler; `author` alanına Keycloak `sub` yazmak lookup’u boş bırakıyordu → `createIssueComment` / `updateIssueComment` içinde Keeper `id`/`userId` (`useUserStore().getUserById(sub)`). Görünen ad: `TmIssueComments` içinde `getUserById` + `assigneeDisplayLabel`; `canModify` hem `sub` hem Keeper id. **`taskManagerService`:** `showHistory` query; **`hydrateIssueWithHistory`**; **`updateIssue`** sonrası liste + hydrate. **`parseIssueHistory`:** `timestamp` / `$date`, `userEmail`, `userInfo`, DG’nin güncelleme gövdesi şeklindeki `changes` haritası (yalnızca yeni değer). **Profil:** sağ panel (yorumlar / geçmiş) **genişletildi** (`TmIssueProfileView` ızgara). **Etiketler:** yalnızca **projeye bağlı** liste (`loadLabels` sıkı filtre); store **`updateLabel`** / **`deleteLabel`**; sayfa **`/apps/task-manager/projects/[id]/labels`**, proje özetinde **Etiketler** butonu; i18n **tr/en** `projectLabels*`. Setup script: `tm_labels.projectId` alan açıklaması (mevcut şemayı kırmamak için mandatory değiştirilmedi). |
| **Eski veri / migrasyon** | DB’de `author` = `sub` kalan eski yorumlar: UI `getUserById` ile isim gösterir. **`projectId` boş** global etiketler artık listede yok; varsa DG’de projeye bağlanmalı veya yeniden oluşturulmalı. |
| **Kaldığınız yer (devamda)** | Faz 1 UI güçlü; sıradaki büyük işler yine **MngWorkflow** sunucu doğrulaması, **dataset kategorisi**, **breadcrumb / fr-zh-ar i18n**, yorum **bildirimi**, **DG validation** `tm_issue_comments`, board kartında **yorum sayısı**, route guard / yetki sertleştirme. |
| **Öncelikli 3 iş (genel backlog)** | (1) **MngWorkflow** — `projectKey` ↔ `projectId` (**§10** / **§6.2.2**). (2) **Dataset kategorisi** — “Task Manager” + `tm_*` (**§10** m.1). (3) **Breadcrumb** tek tur (**§12**) veya bildirim / kart yorum sayısı — ihtiyaca göre. |
| **Hızlı giriş** | Store: `stores/apps/taskManager.ts` (`hydrateIssueWithHistory`, `loadIssueComments`, `createIssueComment`, …). Yorum UI: `TmIssueComments.vue`. Geçmiş ayrıştırma: `utils/taskManagerIssueHistory.ts`. Etiket sayfası: `pages/.../projects/[id]/labels.vue`. DG setup: `scripts/.../setup-task-manager-datasets.ps1` (**10 dataset**). Özet: **§1.3**. |
| **Belge sırası** | **§1–§10** ana plan; **§12** breadcrumb; **§11** şubat checkpoint (tarihsel — güncel Workflow **§1.2**). |

### 1.1 Mevcut uygulama durumu (Nisan 2026)

| Alan | Durum |
|------|--------|
| **DG setup** | `scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1` — **10 dataset** (son eklenen: **`tm_issue_comments`**) + seed. Script sonunda **dataset kategorisi** için manuel/UI adımı önerisi yazdırılır (otomatik kategori oluşturma yok). |
| **Mng.Ui servis / store** | `services/taskManagerService.ts` (DG CRUD); `stores/apps/taskManager.ts` (tek store: proje, board, issue, lookup’lar, **`fieldDefinitions`**, alan havuzu CRUD). |
| **Sayfalar** | `/apps/task-manager` proje listesi; `projects/[id]` board listesi/oluşturma + **Etiketler** → **`projects/[id]/labels`** (yönetici: etiket CRUD); **`boards/[boardId]/index`** Kanban veya liste (`vue-draggable-next`); **`boards/[boardId]/settings`** tablo sütunları; **`/apps/task-manager/workspace`** çalışma alanı (ağaç + liste); `issues/[key]` detay; **`issues/[key]/profile`** tam sayfa profil (yorumlar + geçmiş); **`/apps/task-manager/statuses`**, **`issue-types`**, **`priorities`**; **`/apps/task-manager/field-pool`** alan havuzu (**yönetici: CRUD**). |
| **Menü** | Yatay + dikey sidebar: **Görevler** → `/apps/task-manager` (`horizontalItems.ts`, `sidebarItem.ts`), i18n `taskManager.menuTitle`. |
| **Yetki (kısmi)** | `tm_projects.permissions` için `canViewProject` + ana listede `filterProjectsForUser` (admin hariç). Route guard / tam gizleme **tamamlanmış sayılmaz**. |
| **UI (güncel)** | Özgün **Monitra Flow** teması (`assets/css/task-manager.css`, `tm-flow`). Proje grid + arama + silme; proje düzenle/sil, board sil; Kanban’da arama / atanan / öncelik **filtreleri** (filtre açıkken sürükleme kapalı); kartlarda öncelik rengi, tip, atanan baş harfleri. Görev detayında durum, öncelik, tip, **assignee**, **etiketler** (projeye özel liste; yeni etiket proje bağlamında), **bitiş tarihi**, **story point**, **yorumlar** (yazar adı: Keeper id + `getUserById` / DG `persons` genişlemesi), silme. **Profil** sayfasında **geçmiş** sekmesi: `__history` + `showHistory` / hydrate. **Liste/tablo sütunları** ve **yeni görev** alanları, proje **`selections`** + **`tm_field_definitions`** ile uyumlu (bkz. §1.3). |
| **Proje durum akışı** | `tm_projects.workflow` (object): `statusIds[]`, `initialStatusId`, `closedStatusId`, `transitions: { [from]: to[] }`. Yeni proje oluşturulurken varsayılan: havuz sırasına göre **doğrusal komşu** geçişler. Ayar sayfası: `pages/.../projects/[id]/workflow.vue`. |
| **Eksik / sonraki** | Geçiş kuralları **sunucu tarafında MngWorkflow ile zorunlu doğrulama** (şu an yalnızca UI); dataset **kategori** ataması; predefined query POC; fr/zh/ar i18n tam eşleme. |

### 1.2 MngWorkflow ve API Gateway (altyapı)

- **Port:** 5085 (`mngworkflow` Docker servisi).  
- **Gateway:** `/workflow/api/v1/{...}` → downstream `/api/v1/{...}`; `/workflow/health`; Swagger `/workflow/swagger` (Swagger UI OpenAPI URL’si göreli, Gateway arkasında çalışır).  
- **Doğrulama:** Issue create üzerinde `projectKey` ↔ `projectId` tutarlılığı **henüz DG’ye bağlı pipeline ile zorunlu kılınmadı** (Faz 1.1 / Workflow genişlemesi).

### 1.3 Nisan 2026 — Konuşma / kod özeti (Task Manager UI)

Aşağıdaki maddeler bu dönemde uygulanmış davranışları özetler; detay için ilgili kaynak dosyalara bakın.

| Konu | Uygulama |
|------|-----------|
| **Tablo sütunları (board + workspace)** | `utils/boardTableColumns.ts`: `selectableBoardColumnIdsForProject`, `resolveBoardTableColumnIds`, `defaultBoardTableColumnIdsForProject`. Projede seçilen öncelik/tip/alanlar (`TmProject.selections`) ile uyumlu sütun kümesi; kayıtlı `board.config.tableColumns` önce izin kümesiyle süzülür. |
| **Board — tablo sütunları ayarı** | `pages/.../boards/[boardId]/settings.vue`. Kayıt sonrası **`/apps/task-manager/workspace?project=&board=`** ile çalışma alanına dönüş; `workspace/index.vue` bootstrap sonrası sorguyu okuyup ağaç seçimini uygular ve URL’yi temizler. |
| **Çalışma alanı araç çubuğu** | Manager için **Ayarlar** (önceden “Sütunlar”): tablo sütunları + **yeni görev formu** seçimi (board’da `issueCreateFormId` yoksa proje varsayılanı). Yalnızca **`auth.isManager`**. **Kanban** kısayolu: yalnızca **`projectUsesKanban`**. Liste-only projelerde ayrı “liste görünümü” butonu yok (liste zaten çalışma alanında). |
| **Board sayfası** | `boards/[boardId]/index.vue`: Tablo sütunları linki aynı yetki kuralıyla. |
| **Route koruması** | `middleware/task-manager-board-settings.ts`: tablo sütunları ayarı yalnızca manager; aksi halde workspace’e yönlendirme (istemci middleware). |
| **Alan havuzu şeması** | `tm_field_definitions`: **`cardinality`** (`single` \| `multi`), **`optionsJson`** (JSON metin). Setup: `setup-task-manager-datasets.ps1`. Tipler: `utils/taskManagerFieldDefinitions.ts` (`parseTmFieldOptionsJson`, `effectiveFieldCardinality`, `TM_POOL_FIELD_TYPE_VALUES`). |
| **Alan havuzu UI** | `pages/.../field-pool/index.vue`: Yöneticiye **oluştur / düzenle / sil** (store: `createFieldDefinition`, `updateFieldDefinition`, `deleteFieldDefinition`). `key` düzenlemede değişmez. |
| **Yeni görev modalı** | `components/apps/task-manager/TmNewIssueFormFields.vue` + `utils/taskManagerNewIssueForm.ts` (`resolveNewIssueFormRows`, `IssueFormModel`, `normalizeDueDateInput`, `pruneIssueExtraFields`). **Çalışma alanı** ve **board** sayfalarında; görev tipi/öncelik listeleri proje `selections` ile süzülür. |
| **Görev yorumları** | Dataset **`tm_issue_comments`**: `author` **persons** — DG listelerde `expand` ile `@users` genişlemesi **`foreignField: __dataId`**; kalıcı kayıtta **`author` = Keeper kullanıcı `id`/`userId`** (oluşturma/güncelleme store’da `sub` → `getUserById` çözümü). UI: `TmIssueComments.vue` (yazar etiketi: `assigneeDisplayLabel` + **`getUserById`**); görev detayı / profil. Store: `commentsByIssueId`, `loadIssueComments`, `createIssueComment`, `updateIssueComment`, `deleteIssueComment`. Mention `@[userId]`, `parentCommentId`, emoji. i18n: `taskManager.issueComments*`. |
| **Geçmiş (`__history`)** | DG varsayılan listede `__history` yok: `taskManagerService` **`showHistory`**, **`hydrateIssueWithHistory(issueId)`** (`tmGetById`); profil + `issues/[key]` mount ve **`updateIssue`** sonrası hydrate. **`utils/taskManagerIssueHistory.ts`**: `timestamp` / Mongo `$date`, `userEmail`, `userInfo`, güncelleme paketindeki **alan → yeni değer** haritası. Panel: `TmIssueHistoryPanel.vue` (`getUserById` ile aktör). |
| **Profil tam sayfa** | `issues/[key]/profile.vue` + `TmIssueProfileView.vue`: form + sağ sekme **Yorumlar** / **Geçmiş**; sağ sütun **genişletildi** (daha okunakır tablo/timeline). |
| **Proje etiketleri** | Yalnızca **`projectId` eşleşen** etiketler (`loadLabels` + seçici bileşenler). Store: **`createLabel`**, **`updateLabel`**, **`deleteLabel`**. Sayfa **`pages/.../projects/[id]/labels.vue`**; proje özeti butonu. i18n **tr/en** `taskManager.projectLabels*`. |
| **Issue create API** | `store.createIssue`: isteğe bağlı **`labels`**, **`dueDate`**, **`storyPoints`**, **`extraFields`** (gövdeye düz yayılan ek alanlar; DG şemasıyla uyum kullanıcı sorumluluğunda). |
| **Çoklu kişi alanı (ör. watchers)** | Üretim için `tm_issues` üzerinde `persons` + **`isArray: true`** ve havuzda uygun `tm_field_definitions` satırı gerekir; yalnızca havuz meta yeterli değil. |
| **Proje durum akışı (UI doğrulama)** | `utils/taskManagerWorkflow.ts` (`getEffectiveWorkflow`, `normalizeWorkflow`, `isTransitionAllowed`, …); `ProjectWorkflowEditor` + proje oluşturma/düzenleme sekmesi. Kanban sürükle-bırak ve görev detayındaki durum değişimi, `tm_projects.workflow.transitions` ile uyumlu olmayan geçişlere izin vermez. |

**Görev oluşturma düzeni (`issueCreateLayout`) — uygulandı (Nisan 2026)**

- **Kim:** Yöneticiler (`auth.isManager`), proje oluşturma / düzenlemede **Yeni görev formu** sekmesi; sürükle-bırak + Kaydet.
- **Çoklu şablon:** Projede birden fazla form tanımı: `issueCreateForms[]` (her biri `id`, `name`, `layout`), varsayılan **`defaultIssueCreateFormId`**. Board tarafında isteğe bağlı **`issueCreateFormId`**; boşsa proje varsayılanı kullanılır. Etkin layout çözümü: `resolveEffectiveIssueCreateLayout` (`taskManagerNewIssueForm.ts`).
- **Legacy tek layout:** Hâlâ desteklenir: kök **`issueCreateLayout`** (şablon listesi yokken veya migrasyon öncesi veri).
- **Alan sırası ve alan genişliği:** `layout.rows` (sütun kimlikleri sırası), isteğe bağlı **`fieldCols`** (12 sütunlu ızgarada `span`, tam satır için genelde 12 veya alan yok).
- **Bölüm sırası ve bölüm genişliği (Nisan 2026):** `layout.sectionOrder` (bölüm anahtarları sırası), `layout.sectionCols` (bölüm blokları için 12/6/4/3). Doğal sıra: `naturalSectionOrderFromLayout`; render: `TmNewIssueFormFields.vue` içinde `orderedRowSections` + dış ızgara `span`. Düzenleme: `ProjectIssueCreateLayoutEditor.vue` (“Bölüm sırası ve genişlik” kartı); taslak ve kayıt: `ProjectEditorForm.vue` (`buildLayoutPayloadFromDraft` içinde geçerli bölüm anahtarlarına göre süzme).
- **Form üst metni:** `formHeading`, `formIntro` (isteğe bağlı).
- **Bölüm eşlemesi ve başlıklar:** `columnSections` (sütun id → bölüm anahtarı), `sectionTitles` (görünen ad). Özel bölüm silinince alanlar **core**’a taşınır; `sectionOrder` / `sectionCols` ilgili anahtardan temizlenir.
- **Modal genişliği:** `dialogMaxWidth` (px); normalizasyon `normalizeDialogMaxWidthPx`. Kanban / workspace “Yeni görev” `v-dialog` **`max-width`** bu değere bağlanır.
- **Kapsam:** İzinli alan kümesi **`selections`** + **`tm_field_definitions`**; sıra birleştirmesi `mergeIssueCreateLayoutColumnIds`. Yeni havuz alanları otomatik sona eklenir.
- **Store / tip:** `stores/apps/taskManager.ts` içinde `mapIssueCreateLayout` (API’den gelen layout nesnesini UI tipine çevirir; PascalCase yedekleri varsa okunur). `types/apps/taskManager.ts` — `TmIssueCreateLayout`.
- **i18n:** `taskManager.editorIssueCreateSectionLayout*`, `editorIssueCreateSectionColWidth` vb. (`Mng.Ui/utils/locales/tr.json`, `en.json`).
- **Veri / setup:** `setup-task-manager-datasets.ps1` içinde `tm_projects.issueCreateLayout` (object); çoklu form alanları ortama göre şemaya eklenmiş olmalı. Mevcut ortamlarda alan yoksa DG şemasına eklenmeli veya script yeniden çalıştırılmalı.
- **Ertelenen (not):** **Görev tipi bazlı** formlar — sonra değerlendirme.

---

## 1. Özet ve Hedef

Jira benzeri bir Task Manager uygulaması geliştirilecek. Backend olarak **MngDataGateway (DG)** üzerindeki dataset'ler kullanılacak. Yeni bir mikroservis yazılmayacak; mevcut DG API'leri (`/data/api/v1/datasets`, `/data/api/v1/data`) kullanılacak.

**Temel Özellikler:**
- Projeler (Projects)
- Board'lar (Kanban / Sprint)
- Görev tipleri (Task, Bug, Story, Epic)
- Durum akışları (Status workflow)
- Atama (Assignee) — MngKeeper `persons` field ile
- Etiketler (Labels)
- Öncelik (Priority)
- Backlog ve Sprint yönetimi

---

## 2. DG Dataset Mimarisi

### 2.1 Kullanılacak DG Özellikleri

| Özellik | Kullanım |
|---------|----------|
| **relation** | Proje → Board, Task → Proje, Task → Status, Task → Epic |
| **persons** | Assignee (MngKeeper kullanıcı ID) |
| **incremental** | Task key (örn. `PROJ-123`) |
| **object** | Custom fields, sprint config |
| **datetime** | Created, Updated, Due date |
| **Predefined queries** | Board kolonlarına göre task listesi, backlog sorguları |
| **Index** | projectId, status, assignee, sprintId vb. |

### 2.2 Dataset Listesi (Önerilen)

| Dataset | Açıklama | Örnek Prefix |
|---------|----------|--------------|
| `tm_projects` | Projeler | — |
| `tm_boards` | Board tanımları (Kanban/Sprint) | — |
| `tm_issue_types` | Görev tipleri (Task, Bug, Story, Epic) | — |
| `tm_statuses` | Durumlar (To Do, In Progress, Done) | — |
| `tm_priorities` | Öncelikler (Low, Medium, High, Critical) | — |
| `tm_labels` | Etiketler | — |
| `tm_sprints` | Sprint tanımları | — |
| `tm_field_definitions` | Alan havuzu (meta: key, scope, cardinality, optionsJson) | — |
| `tm_issues` | Ana görev/görev kayıtları | PROJ-001 |

---

## 3. Dataset Şemaları (Detaylı)

### 3.1 tm_projects

```json
{
  "name": "tm_projects",
  "description": "Task Manager - Projeler",
  "forceSchema": true,
  "logging": "none",
  "publishMode": "none",
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Proje adı", "mandatory": true, "unique": true, "isArray": false },
    { "fieldType": "text", "name": "key", "title": "Proje kodu (PROJ)", "mandatory": true, "unique": true, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false },
    { "fieldType": "persons", "name": "lead", "title": "Proje lideri", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "avatarUrl", "title": "Avatar URL", "mandatory": false, "isArray": false },
    { "fieldType": "object", "name": "permissions", "title": "Yetkiler (view, edit, admin)", "mandatory": false, "isArray": false },
    { "fieldType": "object", "name": "selections", "title": "Havuz seçimleri (öncelik, tip, alan anahtarları)", "mandatory": false, "isArray": false },
    { "fieldType": "object", "name": "workflow", "title": "Durum akışı (statusIds, initial, closed, transitions)", "mandatory": false, "isArray": false },
    { "fieldType": "bool", "name": "useKanban", "title": "Kanban kullan", "mandatory": false, "isArray": false }
  ],
  "indexList": [
    { "name": "idx_key", "fields": { "key": 1 }, "unique": true },
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true }
  ]
}
```

**permissions yapısı:** `{ "view": { "personIds": [], "groupIds": [] }, "edit": { ... }, "admin": { ... } }` — Boş = domain içi herkes.

**selections (örnek):** Havuzdan seçilen `priorityId` / `issueTypeId` listeleri ve isteğe bağlı ek alan anahtarları (`fieldKeys`); UI ve tablo sütunları bu nesneye göre süzülür (bkz. §1.3, `boardTableColumns.ts`). **useKanban:** `false` ise proje liste odaklıdır; çalışma alanında Kanban kısayolu gösterilmez.

**workflow yapısı (örnek):** `{ "statusIds": ["id1","id2","id3"], "initialStatusId": "id1", "closedStatusId": "id3", "transitions": { "id1": ["id2"], "id2": ["id3"], "id3": [] } }` — `transitions[from]` listesinde olmayan hedefe geçiş engellenir (UI + ileride Workflow).

### 3.2 tm_boards

```json
{
  "name": "tm_boards",
  "description": "Task Manager - Board tanımları (Kanban/Sprint)",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Board adı", "mandatory": true, "isArray": false },
    { "fieldType": "relation", "name": "projectId", "title": "Proje", "mandatory": true, "relationDataset": "tm_projects", "isArray": false },
    { "fieldType": "text", "name": "type", "title": "Tip (kanban|scrum)", "mandatory": true, "isArray": false },
    { "fieldType": "object", "name": "config", "title": "Board config (kolonlar vb.)", "mandatory": false, "isArray": false }
  ],
  "indexList": [
    { "name": "idx_projectId", "fields": { "projectId": 1 }, "unique": false }
  ]
}
```

**config örneği (Kanban):**
```json
{
  "columns": [
    { "statusId": "status-todo-id", "title": "To Do", "wipLimit": null },
    { "statusId": "status-progress-id", "title": "In Progress", "wipLimit": 5 },
    { "statusId": "status-done-id", "title": "Done", "wipLimit": null }
  ]
}
```

### 3.3 tm_issue_types

```json
{
  "name": "tm_issue_types",
  "description": "Task Manager - Görev tipleri",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Tip adı", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "icon", "title": "İkon (task, bug, story, epic)", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "color", "title": "Renk (#hex)", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false }
  ],
  "indexList": [
    { "name": "idx_name", "fields": { "name": 1 }, "unique": true }
  ]
}
```

**Seed veriler:** Task, Bug, Story, Epic (varsayılan renkler ve ikonlarla).

### 3.4 tm_statuses

```json
{
  "name": "tm_statuses",
  "description": "Task Manager - Durumlar",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Durum adı", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "icon", "title": "İkon (Tabler adı)", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "color", "title": "Tema rengi (Vuetify: primary|secondary|success|info|warning|error)", "mandatory": false, "isArray": false }
  ],
  "indexList": []
}
```

**Seed veriler:** To Do, In Progress, Done (ve opsiyonel: Blocked, Review). Varsayılan seed’de ikon alanları doldurulur. **Kategori / sıra** alanları kaldırıldı; sıra proje **workflow** ile, anlamsal gruplama ileride istenirse yeniden eklenebilir.

### 3.4.1 Proje kuralları ve durum geçişleri

- **Durum havuzu** (`tm_statuses`): global liste — CRUD (`/apps/task-manager/statuses`).
- **Proje workflow** (`tm_projects.workflow`, object alanı — setup script’te tanımlı):
  - `statusIds`: Bu projede kullanılan durumlar ve **Kanban kolon sırası**.
  - `initialStatusId`: Yeni görevlerin başlangıç `statusId` değeri.
  - `closedStatusId`: Kapalı/terminal anlamı (raporlama; geçiş yasağı için `transitions` kullanılır).
  - `transitions`: Kaynak `__dataId` → izin verilen hedef `__dataId` listesi (**yönlü graf**). Örn. To Do → Done doğrudan yok; Closed → To Do yok — ilgili kenarlar tanımlanmaz.
- **UI:** `/apps/task-manager/projects/:id/workflow` — seçim, sıra (sürükle-bırak), başlangıç/kapalı seçimi, durum başına hedef çoklu seçim; “Geçişleri sıralı komşu yap” ve “Havuz sırasına sıfırla” kısayolları.
- **Kanban / görev detayı:** İzin verilmeyen sürükleme veya kayıtta uyarı; detayda durum listesi mevcut durum + izin verilen hedeflerle sınırlı.
- **Sunucu:** İdeal olarak aynı kurallar **MngWorkflow** ile `tm_issues` güncellemesinde doğrulanır (UI tek başına yeterli değildir) — sıradaki backend adımı.

### 3.5 tm_priorities

```json
{
  "name": "tm_priorities",
  "description": "Task Manager - Öncelikler",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Öncelik adı", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "icon", "title": "İkon", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "color", "title": "Renk", "mandatory": false, "isArray": false }
  ]
}
```

**Seed veriler:** Highest, High, Medium, Low, Lowest. **`order` alanı kaldırıldı**; anlam için `description` (ve ad) kullanılır.

### 3.5.1 tm_field_definitions (alan havuzu)

`tm_issues` ile uyumlu alan anahtarlarının meta tanımı: hangi alanın **temel** (tüm projelerde) veya **havuz** (projede seçilebilir) olduğu, veri tipi (`fieldType`: text, number, datetime, persons, relation, **tags**, file, …), **cardinality** (`single` \| `multi`), isteğe bağlı **optionsJson** (min/max, `relationDataset`, dosya limitleri). **Seed / script** ile ilk kayıtlar; **Mng.Ui** alan havuzu sayfasında yönetici (**`auth.isManager`**) **CRUD** yapabilir. **UI:** `/apps/task-manager/field-pool`. **Kod:** `types/apps/taskManager.ts` (`TmFieldDefinition`), `utils/taskManagerFieldDefinitions.ts`, `stores/apps/taskManager.ts` (alan havuzu aksiyonları). **Yeni görev formu** bu meta + proje `selections` ile dinamik (bkz. §1.3).

```json
{
  "name": "tm_field_definitions",
  "description": "Task Manager - Alan havuzu (meta)",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "key", "title": "Alan anahtarı", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "label", "title": "Görünen ad", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "fieldType", "title": "Veri tipi", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "scope", "title": "Kapsam (core|pool)", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "cardinality", "title": "Seçim (single|multi)", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "optionsJson", "title": "Seçenekler (JSON)", "mandatory": false, "isArray": false },
    { "fieldType": "number", "name": "sortOrder", "title": "Sıra", "mandatory": false, "isArray": false }
  ]
}
```

### 3.6 tm_labels

```json
{
  "name": "tm_labels",
  "description": "Task Manager - Etiketler",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Etiket adı", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "color", "title": "Renk (#hex)", "mandatory": false, "isArray": false },
    { "fieldType": "relation", "name": "projectId", "title": "Proje (boş=global)", "mandatory": false, "relationDataset": "tm_projects", "isArray": false }
  ],
  "indexList": [
    { "name": "idx_projectId_name", "fields": { "projectId": 1, "name": 1 }, "unique": true }
  ]
}
```

### 3.7 tm_sprints

```json
{
  "name": "tm_sprints",
  "description": "Task Manager - Sprint tanımları (Scrum)",
  "forceSchema": true,
  "fields": [
    { "fieldType": "text", "name": "name", "title": "Sprint adı", "mandatory": true, "isArray": false },
    { "fieldType": "relation", "name": "boardId", "title": "Board", "mandatory": true, "relationDataset": "tm_boards", "isArray": false },
    { "fieldType": "datetime", "name": "startDate", "title": "Başlangıç", "mandatory": true, "isArray": false },
    { "fieldType": "datetime", "name": "endDate", "title": "Bitiş", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "goal", "title": "Sprint hedefi", "mandatory": false, "isArray": false },
    { "fieldType": "text", "name": "state", "title": "Durum (future|active|closed)", "mandatory": true, "isArray": false }
  ],
  "indexList": [
    { "name": "idx_boardId_state", "fields": { "boardId": 1, "state": 1 }, "unique": false },
    { "name": "idx_boardId_startDate", "fields": { "boardId": 1, "startDate": 1 }, "unique": false }
  ]
}
```

### 3.8 tm_issues (Ana Görev Tablosu)

```json
{
  "name": "tm_issues",
  "description": "Task Manager - Görevler",
  "forceSchema": true,
  "logging": "self",
  "publishMode": "basic",
  "fields": [
    { "fieldType": "incremental", "name": "key", "title": "Görev kodu", "mandatory": true, "unique": true, "isArray": false,
      "incrementalOptions": { "format": "{projectKey}-{0:D4}", "startValue": 1, "incrementStep": 1 } },
    { "fieldType": "text", "name": "projectKey", "title": "Proje kodu (key üretimi için)", "mandatory": true, "isArray": false },
    { "fieldType": "relation", "name": "projectId", "title": "Proje", "mandatory": true, "relationDataset": "tm_projects", "isArray": false },
    { "fieldType": "relation", "name": "issueTypeId", "title": "Görev tipi", "mandatory": true, "relationDataset": "tm_issue_types", "isArray": false },
    { "fieldType": "text", "name": "title", "title": "Başlık", "mandatory": true, "isArray": false },
    { "fieldType": "text", "name": "description", "title": "Açıklama", "mandatory": false, "isArray": false },
    { "fieldType": "relation", "name": "statusId", "title": "Durum", "mandatory": true, "relationDataset": "tm_statuses", "isArray": false },
    { "fieldType": "relation", "name": "priorityId", "title": "Öncelik", "mandatory": false, "relationDataset": "tm_priorities", "isArray": false },
    { "fieldType": "persons", "name": "assignee", "title": "Atanan", "mandatory": false, "isArray": false },
    { "fieldType": "relation", "name": "epicId", "title": "Epic", "mandatory": false, "relationDataset": "tm_issues", "isArray": false },
    { "fieldType": "relation", "name": "sprintId", "title": "Sprint", "mandatory": false, "relationDataset": "tm_sprints", "isArray": false },
    { "fieldType": "relation", "name": "labels", "title": "Etiketler", "mandatory": false, "relationDataset": "tm_labels", "isArray": true },
    { "fieldType": "datetime", "name": "dueDate", "title": "Bitiş tarihi", "mandatory": false, "isArray": false },
    { "fieldType": "number", "name": "storyPoints", "title": "Story points", "mandatory": false, "isArray": false },
    { "fieldType": "number", "name": "order", "title": "Sıra (board kolonu içinde)", "mandatory": false, "isArray": false }
  ],
  "indexList": [
    { "name": "idx_projectId", "fields": { "projectId": 1 }, "unique": false },
    { "name": "idx_statusId", "fields": { "statusId": 1 }, "unique": false },
    { "name": "idx_assignee", "fields": { "assignee": 1 }, "unique": false },
    { "name": "idx_sprintId", "fields": { "sprintId": 1 }, "unique": false },
    { "name": "idx_epicId", "fields": { "epicId": 1 }, "unique": false },
    { "name": "idx_projectId_key", "fields": { "projectId": 1, "key": 1 }, "unique": true }
  ]
}
```

**Not:** DG `IncrementalFieldService` `{fieldName}` placeholder destekliyor. `projectKey` create request'te gönderilir.

---

## 4. Incremental Key Stratejisi

DG'nin `IncrementalFieldService`'i **`{fieldName}` placeholder** destekliyor. Yani `data` içinde gönderilen alan adları format'ta kullanılabilir.

**Format:** `"{projectKey}-{0:D4}"`  
**Counter key:** `tm_issues.key.PROJ` (proje bazlı izolasyon)

**Gereksinim:** Issue oluştururken client `projectKey` değerini göndermeli (örn. `tm_projects.key`). Bu alan `tm_issues` şemasında **zorunlu değil** (incremental işlenirken kullanılır, saklanmayabilir) — ancak format çözümü için create request'te bulunmalı.

**İki yaklaşım:**

| Seçenek | Açıklama |
|---------|----------|
| **A) projectKey sakla** | `tm_issues`'a `projectKey` (text) field eklenir, mandatory. Client her create'te project'tan key alıp gönderir. Incremental format: `{projectKey}-{0:D4}`. |
| **B) projectKey sadece create'te** | `projectKey` schema'da yok; client sadece create body'de gönderir. DG incremental işlerken `data["projectKey"]` kullanır. **Ancak** DG şu an sadece schema'daki field'ları kabul ediyor olabilir (forceSchema). Bu durumda A tercih edilmeli. |

**Öneri:** **A** — `projectKey` field'ı ekle (text, mandatory). Proje ile tutarlılık için validation ile `projectId`'ye karşılık gelen projenin key'i ile eşleşmeli.

---

## 5. UI Mimarisi

### 5.1 Sayfa Yapısı (Mng.Ui)

**Uygulanan yapı (Nisan 2026):**

```
pages/apps/task-manager/
├── index.vue                      # Proje listesi + yeni proje
├── projects/index.vue             # Projeler listesi (ayrı rota)
├── projects/new.vue               # Yeni proje sihirbazı
├── workspace/index.vue            # Çalışma alanı (ağaç, board listesi, tablo; yeni görev modalı)
├── assigned.vue                   # Bana atananlar (varsa bu rota)
├── projects/[id]/                 # Proje detay
│   ├── index.vue
│   ├── edit.vue                   # Proje düzenleme (workflow sekmesi dahil)
│   └── workflow.vue               # Durum akışı (ayrı sayfa)
├── boards/[boardId]/
│   ├── index.vue                  # Kanban veya liste (board); yeni görev modalı
│   └── settings.vue               # Tablo sütunları (board.config.tableColumns)
├── field-pool/index.vue           # Alan havuzu (tm_field_definitions), yönetici CRUD
├── statuses/, issue-types/, priorities/
└── issues/[key].vue               # Görev detay
```

**Not:** `issues/create` ayrı sayfa yerine modal akışı kullanılıyor. **Planda kalan / sonraki:** `backlog` ayrı sayfa, form **layout designer** (§1.3), `projects/index` birleştirme ihtiyacına göre.

### 5.2 Bileşen Yapısı

```
components/apps/task-manager/
├── TmNewIssueFormFields.vue      # Yeni görev — proje seçimlerine göre dinamik alanlar
├── TmWorkspaceTree.vue           # Çalışma alanı sol ağaç
├── ProjectEditorForm.vue
├── ProjectCard.vue
├── Board/
│   ├── KanbanBoard.vue
│   ├── KanbanColumn.vue
│   ├── KanbanCard.vue
│   ├── SprintBoard.vue
│   └── BacklogPanel.vue
├── Issue/
│   ├── IssueCard.vue
│   ├── IssueDetail.vue
│   ├── IssueForm.vue
│   └── IssueTypeBadge.vue
├── Common/
│   ├── AssigneeAvatar.vue
│   ├── PriorityBadge.vue
│   ├── StatusBadge.vue
│   └── LabelChip.vue
└── Toolbar/
    ├── BoardToolbar.vue
    └── FilterBar.vue
```

### 5.3 Store Yapısı

**Uygulanan:** tek modül `stores/apps/taskManager.ts` (proje, board, issue, statuses/issueTypes/priorities, **fieldDefinitions**; `filterProjectsForUser`, `createProject`, `createBoard`, `loadIssues`, **`createIssue`** (labels/dueDate/storyPoints/extraFields), `updateIssue`, `fetchIssueByKey`; alan havuzu **`createFieldDefinition` / `updateFieldDefinition` / `deleteFieldDefinition`**).

**Plana uygun ayrıştırma (isteğe bağlı):** ileride `project` / `board` / `issue` / `lookup` dosyalarına bölünebilir.

### 5.4 API Servisleri

- `taskManagerService.ts`: DG `/data/api/v1/data/{dataset}` çağrıları  
- Yardımcılar: `utils/boardTableColumns.ts`, `utils/taskManagerFieldDefinitions.ts`, `utils/taskManagerNewIssueForm.ts`, `middleware/task-manager-board-settings.ts`
- Mevcut `$fetch` veya `useApi` pattern'i kullanılacak
- Gateway üzerinden: `/data/api/v1/...` (JWT + domain)

---

## 6. Özellik Fazları ve Yol Haritası

### 6.1 Öneriler (Yol Haritasına Dahil)

Aşağıdaki öneriler yol haritasının parçası olarak uygulanacaktır:

| # | Öneri | Uygulama |
|---|-------|----------|
| 1 | **Setup script** | `setup-monitoring-datasets.ps1` referans alınarak `scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1` yazılacak. **9 dataset** + seed veriler (issue types, statuses, priorities, `tm_field_definitions`) tek script ile oluşturulacak. |
| 2 | **Key stratejisi** | `projectKey` alanı create sırasında client tarafından gönderilecek. DG incremental `{projectKey}-{0:D4}` ile proje bazlı sıra üretecek (örn. PROJ-0001). |
| 3 | **UI referansları** | Side Menu Manager ve Organization sayfası — tree + form panel yapısı referans alınacak. Benzer layout (Toolbar + 2 kolon: tree/form) kullanılacak. |
| 4 | **Real-time güncellemeler** | Faz 2+ için `tm_issues` dataset'inde `publishMode: "basic"` ile RabbitMQ event'leri. MngHub SignalR ile board üzerinde anlık güncellemeler. |
| 5 | **Side Menu entegrasyonu** | "Task Manager" veya "Görevler" menü öğesi Side Menu Manager üzerinden eklenebilir veya sabit route olarak tanımlanacak. |
| 6 | **Dataset kategorisi** | "Task Manager" adında bir Dataset Kategori oluşturulup tüm tm_* dataset'leri altına alınacak (monitoring örneğindeki gibi). |

### 6.2 Faz 1 — Temel (MVP)
- [x] **Setup script** — `setup-task-manager-datasets.ps1` (9 dataset + seed)
- [ ] **Dataset kategorisi** — "Task Manager" kategori oluşturup `tm_*` dataset’lerini altına alma (script şu an yalnızca öneri mesajı verir; UI veya API ile manuel)
- [x] Seed veriler (issue types, statuses, priorities) — script ile
- [x] Proje **CRUD** (oluşturma, listeleme, düzenleme, silme + bağlı issues/boards/labels temizliği)
- [x] Board **oluşturma + listeleme + silme** (Kanban varsayılan kolon config) — [ ] board adı düzenleme (isteğe bağlı)
- [x] Issue **tam CRUD** (`projectKey` ile incremental key); detayda öncelik, tip, assignee, etiket, tarih, story point
- [x] Kanban görünümü (`tm_boards.config` veya tüm `tm_statuses`)
- [x] Drag & drop — kolonlar arası **statusId** + kolon içi / çoklu kolon **`order`** kalıcılığı (Kanban `persistOrdersForColumns`)
- [x] Assignee (**persons**) — issue detay + yeni görev dialog’u; Kanban kartında baş harf
- [x] Basit filtreleme — Kanban’da metin / atanan / öncelik
- [x] Side Menu — **Görevler** → `/apps/task-manager` (sidebar + horizontal)

### 6.2.1 Faz 1 — Netleştirme adımları ve varsayılan kararlar

Aşağıdaki maddeler **Nisan 2026** itibarıyla netleştirilmiş kabul edilir; ürün sahibi farklı seçerse yalnızca ilgili satır güncellenir.

| Adım | Konu | Karar |
|------|------|--------|
| 1 | **Route / menü** | Uygulama route kökü: **`/apps/task-manager`**. Side menu etiketi: **Görevler** (i18n). Sayfa içi başlıkta "Task Manager" kullanılabilir. |
| 2 | **MngWorkflow ve Faz 1** | Faz 1 MVP için **Workflow zorunlu tutulmaz** (blokaj değil). `projectKey` istemcide **seçilen projeden** set edilir ve DG create ile gider. **Sunucu tarafı `projectKey` / proje tutarlılığı** doğrulaması **MngWorkflow pipeline hazır olduğunda** veya **Faz 1.1** olarak eklenir. *Risk:* Kötü niyetli veya hatalı client; Faz 1 için bilinçli kabul. |
| 3 | **Issue detayı (Faz 1)** | `pages/apps/task-manager/issues/[key].vue` — paylaşılabilir URL; başlık, açıklama, durum (workflow geçiş kuralları), öncelik, tip, assignee, etiketler, tarih, story point. **Sunucu tarafı** workflow doğrulaması hâlâ **Faz 1.1**; “tam form” / layout designer **§1.3 planı**. |
| 4 | **Drag & drop** | Issue güncelleme: **`statusId`** ve kolon içi sıra için **`order`** (DG PUT/PATCH). Optimistic UI isteğe bağlı. |
| 5 | **Board issue listesi (Faz 1)** | Önce **`projectId`** (ve gerektiğinde `statusId`) ile DG sorgusu veya çoklu istek; kolon eşlemesi **`tm_boards.config`**. Dataset **predefined query** ile tek round-trip **POC**: Faz 1 sonu veya erken Faz 2. |
| 6 | **Proje yetkileri (Faz 1)** | `tm_projects.permissions`: yetkisiz kullanıcı için proje **listede gösterilmez** (403 spam yerine gizleme); düzenleme ekranına sızmayı UI route guard ile engelleme. |
| 7 | **Yorum / mention** | **Faz 3**; yorumlar **`tm_comments`** ayrı dataset (Bölüm 7.5 ile uyumlu; karar verildi). |

**Netleştirme özeti:** Faz 1 = DG + Mng.Ui + Side Menu; Workflow doğrulaması takip eder; yorum/mention Faz 3.

### 6.2.2 Faz 1 — Uygulama sırası (öneri)

1. [x] `setup-task-manager-datasets.ps1` çalıştır; [ ] DG’de **Task Manager** dataset kategorisi (UI veya kategori API’si).
2. [x] `taskManagerService.ts` + Pinia `taskManager` store.
3. [x] Proje listesi / oluşturma; board listesi ve Kanban sayfası.
4. [x] Issue oluşturma / listeleme; `projectKey` + incremental key akışı.
5. [x] Kanban kolonları; drag & drop ile **statusId** + **`order`** güncelleme. [ ] Predefined query POC.
6. [x] Assignee + basit filtre (Kanban araç çubuğu); [x] issue detay route (`issues/[key].vue`) — zengin alanlar Faz 1’de mevcut; **sunucu doğrulaması** hâlâ Faz 1.1.
7. [x] Side menu **Görevler** → `/apps/task-manager`.
8. [ ] **MngWorkflow** validation: create/update’te `projectKey` ↔ `projectId` (Faz 1.1).

### 6.3 Faz 2 — Gelişmiş
- [ ] Sprint ve Backlog (Scrum)
- [ ] Epic → Story ilişkisi
- [ ] Labels — *Faz 1’de issue’da etiket atanır; “labels dataset yönetimi / global arama” Faz 2*
- [ ] Öncelik — *Faz 1’de atanır; öncelik havuzu CRUD ayrı sayfa mevcut; iş kuralları genişletmesi Faz 2*
- [ ] Due date — *Faz 1’de issue’da mevcut; takvim/rapor Faz 2*
- [ ] Arama ve gelişmiş filtreler
- [ ] Issue detay sayfası (tam form) — *Faz 1 minimal+; layout designer / ek alanlar sonrası “tam” sayılır*
- [ ] **Real-time** — tm_issues publishMode: basic, MngHub SignalR board güncellemeleri

### 6.4 Faz 3 — İyileştirmeler
- [ ] Yorumlar (`tm_comments` dataset)
- [ ] **Global mentions** — `mentions` (veya benzeri) dataset: `persons` + `personGroups`, rich text ile uyum, MngWorkflow doğrulama; sorgulanabilir mention listesi
- [ ] **Bildirimler** — genel + mention/yorum (tasarım ayrı; RabbitMQ events → MngNotifier)
- [ ] Bağlantılar (issue links: blocks, relates to)
- [ ] Aktivite geçmişi (__history zaten var)
- [ ] Dashboard / raporlama

---

## 7. Teknik Notlar

### 7.1 DG Predefined Queries

Board kolonlarına göre issue listesi için örnek query:

```json
{
  "name": "board_issues_by_status",
  "parameters": [
    { "name": "boardId", "type": "text", "required": true },
    { "name": "statusIds", "type": "text", "required": true }
  ],
  "pipeline": [
    { "$match": { "projectId": { "$in": ["..."] }, "statusId": { "$in": ["..."] } } },
    { "$sort": { "order": 1, "createInfo.createdAt": 1 } },
    { "$lookup": { "from": "tm_statuses", "localField": "statusId", "foreignField": "__dataId", "as": "status" } },
    { "$unwind": { "path": "$status", "preserveNullAndEmptyArrays": true } }
  ]
}
```

Not: `boardId` ile önce board'dan projectId alınır; pipeline'da parametre kullanımı DG'nin query execution'ına bağlı.

### 7.2 Domain İzolasyonu

- Tüm dataset'ler JWT'deki domain'e göre `mng_{domain}` veritabanında saklanır.
- Projeler domain içinde izole.

### 7.3 MngKeeper Entegrasyonu

- `persons` field (assignee): MngKeeper user ID. DG GET response'da expand edilir (username, firstName, lastName vb.).
- Keeper API: `/keeper/api/...` (Gateway üzerinden).

### 7.4 Side Menu

- Yeni menü öğesi: "Task Manager" veya "Görevler"
- Side Menu Manager üzerinden eklenebilir veya sabit route.

### 7.5 Global Mentions (@mention)

Task Manager ile sınırlı değil; **ürün genelinde** (issue açıklaması, yorum, ileride diğer modüller) kullanılacak mention’lar için ortak bir model.

**Hedefler**

- Issue **description** ve **`tm_comments`** gövdesinde **rich text**; mention hem görünür metinde hem **sorgulanabilir yapıda** tutulur.
- **Kişi mention:** DG **`persons`** field type.
- **Grup mention:** DG **`personGroups`** field type — **referans** (kayıtta üyelik expand’i yok; üyelik değişince geçmiş satırları yeniden yazma yok). GET sırasında DG’nin `@groups` lookup ile zenginleştirmesi kullanılabilir.
- **Merkezi kayıt:** Tek bir global dataset (ör. `mentions`) içinde kaynak adresleme ile; böylece “kim kimi / hangi grubu nerede mention etti?” ve “beni etkileyen mention’lar” sorguları task manager dışı kaynakları da kapsar.

**Önerilen adresleme alanları (kavramsal)**

| Alan | Açıklama |
|------|----------|
| `sourceDataset` | Örn. `tm_issues`, `tm_comments` |
| `sourceRecordId` | İlgili kaydın `__dataId` değeri |
| `sourceField` | Örn. `description`, `body` |
| `mentionedPersons` | `persons` — bir veya çoklu (şema `isArray` ile) |
| `mentionedGroups` | `personGroups` — grup referansları |

Aynı kaynak güncellenince mention satırları **yeniden üretilir** veya diff ile senkronlanır (uygulama/Workflow stratejisi).

**MngWorkflow**

- Create/update sırasında: geçerli kullanıcı / grup id’leri, mention etmeye **yetki**, rich text ile gönderilen yapısal id setinin **tutarlılığı** (client’a kör güven yok).
- Task Manager’a özel kurallar minimumda; DG generic kalır.

**“Beni etkileyen mention’lar” (grup referansı)**

- Doğrudan `persons` içinde arama + **`personGroups` ile eşleşen** kayıtlar için sorgu anında veya uygulama katmanında **grup üyeliği** ile birleştirme (referans modelinin doğal maliyeti).

**Bildirimler**

- Mention tetikli bildirimler **ayrı tasarlanır**; **Faz 3** ile uyumlu (MngNotifier / event pipeline). Grupta bildirim: o anki üyelik expand’i bildirim katmanında değerlendirilebilir.

**Yorumlar (`tm_comments`) ile ilişki**

- Yorum **ayrı dataset kaydı** olduğunda her yorumun stabil `__dataId`’si vardır; `mentions.sourceRecordId` ile adreslemek doğal. Gömülü yorum dizisi bu modelle zorlaşır; global mention + ayrı `tm_comments` birbirini destekler.

---

## 8. Setup Script Örneği

**Yol haritası:** Faz 1 — Setup script (Bölüm 6.2)

**Script:** [`scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1`](../../../scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1) — **9 dataset** + seed (issue types, statuses, priorities, `tm_field_definitions`). `setup-monitoring-datasets.ps1` ile aynı token/Gateway mantığı.

**Parametreler:** Token (load-token.ps1), BaseUrl, UseGateway — monitoring script ile aynı mantık.

**İçerik:** 9 dataset + seed (tm_issue_types: Task, Bug, Story, Epic; tm_statuses: To Do, In Progress, Done; tm_priorities: Highest → Lowest; `tm_field_definitions` şema + isteğe bağlı seed).

---

## 9. Açık Sorular ve Kararlar

| Konu | Seçenekler | Karar |
|------|------------|-------|
| Key formatı | incremental vs text + app logic | **incremental** — `{projectKey}-{0:D4}`, client create'te projectKey gönderir |
| projectKey validation | DG vs ayrı servis | **MngWorkflow** validation pipeline. DG generic kalacak. |
| Proje yetkileri | view, edit, admin | `tm_projects.permissions` — personIds, groupIds. Faz 1: UI filtreleme. |
| Yorumlar | Ayrı dataset vs object array | **Ayrı `tm_comments`** (Faz 3); global mentions ile uyum (Bölüm 7.5). |
| Mentions (@mention) | Gömülü vs global dataset; kişi vs grup | **Global `mentions` dataset**; kişi: DG **`persons`**; grup: DG **`personGroups`** (referans, expand yok). Rich text + Workflow doğrulama. Bildirim Faz 3. |
| Real-time | SignalR (MngHub) | Faz 2+ için tm_issues publishMode: basic, MngHub ile board güncellemeleri |
| Board config | Her board kendi status set'i mi? | Başlangıçta global statuses; proje bazlı sonra |
| Setup script | Ayrı script vs mevcut script'e ekleme | Ayrı `setup-task-manager-datasets.ps1` (monitoring örneği gibi) |
| UI referansı | Hangi sayfalar referans? | Side Menu Manager, Organization (tree + form panel) |

---

## 10. Sonraki Adımlar

1. **Dataset kategorisi** — "Task Manager" kategorisi oluştur; `tm_*` dataset’lerini bağla (Bölüm 6.1 öneri 6).
2. **Görev oluşturma düzeni (`issueCreateLayout`)** — §1.3: yönetici, proje bazında sıralı alan listesi; görev tipi bazlı layout **ertelendi**; `fieldKeys` / `selections` ile uyum.
3. **Proje kuralları / durum geçişleri** — Bölüm 3.4.1: **UI** (workflow editörü + Kanban/detay doğrulaması) mevcut; **sunucuda** MngWorkflow veya ek doğrulama (**Faz 1.1**).
4. [x] **Kanban `order`** — Kolon içi ve kolonlar arası sürüklemelerde `order` + `statusId` DG’ye yazılıyor (`boards/[boardId]/index.vue`).
5. [x] **Assignee + basit filtre (Faz 1 UI)** — Issue detay / Kanban kartı / araç çubuğu filtreleri mevcut. Kalan: gelişmiş kullanıcı seçici, `userStore` ile zengin liste (**Faz 2** UX).
6. [x] **Basit filtre** — Kanban’da metin / atanan / öncelik (bkz. §6.2). Çalışma alanı tablosunda ek filtreler istenirse ayrı iş kalemi.
7. **MngWorkflow** — `tm_issues` create/update için `projectKey`–`projectId` tutarlılık pipeline’ı (Faz 1.1).
8. **Silme / düzenleme** — Proje, board, issue silme; proje/board ayarları (Faz 1 tamamlayıcı).
9. **Proje yetkileri** — Route guard + listede gizleme tamamlanması.
10. **Faz 2** — Sprint, backlog, labels, öncelik UI, real-time; predefined query POC.
11. **Faz 3** — `tm_comments`, global **`mentions`**, bildirimler.
12. **Breadcrumb (toplu)** — Task Manager sayfalarında `BaseBreadcrumb` + yerel `mt()` / `breadcrumbs` dizileri; i18n anahtarları (`breadcrumbs.*`, `taskManager.*`) ve hiyerarşi **tek turda** netleştirilecek (**§12**).

---

## 12. Breadcrumb — toplu düzenleme (bekleyen iş)

**Durum:** Şu an her sayfa kendi breadcrumb dizisini üretiyor (`BaseBreadcrumb`, çoğunlukla `mt('breadcrumbs.home', …)` + Task Manager yolu). Tutarlılık, tam çeviri eşlemesi ve isteğe bağlı üst seviye (ör. “Uygulamalar”) **bilinçli olarak ertelendi**.

**Sonraki turda yapılacaklar (öneri checklist):**

- Tüm `pages/apps/task-manager/**/*.vue` dosyalarında breadcrumb tanımlarını listelemek.
- Ortak yardımcı: örn. `useTaskManagerBreadcrumbs()` veya `taskManagerBreadcrumbs.ts` ile tekrarı azaltmak; proje/board/issue adlarının asenkron yüklenmesi gerekiyorsa davranışı netleştirmek.
- i18n: `utils/locales/*` içinde `breadcrumbs` / `taskManager` altında eksik anahtarları tamamlamak (fr/zh/ar dahil, politika neyse).
- `default.vue` / layout ile çakışma yoksa doğrulamak; mobilde uzun yol kırpma ihtiyacı varsa not almak.

Bu madde **§10.12** ile aynı işi işaret eder; kod değişikliği yapılmadan yalnızca plan notu olarak tutulur.

---

## 11. Checkpoint — Durak Noktası (25 Şubat 2026)

**Buraya dönülecek.** Workflow çalışması başka bir chat'te yapılacak.

### Alınan Kararlar

| Konu | Karar |
|------|-------|
| **DG konumu** | DG genel veri erişim katmanı olarak kalacak. Task Manager veya domain-specific endpoint'ler DG'de olmayacak. |
| **projectKey validation** | MngWorkflow validation pipeline ile yapılacak. |
| **Proje yetkileri** | `view`, `edit`, `admin` — `tm_projects.permissions` ile `personIds`, `groupIds`. Faz 1'de sadece UI'da filtreleme. |
| **Servis seçimi** | MngTaskManager yok. **MngWorkflow** (ayrı servis) + DG kullanılacak. |
| **Global mentions** | Merkezi dataset (`mentions`); **`persons`** + **`personGroups`** (grup referansı); rich text; Workflow doğrulama; bildirim Faz 3. |
| **Faz 1 Workflow** | MVP’de Workflow **zorunlu değil**; `projectKey` doğrulaması sonraki adım (Bölüm 6.2.1). |
| **Yorumlar** | **`tm_comments`** ayrı dataset (Faz 3); issue içi dizi yok. |

### Bekleyen / Ertelenen

- **projectKey validation** — MngWorkflow pipeline ile DG create/update öncesi/sonrası bağlama (Faz 1.1).
- Global `mentions` dataset şeması ve setup (Faz 3 öncesi tasarım Bölüm 7.5).
- **Breadcrumb** — Task Manager sayfalarında tek turda hizalama ve i18n (**§12**, **§10.12**).

### Güncelleme (3 Nisan 2026)

- MngWorkflow **Docker** (5085) + **API Gateway** rotaları (`/workflow/...`); geliştirme ortamında kullanıma hazır.
- Mng.Ui Task Manager **Faz 1 iskeleti** (Bölüm 1.1) — tam liste **§6.2** checklist’inde.

### Güncelleme (16 Nisan 2026)

- Belge **9 dataset**, `tm_projects.selections` / `useKanban`, **§6.2.2** ve **§10** maddeleri gerçek kod durumuyla hizalandı; üste **el değiştirme** tablosu eklendi.

### Dönüldüğünde

1. **projectKey** validation pipeline’ını DG + MngWorkflow ile netleştir ve uygula.
2. Proje yetkileri (route guard, liste filtreleri) ve Faz 1 eksikleri (**§10**).
3. Faz 2/3 maddeleri (Bölüm 6.3–6.4, Bölüm 7.5).

---

**İlgili Dokümanlar:**
- [Workflow Planlama](../workflow/WORKFLOW_PLANNING.md)
- [MngDataGateway TECHNICAL_SPECS](../MngDataGateway/main/TECHNICAL_SPECS.md)
- [Dataset UI Design](../Mng.Ui/support/specs/DATASET_UI_DESIGN.md)
- [ORGANIZATION_PAGE_SPEC](../Mng.Ui/support/specs/ORGANIZATION_PAGE_SPEC.md) — Tree + Form panel referansı
- [setup-monitoring-datasets.ps1](../../../scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1)
- [setup-task-manager-datasets.ps1](../../../scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1)
