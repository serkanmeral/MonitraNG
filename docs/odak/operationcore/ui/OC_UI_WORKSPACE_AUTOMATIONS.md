# Mng.Ui — Otomatik işler (workspace otomasyonu)

**Son güncelleme:** 11 Haziran 2026  
**Durum:** Planlama onaylı — **implementasyon başlamadı** (SW-A2)  
**Backend plan:** [../mngoperations/WORKSPACE_AUTOMATION_PLANNING.md](../mngoperations/WORKSPACE_AUTOMATION_PLANNING.md)  
**Kardeş UI:** [OC_UI_SCHEDULED_WORK_ITEMS.md](./OC_UI_SCHEDULED_WORK_ITEMS.md)  
**Handoff:** [../mngoperations/DEVAM.md](../mngoperations/DEVAM.md)

---

## 1. Ürün kararı (özet)

Kullanıcı workspace'te **bir kez** otomasyon tanımlar: **ne zaman** (olay + koşul) ve **ne yapsın** (hedef board/tip + alan eşlemesi). Belirli bir iş belirli duruma geldiğinde (veya ileride alarm oluştuğunda) sistem **Yeni iş** formunu doldurup kaydetmiş gibi otomatik WI açar.

| Karar | Değer |
|-------|--------|
| UI adı | **Otomatik işler** |
| Kapsam | Tek **workspace** (MVP) |
| Tetik (MVP) | İş **duruma geldi** (+ isteğe bağlı geçiş + koşullar) |
| Tetik (faz 2) | Alarm — şema hazır, sekmede «Yakında» |
| Aksiyon (MVP) | Hedef board'da **iş oluştur** + alan eşlemesi |
| İlişki | Varsayılan **üst işe bağla** (`parentItemId`) |
| Yetki | Yalnızca **manager** (zamanlanmış işler ile aynı) |
| Yazma yolu | UI → DG `op_workspace_automations` (schedule deseni) |

---

## 2. Yerleşim — Workspace tanımları

Yeni üst sekme (önerilen sıra):

```text
[Genel] [Değerler ▾] … [Kurallar] [Zamanlanmış işler] [Otomatik işler] [SLA] …
```

| Öğe | Değer |
|-----|--------|
| Route anahtarı | `tab=automations` |
| Composable | `useOcWorkspaceDefinitionTabs.ts` — `automations` ekle |
| Bileşen | `OcWorkspaceDefinitionsAutomationsTab.vue` |
| Sayfa | `pages/apps/operation-core/admin/workspace-definitions/index.vue` |
| İkon | `mdi-lightning-bolt` veya `mdi-robot-outline` |

**Menü:** Workspace tanımları zaten `manager` pageType; ek menü gerekmez.

---

## 3. Sekme içeriği

### 3.1 Liste

| Sütun | İçerik |
|-------|--------|
| Ad | `name` |
| Tetik özeti | `trigger` — örn. «Üretim emri → hold_quality → uygunsuz» |
| Hedef | `actions[0].target` — board adı / tip adı |
| İlişki | `relation.mode` chip — «Üst işe bağla» / «Yok» |
| Durum | `isActive` chip |
| Son çalışma | `lastRunAt` |
| Son iş | `lastCreatedWorkItemId` → profil link |
| İşlemler | Düzenle, sil, **Simüle et** (SW-A4) |

**Not:** Zamanlanmış işlerdeki **«Şimdi çalıştır»** burada yok — tetik dış olaydan gelir. Simülasyon: mevcut WI ile eşleşme + eşleme önizlemesi (WI oluşturmadan).

### 3.2 Editör (dialog)

Geniş dialog; accordion veya dikey stepper (schedule editörü ile aynı yoğunluk).

#### Bölüm A — Genel

| Alan | Tip | Zorunlu |
|------|-----|---------|
| Ad | text | evet |
| Açıklama | textarea | hayır |
| Aktif | switch | evet |

#### Bölüm B — Ne zaman (tetik)

**Tetik türü** (segmented / radio):

