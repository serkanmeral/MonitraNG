# Organizasyon Sayfası — Tree + View/Edit Panel Spec

**Amaç:** Side Menu Manager benzeri tree + form panel yapısı ile Organizasyon (Item hiyerarşisi + Asset'ler) sayfası tasarımı. Tree içinde Item'lar ve altında nested Asset'ler; seçilen öğe için view/edit paneli.

**Referanslar:**
- **Side Menu Manager** — Sayfa: `Mng.Ui/pages/apps/side-menu-manager/index.vue`; component’ler: `components/apps/side-menu-manager/` (MenuTreeView, TreeItem, MenuItemForm, MenuItemToolbar).
- [MONITORING_ASSET_DATASETS](../../../monitoring_plans/MONITORING_ASSET_DATASETS.md) — mon_items, mon_assets şemaları
- [monitrang_monitoring_planlama – Organizasyon Mimarisi](../../../monitoring_plans/monitrang_monitoring_planlama.md#3-organizasyon-mimarisi)

---

## 1. Side Menu Manager Özeti (Referans Alınacak Yapı)

### 1.1 Sayfa Layout

- **Toolbar** (üst): Yeni öğe, arama, yenile.
- **Sol panel (Tree):** `MenuTreeView` → recursive `TreeItem`; expand/collapse, seçim, drag & drop (vue-draggable-next).
- **Sağ panel (Form):** `MenuItemForm`; seçilen öğe için Yeni/Düzenle başlığı, kaydet/sil/iptal.

### 1.2 Store Akışı

- `sideMenuManager`: `menuItems` (flat), `menuItemsTree` (parentId ile build edilmiş), `selectedItem`, `loading`, `error`, `searchQuery`.
- `loadMenuItems()` → API'den veri → `buildMenuTree()` → `filteredMenuItemsTree` getter (arama ile filtrelenmiş).
- `selectItem`, `createMenuItem`, `updateMenuItem`, `deleteMenuItem`, `resetSelection`.

### 1.3 Tree Bileşenleri

| Bileşen | Rol |
|---------|-----|
| **MenuTreeView** | Kök liste; draggable wrapper; expanded state; `item-select`, `item-order-change` emit. |
| **TreeItem** | Tek düğüm; expand/collapse, label, icon, drag handle; recursive children; navigate butonu (menu için). |
| **MenuItemForm** | Form alanları; item/allItems ile parent seçimi; kaydet/sil/iptal. |
| **MenuItemToolbar** | Yeni header/item, arama, yenile. |

### 1.4 Taşınabilir Özellikler

- **Layout:** Toolbar + 2 kolon (tree | form); responsive (md/lg kolon genişlikleri).
- **Tree:** Recursive tree, expand/collapse, seçim vurgusu, boş durum mesajı.
- **Drag & drop:** İsteğe bağlı; Item'lar için parent/sıra değişimi mantıklı (Asset'ler için farklı kurallar gerekebilir).
- **Form panel:** Seçilen öğe null ise “Yeni …” / dolu ise “Düzenle”; kaydet/sil/iptal.

---

## 2. Organizasyon Veri Modeli (Özet)

- **mon_items:** Hiyerarşik. `parentId` → mon_items (null = kök). Alanlar: name, description, location (lat/lon), kind, tags.
- **mon_assets:** Her asset **bir Item’a bağlı** (`itemId` → mon_items). Alanlar: name, type (mon_asset_types), itemId, description, connection_info, collectible_config, tags, status.
- **Tree’de gösterim:** Bir Item node’unun altında hem **alt Item’lar** (parentId = bu item) hem **bu Item’a ait Asset’ler** (itemId = bu item) gösterilebilir. Yani tek tree’de iki tip düğüm: **Item** ve **Asset**. Asset’ler yaprak olur (altında başka tree node’u yok).

---

## 3. Organizasyon Sayfası Önerilen Yapı

### 3.1 Route ve Sayfa

- **Route:** `/apps/organization` (veya `/apps/monitoring/organization`).
- **Sayfa:** `pages/apps/organization/index.vue` (veya `pages/apps/monitoring/organization/index.vue`).

### 3.2 Layout (Side Menu Manager ile Aynı Mantık)

```
┌─────────────────────────────────────────────────────────────────────────┐
│  BaseBreadcrumb: Organizasyon                                            │
├─────────────────────────────────────────────────────────────────────────┤
│  Toolbar: [Yeni Item] [Yeni Asset] | Arama | Yenile                      │
├──────────────────────┬──────────────────────────────────────────────────┤
│  Sol: Tree            │  Sağ: View/Edit Panel                            │
│  (Organizasyon ağacı) │  (Seçilen Item veya Asset)                        │
│                       │                                                   │
│  ▼ Istanbul           │  Başlık: "Yeni Item" / "Düzenle: sunucu1"        │
│    ▼ Çamlıca Bölge    │  ─────────────────────────────────────────────  │
│      ▼ 1. Sistem odası│  [Form alanları – Item veya Asset’e göre]        │
│        ▼ 2 nolu kabin │  [Kaydet] [Sil] [İptal]                          │
│          • sunucu1    │                                                   │
│          • PDU-01     │                                                   │
│          • sunucu1-OS │  (Asset seçiliyse: connection_info, type, vb.)   │
│                       │                                                   │
└──────────────────────┴──────────────────────────────────────────────────┘
```

### 3.3 Tree İçeriği ve İki Düğüm Tipi

- **Item düğümü:** `mon_items` kaydı. Altında:
  - Aynı item’ın `parentId`’si olan **Item’lar** (alt lokasyonlar),
  - Aynı item’ın `itemId`’si olan **Asset’ler**.
- **Asset düğümü:** `mon_assets` kaydı. Yaprak; altında düğüm yok.
- **Sıralama:** Önce alt Item’lar (ör. name ile), sonra Asset’ler (ör. name ile). İsteğe bağlı: Item/Asset ayrı gruplar (“Lokasyonlar” / “Cihazlar”) şeklinde de gösterilebilir.

### 3.4 Tree Bileşenleri (Yeniden Kullanım / Uyarlama)

| Seçenek | Açıklama |
|---------|----------|
| **A) Generic tree component** | Side Menu’daki `TreeItem`/tree view’ı generic hale getirip `nodeType: 'item' | 'asset'` ve farklı label/icon kullanmak. |
| **B) Organizasyon’a özel tree** | `OrganizationTreeView.vue` + `OrganizationTreeItem.vue`; Item/Asset ayrımı, “alt item” ve “asset listesi” veri yapısına göre. |

