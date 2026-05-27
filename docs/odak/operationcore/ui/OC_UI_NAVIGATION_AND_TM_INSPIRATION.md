# Operation Core UI — Task Manager ilhamı, side menu ve breadcrumb

**Son güncelleme:** 26 Mayıs 2026  
**İlişkili:** [OC_UI_PHASE1_PLAN.md](./OC_UI_PHASE1_PLAN.md) · [TASK_MANAGER_PLANNING.md](../../../content/task_manager/TASK_MANAGER_PLANNING.md)

OC, Task Manager (TM) sayfalarını **kopyalamaz**; TM’de geliştirdiğimiz UX desenleri **referans** alınır. Side menu ve breadcrumb OC için **baştan** tasarlanır — TM’de ertelenen breadcrumb borcu tekrarlanmaz.

---

## 1. Task Manager envanteri (fikir kaynağı)

| TM dosya / alan | Ne iyi çalıştı? | OC karşılığı | Faz |
|-----------------|-----------------|--------------|-----|
| `pages/.../workspace/index.vue` | Sol **ağaç** (proje → board), yeniden boyutlanabilir panel, liste/kanban ana alan, URL `?project=&board=` | **`/operation-core/workspace`** — workspace → board ağacı | **Faz 1** |
| `TmWorkspaceTree.vue` | Kök gruplar (Projeler / Filtreler), chevron, seçim vurgusu | `OcWorkspaceTree` — workspace → board; filtreler sprint 2+ | Faz 1 / 2 |
| `useResizableTreePanel.ts` | localStorage genişlik + collapse | Aynı composable, key: `operation-core-workspace-tree` | Faz 1 |
| `pages/.../projects/index.vue` | Arama + tablo/grid proje listesi | Hub veya workspace üstü **workspace kartları** (DG `op_workspaces`) | Faz 1 |
| `ProjectEditorForm.vue` | **Sekmeli sihirbaz**: genel, workflow, selections, permissions, form layout editörleri | **Faz 2** metadata admin (`OcWorkspaceEditor`) — Faz 1’de yok | Faz 2 |
| `ProjectIssueCreateLayoutEditor.vue` | Sürükle-bırak form satır/bölüm, `sectionOrder`, `fieldCols` | Faz 2 form designer; Faz 1 MO `FormRuntimeContext` render | Faz 1 render only |
| `ProjectWorkflowEditor.vue` | Görsel geçiş matrisi | **Kullanılmaz** — OC geçişler `op_state_flows` + MO resolve | — |
| `boards/[boardId]/index.vue` | Kanban + filtreler + yeni görev dialog | `OcBoardKanban` — transition MO’dan | Faz 1 |
| `issues/[key]/profile.vue` | Tam sayfa profil, sağ panel sekmeler | `OcProfilePage` — timeline/segments MO | Faz 1 |
| `TmIssueComments.vue` / `TmIssueHistoryPanel.vue` | Yorum + geçmiş UX | Adapt — veri MO timeline | Faz 1 |
| `assets/css/task-manager.css` (`tm-flow`) | Görsel kimlik | `operation-core.css` (`oc-flow`) — **ayrı** tema sınıfı | Faz 1 |

**Bilinçli olarak TM’den alınmayanlar:** `taskManagerWorkflow.ts`, DG issue CRUD, proje `workflow` object’i UI’da doğrulama.

---

## 2. OC bilgi mimarisi — giriş noktası

TM’de kullanıcılar çoğunlukla **Çalışma alanı** (`/workspace`) üzerinden gezinir; proje listesi ikincil.

OC için aynı mantık:

```text
Side menu “Operasyon Merkezi”
    └─ Çalışma alanı     → /apps/operation-core/workspace   ← birincil giriş
    └─ (opsiyonel) Hub   → /apps/operation-core             ← kart özeti / kısayol
```

Board ve profil derin link ile açılır; explorer seçimi URL ile senkron kalır.

### 2.1 Workspace explorer (TM workspace’ten ilham)

**Layout (3 bölge):**

```text
┌─────────────────────────────────────────────────────────────┐
│ BaseBreadcrumb                                              │
├──────────────┬──────────────────────────────────────────────┤
│ OcWorkspace  │  Toolbar: arama, yeni iş, yenile, board link │
│ Tree         ├──────────────────────────────────────────────┤
│ (resize)     │  Board embed (kanban/list) VEYA              │
│              │  “Board seçin” empty state                   │
└──────────────┴──────────────────────────────────────────────┘
```

**Ağaç düğümleri (Faz 1):**

```text
Workspaces (kök)
  └─ {workspace name} [{key}]
       └─ {board name}
```

**Faz 2+ filtre kökü (TM `Filtreler` gibi):**

- Bana atanan
- Açık işlerim
- Kayıtlı filtreler (`op_saved_filters`)

**URL sözleşmesi:**

| Query | Anlam |
|-------|--------|
| `?workspaceId=` | Seçili workspace |
| `?boardId=` | Seçili board — ana alanda board runtime embed veya `/boards/{id}`’e yönlendirme |