| `kind` | MVP | UI |
|--------|-----|-----|
| `workItemStateReached` | ✅ | Varsayılan |
| `alarmRaised` | şema | Disabled + «Yakında» chip |

**Kaynak kapsamı** (WI tetik):

| Alan | Tip | Zorunlu |
|------|-----|---------|
| Kaynak board | select (workspace boards) | hayır |
| Kaynak iş tipi | select | hayır |
| Hedef durum | state select | hayır |
| Geçiş | transition select (akıştan filtreli) | hayır |
| Ek koşullar | condition builder | hayır |

Condition builder: **Politikalar & Kurallar** ile aynı UX (`op`, `field`, `cmp`, `value`). Kaynak alan listesi: scope tipinin enabled pool alanları + çekirdek alanlar.

**Canlı özet kutusu:** «Üretim panosundaki Üretim emri, `hold_quality` geçişi ile ve `qualityResult = uygunsuz` iken tetiklenir.»

#### Bölüm C — Ne yapsın (aksiyon)

MVP: tek kart **İş oluştur**. Faz 2: «+ Aksiyon» (`generateDocument` placeholder).

| Alan | Tip | Zorunlu |
|------|-----|---------|
| Hedef board | select | evet |
| Hedef iş tipi | select | evet |
| Başlık | text + token chip'leri | evet |
| Açıklama | textarea | hayır |
| Atanan | person picker / token / sabit | hayır |
| Öncelik | select | hayır |

**Alan eşleme tablosu:**

| Hedef alan | Kaynak türü | Değer |
|------------|-------------|-------|
| (hedef tip form alanı) | Kaynak alan / Sabit / Token / İlişki | path veya değer |

| Kaynak türü | UI |
|-------------|-----|
| `field` | Dropdown: kaynak tip alanları (`fields.lotSerial`, …) |
| `static` | Serbest metin |
| `token` | `{{source.key}}` vb. |
| `relation` | «Kaynak işin id'si» (parentItemId satırı) |

Butonlar: **Satır ekle** · **Zorunlu hedef alanları öner** (hedef tip formundan).

**Token cheat-sheet** (yan panel veya expandable):

```text
{{source.key}}
{{source.assignee}}
{{source.fields.<alan>}}
{{event.transitionKey}}
```

#### Bölüm D — İlişki ve tekrar

| Alan | Seçenekler | Varsayılan |
|------|------------|------------|
| İlişki | `parent` / `none` | `parent` |
| Idempotency | `none` / `one_per_source` | `none` |

Kısa açıklama (i18n): «Her tetikte yeni alt iş» vs «Kaynak başına en fazla bir».

#### Bölüm E — Önizleme

Örnek kaynak WI (kullanıcı seçimi veya seed örneği) ile üretilecek hedef payload özeti (salt okunur JSON veya alan listesi).

**Kayıt:** DG `op_workspace_automations` — MO runtime hook SW-A1 ile devreye girer.

---

## 4. Kullanıcı deneyimi notları

- Manuel **Yeni iş** ile aynı sonuç; fark yalnızca tetikleyici (olay).  
- Oluşan WI'lar normal board'da görünür; köken profilde/activity'de `workspace_automation` olarak işaretlenir.  
- Otomasyon silinince **geçmiş WI'lar kalır**.  
- Üst iş profilinde **alt kayıtlar** (`parentItemId`) ile NCR görünür.  
- Mail/bildirim aynı olayda **Politikalar & Kurallar** üzerinden ayrı tanımlanır.

---

## 5. Servis / tipler (plan)

`Mng.Ui/types/apps/operationCore.ts`:

