# MngOperations — SLA Faz 1 planı (planlama belgesi)

**Son güncelleme:** 28 Mayıs 2026  
**Durum:** **Planlama** — bugün geliştirme yok; metadata + sınırlar netleştirilir  
**Spec:** [operationcore_phase1.md §20](../operationcore_phase1.md) · **Runtime:** [RUNTIME_CONTEXT.md](./RUNTIME_CONTEXT.md) (`sla` DTO) · **Admin sırası:** [OC_UI_ADMIN_FAZ1_PLAN.md Epic C](../ui/OC_UI_ADMIN_FAZ1_PLAN.md)

---

## 1. Faz 1 kapsam özeti

| Dahil (foundation) | Hariç (Faz 2+) |
|--------------------|----------------|
| `op_sla_policies` dataset taslağı | Working hours |
| WI `slaPolicyId` relation | Holiday calendar |
| Create/transition’da **due** alanları hesabı (MO `ISlaCalculator` — mevcut) | Pause / resume |
| Profil runtime’da SLA chip (operasyonel UI) | Escalation engine / job |
| Basit breach flag (`response` / `resolve`) | SLA risk prediction (AI) |

**Not:** Zamanlanmış WI ([SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md)) SLA’dan **ayrı epic**; schedule tetik WI açar, SLA ayrı policy bağlar.

---

## 2. Metadata — `op_sla_policies` (taslak)

| Alan | Tip | Açıklama |
|------|-----|----------|
| `name` | text | Politika adı |
| `workspaceId` | relation? | Boş = global şablon |
| `description` | text? | |
| `isActive` | bool | |
| `responseTargetMinutes` | number? | İlk yanıt hedefi |
| `resolveTargetMinutes` | number? | Çözüm hedefi |
| `businessCalendarId` | relation? | **Faz 2** — şimdilik null = 7/24 takvim |
| `priorityRules` | object? | Öncelik/tipe göre çarpan (Faz 1.5) |
| `breachActions` | object[]? | **Faz 2** — notification hook |

**WI tarafı:** `op_work_items.slaPolicyId` → relation; snapshot alanları MO hesaplar (`responseDueAt`, `resolveDueAt`, breach flags).

---

## 3. MO davranışı (mevcut + hedef)

| Olay | MO |
|------|-----|
| WI create | Policy seçiliyse due alanları set |
| Transition | State değişiminde SLA segment / due güncelleme |
| Query | «Breached» predefined query (Faz 1.5) |

**Entegrasyon:** [INTEGRATIONS.md §6](./INTEGRATIONS.md) — SLA job Faz 2; Faz 1 yalnızca **senkron hesap**.

---

## 4. Admin UI (Faz 1.5 — operasyonel UI öncesi plan)

| Yerleşim | İçerik |
|----------|--------|
| **Seçenek A** | Workspace tanımları → yeni sekme «SLA» |
| **Seçenek B** | Global OC admin (`/admin/definitions`) — `op_sla_policies` CRUD |

**Öneri (taslak):** Workspace-scoped politikalar → **Workspace tanımları / SLA**; global şablonlar → definitions hub.

**Form / tip bağlantısı:** `op_work_item_types.defaultSlaPolicyId` (Faz 1.5 alan taslağı).

---

## 5. Operasyonel UI (Epic F — admin kapandıktan sonra)

| Bileşen | Kaynak |
|---------|--------|
| Profil header SLA chip | `ProfileRuntimeContext.sla` |
| Breach renkleri | `responseBreached`, `resolveBreached` |
| Board kart SLA göstergesi | opsiyonel `cardFieldKeys` |

---

## 6. Uygulama fazları

| Faz | Kod | İş | Bağımlılık |
|-----|-----|-----|------------|
| **SLA-P0** | | Bu belge + dataset JSON taslağı | — |
| **SLA-0** | | `op_sla_policies` dataset + draft script | SW-0 pattern |
| **SLA-1** | | MO: policy CRUD read + create/transition hesap doğrulama | MO mevcut calculator |
| **SLA-2** | | Admin UI sekme | Admin Faz 1 kapanış |
| **SLA-3** | | Profil/board SLA gösterimi | Operasyonel UI |

---

## 7. Açık kararlar (tartışma)

| # | Soru | Not |
|---|------|-----|
| S1 | SLA politikası workspace mi global mi? | Her ikisi; workspace override |
| S2 | Tip bazlı default policy? | `op_work_item_types` relation |
| S3 | Breach sonrası otomasyon | Faz 2 — `op_rules` automation ile birleşir |
| S4 | Working hours ilk müşteri | Faz 2 — takvim dataset |

---

## 8. Bugün (28 Mayıs) çıktı

- [x] Plan belgesi (bu dosya)
- [ ] Dataset JSON taslağı (`SLA-0`) — SW-0 sonrası veya paralel
- [ ] §7 kararları — ürün oturumu

---

*Sıra: Kurallar → SW → **SLA plan** → Board admin → Yetki → Operasyonel UI.*
