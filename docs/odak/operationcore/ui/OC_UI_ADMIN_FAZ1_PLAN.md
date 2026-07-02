# Operation Core — Admin UX Faz 1 planı

**Son güncelleme:** 28 Mayıs 2026 (Politikalar + Kurallar UX kaydı)  
**Kapsam:** Workspace tanımları hub (`/apps/operation-core/admin/workspace-definitions`) — operasyonel çalışma sayfası (**board/profil**) bu plan **bittikten sonra**.  
**Handoff:** [DEVAM.md](../mngoperations/DEVAM.md) · **Kurallar detay:** [OC_UI_RULES_FAZ1.md](./OC_UI_RULES_FAZ1.md) · **SW:** [OC_UI_SCHEDULED_WORK_ITEMS.md](./OC_UI_SCHEDULED_WORK_ITEMS.md) · **SLA:** [SLA_FAZ1_PLAN.md](../mngoperations/SLA_FAZ1_PLAN.md) · **Yetki:** [PERMISSIONS_LAYERING.md](../mngoperations/PERMISSIONS_LAYERING.md)

---

## 1. Stratejik sıra (onaylı — 28 Mayıs 2026)

```text
1. Kurallar (op_rules) — R-Core + R-UX ✅ · R-Plus backlog
2. Zamanlanmış işler (SW-0 … SW-5)     ← şu an (SW UX geri bildirimi + SW-2/3 MO+Scheduler)
3. SLA — plan + metadata taslağı (runtime UI sonra)
4. Board tanımları — Faz 1 admin tamamlama
5. Admin UX kapanış denetimi + yetkilendirme kararları
6. Operasyonel çalışma sayfası (board/profil runtime) — Admin Faz 1 kapandıktan sonra
```

**İlke:** Metadata admin ve operasyonel runtime **ayrı epik**. Faz 1 admin «eksik kalmadı» denetimi geçmeden S2–S4 board/profil sprintine geçilmez.

---

## 2. Admin sekmeleri — durum matrisi

| Sekme | Faz 1 hedef | Durum | Eksik (özet) |
|-------|-------------|--------|--------------|
| **Genel** | Workspace kimliği + temel yetki grupları | 🟡 | `viewGroups` / `editGroups` / `adminGroups` UI yok |
| **Değerler** | `enabled*Ids` + katalog CRUD | ✅ | — |
| **Durum akışı** | Flow + geçişler | 🟡 | E1-P2 ✅ (`requiredFields`, permissions); **görsel editör backlog** → [OC_UI_STATE_FLOW_UX_PLANNING.md](./OC_UI_STATE_FLOW_UX_PLANNING.md) |
| **Formlar** | Layout + alan politikaları v1 | ✅ | İnce UX (§10 backlog — form politikası); operasyonel öncelik düşük |
| **Board'lar** | Runtime board tanımı | 🟡 | Kolon `defaultTransitionKey`, profil/tip varsayılanları, board grupları |
| **Politikalar** | `fieldPolicies` | ✅ | W2 `typeId`/`boardId` scope — backlog (Faz 1.1); **P-UX** ✅ |
| **Kurallar** | `op_rules` tam admin | ✅ | R-Core + **R-UX** ✅; R-Plus automation backlog |
| **Zamanlanmış işler** | `op_work_item_schedules` | 🟡 | SW-0 + UI ✅; MO execute + Scheduler SW-2/3 |
| **Yetkilendirme** | MO katman B + admin guard | 🟡 | Sayfa `isManager`; workspace/board grup UI + tartışma maddeleri |

---

## 3. Epic sırası ve fazlar

### Epic A — Kurallar tamamlama (A1)

**Hedef:** MO `RuleEngineService` + `op_rules` şemasının Faz 1 admin yüzeyi UI’da **parity**; create/sil-only döneminden çıkış.