```ts
export type OcAutomationIdempotencyMode = 'none' | 'one_per_source';
export type OcAutomationRelationMode = 'parent' | 'none';

export type OcAutomationTrigger =
  | {
      kind: 'workItemStateReached';
      boardId?: string;
      typeId?: string;
      toStateId?: string;
      transitionKey?: string;
      conditions?: OcRuleConditionTree;
    }
  | {
      kind: 'alarmRaised';
      alarmProfileId?: string;
      severity?: string[];
      conditions?: OcRuleConditionTree;
    };

export type OcFieldMappingSource =
  | { source: 'field'; path: string }
  | { source: 'static'; value: string }
  | { source: 'token'; template: string }
  | { source: 'relation'; relation: 'parent' };

export interface OcFieldMapping {
  target: string;
  source: OcFieldMappingSource['source'];
  path?: string;
  value?: string;
  template?: string;
  relation?: 'parent';
}

export type OcAutomationAction =
  | {
      type: 'createWorkItem';
      order: number;
      target: { boardId: string; typeId: string };
      title: string;
      description?: string;
      assignee?: string;
      priorityId?: string;
      fieldMappings: OcFieldMapping[];
    }
  | { type: 'generateDocument'; order: number; templateId?: string };

export interface OpWorkspaceAutomation {
  __dataId: string;
  workspaceId: string;
  name: string;
  description?: string;
  isActive: boolean;
  trigger: OcAutomationTrigger;
  idempotency: { mode: OcAutomationIdempotencyMode };
  relation: { mode: OcAutomationRelationMode };
  actions: OcAutomationAction[];
  lastRunAt?: string;
  lastCreatedWorkItemId?: string;
  runCount?: number;
}
```

`operationCoreService.ts` (veya `services/operationCore/automations.ts`):

- `ocListAutomationsForWorkspace(wsId)`
- `ocCreateWorkspaceAutomation(wsId, body)`
- `ocUpdateWorkspaceAutomation(id, body)`
- `ocDeleteWorkspaceAutomation(id)`
- `ocSimulateWorkspaceAutomation(id, { workItemId })` — SW-A4, MO endpoint

Dataset sabiti: `workItemAutomations: 'op_workspace_automations'`

---

## 6. i18n anahtarları (öneri)

Prefix: `operationCore.workspaceDefinitions.automations.*`

| Anahtar | TR örnek |
|---------|----------|
| `tabTitle` | Otomatik işler |
| `triggerSummary` | Tetik özeti |
| `targetBoard` | Hedef board |
| `fieldMappings` | Alan eşlemeleri |
| `relationParent` | Üst işe bağla |
| `idempotencyNone` | Her tetikte yeni iş |
| `idempotencyOnePerSource` | Kaynak başına bir |
| `simulate` | Simüle et |
| `alarmComingSoon` | Alarm tetikleyicisi yakında |

---

## 7. Uygulama sırası (UI)

| Sıra | Kod | İş |
|------|-----|-----|
| 1 | SW-A0 | Dataset (backend önce) |
| 2 | SW-A2a | `useOcWorkspaceDefinitionTabs` + sekme iskeleti + boş liste |
| 3 | SW-A2b | Liste + CRUD dialog (Genel + Tetik + Aksiyon + Eşleme) |
| 4 | SW-A2c | Condition builder entegrasyonu (mevcut rule builder reuse) |
| 5 | SW-A4 | Simüle et + i18n tamamlama |

**Bağımlılık:** SW-A1 (MO hook) olmadan kayıt DG'de tutulur; canlı tetik SW-A1 sonrası çalışır.

---

## 8. Odak doğrulama checklist (SW-A3 sonrası)

1. Workspace Tanımları → Otomatik işler → «Uygunsuzluk → NCR» kaydı görünür  
2. Üretim emri `hold_quality` + uygunsuz → NCR Kalite kuyruğunda oluşur  
3. NCR profilinde üst emir (ODF) görünür; ODF profilinde alt NCR listelenir  
4. Activity'de otomasyon kökeni / `AutomationExecuted` (SW-A4)

---

## 9. İlgili dosyalar

| Dosya | Rol |
|-------|-----|
| [WORKSPACE_AUTOMATION_PLANNING.md](../mngoperations/WORKSPACE_AUTOMATION_PLANNING.md) | Mimari + dataset + MO |
| [OC_UI_SCHEDULED_WORK_ITEMS.md](./OC_UI_SCHEDULED_WORK_ITEMS.md) | Kardeş sekme UX |
| [../../is_surecleri/DEVAM.md](../../is_surecleri/DEVAM.md) | Odak workspace id'leri |
