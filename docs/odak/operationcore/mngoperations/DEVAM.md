# MngOperations & Operation Core UI — Devam noktası (checkpoint)

**Son güncelleme:** 30 Mayıs 2026 (board liste audit/SLA sütunları + sabit actions + form chrome: profil/yorum/SLA/politika/mention/ekler)  
**Durum:** SW **SW-0…SW-6** ✅ · A1 R-Plus ✅ · **SLA-0/1/2** ✅ · **D1 Board admin** ✅ · **BL (board liste enrichment)** ✅ · **BO (board liste aksiyonlar)** ✅ · **BLF (server-side liste + gelişmiş arama)** ✅ · **BLC (audit/SLA sütunları + sabit actions)** ✅ · **FC (form chrome: profil/yorum/SLA/politika/mention/ekler)** ✅ · **NP (in-app bildirim paneli + mention görünür)** ✅ (lokal) · **mngui Odak deploy** ✅

**Ana plan:** [OC_UI_ADMIN_FAZ1_PLAN.md](../ui/OC_UI_ADMIN_FAZ1_PLAN.md)

---

## NP — In-app bildirim paneli + mention görünürlüğü (bu oturum, 30 May)

Header bildirim paneli (`NotificationDD.vue`) **mock veriyi bırakıp** geçerli kullanıcının `op_notifications` kayıtlarını gösterir. FC-4 mention bildirimleri (`CommentMention`) ve tüm event/kural bildirimleri artık kullanıcıya görünür.

| Kod | Durum | Not |
|-----|--------|-----|
| **NP-1** | ✅ | **MO bildirim ucu** — `GET /notifications` (`unreadOnly`, skip/take, en yeni önce) + `POST /notifications/{id}/read` + `POST /notifications/read-all`. `INotificationQueryService`/`NotificationQueryService`: kapsam daima `IRequestContext.UserId`; match user'a sabit, mark işlemleri sahiplik doğrular (404). `NotificationDto`/`NotificationListResponse` (+`unreadCount`). |
| **NP-2** | ✅ | **createdAt damgası** — `NotificationOrchestratorService` (mention + in-app) artık `createdAt` (ISO) yazar; DG otomatik damgalamadığı için sıralama (`-createdAt`) buna dayanır. Forward-only (eski kayıtlarda yok → en alta düşer). |
| **NP-3** | ✅ | **Panel UI** — `NotificationDD.vue`: rozet (okunmamış sayısı), tip ikon/renk (`CommentMention`=@), göreli zaman (`Intl.RelativeTimeFormat`), okunmamış vurgusu, tıklayınca okundu+iş kaydı profiline git, "Tümünü okundu işaretle", 60sn poll. Admin kapısı kaldırıldı (mention edilen kullanıcı admin olmayabilir). i18n `header.notifications.*` (en/tr). |

| **NP-4** | ✅ | **Kimlik uzayı düzeltmesi (bug)** — Bildirimler person picker kimliğiyle (`mng_person_id` = Keeper `@users` id) yazılıyor; panel ise `sub` (Keycloak id) ile sorguluyordu → **asla eşleşmiyordu** (mention + atama bildirimleri görünmez). `IRequestContext.MngPersonId` (claim `mng_person_id`, `sub` yedek); `NotificationQueryService` artık `userId $in {mng_person_id, sub}` ile eşler. Ayrıca mention/event **actor** = `MngPersonId` (self-exclude doğru çalışır). |
| **NP-5** | ✅ | **Yerleşik atama bildirimi** — `INotificationOrchestrator.DispatchAssignmentAsync`: politikadan bağımsız, atama yapılınca her zaman atanan kişiye in-app `WorkItemAssigned` bildirimi (best-effort). Create (assignee varsa) + Patch (assignee **değiştiyse**). Atayan kişi kendine atadıysa veya değişmediyse atlanır. UI: `WorkItemAssigned` ikon/etiket (`mdi-account-arrow-right`, "Atama"). |