**Tavsiye:** B. Organizasyon tree’de iki farklı veri kaynağı (items tree + items’a göre gruplanmış assets) birleştirildiği için, Side Menu’daki tek `parentId` listesinden farklı bir **birleşik tree modeli** (unified list of nodes: Item veya Asset) store’da üretilip tree’e verilir. Böylece Side Menu’daki recursive yapı korunur, ama her düğümde `nodeType` ve `item` | `asset` verisi taşınır.

### 3.5 Store Önerisi

- **organizationStore** (veya `organizationManager`):
  - `items: MonItem[]` — mon_items flat liste.
  - `assets: MonAsset[]` — mon_assets flat liste.
  - `treeNodes: OrganizationTreeNode[]` — birleşik ağaç (her eleman `{ type: 'item', data: MonItem }` veya `{ type: 'asset', data: MonAsset }`; children ile).
  - `selectedNode: OrganizationTreeNode | null`
  - `loading`, `error`, `searchQuery`
  - Actions: `loadItems()`, `loadAssets()`, `buildTree()`, `selectNode()`, `createItem()`, `updateItem()`, `deleteItem()`, `createAsset()`, `updateAsset()`, `deleteAsset()`.
- **Tree build:** Önce items’tan parentId ile hiyerarşi kurulur; her item node’una, o item’ın `__dataId`’sine sahip asset’ler children olarak eklenir (type: 'asset'). Böylece tek recursive tree elde edilir.

### 3.6 View/Edit Panel

