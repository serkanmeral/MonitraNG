# Dataset Fixing — Canlı Envanter (Odak)

**Durum:** ✅ Final (legacy temizlik sonrası)  
**Oturum:** Kapatıldı — bkz. [CURRENT_STATUS.md](./CURRENT_STATUS.md)  
**Ortam:** Odak · domain `odak` · `http://192.168.20.20:5040`  
**Son güncelleme:** 2026-06-10 20:16 UTC+local

**Ham JSON:** [reports/audit_odak_latest.json](./reports/audit_odak_latest.json)

---

## Özet

| Metrik | Değer |
|--------|------:|
| Toplam kategori | 11 |
| Sistem kategorisi | 9 |
| Toplam dataset | 60 |
| Kategorisiz | 0 |
| Sistem kategorisindeki dataset | 57 |
| AF'e bağlı dataset | 1 (`tedarikciler`) |
| Manager-visible dataset | 3 |

**Tip dağılımı (heuristic):** A=2 · B=41 · C=1 · D=16 · E=0

---

## Kategoriler

| categoryName | dataId | isSystemCategory | datasetCount | description |
|---|---|---|---:|---|
| BusinessDatasets | 58f62fed-c086-4f1d-af04-95961d7bc85d | false | 1 | Is verisi dataset'leri (tedarikci, urun vb.) — OC lookup demo |
| ChatRoomDatasets | e53dca02-fdf3-405e-a854-608b0118c9df | true | 5 | MonitraNG Chat Room — cht_* dataset'leri |
| DocumentIntelligenceDatasets | 95c5e5db-3995-4f6c-860d-8ad36296c862 | true | 3 | Document Intelligence — dm_* dataset'leri |
| Monitoring | 99412edd-033b-4168-98ea-5062f8f15c24 | true | 11 | Monitoring Datasets |
| NotifierDatasets | bf526953-69ae-482c-9b13-ffae15d66181 | true | 3 | MngNotifier — e-posta sablonlari ve bildirim sablonlari |
| OperationCoreDatasets | f465403f-f7d2-4b40-b87d-d4e56295485a | true | 24 | Operation Core (OC) — MngOperations metadata ve operasyonel veri (op_*) |
| ReferenceDatasets | 82572157-71f0-4d39-91b0-cab33b8fa6b2 | false | 2 | Paylasilan lookup / referans veri (ulkeler, sehirler vb.) — manager duzenleyebilir |
| SchedulerDatasets | 45f2884a-a2d2-45eb-9e58-2044658672f7 | true | 2 | MngScheduler — @scheduled_jobs, @job_executions |
| System Datasets | 3b43de28-8a51-4bcc-a61a-666d25768283 | true | 7 | System-level datasets for application configuration (e.g., side menu, settings) |
| WidgetDatasets | 672cb92a-c3dd-4083-82eb-c103a82eba60 | true | 1 | Widget template catalog |
| WorkflowDatasets | 624ced7f-1bf6-49ae-b1a1-cb640937dbcd | true | 1 | MngWorkflow — pipeline ve workflow dataset tanimlari |

---

## Dataset'ler

