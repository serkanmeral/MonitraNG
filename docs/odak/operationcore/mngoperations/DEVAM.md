# MngOperations & Operation Core UI — Devam noktası (checkpoint)

**Son güncelleme:** 30 Mayıs 2026 (board liste audit/SLA sütunları + sabit actions + form chrome: profil/yorum/SLA/politika/mention/ekler)  
**Durum:** SW **SW-0…SW-6** ✅ · A1 R-Plus ✅ · **SLA-0/1/2** ✅ · **D1 Board admin** ✅ · **BL (board liste enrichment)** ✅ · **BO (board liste aksiyonlar)** ✅ · **BLF (server-side liste + gelişmiş arama)** ✅ · **BLC (audit/SLA sütunları + sabit actions)** ✅ · **FC (form chrome: profil/yorum/SLA/politika/mention/ekler)** ✅ · **mngui Odak deploy** ✅

**Ana plan:** [OC_UI_ADMIN_FAZ1_PLAN.md](../ui/OC_UI_ADMIN_FAZ1_PLAN.md)

---

## BLC — Board liste audit/SLA sütunları + sabit actions (bu oturum, 30 May)

Liste tablosuna **sistem sütunları** (oluşturan/oluşturma zamanı/geçen süre/SLA durumu) + paylaşılan format katmanı; actions sütunu yatay scroll'da **sağda sabit**. Plan: [BOARD_LIST_FORM_CHROME_PLAN.md](./BOARD_LIST_FORM_CHROME_PLAN.md).