Board tam ekran tercih edilirse: ağaçtan board tıklanınca `router.push('/apps/operation-core/boards/' + boardId)`; geri dönüşte query korunur (TM settings → workspace pattern).

**Veri:**

- Ağaç: DG `op_workspaces`, `op_boards` (filter: workspaceId) — salt okuma, hafif liste.
- Board içeriği: MO runtime (Faz 1 planındaki gibi).

---

## 3. Side menu yapılandırması

### 3.1 Mevcut platform davranışı

- Menü önceliği: **MongoDB** (`sideMenu` store → Hub API) — bkz. `vertical-sidebar/index.vue`.
- `sidebarItem.ts` / `horizontalItems.ts`: yalnızca `enableFallbackMenu=true` iken yedek.
- Odak prod: genelde **DB menü**; yeni modül **Side Menu Manager** veya seed script ile eklenmeli.

### 3.2 OC menü kararı

| Konu | Karar |
|------|--------|
| TM “Görevler” ile birleşme | **Hayır** — ayrı üst öğe |
| TM menüsü | Olduğu gibi kalır (deprecated ayrı karar) |
| OC kök `pageCode` | `operationCore.menuTitle` |
| OC ikon | `mdi-clipboard-flow-outline` veya Tabler `LayoutKanban` |
| `pageType` | `user` (Faz 1); admin alt sayfaları `manager` (Faz 2) |

**Önerilen menü ağacı (MongoDB / Side Menu Manager):**

```text
[header] Operasyon
  Operasyon Merkezi          pageCode: operationCore.menuTitle
    route: /apps/operation-core/workspace
    icon: mdi-clipboard-flow-outline
    children (opsiyonel Faz 1):
      Çalışma alanı          → /apps/operation-core/workspace
      (Faz 2) Yapılandırma   → manager only, metadata admin
```

**Faz 1 minimum:** tek menü maddesi → **Çalışma alanı** (`/workspace`).

**Hard-coded yedek** (geliştirme, `enableFallbackMenu`):

`sidebarItem.ts` ve `horizontalItems.ts` içine TM “Görevler” satırının **altına değil**, ayrı header altına ekle:

```ts
{ header: "Operasyon" },
{
  title: "Operasyon Merkezi",
  pageCode: "operationCore.menuTitle",
  icon: LayoutKanbanIcon, // veya uygun Tabler ikon
  to: "/apps/operation-core/workspace",
},
```

### 3.3 Side Menu Manager checklist (Odak)

1. Side Menu Manager UI → yeni header **Operasyon** (veya mevcut uygun header).
2. Item: `pageCode` = `operationCore.menuTitle`, `route` = `/apps/operation-core/workspace`.
3. `tr.json` / `en.json`: `operationCore.menuTitle` = "Operasyon Merkezi" / "Operation Core".
4. İzinler: Faz 1 boş veya tüm authenticated; ileride workspace scope.
5. Welcome kartı: [WELCOME_HOME.md](../../ui/WELCOME_HOME.md) — TM kartına paralel OC kartı.

### 3.4 Route ↔ menü tutarlılığı

| Menü metni | Route | Not |
|------------|-------|-----|
| Operasyon Merkezi | `/apps/operation-core/workspace` | Varsayılan |
| (welcome link) | `/apps/operation-core/workspace?workspaceId={demo}` | seed json id |

`/apps/operation-core` index: `workspace`’e **redirect** (TM’de `/task-manager` → projects listesi yerine explorer öncelikli).

---

## 4. Breadcrumb yapılandırması

### 4.1 TM’den öğrenilen sorun

[TASK_MANAGER_PLANNING §12](../../../content/task_manager/TASK_MANAGER_PLANNING.md): her sayfa kendi `breadcrumbs` dizisini üretiyor; ortak helper yok; i18n parçalı. OC’de **Sprint 1’de** merkezi helper zorunlu.

### 4.2 OC hiyerarşisi

```text
Ana sayfa (/)                    → breadcrumbs.home  (veya welcome)
Operasyon Merkezi                → operationCore.breadcrumbRoot
{Workspace adı}                  → dinamik (DG/MO)
{Board adı}                      → dinamik (opsiyonel segment)
{WorkItem key}                   → profil son segment
```

**Örnek yollar:**

| Sayfa | Breadcrumb |
|-------|------------|
| Workspace explorer | Ana › Operasyon Merkezi › Çalışma alanı |
| Board (tam sayfa) | Ana › Operasyon Merkezi › {WS} › {Board} |
| WI profil | Ana › Operasyon Merkezi › {WS} › {Board}? › **OCD-0005** |
| Yeni iş | Ana › Operasyon Merkezi › {WS} › Yeni iş |

“Ana sayfa” linki: TM analitik dashboard (`/dashboards/analytical`) yerine **`/`** (welcome) — Odak home ile hizalı ([WELCOME_HOME.md](../../ui/WELCOME_HOME.md)).

### 4.3 Teknik uygulama

**Yeni dosya:** `Mng.Ui/composables/useOperationCoreBreadcrumbs.ts`