| name | categoryName | isSystemCategory | fields | AF forms | side menu | tip | aksiyon |
|---|---|---|---:|---|---|---|---|
| `@automated_forms` | System Datasets | true | 8 | — | — | A | keep |
| `@dashboards` | System Datasets | true | 10 | — | — | B | keep |
| `@job_executions` | SchedulerDatasets | true | 0 | — | — | B | keep |
| `@mail_layouts` | NotifierDatasets | true | 9 | — | — | B | keep |
| `@mail_templates` | NotifierDatasets | true | 12 | — | — | B | keep |
| `@notification_templates` | NotifierDatasets | true | 10 | — | — | B | keep |
| `@scheduled_jobs` | SchedulerDatasets | true | 0 | — | — | B | keep |
| `@side_menu` | System Datasets | true | 20 | — | — | A | keep |
| `@user_notes` | System Datasets | true | 5 | — | — | B | keep |
| `@user_preferences` | System Datasets | true | 7 | — | — | B | keep |
| `@wf_validation_pipelines` | WorkflowDatasets | true | 4 | — | — | B | keep |
| `@widget_categories` | System Datasets | true | 6 | — | — | B | keep |
| `@widget_templates` | WidgetDatasets | true | 11 | — | — | B | keep |
| `@widgets` | System Datasets | true | 12 | — | — | B | keep |
| `cht_direct_conversations` | ChatRoomDatasets | true | 5 | — | — | B | keep |
| `cht_group_chats` | ChatRoomDatasets | true | 3 | — | — | B | keep |
| `cht_messages` | ChatRoomDatasets | true | 7 | — | — | B | keep |
| `cht_topic_members` | ChatRoomDatasets | true | 4 | — | — | B | keep |
| `cht_topic_rooms` | ChatRoomDatasets | true | 7 | — | — | B | keep |
| `dm_resource_permissions` | DocumentIntelligenceDatasets | true | 4 | — | — | D | keep |
| `dm_resource_versions` | DocumentIntelligenceDatasets | true | 7 | — | — | D | keep |
| `dm_resources` | DocumentIntelligenceDatasets | true | 14 | — | — | D | keep |
| `mon_agents` | Monitoring | true | 8 | — | — | D | keep |
| `mon_asset_type_family` | Monitoring | true | 3 | — | — | D | keep |
| `mon_asset_types` | Monitoring | true | 5 | — | — | D | keep |
| `mon_assets` | Monitoring | true | 8 | — | — | D | keep |
| `mon_collectible_templates` | Monitoring | true | 4 | — | — | D | keep |
| `mon_collection_periods` | Monitoring | true | 3 | — | — | D | keep |
| `mon_engines` | Monitoring | true | 9 | — | — | D | keep |
| `mon_http_auth_configs` | Monitoring | true | 7 | — | — | D | keep |
| `mon_items` | Monitoring | true | 6 | — | — | D | keep |
| `mon_metrics` | Monitoring | true | 3 | — | — | D | keep |
| `mon_schedules` | Monitoring | true | 4 | — | — | D | keep |
| `op_activities` | OperationCoreDatasets | true | 9 | — | — | B | keep |
| `op_boards` | OperationCoreDatasets | true | 25 | — | — | B | keep |
| `op_comments` | OperationCoreDatasets | true | 8 | — | — | B | keep |
| `op_dashboards` | OperationCoreDatasets | true | 9 | — | — | B | keep |
| `op_fields` | OperationCoreDatasets | true | 17 | — | — | B | keep |
| `op_forms` | OperationCoreDatasets | true | 19 | — | — | B | keep |
| `op_labels` | OperationCoreDatasets | true | 4 | — | — | B | keep |
| `op_links` | OperationCoreDatasets | true | 7 | — | — | B | keep |
| `op_notification_policies` | OperationCoreDatasets | true | 19 | — | — | B | keep |
| `op_notifications` | OperationCoreDatasets | true | 13 | — | — | B | keep |
| `op_priorities` | OperationCoreDatasets | true | 6 | — | — | B | keep |
| `op_profiles` | OperationCoreDatasets | true | 16 | — | — | B | keep |
| `op_reports` | OperationCoreDatasets | true | 10 | — | — | B | keep |
| `op_rules` | OperationCoreDatasets | true | 24 | — | — | B | keep |
| `op_saved_filters` | OperationCoreDatasets | true | 14 | — | — | B | keep |
| `op_sla_policies` | OperationCoreDatasets | true | 10 | — | — | B | keep |
| `op_state_flows` | OperationCoreDatasets | true | 9 | — | — | B | keep |
| `op_states` | OperationCoreDatasets | true | 11 | — | — | B | keep |
| `op_tags` | OperationCoreDatasets | true | 4 | — | — | B | keep |
| `op_work_item_schedules` | OperationCoreDatasets | true | 17 | — | — | B | keep |
| `op_work_item_timelines` | OperationCoreDatasets | true | 11 | — | — | B | keep |
| `op_work_item_types` | OperationCoreDatasets | true | 12 | — | — | B | keep |
| `op_work_items` | OperationCoreDatasets | true | 40 | — | — | B | keep |
| `op_workspaces` | OperationCoreDatasets | true | 20 | — | — | B | keep |
| `sehirler` | ReferenceDatasets | false | 4 | — | — | D | keep |
| `tedarikciler` | BusinessDatasets | false | 16 | tedarikciler-form | — | C | keep |
| `ulkeler` | ReferenceDatasets | false | 3 | — | — | D | keep |

---

## Notlar

- `suggestedType` / `suggestedAction` otomatik heuristic; [PLAN.md](./PLAN.md) Faz 2'de manuel doğrulanmalı.
- `side menu` = Automated Form route üzerinden menüde görünen dataset (dolaylı).
- Tip açıklaması: [PLAN.md §2](./PLAN.md#2-dataset-sınıflandırma-matrisi).