- **Seçim yok:** “Bir öğe seçin veya Yeni Item / Yeni Asset” mesajı (veya toolbar’daki Yeni Item/Asset ile hemen form açılır).
- **Item seçili:** Item formu — name, parentId (dropdown/tree select), description, location (lat/lon), kind, tags. Kaydet / Sil / İptal.
- **Asset seçili:** Asset formu — name, itemId (readonly veya seçildiği item’dan gelir), type (mon_asset_types relation), description, connection_info, collectible_config, tags, status. Kaydet / Sil / İptal.
- **Yeni Item:** parentId seçilebilir (mevcut item seçiliyse varsayılan parent olabilir).
- **Yeni Asset:** itemId zorunlu (mevcut item seçiliyse varsayılan item olabilir).

### 3.7 Toolbar

- **Yeni Item:** selectedNode’u null veya “yeni item” moduna alır; parentId opsiyonel (şu an seçili node Item ise onu parent yap).
- **Yeni Asset:** “yeni asset” modu; itemId için seçili node Item ise onu kullan, değilse zorunlu alan.
- **Arama:** Tree’de name/description üzerinden filtre (store’da `searchQuery` → filtered tree).
- **Yenile:** `loadItems()` + `loadAssets()` + `buildTree()`.

### 3.8 Yetkilendirme ve buton görünürlüğü (Monitoring UI)

**Not:** Monitoring UI'da (Organizasyon sayfası dahil) ekleme, silme ve güncelleme ile ilgili tüm butonlar **sadece `is_manager` veya `is_admin` kullanıcılarına** gösterilir.

- **Auth store:** `useAuthStore().isManager` getter'ı `userInfo.isAdmin === true` veya `userInfo.is_manager === true` ise `true` döner; admin kullanıcılar otomatik olarak manager yetkisine sahiptir.
- **Sayfa:** `canEdit = computed(() => authStore.isManager)` hesaplanır ve ilgili bileşenlere `canEdit` prop'u ile geçirilir.
- **Görünürlük:**
  - **Toolbar:** "Yeni Item" ve "Yeni Asset" butonları yalnızca `canEdit === true` iken gösterilir.
  - **Item form:** "Kaydet" ve "Sil" butonları yalnızca `canEdit === true` iken gösterilir; "İptal" her zaman gösterilir.
  - **Asset form:** Aynı şekilde "Kaydet" ve "Sil" yalnızca `canEdit` iken; "İptal" her zaman.
- Arama ve Yenile butonları tüm kullanıcılara açıktır (sadece okuma). Tree'den öğe seçimi ve form alanlarının görüntülenmesi de tüm kullanıcılar için geçerlidir; sadece değişiklik yapma aksiyonları (ekle/güncelle/sil) manager/admin'e kısıtlıdır.

---

## 4. Side Menu Manager’dan Farklar ve Dikkat Edilecekler

| Konu | Side Menu Manager | Organizasyon Sayfası |
|------|-------------------|----------------------|
| Veri kaynağı | Tek dataset (@side_menu) | İki dataset (mon_items, mon_assets) |
| Tree yapısı | parentId ile tek tip | Item (parentId) + Asset (itemId) birleşik |
| Drag & drop | Tüm düğümler taşınabilir | Item’lar taşınabilir (parentId/order); Asset’ler sadece aynı Item altında sıralanabilir veya drag kapatılabilir |
| Form | Tek MenuItemForm | İki form: ItemForm, AssetForm (veya tek form içinde type’a göre alan seti) |
| API | DG data @side_menu | DG data mon_items, mon_assets (Reactor üzerinden de olabilir; plana göre) |
| Silme | Menu item silme | Item silmeden önce alt item ve asset kontrolü; Asset doğrudan silinebilir |

---

## 5. Implementasyon Tavsiyeleri