```ts
export function useOperationCoreBreadcrumbs(ctx: {
  workspace?: { id: string; name: string } | null;
  board?: { id: string; name: string } | null;
  workItem?: { id: string; key: string } | null;
  tail?: { text: string; href?: string; disabled?: boolean };
}) {
  // BaseBreadcrumb items: { text, href?, disabled? }
}
```

**Kurallar:**

- Tüm metinler **i18n key** (`operationCore.breadcrumb.*`); dinamik segmentler ham ad/key.
- Asenkron yükleme: workspace/board adı gelene kadar skeleton veya id kısaltması (`…`) — TM’deki eksikliği giderir.
- Profilde board bilinmiyorsa segment atlanır (workItem.workspaceId ile WS yüklenir).
- Mobil: 4+ segmentte son 2 göster + overflow menu (Faz 1 opsiyonel; en azından `disabled: true` son segment).

**Kullanım:**

```vue
const crumbs = useOperationCoreBreadcrumbs({
  workspace: wsRef,
  board: boardRef,
  workItem: wiRef,
});
```

Sayfa başına 15 satırlık `mt()` + dizi **yazılmaz**.

### 4.4 i18n anahtarları (breadcrumb)

```json
"operationCore": {
  "breadcrumbRoot": "Operasyon Merkezi",
  "breadcrumbWorkspace": "Çalışma alanı",
  "breadcrumbNewWorkItem": "Yeni iş",
  "breadcrumbDashboard": "Pano"
}
```

Faz 1: **tr + en** zorunlu; fr/zh/ar TM ile aynı politika (OC açıldığında en az tr/en).

---

## 5. Proje ekleme / yapılandırma (TM ProjectEditorForm fikirleri)

Faz 1’de OC **workspace oluşturma UI’si yok** (seed + DG). TM `ProjectEditorForm` yine de **Faz 2 metadata admin** için yol haritası:

| TM sekmesi | OC Faz 2 karşılığı | Backend |
|------------|-------------------|---------|
| Genel (ad, key, açıklama, lead) | Workspace genel | DG `op_workspaces` |
| Workflow | State flow editor | DG `op_state_flows` + `op_states` |
| Selections (tip, öncelik, alan) | WI type / form bağlantıları | DG metadata |
| Permissions | Workspace/board scope | DG + MO evaluator |
| Yeni görev formu layout | Form designer | DG `op_forms` |
| Profil layout | Profile designer | DG `op_profiles` |

**Faz 1’den taşınacak UX fikirleri (kod değil, pattern):**

- Sekmeli editor + `?tab=` URL sync (`ProjectEditorForm` satır 81–83).
- Layout editöründe **canlı önizleme** (`projectForLayout` computed).
- Kaydet / iptal toolbar üstte sabit.

Operasyonel kullanıcı Faz 1’de yalnızca explorer + board + profil görür; yapılandırma script/DG admin kalır.

---

## 6. Güncellenmiş route özeti

Önceki planda hub ön plandaydı; TM deneyimine göre **workspace birincil**:

| Route | Rol | Sprint |
|-------|-----|--------|
| `/apps/operation-core` | → redirect `/workspace` | S1 |
| `/apps/operation-core/workspace` | **Ana explorer** (ağaç + board embed) | S1–S2 |
| `/apps/operation-core/boards/[boardId]` | Tam ekran board (derin link) | S2 |
| `/apps/operation-core/work-items/new` | Create form | S4 |
| `/apps/operation-core/work-items/[id]/profile` | Profil | S3–S4 |
| `/apps/operation-core/dashboards/[dashboardId]` | Dashboard | S5 |

---

## 7. Uygulama sırası (navigasyon odaklı)

**S1a — Navigasyon iskeleti (OC’ye özel, TM borcunu tekrarlama):**

1. `useOperationCoreBreadcrumbs.ts`
2. `operationCore.*` i18n (menu + breadcrumb)
3. Side menu kaydı (MongoDB + fallback ts)
4. `/workspace` boş sayfa + breadcrumb + redirect from index
5. Welcome modül kartı

**S1b — Explorer:**

6. `OcWorkspaceTree` + `useResizableTreePanel`
7. DG workspace/board listesi
8. URL query sync

Sonra board/profil (Faz 1 plan §11).

---

## 8. Açık kararlar

| # | Soru | Öneri |
|---|------|--------|
| N1 | Explorer’da board embed mi tam sayfa mı? | **Faz 1:** tam sayfa route; explorer’da seçim + “Board’u aç” — embed sprint 2 opsiyonel |
| N2 | Breadcrumb ana link `/` mi dashboard mu? | **`/`** (welcome) |
| N3 | Side menu header adı | **Operasyon** (Major Plan 4.8 ile uyumlu) |
| N4 | TM workspace’teki “Yeni proje” OC’de? | Faz 2 admin; Faz 1 menüde **yok** |

Onay sonrası [OC_UI_PHASE1_PLAN.md](./OC_UI_PHASE1_PLAN.md) route §3 ile senkron tutulur.