**MO yeni:** `Contracts/Notifications/NotificationDto.cs`, `Interfaces/INotificationQueryService.cs`, `Services/NotificationQueryService.cs`, `Controllers/NotificationsController.cs`. **Değişen:** `ServiceRegistration.cs` (DI), `Services/NotificationOrchestratorService.cs` (`createdAt`), `Interfaces/IRequestContext.cs` + `Services/HttpRequestContext.cs` (`MngPersonId`), `Services/WorkItemCommandService.cs` (mention/event actor = `MngPersonId`).
**UI yeni/değişen:** `components/lc/Full/vertical-header/NotificationDD.vue` (yeniden yazıldı), `services/operationCoreService.ts` (`ocGetNotifications`/`ocMarkNotificationRead`/`ocMarkAllNotificationsRead`), `types/apps/operationCore.ts` (`OcNotification`/`OcNotificationListResponse`), `vertical-header/index.vue` (admin gate kaldırıldı), locale `en/tr`.
**Deploy:** MO build temiz (0 hata) · UI `npm run generate` temiz (169 route). **`mngoperations` Odak'a deploy edildi (30 May, healthy — NP-1…5).** **`mngui` Odak'a deploy edildi (30 May 11:03, healthy — `ui=200`; NP paneli + BLF-8 canlı).**