1. **Önce store + tree build:** `mon_items` ve `mon_assets` çekip birleşik `treeNodes` üretin; tree’i sadece bu listeyle besleyin. Expand/collapse ve seçim Side Menu ile aynı mantıkta.
2. **Tree component:** `OrganizationTreeView` + `OrganizationTreeItem` ile başlayın; her node’da `nodeType` ve `data` kullanın. Icon: Item için klasör/yer, Asset için cihaz ikonu.
3. **Form panel:** Item ve Asset için ayrı iki form component (veya tek component içinde `v-if` ile) kullanın. connection_info/collectible_config karmaşık olduğu için Asset formu aşamalı genişletilebilir (önce name, type, itemId, status).
4. **Drag & drop:** İlk sürümde sadece Item’lar için açın; Asset’leri taşıma (itemId değiştirme) sonra eklenebilir. Side Menu’daki `item-order-change` benzeri bir event ile parentId/order güncelleyin.
5. **API:** DG `GET /api/v1/data/mon_items`, `mon_assets` (limit uygun; sayfa sayfa da alınabilir). Create/Update/Delete DG veya Reactor endpoint’lerine göre yapılacak (Monitoring planında Reactor üzerinden CRUD da var).
6. **Yetkilendirme:** `usePagePermissions` ve menü yetkisi; Side Menu Manager’daki gibi sayfa erişim kontrolü.
7. **i18n:** Baştan key’ler (organization.tree.*, organization.form.item.*, organization.form.asset.*) tanımlayın.

---

## 6. Kısa Checklist

- [ ] `organizationStore` (veya organizationManager) — items, assets, treeNodes, selectedNode, load/build/CRUD.
- [ ] `OrganizationTreeView.vue` — tree container, expand all/collapse all, arama sonucu.
- [ ] `OrganizationTreeItem.vue` — Item/Asset node, recursive children, seçim, (opsiyonel) drag handle.
- [ ] `OrganizationToolbar.vue` — Yeni Item, Yeni Asset, Arama, Yenile.
- [ ] `OrganizationItemForm.vue` — mon_items alanları.
- [ ] `OrganizationAssetForm.vue` — mon_assets alanları (ilk aşamada temel alanlar).
- [ ] `pages/apps/organization/index.vue` — layout, toolbar + tree + form panel.
- [ ] Side menüye “Organizasyon” linki (Monitoring veya Apps altında).
- [ ] mon_items / mon_assets dataset’lerinin DG’de oluşturulmuş olması (Monitoring Faz 0).

---

## 7. Store Interface ve Component Prop/Emit Taslakları

Aşağıdaki TypeScript interface'leri ve Vue prop/emit tanımları implementasyon için referans alınabilir. DG/Reactor API yanıt alanları (camelCase / PascalCase) projede kullanılan convention'a göre normalize edilebilir.

### 7.1 MonItem (mon_items)

```typescript
/** mon_items dataset kaydı */
export interface MonItem {
  __dataId: string;
  name: string;
  parentId: string | null;
  description?: string | null;
  /** Opsiyonel: { lat: number; lon: number } */
  location?: { lat?: number; lon?: number } | null;
  /** Örn. city, region, room, cabinet, server, pdu */
  kind?: string | null;
  /** key-value dizisi */
  tags?: Array<{ key: string; value: string }> | null;
}
```

### 7.2 MonAsset (mon_assets)

```typescript
/** mon_assets dataset kaydı */
export interface MonAsset {
  __dataId: string;
  name: string;
  /** mon_asset_types __dataId */
  type: string;
  /** mon_items __dataId — zorunlu */
  itemId: string;
  description?: string | null;
  tags?: Array<{ key: string; value: string }> | null;
  status: 'active' | 'maintenance' | 'decommissioned';
  /** Bağlantı bilgisi; Reactor şifreleyebilir */
  connection_info: Record<string, unknown>;
  /** [{ code, enabled, params? }] */
  collectible_config?: Array<{ code: string; enabled: boolean; params?: Record<string, unknown> }> | null;
}
```

### 7.3 OrganizationTreeNode (birleşik tree düğümü)

```typescript
/** Tree'de tek düğüm: Item veya Asset */
export type OrganizationTreeNode =
  | { type: 'item'; data: MonItem; children: OrganizationTreeNode[] }
  | { type: 'asset'; data: MonAsset; children: [] };

/** Seçim / form için: hangi tip ve hangi data */
export type OrganizationSelectedNode =
  | { type: 'item'; data: MonItem | Partial<MonItem> }
  | { type: 'asset'; data: MonAsset | Partial<MonAsset> }
  | null;
```

