# MngOperations — DG dataset permission × MO workspace kuralları

**Son güncelleme:** 26 Mayıs 2026  
**Amaç:** İki yetki katmanının nasıl birlikte çalıştığını netleştirmek (konuşma / implementasyon referansı).

---

## 1. İki farklı katman (farklı kavramlar)

| Katman | Nerede | Ne kontrol eder | Kim uygular |
|--------|--------|-----------------|-------------|
| **A — DG dataset erişimi** | `@datasets.permissions` (read/create/update/delete + `groups`) | “Bu kullanıcı **bu dataset adına** DG API çağırabilir mi?” — kayıt/alan/workspace **bilmez** | **MngDataGateway** ([CheckDatasetPermission](../../../../MngDataGateway/Presentation/MngDataGateway.Api/Controllers/DataController.cs)) |
| **B — MO operasyonel yetki** | Workspace, board, work item, transition, field behavior | “**Bu proje/workspace/board**’da kim view/create/edit? **Bu kayıt** ve **bu alan** için ne geçerli?” | **MngOperations** ([PERMISSIONS_AND_FIELD_BEHAVIOR](./PERMISSIONS_AND_FIELD_BEHAVIOR.md)) |

**OC kararı:** Tüm **`op_*`** DG’de **herkese açık** (`permissions` null). Asıl yetkilendirme **yalnızca B**’dedir — birbirinden farklı kavramlar; DG’deki grup listesi workspace `viewGroups` ile **aynı şey değildir**.

---

## 2. İstek yolları

```text
┌─────────────────────────────────────────────────────────────────┐
│ Yol 1 — Operasyonel (Faz 1 OC UI hedefi)                        │
│   UI → MO /operations/api/v1/...  (Bearer)                      │
│        → MO katman B (workspace, transition, rules)             │
│        → MO katman A ön kontrol (opsiyonel, DG ile aynı grup)   │
│        → DG /api/v1/data/op_*  (aynı Bearer)  → katman A zorunlu│
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ Yol 2 — Metadata yapılandırma (Faz 1 plan)                      │
│   Admin UI → DG /data/api/v1/...  (doğrudan)                    │
│        → yalnızca katman A (ilgili @datasets permissions)       │
│        → MO workspace kuralları devreye girmez                  │
└─────────────────────────────────────────────────────────────────┘
```

Yol 2’de kullanıcı `op_workspaces` şemasını DG’den düzenleyebilir; bu **yapılandırma**, operasyonel iş akışı değildir.

---

## 3. Birleşik karar matrisi (MO → DG çağrısı)

MO bir komut için DG’ye yazmadan önce:

| Sıra | Kontrol | Başarısız |
|------|---------|-----------|
| 1 | MO **B**: workspace — view / create / edit; board bağlamı | 403 `WORKSPACE_FORBIDDEN` |
| 2 | MO **B**: kayıt (work item) görünürlüğü; transition `permissions` | 403 / 400 |
| 3 | MO **B**: alan bazlı `visible` / `readonly` / `required` / `masked` | 400 / 403 |
| 4 | MO **B**: `op_rules` validation | 400 rule |
| 5 | DG **A**: `op_*` için dataset erişimi (OC’de **herkese açık**) | 403 (yalnızca token/domain hatası vb.) |

MO, 5’te `op_*` için ek grup kontrolü **beklemez**; asıl red 1–4’te olur.

**DG bypass riski:** `op_*` DG’de açıkken biri doğrudan DG API ile ham CRUD yapabilir (ör. `stateId` patch). **Operasyonel disiplin:** UI → MO komutları; ham iş kuralı bypass’ı MO + API tasarımı ile engellenir ([API_SURFACE](./API_SURFACE.md)). İleride isteğe bağlı DG hook veya gateway kuralı ayrı değerlendirilir.

---

## 4. Veri modeli eşlemesi

### 4.1 Workspace / proje (`op_workspaces`) — katman B (MO)

Eski TM **proje** karşılığı. Kimlerin ne yapabileceği **burada** tanımlanır (DG bunu bilmez):

| Yetki (kavram) | Tipik kaynak | Örnek |
|----------------|--------------|--------|
| **View** | `viewGroups`, board config | Workspace / board listesi ve kartları görme |
| **Create** | workspace create policy + `editGroups` / manager | Yeni work item |
| **Edit** | `editGroups`, kayıt assignee, rules | PATCH alanları |
| **Admin** | `adminGroups`, `isManager`, `isAdmin` | Flow, rule, workspace ayarı (UI yolu MO veya DG metadata) |

Alanlar: `viewGroups`, `editGroups`, `adminGroups`, `ownerGroups`, `permissions` (object).

### 4.2 Board (`op_boards`) — katman B (MO)

Board bazlı görünürlük ve kolon bağlamı (`BoardRuntimeContext`); workspace yetkisinin alt kümesi veya board’a özel grup listesi (spec / implementasyon).

### 4.3 Kayıt — WorkItem (`op_work_items`) — katman B (MO)

Tekil kayıt: assignee, workspace membership, state, link — görünür mü, düzenlenebilir mi? DG yalnızca `POST/GET/PUT data/op_work_items` kabul eder.