| Faz | Kod | İş | DoD |
|-----|-----|-----|-----|
| **A1-R1** | R1 | Kural **düzenleme** + `ocUpdateRule` | Mevcut kayıt dialog ile güncellenir |
| **A1-R2** | R2 | **Scope:** `typeId`, `boardId`, `stateId`, `fromStateId`, `toStateId`, `transitionKey` | Akış kataloğundan geçiş seçici |
| **A1-R3** | R3 | **Koşul:** `eq`, `ne`, `empty`, `notEmpty`, `in`, `gt`, `lt` + AND grubu | MO `RuleConditionEvaluator` ile uyumlu JSON |
| **A1-R4** | R4 | **Meta:** `description`, `priority`, `isActive`, `applyMode` (validation pre/post) | Liste filtre + aktif chip |
| **A1-R5** | R5 | **Default aksiyonlar:** `setField`, `setAssignee`, `setAssignmentGroups` | Tip-aware değer widget |
| **A1-R6** | R6 | **Automation** (opsiyonel Faz 1 kapanış): `addWatcher`, `createNotification`, … | MO side-effect tipleri; basit form |
| **A1-R7** | R7 | Özet metin + katalog ad çözümleme | Politika explorer kalitesinde |

Detaylı madde listesi: [OC_UI_RULES_FAZ1.md](./OC_UI_RULES_FAZ1.md).

**Faz 1 dışı (bilinçli):** `validFrom`/`validTo`, webhook, script, karmaşık OR ağaçları.

---

### Epic B — Zamanlanmış işler (SW)

**Önkoşul:** Epic A tamam (kurallar runtime create pipeline’da zaten devrede; schedule tetik aynı hattı kullanır).

| Faz | Kod | İş | DoD |
|-----|-----|-----|-----|
| **SW-0** | | `op_work_item_schedules` dataset + draft script | DG taslak + `build-operationcore-datasets-draft.mjs` |
| **SW-1** | | MO schedule CRUD veya UI→DG + manager guard | Manager-only yazma |
| **SW-2** | | Tetik: `from-origin` / execute endpoint | WI oluşur, `lastRun*` güncellenir |
| **SW-3** | | MngScheduler job senkron | create/update/delete |
| **SW-4** | | UI: `tab=scheduled` sekmesi | [OC_UI_SCHEDULED_WORK_ITEMS.md](./OC_UI_SCHEDULED_WORK_ITEMS.md) |
| **SW-5** | | E2E: cron → board’da WI | Demo kayıt |

Belge: [SCHEDULED_WORK_ITEMS.md](../mngoperations/SCHEDULED_WORK_ITEMS.md).

---

### Epic C — SLA (planlama; geliştirme Faz 1.5+)

**Bugün:** metadata + MO/UI sınırı **planlanır**; working-hours / escalation **yok**.

| Faz | Kod | İş | DoD |
|-----|-----|-----|-----|
| **SLA-P0** | | Ürün kararı + dataset taslağı `op_sla_policies` | [SLA_FAZ1_PLAN.md](../mngoperations/SLA_FAZ1_PLAN.md) |
| **SLA-1** | | WI `slaPolicyId` + MO due hesabı (mevcut foundation) | Smoke |
| **SLA-2** | | Admin: workspace SLA politikası sekmesi | Faz 1.5 |
| **SLA-3** | | Profil SLA chip + breach renk | Operasyonel UI ile |

---

### Epic D — Board tanımları tamamlama (D1)

**Önkoşul:** Epic B planlandı veya SW-0 bitti (paralel mümkün; board önce de gidebilir).

| Faz | Kod | İş | DoD |
|-----|-----|-----|-----|
| **D1-B1** | B1 | Kolon **`defaultTransitionKey`** — akış geçiş listesinden | Kanban DnD runtime hazırlığı |
| **D1-B2** | B2 | Board varsayılanları: `defaultProfileId`, `defaultTypeId`, `defaultPriorityId`, `defaultStateId` | Dataset alanları persist |
| **D1-B3** | B3 | `visibleFields` / kart alanları — runtime `cardFieldKeys` ile hizalama | Board context smoke |
| **D1-B4** | B4 | Board **`viewGroups` / `editGroups`** (MO katman B) | [PERMISSIONS_LAYERING §4.2](../mngoperations/PERMISSIONS_LAYERING.md) |
| **D1-B5** | B5 | Kolon state’leri ↔ workspace `enabledStateIds` doğrulama | UX uyarı |