**Açık/ileriki:**
- ✅ **Atama bildirimi** — NP-5 ile yerleşik (politikadan bağımsız) hale getirildi. Diğer event'ler (`WorkItemUpdated`/`Transitioned` vb.) hâlâ `op_notification_policies` gerektirir.
- ⬜ `createdBy` damgası `sub` (Keycloak id) ile yazılıyor; person çözümü `mng_person_id`/@users id beklediği için ad çözülmeyebilir — gözden geçir.
- ⬜ Bildirim okundu durumunu farklı sekmeler/cihazlar arası canlı senkron (şu an 60sn poll).
- ⬜ "Tümünü gör" ayrı bildirim sayfası (şu an son 20 dropdown'da).

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
- ✅ Mention bildirim paneli `CommentMention` tipini gösteriyor → **NP** bölümü (in-app bildirim paneli) ile çözüldü.
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
| **BLF-8** | ✅ | **Sayısal/tarih operatörleri UI'a açıldı** — gelişmiş aramada `number` ve `date` alan tipleri: `gt/gte/lt/lte` (+`eq/ne`). Alan tipi tespiti: `filterKind()` artık `columnFormat`(`number`/`money`/`date`) + pool `fieldType`(`number`/`date`/`datetime`) ile karar veriyor (core date: createdAt/lastStateChangeAt/closedAt). Değer girişi: number=`type=number`, date=`type=datetime-local` → `toISOString()` UTC ile gönderilir (saklama ISO string ile sözlüksel uyumlu). Bu alanlar hızlı filtreden hariç (operatör gerektirir). **Backend değişikliği yok** (`CoerceScalar` long/double; ISO string lexicographic). |
| **BLF-9** | ✅ | **Relation alanlarda option/relation etiketi** — pool `relation` alanları (`relationDatasetName`): liste hücresinde ham `__dataId` yerine ilgili kaydın **adı**; filtrede (hızlı + gelişmiş) ham metin yerine **v-select** (option=ad, value=id, `in/nin/eq/ne`). Board sayfası relation dataset'lerini `ocListDataset`+`recordToDatasetItems` ile (dataset bazında tek sefer) yükler → `relationOptionsByKey`/`relationLabelByKey`. Filtre bileşeni `OcBoardFilterKind`'a `relation` eklendi; katalog mantığı "select" (katalog ∪ relation) olarak genelleştirildi. *(Statik `select`/`enum` pool tipi yok; OC tipleri text/number/bool/datetime/relation/persons/personGroups/tags/file.)* **Backend değişikliği yok.** |

**Düzeltmeler (aynı oturum grubu):**
- ✅ **assignee bozulması** — edit'te `assignee` object olarak persist ediliyordu; `MngDataGatewayClient.CollapseRelationValue` `assignee` (tekil) + `watchers` (çoklu) relation'ı id'ye indirger.
- ✅ **Çoklu person combobox** — form içinde 2+ person alanında ikinci picker liste getirmiyordu; `useOcDynamicFormLookups` artık alan başına izole `useOcPersonPicker()` (`pickerForField`).

**MO yeni/değişen:** `IMngDataGatewayClient.QueryPageAsync` + `DataGatewayPage`, `Clients/MngDataGatewayClient.cs`, `Contracts/Runtime/BoardRuntimeContext.cs` (`ListColumns`/`DefaultSort` + yeni DTO'lar), `Services/RuntimeContextService.cs` (`GetBoardListAsync`, `ParseListColumns`/`ParseDefaultSort`, `BuildBoardCatalogsAsync` scope, `$and` filtre, `BuildMatchCondition`), `Controllers/RuntimeController.cs` (`[HttpPost] boards/{id}/list`).
**DG değişen:** `Services/DataService.cs` `QueryWithMatchAsync` (`$facet` total + search), `Controllers/DataController.cs` (`search` param + `X-Total-Count`).
**UI yeni/değişen:** `components/.../OcBoardListFilters.vue` (yeni — hızlı filtre + gelişmiş arama paneli), `OcWorkspaceBoardListScopeEditor.vue`, `OcWorkspaceBoardDialog.vue`, `pages/.../boards/[boardId]/index.vue`, `services/operationCoreService.ts` (`ocGetBoardListPage`), `stores/apps/operationCore.ts`, `types/apps/operationCore.ts`, `utils/ocBoardListColumns.ts`, locale `en/tr`.

**Deploy:** `mngdatagateway` + `mngoperations` Odak'a deploy edildi (30 May, healthy). MO/DG build temiz (0 hata). UI `npm run generate` temiz (169 route) — **`mngui` deploy EDİLMEDİ** (kullanıcı talebi bekleniyor).

**Belgeler:** [API_SURFACE §3.1/3.1.2](./API_SURFACE.md) (board context + liste ucu) · [RUNTIME_CONTEXT §5.2](./RUNTIME_CONTEXT.md) (katalog scope).

**Açık/ileriki:**
- ✅ Gelişmiş aramada sayısal/tarih `gt/gte/lt/lte` → **BLF-8** (UI-only; Odak'a deploy edildi 30 May 11:03, healthy).
- ✅ Pool relation alanlarda filtre/hücre option-relation etiketi → **BLF-9** (UI-only, deploy bekliyor).
- ⬜ `tags` (çoklu serbest etiket) alanları için etiket/option çözümü — şimdilik yalnızca `relation` kapsandı.
- ✅ **`mngui` Odak deploy** (30 May, healthy — BLF+BLC+FC+NP+BLF-8 canlı). *Not: BLF-9 yeni UI değişikliği, henüz deploy edilmedi.*

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
- ✅ Pool **relation** hücrelerinde relation adı (ham id yerine) → **BLF-9** (`tags` hariç).
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

## E1-P1 + W-CREATE — Workspace yetki grupları + Yeni workspace UI (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **E1-P1** | ✅ | **Genel sekmede yetki grupları** — `OcWorkspaceDefinitionsGeneralTab.vue`'ye `viewGroups`/`editGroups`/`adminGroups` Keeper grup seçicileri (board dialog pattern: `useGroupStore` + multi-select chips). Load `ocGetWorkspace` → form; save `ocUpdateWorkspace` payload'una eklenir. Tip: `OpWorkspaceDetail`'e `viewGroups/editGroups/adminGroups/ownerGroups`; `mapWorkspaceDetail` `resolveRelationIds` ile parse (string id veya relation obje). |
| **W-CREATE** | ✅ | **Yeni workspace oluşturma UI** — `OcWorkspaceCreateDialog.vue` (name zorunlu + workspaceType + prefix + açıklama; `key` DG incremental). Servis `ocCreateWorkspace` = `ocCreateRecordId('op_workspaces', …)`. Hub (`admin/workspace-definitions/index.vue`): v-select yanına "Yeni workspace" butonu + boş-durum CTA; create sonrası `loadWorkspaces` + yeni id seçilir + Genel sekme. |

**UI yeni:** `OcWorkspaceCreateDialog.vue`. **Değişen:** `OcWorkspaceDefinitionsGeneralTab.vue`, `admin/workspace-definitions/index.vue`, `services/operationCoreService.ts` (`ocCreateWorkspace` + `mapWorkspaceDetail` grupları), `types/apps/operationCore.ts`, locale `en/tr`.
**Backend değişikliği yok** — `WorkspaceRecord` zaten `ViewGroups/EditGroups/AdminGroups/OwnerGroups` içeriyor; `op_workspaces` create DG `key` incremental üretir.
**Deploy:** UI-only, **Odak'ta canlı** (30 May, commit `cd6848a`).

---

## E1-P2 — Akış geçişlerinde requiredFields + yetki grupları (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **E1-P2** | ✅ | **Flows sekmesi geçiş editörü** — her transition kartına `requiredFields` (core form-layout key'leri + pool alanları, etiketli) ve `permissions.groups` (aktif Keeper grupları) çoklu seçimi. `OcWorkspaceDefinitionsFlowsTab.vue`: `ocListPoolFieldsForWorkspace` + `useGroupStore` yüklenir; `fieldKeyItems` `resolveOcFieldDisplayLabel` ile etiket; `buildPayload` her geçişe `requiredFields:[]` + `permissions:{groups:[]}` yazar. Tip `OpStateFlowTransition.permissionGroups`; `mapOpStateFlowTransition` `permissions.groups` parse eder. |

**Backend değişikliği yok** — MO zaten zorunlu kılıyor: `StateFlowCatalog.EnsureRequiredFields` (WI transition akışında) + `PermissionEvaluator.EnsureTransition`/`CanApplyTransition` (`permissions.groups`). Boş grup = kısıtlama yok (`GroupListParser.Intersects` `requiredGroups.Count==0 → true`).
**Değişen:** `OcWorkspaceDefinitionsFlowsTab.vue`, `services/operationCoreService.ts` (`mapOpStateFlowTransition`), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** UI-only, **deploy bekliyor**.

**Açık/ileriki:**
- ⬜ Create dialog'da opsiyonel: ilk board/akış seed'i (şimdilik boş; Değerler/Akış sekmelerinden kurulur).
- ⬜ Workspace silme/devre dışı bırakma (Genel sekme) — guard + ilişki kontrolü.

---

## CC — Dinamik (computed) sütunlar, faz-1 (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **CC** | ✅ | **Hesaplanan liste sütunu (display-only, client-eval)** — board liste sütun editöründe "Hesaplanan sütun" girişi (`key`/`label`/`expr`/`format`); `expr-eval` ile güvenli ifade (eval yok). Satır bağlamı = core alanlar + pool `fields`; değer board sayfasında `evaluateComputedExpr` ile hesaplanıp `formatCellValue`'dan geçer. Sunucu sort/filter yok (computed → kapalı). |

**Yaklaşım A (MO passthrough):** `BoardListColumnDto`'ya `Computed`/`Expr`/`Label`; `ParseListColumns` bunları okur, computed sütunları `cardFieldKeys`'ten (DG alan seçimi) ve sortable/filterable'dan hariç tutar. UI runtime context'ten okuyup hesaplar.
**Yeni:** `utils/ocComputedColumns.ts` (expr-eval değerlendirici, parse cache, güvenli değişken çözümü), bağımlılık `expr-eval`.
**Değişen (MO):** `BoardRuntimeContext.cs` (`BoardListColumnDto`), `RuntimeContextService.cs` (`ParseListColumns`, cardFieldKeys). **Değişen (UI):** `OcWorkspaceBoardListScopeEditor.vue` (computed sütun ekleme + expr satırı), `OcWorkspaceBoardDialog.vue` (visibleFields'tan computed hariç), `boards/[boardId]/index.vue` (computed render), `ocBoardListColumns.ts` (`deriveBoardListColumns` computed korur), `operationCoreService.ts` (iki mapper), `types/apps/operationCore.ts`, locale `en/tr`.
**Deploy:** MO build temiz (0/0). MO + mngui deploy gerekir.

**Açık/ileriki (CC faz-2+):**
- ⬜ `tags`/relation çoklu değerlerini ifade içinde etiketle kullanma (şu an dizi → eleman sayısı).
- ⬜ Tarih farkı/iş günü fonksiyonları (özel expr fonksiyonları).
- ⬜ Profil/kart görünümünde de computed alan gösterimi.

---

## A — Yorum ekleri (op_comments.attachments) (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **A** | ✅ | **Yorumlara dosya eki** — `op_comments.attachments` (file isArray, şemada zaten mevcut). Composer'da dosya seç/kaldır; gönderimde base64 `content` ile MO'ya gider, DG MinIO'ya yükler. Timeline yorum girdilerinde ekler indirilebilir chip (mevcut `ocDownloadAttachment`). İş kaydı ekleri pattern'i birebir. |

**Backend (MO):** `AddCommentRequest.Attachments` (+ `CommentAttachmentInput`); `AddCommentInternalAsync` payload'a `attachments` yazar; `TimelineEntryDto.Attachments` + timeline yorum döngüsü ham `attachments`'ı geçirir. Build 0/0.
**UI:** `OcCommentComposer.vue` (dosya seçici + bekleyen chip'ler + emit `files`), `operationCoreService.ts` (`ocAddWorkItemComment` files→base64, `mapTimelineEntry` attachments parse), `OcTimelineEntry.attachments`, profil timeline ek chip'leri, locale `en/tr`.
**Backend şema değişikliği yok** — `op_comments.attachments` (file isArray) phase-1 şemasında zaten tanımlı.
**Deploy:** MO + mngui deploy gerekir.

---

## F — Operasyonel runtime: durum geçişleri (bu oturum, 30 May)

| Kod | Durum | Not |
|-----|--------|-----|
| **F-T** | ✅ (lokal) | **Profilde durum geçişi (transition) uygulama** — profil header'ında geçerli durumdan uygulanabilir geçiş butonları; tıklayınca opsiyonel yorumlu onay dialog'u; uygulanınca güncel profil + timeline yeniden yüklenir. MO yetki + koşul + `requiredFields` doğrulamasını yapar (UI eklemeli, ham). |

**Backend değişikliği yok** — MO profil context `Actions` (`ProfileActionDto`: key/label/from/to/enabled/order; `GetAvailableTransitions` ile) ve `POST /work-items/{id}/transitions/{key}` (`TransitionWorkItemRequest`: `comment?`/`fields?`) zaten hazırdı; UI bunları hiç okumuyordu.
**UI:** `types/apps/operationCore.ts` (`OcProfileAction` + `OcWorkItemProfile.actions`), `operationCoreService.ts` (`mapProfileAction`, `mapWorkItemProfile` actions parse, `ocApplyTransition`), profil sayfası `work-items/[id]/profile/index.vue` (header geçiş butonları + onay/yorum dialog + başarı sonrası yenileme), locale `en/tr` (`profile.transitions.*`).
**Deploy:** Yalnızca mngui (MO değişmedi). Kullanıcı isteği üzerine deploy edilecek.

**Açık/ileriki (F faz-2+):**
- ⬜ Geçiş dialog'unda `requiredFields` ön-toplama (şu an MO 400 dönerse hata gösterilir).
- ⬜ Board listede/Kanban'da geçiş uygulama (DnD → defaultTransitionKey).

---

## PERF — Board liste + profil performans/kod optimizasyonu (bu oturum, 30 May)

Ölçüm-öncelikli, davranış-koruyan tur (`perf/oc-optimization` → `main`). Detay: `PERF_OPTIMIZATION.md`.

| Kod | Durum | Not |
|-----|--------|-----|
| **PERF-BE** | ✅ Odak'ta canlı | Profil DG: `op_links`/`timeline` erken paralel başlat (metadata+field-behavior ile örtüşür) + timeline `limit=200→sort=-enteredAt&limit=5`; FieldBehavior istek-başı tek `key→record` map (O(alan×enabledIds) tarama kalktı). **Ölçülen:** profil warm ~1575-1822ms → **~1218ms (~%30)**. Board liste warm zaten optimal (tek DG sorgusu, değişmedi). |
| **PERF-UI** | ✅ Odak'ta canlı | `Intl` formatter memoize, tek global "now" ticker (N timer→1), lookup Map dedup, `listRows` önceden çözüm (slot'lar her render'da `resolveX` çağırmıyor), `OcBoardKanban` lazy. Davranış birebir. |
| **PERF-DIAG** | ✅ (flag kapalı) | `OcCallStats` + `PerfDiagnostics` bayrağı (default kapalı) — istek başına DG/Keeper çağrı sayısı/süresi; UI `localStorage.OC_PERF`. Üretimde kapalı, gelecekte ölçüm için hazır. |

**Açık/ileriki (kapıda, ayrı onay):** ⬜ Tablo sanallaştırma ⬜ büyük dosya bölme refactor (`operationCoreService.ts`, `RuntimeContextService.cs`) — ölçüm gerektirmedi.

---

## Sıradaki işler

| # | Epic | Hedef |
|---|------|--------|
| ~~1~~ | ~~**E1-P1**~~ | ✅ Genel sekme yetki grupları (view/edit/admin) — Odak'ta canlı |
| ~~2~~ | ~~**W-CREATE**~~ | ✅ Yeni workspace oluşturma UI — Odak'ta canlı |
| ~~3~~ | ~~**E1-P2**~~ | ✅ Akışlar: geçiş `requiredFields` + `permissions.groups` — Odak'ta canlı |
| ~~4~~ | ~~**CC**~~ | ✅ Dinamik (computed) sütunlar (expr-eval, display-only) — Odak'ta canlı |
| ~~5~~ | ~~**A**~~ | ✅ `op_comments.attachments` (yorum ekleri) — bu oturum (MO+mngui deploy bekliyor) |
| ~~6~~ | ~~**F**~~ | ✅ Operasyonel runtime: profilde durum geçişi (transition) uygulama — lokal (mngui deploy bekliyor); SLA-3 chip zaten ✅ (FC/BLC) |

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
[✓] In-app bildirim paneli (op_notifications + mention görünür) — Odak'ta canlı
[✓] Gelişmiş arama gt/gte/lt/lte UI (BLF-8) — Odak'ta canlı
[✓] Relation alanlarda option/relation etiketi (BLF-9) — Odak'ta canlı
[✓] Workspace yetki grupları (E1-P1) + Yeni workspace UI (W-CREATE) — Odak'ta canlı
[✓] Admin kapanış: akış geçiş requiredFields + yetki grupları (E1-P2) — Odak'ta canlı
[✓] Dinamik (computed) sütunlar (CC, expr-eval display-only) — Odak'ta canlı
[✓] Yorum ekleri (op_comments.attachments) — lokal (MO+mngui deploy bekliyor)
[✓] Operasyonel runtime: profilde durum geçişi uygulama (F) — lokal (mngui deploy bekliyor); SLA-3 chip zaten ✓
[✓] Performans optimizasyonu (PERF): profil ~%30 hızlanma + UI yapısal kazanımlar — Odak'ta canlı, main'e merge
```
