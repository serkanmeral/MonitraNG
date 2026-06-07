# MngOperations (MO) — Widget katalog kapsamı

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 📋 Planlama  
**Veri yolu:** **`queryRef` → DG `op_work_items` predefined query**  
**İlişkili:** [DATA_CATALOG.md](./DATA_CATALOG.md) · [operationcore/](../operationcore/README.md)

---

## 1. Terminoloji

| Terim | Anlam |
|-------|--------|
| **MO** | MngOperations — Operation Core operasyon motoru |
| **Monitoring** | Metrik/asset modülü — **bu planda değil** |

Manifest `domain`: **`operation-core`**

---

## 2. Modül özeti

| Özellik | Durum |
|---------|--------|
| Workspace hub | ✅ `/apps/task-manager/workspace` |
| Work items | ✅ `op_work_items` dataset |
| Workspace dashboard / pano | ✅ `OcDashboardView`, seed widget’lar |
| MO runtime query execute | ✅ `/api/v1/runtime/queries/{name}/execute` |
| DG predefined queries | ✅ draft schema — Odak’ta kısmen provizyon |

Mevcut workspace pano widget’ları **MO runtime** üzerinden `queryKey` kullanıyor. Birleşik widget motoru **DG queryRef** ile aynı sorguları hedefler (`@op_work_items/queries/{name}`).

---

## 3. Predefined query envanteri

Kaynak: [operationcore_datasets_phase1_draft_2026-05-26.json](../operationcore/datasets/operationcore_datasets_phase1_draft_2026-05-26.json)

| queryName | queryRef | Parametreler | Widget kullanımı |
|-----------|----------|--------------|------------------|
| `wi_by_workspace_and_state` | `@op_work_items/queries/wi_by_workspace_and_state` | `workspaceId`, `stateId` | Duruma göre liste / sayım |
| `wi_board_column` | `@op_work_items/queries/wi_board_column` | `workspaceId`, `boardId`, `stateId` | Board kolon listesi |
| `wi_assigned_to_user` | `@op_work_items/queries/wi_assigned_to_user` | `assignee` | Atanan tüm işler |
| `wi_assigned_open` | `@op_work_items/queries/wi_assigned_open` | `assignee` | **Bana atanan açık işler** ✅ canlı |
| `wi_sla_response_breach` | `@op_work_items/queries/wi_sla_response_breach` | `workspaceId`, `asOf` | SLA yanıt ihlali |
| `wi_sla_resolve_breach` | `@op_work_items/queries/wi_sla_resolve_breach` | `workspaceId`, `asOf` | SLA çözüm ihlali |

### 3.1 Eksik query (widget seed için)

| queryName | Amaç | Durum |
|-----------|------|-------|
| `wi_count_by_state` | Donut/bar — workspace’te durum dağılımı | 🔲 DG schema + pipeline |
| `wi_open_count` | Tek stat — açık iş sayısı | 🔲 aggregate query |

---

## 4. Kullanıcı parametreleri (designer)

| Parametre | UI | Context binding |
|-----------|-----|-----------------|
| Workspace | Workspace seçici | `$variables.workspaceId` (panelde kilitli) |
| Board | Board seçici | `$variables.boardId` |
| Atanan | Gizli / “Bana atanan” | `$variables.currentUserId` → `assignee` |
| SLA asOf | Varsayılan `now` | `$timeRange.to` veya sistem saati |

**Kritik:** `assignee` = **MngPersonId** (`@users` id), Keycloak username değil — bkz. MO DASH-CARDS düzeltmesi.

---

## 5. Öntanımlı widget şablonları (V1 seed)

| templateId | kind | queryRef | preset |
|------------|------|----------|--------|
| `oc.work-items-by-state` | chart | `wi_count_by_state` *(eksik)* | `chart-donut-breakup` |
| `oc.sla-breach-stat` | stat | `wi_sla_response_breach` + count | `stat-simple` |
| `oc.my-assigned-table` | table | `wi_assigned_open` | `table-compact` |
| `oc.open-work-queue-table` | table | `wi_by_workspace_and_state` | `table-drilldown` |

Mevcut seed örnekleri: `seed-operation-core-demo.ps1`, helpdesk/monitrang feedback dashboard seed.

---

## 6. Yüzeyler

| Yüzey | Policy |
|-------|--------|
| `workspace-panel` | `workspaceId` kilitli; action button (O2) |
| `dashboard` | Workspace parametreli genel dashboard |
| Report | Snapshot + workspace filtresi |

**Drill-down:** workspace hub, iş detay drawer/route.

---

## 7. Birleşme (Faz 4)

| Mevcut | Hedef |
|--------|-------|
| `OcDashboardWidgetDef` + MO runtime | `@widgets` manifest + `queryRef` |
| `OcDashboardWidgetForm` | Unified Widget Designer wizard |
| Workspace pano layout | `@dashboards` veya workspace policy layout |

---

## 8. Eksik iş paketi (OC / widget chat)

| # | İş | Sahip |
|---|-----|-------|
| O-W1 | `wi_count_by_state` predefined query | OC dataset chat |
| O-W2 | DG `op_work_items` queries tam provizyon (prod) | setup scripts |
| O-W3 | Widget template seed JSON | Widget Faz 1 |

---

## 9. Kategori seed

`@widget_categories`: `oc-kpi`, `oc-work-queues`, `oc-sla`