---

### Epic E — Admin UX kapanış + yetkilendirme (E1)

**Önkoşul:** A + SW-4 + D1 tamam (veya bilinçli erteleme kaydı).

| Faz | Kod | İş | DoD |
|-----|-----|-----|-----|
| **E1-P0** | | **Yetki tartışma oturumu** — aşağıdaki §4 sorular | Kararlar DEVAM + PERMISSIONS_LAYERING |
| **E1-P1** | | Genel sekme: workspace `viewGroups` / `editGroups` / `adminGroups` | Keeper grup seçici |
| **E1-P2** | | Akışlar: geçiş `requiredFields` + `permissions.groups` | Form politikası özet linki anlamlı |
| **E1-P3** | | Admin checklist (§5) — tüm maddeler ✅ veya ertelendi | «Admin Faz 1 kapalı» |
| **E1-P4** | | Form alan politikası UX (F1–F3): gruplu tablo + katman özeti | [OC_UI_FIELD_POLICY.md §10](./OC_UI_FIELD_POLICY.md) |

---

### Epic F — Operasyonel çalışma sayfası (Faz 1 runtime UI)

**Önkoşul:** Epic E — Admin Faz 1 kapanış onayı.

| Sprint | İş | Belge |
|--------|-----|--------|
| S2–S4 | Board (DnD), profil, create, transition, yorum | [OC_UI_PHASE1_PLAN.md §11](./OC_UI_PHASE1_PLAN.md) |

---

## 4. Yetkilendirme — tartışılacak konular (Epic E1-P0)

Mevcut durum:

- Workspace tanımları sayfası: **`auth.isManager`** → değilse `/unauthorized` ([index.vue](../../../Mng.Ui/pages/apps/operation-core/admin/workspace-definitions/index.vue)).
- DG **`op_*`:** permissions null → dataset API açık ([PERMISSIONS_LAYERING §5.1](../mngoperations/PERMISSIONS_LAYERING.md)).
- MO **katman B:** workspace/board/kayıt/alan/transition — runtime’da uygulanır; **admin UI’da workspace grup alanları henüz yok**.

| # | Soru | Seçenekler | Öneri (taslak) |
|---|------|------------|----------------|
| **Y1** | Admin hub’a kim girer? | yalnızca `isManager` · `isAdmin` · workspace `adminGroups` | Faz 1: **`isManager`** (mevcut); ileride workspace adminGroups |
| **Y2** | Metadata yazma (DG doğrudan) | Aynı manager guard · MO proxy API | Faz 1: **UI manager + DG açık**; disiplin UI üzerinden |
| **Y3** | Workspace grupları nerede? | Genel sekme · ayrı «Yetki» alt sekmesi | **Genel** + board/flow satırı |
| **Y4** | Normal `users` workspace görür mü? | viewGroups kesişimi · tüm domain | **viewGroups** MO runtime; admin’de yapılandır |
| **Y5** | Zamanlanmış iş schedule | yalnızca manager (onaylı) | SW belgesi ile uyumlu |
| **Y6** | `isAdmin` bypass audit | zorunlu `op_activities` | [PERMISSIONS_LAYERING L4](../mngoperations/PERMISSIONS_LAYERING.md) — MO tarafı doğrula |

**Çıktı:** Kararlar `PERMISSIONS_LAYERING.md` §7 güncellemesi + Genel/Board/Flow UI.

---

## 5. Admin Faz 1 kapanış checklist

Tüm maddeler ✅ veya §6’da gerekçeli erteleme:

- [x] **Kurallar:** R-Core + R-UX — [OC_UI_RULES_FAZ1.md](./OC_UI_RULES_FAZ1.md) §8; R-Plus (R6 automation) backlog
- [x] **Politikalar:** W1+ + P-UX — [OC_UI_WORKSPACE_POLICIES.md §0.5](./OC_UI_WORKSPACE_POLICIES.md)
- [ ] **Zamanlanmış işler:** SW-0 … SW-4 (SW-5 E2E isteğe bağlı Odak); **SW UX iyileştirme** kullanıcı geri bildirimi sonrası
- [ ] **SLA:** [SLA_FAZ1_PLAN.md](../mngoperations/SLA_FAZ1_PLAN.md) onaylı; SLA-1+ backlog tarihli
- [ ] **Board'lar:** D1-B1 … B5
- [ ] **Genel yetki grupları:** E1-P1
- [ ] **Akışlar:** `requiredFields` + transition permissions: E1-P2
- [ ] **Form politikaları:** katman özeti + layout gruplu tablo (E1-P4)
- [ ] **Politikalar W2** (type/board scope): erteleme veya ✅
- [ ] **Yetki tartışması:** §4 soruları kapalı

---

## 6. Bilinçli Faz 1.1 / Faz 2 ertelemeleri

| Konu | Neden |
|------|--------|
| Politika W2/W3 (type/board scope, gelişmiş koşul) | Kurallar + board öncelik |
| MO runtime vs taslak karşılaştırma (F9) | Operasyonel UI sonrası |
| Profil politika sekmesi (`op_profiles`) | Profil runtime ile |
| Dashboard admin + runtime | Faz 1.5 |
| Working-hours SLA, escalation job | [SLA_FAZ1_PLAN.md](../mngoperations/SLA_FAZ1_PLAN.md) Faz 2 |

---

## 7. Bugün — 28 Mayıs 2026 hedefleri

**Gün teması:** Admin metadata — **Kurallar eksiksiz** + **SLA plan** + yol haritası kilidi.

| Öncelik | Epic | Hedef | Tahmini |
|---------|------|--------|---------|
| **P0** | A1 | R1–R5 (edit, scope, koşul, meta, özet) | 1 gün |
| **P1** | C | SLA-P0 belge + DEVAM indeks | 1–2 sa |
| **P2** | A1 | R6 automation UI (zaman kalırsa) | ½ gün |
| **P3** | B | SW-0 dataset (Kurallar bittiyse) | ½ gün |
| — | E1-P0 | Yetki soruları — kısa oturum (30 dk) | Gün sonu veya yarın sabah |

**Yarın / sonraki oturum (öneri):** SW-0 → SW-4 dilimi, ardından Epic D (board), Epic E kapanış.

**Bugün yapılmayacak (bilinçli):** Board/profil operasyonel runtime, Kanban DnD, dashboard.

---

## 8. İlgili kod yolları

| Epic | Dosyalar |
|------|----------|
| Kurallar | `OcWorkspaceDefinitionsRulesTab.vue`, `OcWorkspaceRulesExplorer.vue`, `OcWorkspaceRuleDialog.vue`, `ocWorkspaceRules.ts`, `ocConditionClauses.ts`, `operationCoreService.ts` |
| Politikalar | `OcWorkspaceDefinitionsPoliciesTab.vue`, `OcWorkspaceFieldPolicyExplorer.vue`, `OcWorkspacePolicyDialog.vue`, `ocWorkspaceFieldPolicies.ts` |
| Board | `OcWorkspaceDefinitionsBoardsTab.vue`, `operationCoreService.ts` (`parseBoardColumns`) |
| SW | `OcWorkspaceDefinitionsScheduledWorkItemsTab.vue` (yeni), `useOcWorkspaceDefinitionTabs.ts` |
| Yetki | `OcWorkspaceDefinitionsGeneralTab.vue`, `OcWorkspaceDefinitionsFlowsTab.vue` |
| MO rules | `RuleEngineService.cs`, `RuleScopeMatcher.cs`, `RuleConditionEvaluator.cs` |

---

*Operasyonel UI: yalnızca §5 checklist tamamlandıktan sonra [OC_UI_PHASE1_PLAN.md §11](./OC_UI_PHASE1_PLAN.md).*
