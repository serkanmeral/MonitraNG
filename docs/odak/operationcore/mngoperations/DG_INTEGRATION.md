# MngOperations — MngDataGateway entegrasyonu

**Son güncelleme:** 26 Mayıs 2026

---

## 0. DG gerçek yapısı ile hizalama (kod)

Plan, mevcut **MngDataGateway** davranışına göre yazıldı. Özet (kaynak: `MngDataGateway.Persistence`):

| DG davranışı | Kod / API | MO karşılığı |
|--------------|-----------|--------------|
| **Tenant DB** | JWT `domain_name` → Mongo `mng_{domain_name}` ([MongoContextService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/MongoContextService.cs)) | Token forward zorunlu; MO cache/scope `domain_name` ile uyumlu |
| **Kimlik** | `[Authorize]` + `AddJwtBearer`; claim’ler `HttpContext.User` | MO → DG HttpClient’ta **aynı Bearer**; DG isteği kendi middleware’inde tekrar parse eder |
| **Audit** | `preferred_username`, `sub` → [UserInfoService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/UserInfoService.cs) | MO activity actor = aynı claim’ler |
| **Dataset permission** | `permissions` null → herkes; doluysa `user_groups` ∩ `groups` ([PermissionService](../../../../MngDataGateway/Infrastructure/MngDataGateway.Persistence/Services/PermissionService.cs)) | **OC `op_*`:** null — yetki **MO** (workspace/board/kayıt/alan) |
| **Şema + veri** | `@datasets` ve `op_*` verisi **aynı domain DB**’de | Metadata CRUD UI→DG; operasyonel yazma MO→DG `data/{dataset}` |
| **Veri API** | `GET/POST/PUT/DELETE /api/v1/data/{datasetName}` ([DataController](../../../../MngDataGateway/Presentation/MngDataGateway.Api/Controllers/DataController.cs)) | `IMngDataGatewayClient` yolları |
| **Predefined query** | `POST /api/v1/data/{dataset}/queries/{queryName}` | Board / SLA sorguları |
| **Admin claim** | `isAdmin` (Keeper mapper) | Dokümanlarda `is_admin` = aynı kavram; JWT’de claim adı **`isAdmin`** |

MO, DG’nin yerine geçmez. **OC:** tüm `op_*` dataset’leri DG’de **açık** (permissions null); kayıt/alan/workspace yetkisi **MO**’dadır ([PERMISSIONS_LAYERING](./PERMISSIONS_LAYERING.md)).

**`publish_mode` (Q14):** Tüm `op_*` → **`none`**. MO persist sonrası DG ham `dataset.op_*.*` event üretmez; operasyonel olay **`oc.events`**, kullanıcı bildirimi MO pipeline → [NOTIFICATIONS_AND_EVENTS.md](./NOTIFICATIONS_AND_EVENTS.md). Mevcut Odak: [patch-op-publish-mode-none.ps1](../scripts/patch-op-publish-mode-none.ps1).

---

## 1. Prensip

MngOperations **kalıcılık sahibi değildir**; `op_*` için tek yazma/okuma yolu MngDataGateway’dir.

- **Metadata** (workspace, state flow, form, rule, …): Faz 1’de yönetim UI → **doğrudan DG**; MO yalnızca **read** (cache’li).
- **Operasyonel veri** (work item, comment, activity, timeline, notification): MO **write** orchestration.

---

## 2. Client sözleşmesi

`IMngDataGatewayClient` (Scheduler client genişletilmiş):

| Metot | DG yolu | Kullanım |
|-------|---------|----------|
| `GetByIdAsync<T>` | `GET /api/v1/data/{dataset}/{id}` | Tek kayıt |
| `QueryAsync<T>` | `GET /api/v1/data/{dataset}?filter=…` | Liste / filtre |
| `CreateAsync<T>` | `POST /api/v1/data/{dataset}` | Yeni kayıt |
| `UpdateAsync<T>` | `PUT /api/v1/data/{dataset}/{id}` | Tam/partial update (DG semantiği) |
| `ExecutePredefinedQueryAsync` | `POST /api/v1/data/{dataset}/queries/{name}` | Board kolonları, SLA breach |