### 7.4 Store state ve actions (taslak)

```typescript
interface OrganizationState {
  items: MonItem[];
  assets: MonAsset[];
  treeNodes: OrganizationTreeNode[];
  selectedNode: OrganizationSelectedNode | null;
  loading: boolean;
  error: string | null;
  searchQuery: string;
}

// Getter örneği
filteredTreeNodes(state): OrganizationTreeNode[] {
  if (!state.searchQuery.trim()) return state.treeNodes;
  return filterTreeRecursive(state.treeNodes, state.searchQuery.toLowerCase());
}

// Actions (imza)
loadItems(): Promise<void>;
loadAssets(): Promise<void>;
buildTree(): void;
selectNode(node: OrganizationSelectedNode): void;
resetSelection(): void;
createItem(payload: Partial<MonItem>): Promise<void>;
updateItem(dataId: string, payload: Partial<MonItem>): Promise<void>;
deleteItem(dataId: string): Promise<void>;
createAsset(payload: Partial<MonAsset>): Promise<void>;
updateAsset(dataId: string, payload: Partial<MonAsset>): Promise<void>;
deleteAsset(dataId: string): Promise<void>;
setSearchQuery(query: string): void;
```

### 7.5 Tree build helper (pseudo)

```typescript
function buildTree(items: MonItem[], assets: MonAsset[]): OrganizationTreeNode[] {
  const byParent = new Map<string | null, MonItem[]>();
  const assetsByItem = new Map<string, MonAsset[]>();
  items.forEach(i => {
    const p = i.parentId ?? null;
    if (!byParent.has(p)) byParent.set(p, []);
    byParent.get(p)!.push(i);
  });
  assets.forEach(a => {
    if (!assetsByItem.has(a.itemId)) assetsByItem.set(a.itemId, []);
    assetsByItem.get(a.itemId)!.push(a);
  });
  function build(parentId: string | null): OrganizationTreeNode[] {
    const childItems = (byParent.get(parentId) ?? []).sort((a, b) => a.name.localeCompare(b.name));
    const result: OrganizationTreeNode[] = [];
    for (const item of childItems) {
      const itemAssets = (assetsByItem.get(item.__dataId) ?? []).sort((a, b) => a.name.localeCompare(b.name));
      const children: OrganizationTreeNode[] = [
        ...build(item.__dataId),
        ...itemAssets.map(a => ({ type: 'asset' as const, data: a, children: [] })),
      ];
      result.push({ type: 'item', data: item, children });
    }
    return result;
  }
  return build(null);
}
```

---

## 8. OrganizationTreeView — Props ve Emits

### 8.1 Props