| Kod | Durum | Not |
|-----|--------|-----|
| **BLC-1** | ✅ | **Audit/SLA alanları** — `WorkItemCardDto` + `createdAt/createdBy/updatedAt/lastStateChangeAt/closedAt/sla`. Create'te **forward-only `createdBy`** damgası (`IRequestContext.UserId`); `createdBy` person çözümüne eklendi. |
| **BLC-2** | ✅ | **Sistem sütunları admin** — `OcWorkspaceBoardListScopeEditor` sistem sütunlarını (state/priority/type/assignee + createdAt/createdBy/age/sla) seçtirir; per-sütun **Biçim** (`text/number/money/date/relativeTime`). `BoardListColumnDto.Format`. |
| **BLC-3** | ✅ | **Format katmanı + SLA chip** — `utils/ocColumnFormat.ts` (locale-aware date/number/money/relativeTime), `OcSlaStatusChip.vue` (akıllı faz: initial state'ten çıkış = response karşılandı proxy; canlı sayaç). Server-side sort: `age`→`createdAt` (ters), `sla`→`sla.resolveDueAt`. |
| **BLC-4** | ✅ | **Sabit actions sütunu** — son `th/td` `position: sticky; right:0` (scoped CSS, `boards/[boardId]/index.vue`). |

---

## FC — Form chrome (profil + create/edit modal; form tasarımından bağımsız) (bu oturum, 30 May)

Hibrit profil (ana kolonda sekmeler + sağ sidebar özetler) ve sade modal. Plan: [BOARD_LIST_FORM_CHROME_PLAN.md](./BOARD_LIST_FORM_CHROME_PLAN.md).

| Kod | Durum | Not |
|-----|--------|-----|
| **FC-profil** | ✅ | Profil sayfası yeniden yazıldı: ana kolon sekmeler `Detay | Aktivite & yorum[N] | Ekler[N]`; sağ sidebar `SLA · Politikalar · Meta · İzleyenler · Bağlılar`. `ProfileRuntimeContext` + `People` map (assignee/reporter/createdBy/watchers ad çözümü) + `WorkItemSummaryDto.CreatedBy`. |
| **FC-1 (yorum)** | ✅ | Aktivite & yorum sekmesi — `ocGetWorkItemTimeline` + `ocAddWorkItemComment`; timeline (yorum/state/transition). |
| **FC-2 (SLA)** | ✅ | Sidebar SLA paneli — `OcSlaStatusChip` + response/resolve due tarihleri. |
| **FC-3 (politika)** | ✅ | `OcPolicyPanel.vue` (client-side) — eşleşen SLA politikası (id veya type/priority türetme) hedef süreleri + uygulanan `op_rules` (board/type/state kapsamı). Profil sidebar **+ create/edit modalı** (form altı açılır panel, form modelinden canlı type/priority/state). |
| **FC-4 (mention)** | ✅ | `OcCommentComposer.vue` — `@` ile kişi autocomplete (`useOcPersonPicker`), token+id eşleme; `AddCommentRequest.Mentions` → `op_comments.mentions={personIds}` + **in-app bildirim** (`INotificationOrchestrator.DispatchMentionAsync`, `op_notifications` tip `CommentMention`, yazarı dışlar, best-effort). |
| **FC-5 (ekler)** | ✅ | **DG `file` (isArray) alanı** — yeni MinIO backend yok. `op_work_items.attachments` writable (`WorkItemCoreFields`); `ProfileRuntimeContext.Attachments` ham döner. UI Ekler sekmesi: yükle (base64 inline PATCH; mevcut ekler ham `raw` ile korunur, yeni `content` DG'ye yüklenir), indir (`/files/download`→blob), kaldır. |

**MO yeni/değişen:** `WorkItemCoreFields` (`attachments` writable), `Contracts/Runtime/BoardRuntimeContext.cs` (`WorkItemCardDto` audit/SLA + `BoardListColumnDto.Format` + `InitialStateId`), `Contracts/Runtime/ProfileRuntimeContext.cs` (`People`+`Attachments`), `Contracts/WorkItems/AddCommentRequest.cs` (`Mentions`), `Interfaces/INotificationOrchestrator.cs` (`DispatchMentionAsync`), `Services/NotificationOrchestratorService.cs`, `Services/RuntimeContextService.cs` (audit map + sort + people + attachments), `Services/WorkItemCommandService.cs` (`createdBy` damga + comment mentions), `Utilities/ProfileRuntimeBuilder.cs`/`WorkItemCoreFields.cs`.
**UI yeni:** `OcSlaStatusChip.vue`, `OcCommentComposer.vue`, `OcPolicyPanel.vue`, `utils/ocColumnFormat.ts`, `utils/ocBoardListColumns.ts`. **Değişen:** `pages/.../boards/[boardId]/index.vue`, `pages/.../work-items/[id]/profile/index.vue`, `OcWorkItemFormDialog.vue`, `OcWorkspaceBoardListScopeEditor.vue`, `services/operationCoreService.ts` (profil/timeline/comment/attachment fn'leri), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** `mngoperations` Odak'a deploy edildi (healthy, 0 hata) · **`mngui` Odak'a deploy edildi (30 May, healthy — `ui=200`)**. UI build temiz (Nitro built).

**Açık/ileriki:**
- ⬜ **Dinamik (computed) sütunlar** (`expr-eval`, display-only) — BLC kapsamından ertelendi.
- ⬜ Mention bildirim panelinin `CommentMention` tipini gösterdiğini doğrula (gerekirse panel filtresine ekle).
- ⬜ `op_comments.attachments` (yorum ekleri) — şu an sadece iş kaydı ekleri.

---

## BLF — Board liste server-side sıralama/filtre/arama + gelişmiş arama (bu oturum, 30 May)

Board liste görünümü **tam sunucu tarafı**na taşındı: sütun sırası + per-sütun yetkiler (admin), `v-data-table-server`, arama, filtre bileşeni ve **açılır gelişmiş arama** (operatör + çok satırlı AND).

| Kod | Durum | Not |
|-----|--------|-----|
| **BLF-1** | ✅ | **DG `POST /data/{ds}/query` iyileştirme** — `$facet` ile data+total tek sorguda; `search` query param; `X-Total-Count` header. MO `IMngDataGatewayClient.QueryPageAsync` (native Mongo `match` + sort/skip/limit/search → `DataGatewayPage{Items,Total}`). |
| **BLF-2** | ✅ | **MO `POST /runtime/boards/{boardId}/list`** — `GetBoardListAsync`; izin + board akış state kapsamı + filtre + sıralama + arama + person çözümleme → `QueryExecuteResponse`. Sözleşme: `BoardListRequest` + `BoardListColumnDto`/`BoardSortDto`/`BoardListFilterDto`. |
| **BLF-3** | ✅ | **Board admin: sütun sırası + sortable/filterable + defaultSort** — `config.listColumns[]` (sıralı, per-sütun yetki) + `config.defaultSort`. Eski board'larda `visibleFields`'tan geriye dönük türetme (`deriveBoardListColumns`). UI: `OcWorkspaceBoardListScopeEditor` (sıralama + toggle'lar + varsayılan sıralama). |
| **BLF-4** | ✅ | **Liste UI server-side** — `v-data-table-server` (page/itemsPerPage/sortBy), debounce'lu arama, `OcBoardListFilters` (katalog multi-select / person picker / metin). Store `loadBoardListPage` + `listItems/listTotal/listLoading`. |
| **BLF-5** | ✅ | **Katalog filtre seçenekleri workspace kapsamı** — `BuildBoardCatalogsAsync` artık state = board akış kapsamı ∪ `enabledStateIds`; priority/type = `enabled*Ids` (yoksa workspace tipleri / tüm katalog). Enabled ID alandan, yoksa `settings` yedeğinden. *(Önceki: tüm katalog geliyordu.)* |
| **BLF-6** | ✅ | **Gelişmiş arama + AND** — MO filtreleri `$and` ile birleşir (aynı alana çoklu koşul ezilmez; kullanıcı `stateId` filtresi kapsamla kesişir). UI: açılır panel, çok satırlı `[Alan][Operatör][Değer]`. Operatörler: `eq/ne/in/nin/contains/startsWith/endsWith` (+ backend `gt/gte/lt/lte`). |
| **BLF-7** | ✅ | **Search temizleme fix + Clear butonu** — `clearable` `null` → `(searchInput ?? '').trim()` (eski: `null.trim()` patlıyordu, liste yenilenmiyordu); belirgin "Aramayı temizle" butonu + `@click:clear`. |

**Düzeltmeler (aynı oturum grubu):**
- ✅ **assignee bozulması** — edit'te `assignee` object olarak persist ediliyordu; `MngDataGatewayClient.CollapseRelationValue` `assignee` (tekil) + `watchers` (çoklu) relation'ı id'ye indirger.
- ✅ **Çoklu person combobox** — form içinde 2+ person alanında ikinci picker liste getirmiyordu; `useOcDynamicFormLookups` artık alan başına izole `useOcPersonPicker()` (`pickerForField`).

**MO yeni/değişen:** `IMngDataGatewayClient.QueryPageAsync` + `DataGatewayPage`, `Clients/MngDataGatewayClient.cs`, `Contracts/Runtime/BoardRuntimeContext.cs` (`ListColumns`/`DefaultSort` + yeni DTO'lar), `Services/RuntimeContextService.cs` (`GetBoardListAsync`, `ParseListColumns`/`ParseDefaultSort`, `BuildBoardCatalogsAsync` scope, `$and` filtre, `BuildMatchCondition`), `Controllers/RuntimeController.cs` (`[HttpPost] boards/{id}/list`).
**DG değişen:** `Services/DataService.cs` `QueryWithMatchAsync` (`$facet` total + search), `Controllers/DataController.cs` (`search` param + `X-Total-Count`).
**UI yeni/değişen:** `components/.../OcBoardListFilters.vue` (yeni — hızlı filtre + gelişmiş arama paneli), `OcWorkspaceBoardListScopeEditor.vue`, `OcWorkspaceBoardDialog.vue`, `pages/.../boards/[boardId]/index.vue`, `services/operationCoreService.ts` (`ocGetBoardListPage`), `stores/apps/operationCore.ts`, `types/apps/operationCore.ts`, `utils/ocBoardListColumns.ts`, locale `en/tr`.

**Deploy:** `mngdatagateway` + `mngoperations` Odak'a deploy edildi (30 May, healthy). MO/DG build temiz (0 hata). UI `npm run generate` temiz (169 route) — **`mngui` deploy EDİLMEDİ** (kullanıcı talebi bekleniyor).

**Belgeler:** [API_SURFACE §3.1/3.1.2](./API_SURFACE.md) (board context + liste ucu) · [RUNTIME_CONTEXT §5.2](./RUNTIME_CONTEXT.md) (katalog scope).

**Açık/ileriki:**
- ⬜ Gelişmiş aramada sayısal/tarih alanlar için `gt/gte/lt/lte` operatörlerini UI'a aç (alan tipi tespiti gerek).
- ⬜ Pool select/relation alanlarda filtre değeri için option/relation etiketi (şu an ham değer).
- ✅ **`mngui` Odak deploy** (30 May, healthy — BLF+BLC+FC canlı).

## BO — Board liste operasyonel aksiyonlar (bu oturum, 30 May)

Board liste görünümünde **yeni iş modalı + satır aksiyonları** (profil/düzenle/sil).

| Kod | Durum | Not |
|-----|--------|-----|
| **BO-1** | ✅ | **Yeni iş → modal** — `OcWorkItemFormDialog` (create/edit), genişlik form design `layout.dialogMaxWidth`'ten; create board `defaultFormId`'sini çözer. Eski `/work-items/new` sayfası duruyor (geri uyum). |
| **BO-2** | ✅ | **Actions sütunu** — View Profile (ayrı sayfa), Edit (modal, edit context + diff PATCH), Delete (onay modalı). Edit/Delete `permissions.canEdit` gate'li. |
| **BO-3** | ✅ | **MO `DELETE /work-items/{id}`** — `IWorkItemCommandService.DeleteAsync` (DG delete + activity `WorkItemDeleted` + `oc.workitem.deleted` event); yetki = `EnsureWorkItemUpdate`; 204. |
| **BO-4** | ✅ | **Profil sayfası geçici** — seçili formu **salt-okunur** render (`ocGetFormEditContext` + `OcDynamicForm readonly`); gerçek profil tasarımı (Epic F) bekliyor. |

**UI yeni/değişen:** `components/.../OcWorkItemFormDialog.vue` (yeni), `pages/.../boards/[boardId]/index.vue`, `pages/.../work-items/[id]/profile/index.vue`, `services/operationCoreService.ts` (`ocGetFormEditContext`, `ocUpdateWorkItem`, `ocDeleteWorkItem`, `buildUpdateWorkItemRequest`).
**MO değişen:** `IWorkItemCommandService` + `WorkItemCommandService.DeleteAsync`, `WorkItemsController` `[HttpDelete]`.
**Deploy:** `mngoperations` + `mngui` Odak'a deploy edildi (30 May, healthy — `mo_health=200`, `ui=200`). MO build temiz (0 hata); UI `npm run generate` temiz (169 route).

**Açık/ileriki:**
- ⬜ Edit modunda alan **temizleme** (şu an boş→null gönderiyor ama create filtre mantığı boşları atlıyordu; PATCH diff null gönderir). Profil/operasyonel UI (Epic F) ile birlikte gözden geçir.
- ⬜ Silmede kullanım/ilişki guard'ı (yorum/activity orphan) — Faz 2.
- ⬜ Profil gerçek tasarımı (header/sidebar/timeline) — Epic F.

---

## BL — Board liste enrichment (bu oturum, 29 May gece)

Board liste/kanban kartlarında id yerine **isim + ikon + renk**; UI client-side join yapmaz. Üç parça:

| Kod | Durum | Not |
|-----|--------|-----|
| **BL-1** | ✅ | **Katalog enrichment** — board context `catalogs` map'leri (state/priority/type → id/name/color/icon); `OcBoardCatalogLabel` Tabler/MDI ikon + renk. Katalog CRUD MO `/api/v1/catalogs/{source}` (write-through cache, `CatalogService`). DG `$lookup`'ta `__history` hariç. |
| **BL-2** | ✅ | **Pool alan sütunları** — board `visibleFields` artık çekirdek + pool alan key'lerini kabul eder; sütun seçici (scope editor) pool alanları listeler; `WorkItemCardDto.Fields` (`extraFields`) → liste hücresi (`listTablePoolCellValue`). |
| **BL-3** | ✅ | **Person çözümleme MO-side** — `assignee`/`watchers` + person tipi pool alanlar id→ad; `IKeeperDirectoryClient` (`GET api/User/{id}`) + `IPersonDirectory` in-memory cache (`PersonTtlSeconds`); `QueryExecuteResponse.People`; UI `boardPeople` map. |

**MO yeni dosyalar:** `Interfaces/IKeeperDirectoryClient.cs`, `Interfaces/IPersonDirectory.cs`, `Clients/MngKeeperClient.cs`, `Services/PersonDirectoryService.cs`, `Catalogs/OcCatalogRegistry.cs`, `Interfaces/ICatalogService.cs`, `Services/CatalogService.cs`, `Controllers/CatalogsController.cs`.

**Deploy:** `mngoperations` Odak'a deploy edildi (healthy). **`mngui` Odak'a deploy edildi (30 May 2026, healthy — `ui=200`)**; lokal `npm run generate` build temiz (169 route, 0 hata).

**Belgeler:** [RUNTIME_CONTEXT §5.2/5.3](./RUNTIME_CONTEXT.md) · [INTEGRATIONS §1.1](./INTEGRATIONS.md) (person + Redis ileriki faz notu).

**Açık/ileriki:**
- ✅ **mngui** Odak deploy (30 May 2026, healthy).
- ⬜ Person **grup** alanları (`personGroups`/`group`) ad çözümü.
- ⬜ Pool **select/relation** hücrelerinde option etiketi / relation adı (şu an ham değer/key).
- ⬜ **Keeper:** Redis kullanıcı profili cache + `POST api/User/by-ids` toplu endpoint → MO tek istekte çözer ([INTEGRATIONS §1.1](./INTEGRATIONS.md)).

---

## D1 Board admin (önceki oturum)

| Kod | Durum | Not |
|-----|--------|-----|
| **D1-B1** | ✅ | Kolon `defaultTransitionKey` — akış geçiş seçici |
| **D1-B2** | ✅ | Board varsayılanları: profile/type/priority/state |
| **D1-B3** | ✅ | `visibleFields` + MO `cardFieldKeys` etiketleri |
| **D1-B4** | ✅ | `viewGroups` / `editGroups` (Keeper grupları) |
| **D1-B5** | ✅ | Kolon state ↔ `enabledStateIds` uyarı |
| **W-CREATE** | ⬜ | Yeni workspace UI — backlog |

**UI:** `/apps/operation-core/admin/workspace-definitions?tab=boards`

---

## SLA Faz 1

| Kod | Durum | Not |
|-----|--------|-----|
| **SLA-0** | ✅ | `op_sla_policies` Odak'ta mevcut; demo policy `OC Demo Default SLA` |
| **SLA-1** | ✅ | MO create → `profile.sla` + `op_work_items.sla` snapshot (Odak smoke) |
| **SLA-2** | ✅ | Workspace tanımları → **SLA** sekmesi, CRUD dialog |
| **SLA-3** | ✅ | Profil + board liste SLA chip (`OcSlaStatusChip`, akıllı faz) — FC/BLC ile tamamlandı |

**Scriptler:** `setup-op-sla-policies-dataset.ps1` · **`smoke-sla-faz1.ps1`** (SLA-1 DoD)

**UI URL:** `/apps/operation-core/admin/workspace-definitions?workspaceId=...&tab=sla`

---

## Sıradaki işler

| # | Epic | Hedef |
|---|------|--------|
| **1** | **E1** | Admin kapanış + yetki grupları (Genel sekme) |
| **2** | **W-CREATE** | Yeni workspace oluşturma UI |
| **3** | **F** | Operasyonel runtime + **SLA-3** chip |

---

## Görsel ilerleme

```text
[✓] Kurallar + Politikalar + Zamanlanmış işler + Admin jobs
[✓] SLA admin (politika CRUD)
[✓] Board admin (D1-B1…B5 UI)
[✓] Board liste enrichment (katalog ikon/renk + pool sütunları + person MO-side)
[✓] mngui Odak deploy (BL UI canlı)
[✓] Board liste aksiyonlar (yeni-iş modal + profil/düzenle/sil + MO DELETE)
[✓] Board liste server-side (sıralama/filtre/arama + gelişmiş arama + workspace scope)
[✓] Board liste audit/SLA sütunları + sabit actions sütunu
[✓] Form chrome: hibrit profil + yorum/timeline + SLA paneli + politika paneli + mention + ekler (DG file)
[ ] Workspace create → Admin kapanış → dinamik (computed) sütunlar
```
