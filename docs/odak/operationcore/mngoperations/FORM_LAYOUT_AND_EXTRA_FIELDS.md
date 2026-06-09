# MngOperations — Form layout, extraFields, runtime form

**Son güncelleme:** 9 Haziran 2026  
**Spec:** [operationcore_phase1.md §8.5](../operationcore_phase1.md), [RUNTIME_CONTEXT.md](./RUNTIME_CONTEXT.md)

---

## 1. Özet

| Konu | Uygulama |
|------|-----------|
| Pool alan değerleri | `op_work_items.extraFields[key]` |
| Core alan değerleri | Üst seviye kolonlar (`title`, `typeId`, …) |
| Create/Patch `fields` gövdesi | `WorkItemFieldWriter` — core üst seviye, pool → `extraFields` |
| Form layout kaynağı | `op_forms.layout` (JSON) |
| Runtime alan listesi | `FormLayoutHelper` + `FormFieldCatalog` + `FormRuntimeBuilder` |

---

## 2. `op_forms.layout` JSON (UI + DG)

Kayıt: `op_forms` dataset, `layout` object alanı.

```json
{
  "formHeading": "Üst başlık (isteğe bağlı)",
  "formIntro": "Üst açıklama (isteğe bağlı, düz metin)",
  "helpMarkdown": "# Kullanım\n\nMarkdown yardım metni (Yeni iş modalında Yardım butonu)",
  "dialogMaxWidth": 920,
  "sectionOrder": ["main", "other"],
  "sectionCols": { "main": 12, "other": 6 },
  "fieldCols": { "title": 12, "typeId": 6, "urgency": 6 },
  "sections": [
    {
      "key": "main",
      "title": "Temel bilgiler",
      "cols": 12,
      "fields": ["title", "description", "typeId", "assignee", "priorityId"]
    }
  ]
}
```

| Alan | Açıklama |
|------|----------|
| `sections[]` | Bölüm sırası = dizi sırası (veya `sectionOrder` ile override) |
| `sections[].cols` | Bölüm bloğu genişliği (12 sütunlu ızgara, 1–12) |
| `fieldCols` | Alan genişliği (Vuetify `md` cols) |
| `dialogMaxWidth` | Önizleme modalı ve «Yeni iş» içerik genişliği (px, 480–1400) |

MO `FormRuntimeContext.Layout` bu JSON’u **değiştirmeden** UI’ya iletir (`JsonElement`).

---

## 3. extraFields (§8.5)

**Kod:** `MngOperations/Core/MngOperations.Application/Utilities/`

| Dosya | Rol |
|-------|-----|
| `WorkItemCoreFields.cs` | Yazılabilir / reserved core key seti |
| `WorkItemFieldCatalog.cs` | Workspace `enabledFieldIds` + pool metadata |
| `WorkItemFieldWriter.cs` | Create/Patch/transition/rule → core vs `extraFields` |
| `WorkItemDataHelper.GetFieldValue` | Okuma: core veya `extraFields[key]` |

**Create API:** `POST /api/v1/work-items` — üst seviye `title`, `typeId`, … + isteğe bağlı `fields` object (pool + ek core).

**Hata kodları:** `UNKNOWN_FIELD`, `FIELD_NOT_ENABLED`, `RESERVED_FIELD`, `INVALID_FIELDS`.

---

## 4. Form runtime üretimi

| Dosya | Rol |
|-------|-----|
| `FormLayoutHelper.cs` | `layout.sections[].fields` sırasından alan key listesi |
| `FormFieldCatalog.cs` | Core + enabled pool label/type |
| `FormRuntimeBuilder.cs` | `fields` + `fieldBehaviors` parse (layout sırası) |
| `RuntimeContextService.BuildFormContextAsync` | Form seçimi, permissions, behavior resolver |

**Endpoint:**

- Create: `GET /api/v1/runtime/work-items/form?workspaceId=&mode=create&formId=`
- Edit: `GET /api/v1/runtime/work-items/{id}/form?mode=edit`

---

## 5. Metadata önbellek

`IMetadataCache.GetFormAsync` — form kaydı TTL ile cache’lenir (~600 sn Odak prod).

UI’da form / alan tanımı **DG’den** güncellenir; MO runtime birkaç dakika gecikmeli yansıyabilir. Taslak önizleme UI’da editör state’inden üretilir (MO’ya bağlı değil).

**Manuel yenileme (9 Haz 2026):**

| Katman | Detay |
|--------|--------|
| MO | `POST /api/v1/workspaces/{workspaceId}/metadata-cache/reload` — workspace kapsamlı cache anahtarlarını siler; sonraki runtime isteği DG’den taze okur |
| DTO | `MetadataCacheReloadResult` (`keysRemoved`, `workspaceId`) |
| UI | Workspace Tanımları → Genel → **Runtime önbelleğini yenile** (`ocReloadWorkspaceMetadataCache`) |

**Otomatik yenileme (OC-CACHE-3):** Alan / form / board / akış / alan politikası kaydı sonrası UI `useOcWorkspaceMetadataCacheReload` ile aynı uç fail-soft çağrılır; başarı mesajına önbellek sonucu eklenir.

Form veya `op_fields` kaydı sonrası board «Yeni iş» / profil formunda eski layout görünüyorsa önce DG kaydının başarılı olduğunu doğrulayın, ardından bu uç veya ~TTL bekleyin.

---

## 6. Deploy notu

Layout / `FormRuntimeBuilder` değişikliklerinden sonra Odak:

```powershell
.\scripts\odak\sync-odak-source.ps1
.\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations
```

Yerel build: API çalışıyorsa DLL kilitlenebilir — `Infrastructure` + `Application` projelerini build edin veya API’yi durdurun.

---

## 7. İlgili UI dokümanı

[Mng.Ui form tanımlama ve önizleme](../ui/OC_UI_FORM_DEFINITIONS.md)