**Auth:** Her çağrıda gelen isteğin **`Authorization: Bearer`** değeri DG’ye **otomatik forward** ([AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md)). DG multi-tenant için `domain_id`, `user_groups`, `preferred_username` claim’lerini JWT’den okur; MO permission için aynı token’ı parse eder.

**Retry:** 5xx için Polly exponential backoff (Scheduler ile aynı).

**Base URL:** `MngOperationsSettings:DataGateway:BaseUrl` + `ApiVersion` — Development ve Production’da **environment / appsettings** ile değişir (Q2 kararı). Örnekler:

| Ortam | `BaseUrl` |
|--------|-----------|
| Docker / Production | `http://mngdatagateway:5010` |
| Odak Development (host) | `http://192.168.20.20:5010` |

Dış istemci (UI): **gateway** `…/operations/api/v1`. MO → DG: **doğrudan** bu `BaseUrl`.

---

## 3. Dataset erişim matrisi

| Dataset | MO read | MO write | Not |
|---------|---------|----------|-----|
| `op_workspaces` | ✓ | — | Metadata |
| `op_states`, `op_priorities`, `op_work_item_types`, `op_fields` | ✓ | — | |
| `op_state_flows`, `op_rules`, `op_forms`, `op_profiles`, `op_boards` | ✓ | — | |
| `op_sla_policies`, `op_notification_policies` | ✓ | — | |
| `op_work_items` | ✓ | ✓ | Komut pipeline |
| `op_comments` | ✓ | ✓ | `POST .../comments` |
| `op_activities` | ✓ | ✓ | Pipeline side-effect |
| `op_work_item_timelines` | ✓ | ✓ | Transition sonrası |
| `op_notifications` | ✓ | ✓ | Policy + automation |
| `op_links` | ✓ | ✓ | İsteğe bağlı Faz 1 |
| `op_labels` | ✓ | — / ✓ | Etiket atama komutu |
| `op_dashboards`, `op_saved_filters`, `op_reports` | ✓ | — | Runtime read |

---

## 4. Metadata cache (öneri)

Faz 1: `IMemoryCache` veya scoped dictionary, anahtar `{domainId}:{dataset}:{id|name}`.

| Veri | TTL öneri | Invalidation |
|------|-----------|--------------|
| Workspace + enabled types/fields | 2–5 dk | Manuel veya Faz 2 event |
| State flow (transitions[]) | 2–5 dk | |
| Rules (workspace filtered) | 1–2 dk | |
| Form / Profile / Board | 2–5 dk | |

Yüksek frekanslı transition path’te cache miss maliyeti kabul edilebilir; Faz 2’de RabbitMQ `oc.metadata.changed` ile invalidation.

---

## 5. WorkItem key üretimi

`op_work_items.key` = **text**, **MngOperations** üretir (TM’deki DG `incremental` alanı yerine — iş kuralı orchestration ile uyumlu).

### 5.1 TM ile eşleme (onaylanan semantik)

| Task Manager (eski) | Operation Core (yeni) |
|---------------------|------------------------|
| Proje `key` (ör. `TST`) | Workspace `workItemKeyPrefix` (ör. `TST`) |
| Issue `key` incremental `{projectKey}-{0:D4}` | WorkItem `key` örn. `TST-0001`, `TST-0002` |
| Client create’te `projectKey` gönderirdi | Client **key göndermez**; MO create pipeline atar |

Kullanıcı deneyimi aynı: **prefix workspace’te tanımlı**, her yeni iş kaydı o prefix altında **bir artar**.

### 5.2 Workspace alanları (`op_workspaces`)

| Alan | Örnek | Not |
|------|--------|-----|
| `workItemKeyPrefix` | `TST` | Zorunlu (create öncesi validate) |
| `workItemKeyFormat` | `{PREFIX}-{SEQ:D4}` | Boşsa varsayılan; TM ile uyumlu 4 hane |
| `workItemSequenceStart` | `1` | İlk sıra |

Phase1 spec’teki `keyFormat: {PREFIX}-{SEQ:D5}` yerine **varsayılan D4** (TM `PROJ-0001` pattern); workspace format ile 5 haneye çıkılabilir.

### 5.3 Algoritma (Faz 1)

Create pipeline, validation’dan **önce**:

1. `workspaceId` → workspace metadata (`workItemKeyPrefix`, format, sequenceStart).
2. Aynı workspace’teki mevcut `op_work_items.key` değerlerinden prefix’e uyanların **max sıra numarasını** bul (DG filter: `key` regex / starts with `{prefix}-`).
3. `next = max + 1` (kayıt yoksa `workItemSequenceStart`).
4. `key = Format(workItemKeyFormat, prefix, next)` → örn. `TST-0003`.
5. DG `POST op_work_items` ile persist; unique ihlali → 409.

**Not:** TM’de sıra DG `IncrementalFieldService` ile tutuluyordu; OC’de sıra **MO sorumluluğu** (aynı sonuç, farklı katman). İleride yüksek hacimde workspace counter dokümanı eklenebilir.

### 5.4 İstemci sözleşmesi

- `POST /work-items` gövdesinde `key` **yok** veya yok sayılır.
- `PATCH` ile `key` değişimi **yasak** ([API_SURFACE](./API_SURFACE.md)).

---

## 6. Predefined query kullanımı

`op_work_items` üzerindeki 5 query ([draft JSON](../datasets/operationcore_datasets_phase1_draft_2026-05-26.json)):

| Query | MO kullanımı |
|-------|----------------|
| `wi_by_workspace_and_state` | Board kolon kartları |
| `wi_board_column` | Board + kolon |
| `wi_assigned_to_user` | “Bana atananlar” |
| `wi_sla_response_breach` / `wi_sla_resolve_breach` | SLA widget (Faz 1 foundation) |

Parametreler `IQueryParameterResolver` ile doldurulur ([RUNTIME_CONTEXT](./RUNTIME_CONTEXT.md)).

---

## 7. Dosya ekleri — DG `file` alanı (Q8)

Şemada hazır:

| Dataset | Alan | Tip |
|---------|------|-----|
| `op_work_items` | `attachments` | `file`, **isArray: true** |
| `op_comments` | `attachments` | `file`, isArray |

**Karar:** Dosya işlemleri **DG’ye yaslanır**; MngOperations **binary proxy yapmaz** (MinIO, sıkıştırma, şifreleme DG [FilesController](../../../../MngDataGateway/Presentation/MngDataGateway.Api/Controllers/FilesController.cs) ve [DataController file pipeline](../../../../MngDataGateway/Presentation/MngDataGateway.Api/Controllers/DataController.cs)).

### UI → DG (gateway)

| İşlem | Yol |
|--------|-----|
| Yükleme | `POST /data/api/v1/files/upload` — `datasetName`, `fieldName`, `recordId` (work item `__dataId`), base64 content |
| İndirme | `GET /data/api/v1/files/...` (DG download endpoint’leri) |
| Kayıt ile birlikte | `POST`/`PUT /data/api/v1/data/op_work_items/{id}` — gövdede `attachments` file DTO; DG `ProcessFileFields` |

Bearer: kullanıcı token (MO ile aynı).

### MO rolü

| MO yapar | MO yapmaz |
|----------|-----------|
| `FormRuntimeContext` → `attachments` **visible / readonly** | Dosya bytes upload/download |
| İsteğe bağlı: upload sonrası `PATCH /work-items/{id}` yalnızca **metadata** dizisini güncelle (DG dönen `filePath`, `file_name`, …) — iş kuralı + activity | `/operations/.../files/upload` proxy |

**Yetki:** Dosya yüklemeden önce MO form context (alan düzenlenebilir mi?); DG dataset `op_*` açık + token. Kayıt düzeyi kısıt MO **B** katmanında.

**Yorum ekleri:** Aynı model — `op_comments` + `POST .../comments` sonrası veya DG file upload `recordId` = comment id.

---

## 8. Çok adımlı yazma (tutarlılık)

Transition / create pipeline birden fazla dataset yazar:

```text
op_work_items (update)
 → op_work_item_timelines (insert/close segment)
 → op_activities (insert)
 → op_notifications (insert)
```

**Faz 1:** Sıralı HTTP; hata → pipeline durur; önceki adımlar **kalır**; yanıt **`PARTIAL_FAILURE`** + `completedSteps[]` ([PIPELINES.md §9](./PIPELINES.md)). Otomatik rollback **yok**.

**Faz 2:** Outbox / saga — tutarlılık veya compensation.