| Prop | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `items` | `OrganizationTreeNode[]` | Evet | Kök seviye tree düğümleri (store'dan filteredTreeNodes). |
| `selectedNode` | `OrganizationSelectedNode \| null` | Evet | Şu an seçili düğüm (vurgulama için). |
| `loading` | `boolean` | Hayır | Tree yüklenirken linear progress. |

### 8.2 Emits

| Event | Payload | Açıklama |
|-------|---------|----------|
| `node-select` | `OrganizationSelectedNode` | Kullanıcı bir düğüme tıkladığında. |
| `item-order-change` | `(itemDataId: string, newParentId: string \| null, newSiblingIndex?: number)` | Sadece Item drag edildiğinde; parent veya sıra değişti. (İlk sürümde opsiyonel.) |

### 8.3 Örnek kullanım (sayfa)

```vue
<OrganizationTreeView
  :items="organizationStore.filteredTreeNodes"
  :selected-node="organizationStore.selectedNode"
  :loading="organizationStore.loading"
  @node-select="organizationStore.selectNode($event)"
  @item-order-change="handleItemOrderChange"
/>
```

### 8.4 Expose (ref ile)

- `expandAll(): void`
- `collapseAll(): void`

(Side Menu Manager'daki gibi parent'tan expand/collapse all tetiklenebilir.)

---

## 9. OrganizationTreeItem — Props ve Emits

### 9.1 Props

| Prop | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `node` | `OrganizationTreeNode` | Evet | Bu düğümün verisi (item veya asset). |
| `selectedNode` | `OrganizationSelectedNode \| null` | Evet | Seçili düğüm (isSelected hesaplamak için). |
| `expandedIds` | `Set<string>` | Evet | Açık olan düğüm __dataId'leri (Item'lar için; Asset'lerde expand yok). |
| `level` | `number` | Hayır | Girinti seviyesi (varsayılan 0). |
| `draggableItems` | `boolean` | Hayır | Item düğümleri için drag handle gösterilsin mi (varsayılan true). |

### 9.2 Emits

| Event | Payload | Açıklama |
|-------|---------|----------|
| `node-select` | `OrganizationSelectedNode` | Bu düğüme tıklandığında. |
| `toggle-expand` | `OrganizationTreeNode` | Expand/collapse okuna tıklandığında (sadece Item). |
| `item-order-change` | `(itemDataId: string, newParentId: string \| null, newSiblingIndex?: number)` | Item sürüklenip bırakıldığında (opsiyonel). |

### 9.3 Seçim eşlemesi

- **Item düğümü** için emit: `{ type: 'item', data: node.data }`.
- **Asset düğümü** için emit: `{ type: 'asset', data: node.data }`.
- `selectedNode` ile karşılaştırma: `node.type === selectedNode?.type && node.data.__dataId === selectedNode?.data?.__dataId` → isSelected.

### 9.4 Label ve ikon

- **Item:** `node.data.name`; ikon örn. `FolderIcon` / `MapPinIcon` (vue-tabler-icons).
- **Asset:** `node.data.name`; ikon örn. `DeviceDesktopIcon` / `CpuIcon`.
- Asset'lerde `children.length === 0` olduğu için expand ok'u gösterilmez veya disabled.

### 9.5 Örnek kullanım (OrganizationTreeView içinde recursive)

```vue
<OrganizationTreeItem
  v-for="child in node.type === 'item' ? node.children : []"
  :key="child.type === 'item' ? child.data.__dataId : child.data.__dataId"
  :node="child"
  :selected-node="selectedNode"
  :expanded-ids="expandedIds"
  :level="level + 1"
  :draggable-items="draggableItems"
  @node-select="$emit('node-select', $event)"
  @toggle-expand="$emit('toggle-expand', $event)"
  @item-order-change="$emit('item-order-change', $event)"
/>
```

(Item node'unda hem alt Item'lar hem Asset'ler `node.children` içinde; Asset node'unda children boş, recursive render yapılmaz.)

---

## 10. Form panel için kısa payload taslakları

Form'dan store action'a gönderilecek payload'lar; API'ye aynen veya hafifçe map edilerek verilebilir.

```typescript
/** Yeni Item oluşturma */
type CreateItemPayload = Pick<MonItem, 'name' | 'parentId' | 'description' | 'location' | 'kind' | 'tags'>;

/** Item güncelleme */
type UpdateItemPayload = Partial<CreateItemPayload>;

/** Yeni Asset oluşturma */
type CreateAssetPayload = Pick<MonAsset, 'name' | 'type' | 'itemId' | 'description' | 'tags' | 'status' | 'connection_info'> & {
  collectible_config?: MonAsset['collectible_config'];
};

/** Asset güncelleme */
type UpdateAssetPayload = Partial<CreateAssetPayload>;
```

Bu spec, Side Menu Manager deneyimini kullanarak Organizasyon sayfasının nasıl inşa edilebileceğini, store/form tiplerini ve tree component prop/emit'lerini tanımlar.

**İlk implementasyon (Şubat 2026):** Sayfa `pages/apps/organization/index.vue`, store `stores/apps/organization.ts`, types `types/apps/organization.ts`, bileşenler `components/apps/organization/` (OrganizationTreeView, OrganizationTreeItem, OrganizationToolbar, OrganizationItemForm, OrganizationAssetForm) eklendi. Route: `/apps/organization`. Side menüye link manuel eklenir.
