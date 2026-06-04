# Otomasyon Merkezi (UI)

Platform **tepki orkestrasyonu** arayüzü — workflow tanımı, sürüm yönetimi ve (ileride) canvas tasarımcı.

**Backend:** `MngWorkflow.Api` + Worker — değişiklik gerektirmez (W1/W2 yalnızca UI).

---

## Route'lar

| Sayfa | Route |
|-------|--------|
| İş akışı listesi | `/apps/automation-center/workflows` |
| Taslak editör (W1 form) | `/apps/automation-center/workflows/[workflowId]` |

Eski: `/apps/operation-core/admin/workflows/*` → redirect.

---

## Kod (Mng.Ui)

| Alan | Konum |
|------|--------|
| Explorer | `components/apps/automation-center/workflows/AcWorkflowsExplorer.vue` |
| Editör | `components/apps/automation-center/workflows/AcWorkflowEditor.vue` |
| API client | `services/workflowService.ts` |
| Node katalog | `constants/workflowNodeCatalog.ts` |
| i18n | `automationCenter.workflows.*` |

---

## Menü (Odak)

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\automation\scripts\patch-automation-side-menu.ps1
```

Header: **Otomasyon Merkezi** → **İş Akış Yönetimi**

---

## Sıradaki

- **UI-W2:** Vue Flow canvas tasarımcı (form editörün üzerine veya alternatif görünüm)
- Onay inbox Operasyon'da kalır (`/apps/operation-core/approvals`)

Handoff: [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md) · Workflow backend: [../workflow/DEVAM.md](../workflow/DEVAM.md)
