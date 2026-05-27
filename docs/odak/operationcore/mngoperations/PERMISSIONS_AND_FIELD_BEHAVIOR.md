# MngOperations — Permission ve field behavior

**Son güncelleme:** 26 Mayıs 2026  
**Spec:** [operationcore_phase1.md §9–11](../operationcore_phase1.md)  
**DG × MO katmanları:** [PERMISSIONS_LAYERING.md](./PERMISSIONS_LAYERING.md)

---

## 1. Kimlik context

MO’ya gelen istekteki Bearer token parse edilir (`IRequestContext`). Aynı token DG’ye forward edilir — tenant ve DG permission ile MO permission tutarlı kalır ([AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md)).

JWT’den (`IRequestContext`) — Keeper üretir; domain grup üyeliği MO’da **yönetilmez**:

| Claim / alan | Kullanım |
|--------------|----------|
| `preferred_username` | Actor, assignee eşleme |
| `user_groups` | Domain grupları: Odak’ta tipik `managers`, `users`; platform `admins` ayrı |
| `domain_id` / `domain_name` | Tenant izolasyonu |
| `isAdmin` / `is_admin` | Platform admin — operasyonel tam yetki + audit ([PERMISSIONS_LAYERING §5.3](./PERMISSIONS_LAYERING.md)) |
| `isManager` / `is_manager` | Domain yöneticisi — geniş workspace/transition yetkisi |

Stateless: her istekte token parse; session store yok.

---

## 2. Group-first permission (production)

**Sıra** ([PERMISSIONS_LAYERING §5.3](./PERMISSIONS_LAYERING.md)):

1. **`isAdmin`** → allow (platform; Odak domain gruplarından bağımsız)
2. **`isManager`** → domain manager policy (workspace admin / tüm workspace edit — implementasyon detayı L4 ile netleşir)
3. **`user_groups`** ∩ workspace `viewGroups` / `editGroups` / transition `permissions.groups`

| Seviye | Kaynak | Odak örnek gruplar |
|--------|--------|---------------------|
| Workspace | `viewGroups`, `editGroups`, `adminGroups` | `users`, `managers` |
| Transition | `transitions[].permissions.groups` | `managers`, `users` |
| Board | `op_boards` permission metadata | Kolon görünürlüğü |
| DG dataset | `op_*` → permissions **yok** (herkes); detay MO’da |

```text
allowed =
  context.IsPlatformAdmin          // L4: tam bypass + audit activity
  OR context.IsDomainManager && managerPolicyAllows(action)
  OR userGroups.Intersects(requiredGroups)
```

**L4 (`isAdmin`):** `viewGroups` / `editGroups` / transition `permissions.groups` **kontrol edilmez**. `op_activities` (veya eşdeğeri) ile `PlatformAdminOverride` benzeri tip + hedef workspace/workItem kaydı zorunlu.

---

## 3. Field-level runtime

Faz 1 behavior seti:

| Behavior | Anlam |
|----------|--------|
| `visible` | UI alanı gösterme |
| `readonly` | Düzenlenemez |
| `required` | Zorunlu (create/transition) |
| `masked` | Hassas maskeleme |

---

## 4. Merge kaynakları (sıra)

```text
Field Definition (op_fields)
 → Form (op_forms)
 → Profile (op_profiles)
 → Workspace
 → Board
 → State (mevcut stateId)
 → Permission layer
 → Rule (validation/default)
 → Automation (geçici override)
```

**Strateji:** [Most Restrictive Wins](../operationcore_phase1.md) — örn. bir kaynak `readonly: true` ise sonuç readonly.

`IFieldBehaviorResolver.Resolve(workItem, screenContext, fieldName)` → `FieldBehaviorDto`.

---

## 5. Transition required fields

Transition `requiredFields[]` + merged `required: true` birleşimi:

- Apply öncesi eksik alan → 400 `REQUIRED_FIELD_MISSING`
- PATCH’te readonly alan gönderimi → 403 veya 400

---

## 6. DG permission ile ilişki

MngDataGateway dataset-level permission ayrıca çalışır. MO:

- Servis hesabı **kullanmaz** (Faz 1) — kullanıcı token forward
- DG 403 dönerse MO 403 propagate

Dataset tanım CRUD yalnızca admin kullanıcılar (DG + Keeper).

---

## 7. Audit

Permission deny → `op_activities` tipi `PermissionDenied` (opsiyonel Faz 1, önerilir).