### 4.4 Alan — field behavior — katman B (MO)

`IFieldBehaviorResolver`: kayıt + ekran (form/profile/board/state) + rules → `visible`, `readonly`, `required`, `masked` ([PERMISSIONS_AND_FIELD_BEHAVIOR](./PERMISSIONS_AND_FIELD_BEHAVIOR.md)).

### 4.5 Transition — katman B (MO)

`op_state_flows.transitions[].permissions` — operasyonel aksiyon; DG `op_state_flows` dataset’ine erişim ayrı (A’da açık).

### 4.6 DG — tüm `op_*` (katman A)

```json
"permissions": null
```

veya `@datasets` kaydında `permissions` alanı **tanımsız** → DG: domain’de authenticate olan her kullanıcı tüm `op_*` dataset API’lerini çağırabilir.

Platform `@datasets`, `@dataset_categories` vb. kendi platform kurallarında kalır; **OperationCoreDatasets** altındaki `op_*` **hepsi açık**.

---

## 5. Kararlar — production + net ayrım (26 May 2026)

### 5.1 DG (katman A) — tüm `op_*` açık

| Karar | Açıklama |
|--------|----------|
| **L1 (revize)** | Operation Core kategorisindeki **bütün `op_*` dataset’leri** DG’de **herkes için erişilebilir** — `permissions` **null / tanımsız** |
| Gerekçe | DG yalnızca dataset kapısıdır; workspace/board/kayıt/alan MO’dadır |
| Kurulum | Mevcut setup script şema yükler; `@datasets` üzerinde `op_*` için **permissions eklenmez** |

### 5.2 MO (katman B) — asıl yetkilendirme

Production disiplini **MO tarafında**:

| Seviye | Örnek |
|--------|--------|
| Workspace (proje) | view / create / edit grupları |
| Board | board’a özel view/edit |
| Kayıt (WorkItem) | görünürlük, assignee, transition |
| Alan | readonly, required, masked |
| Transition | `transitionKey` + `permissions.groups` |

Keeper token ([§5.3](#53-keeper-claimleri-mo-yalnızca-okur)):

| Claim / grup | MO’da |
|--------------|--------|
| `isAdmin` | Platform — operasyonel **tam bypass** (L4); workspace/board/transition kısıtı uygulanmaz; **audit zorunlu** |
| `isManager` | Domain manager — geniş workspace/board (workspace `adminGroups` / manager policy) |
| `user_groups` (`managers`, `users`, …) | Workspace `viewGroups` / `editGroups` ile kesişim |
| `admins` | Platform; Odak domain gruplarından ayrı |

Domain grup üyeliği (mngkeeper Mongo) **MO kodunda yönetilmez**.

### 5.3 Keeper claim’leri (MO yalnızca okur)

Bkz. önceki tablo — `managers` / `users` workspace ve board listelerinde referans edilir; DG `op_*` listelerinde **grup kısıtı yok**.

---

## 6. MO implementasyon notları

1. **`IPermissionEvaluator`**
   - `CanAccessWorkspace(workspace, action: View|Edit|Admin)`
   - `CanApplyTransition(workspace, transition, workItem)`
   - Girdi: `IRequestContext.UserGroups` (JWT `user_groups` parse).

2. **`IDgPermissionPrecheck` (opsiyonel)**
   - Komuttan önce: hedef dataset + operation için DG ile aynı grup mantığını simüle et veya DG’ye hafif HEAD/GET — Faz 1’de **gerek yok**; DG 403 yeterli.

3. **RuntimeContext**
   - `permissions.canCreate`, `canEdit`, `availableTransitions` → katman B sonucu; UI DG’yi çağırmadan operasyon yapar.

4. **Hata mesajları**
   - DG 403: `DATASET_FORBIDDEN` (dataset adı + operation).
   - MO 403: `WORKSPACE_FORBIDDEN` / `TRANSITION_FORBIDDEN` — kullanıcıya ayırt edilebilir.

---

## 7. Tartışma soruları (sizinle)

| # | Durum | Karar |
|---|--------|--------|
| **L1** | **Kararlandı (revize)** | Tüm `op_*` DG’de **permissions yok** — herkes dataset API erişimi |
| **L2** | **Kararlandı** | MO: workspace/board/kayıt/alan; Keeper `managers`/`users`/`isAdmin`/`isManager` |
| **L3** | **İptal / birleşti** | Metadata da `op_*` → DG açık; metadata **düzenleme yetkisi MO** (workspace admin, `isManager`) |
| **L4** | **Kararlandı** | `isAdmin` → operasyonel komutlarda workspace/board/transition kısıtı **yok**; her işlem **audit** (`op_activities`) |

---

## 8. İlgili belgeler

- [DG_INTEGRATION.md §0](./DG_INTEGRATION.md) — DG kod hizası
- [PERMISSIONS_AND_FIELD_BEHAVIOR.md](./PERMISSIONS_AND_FIELD_BEHAVIOR.md) — field merge
- [AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md) — `user_groups` claim
