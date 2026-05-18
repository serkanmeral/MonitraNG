/**
 * Organizasyon sayfası — mon_items ve mon_assets veri tipleri.
 * Referans: docs/content/Mng.Ui/support/specs/ORGANIZATION_PAGE_SPEC.md
 */

/** mon_items dataset kaydı */
export interface MonItem {
  __dataId: string;
  name: string;
  parentId: string | null;
  description?: string | null;
  location?: { lat?: number; lon?: number } | null;
  kind?: string | null;
  tags?: Array<{ key: string; value: string }> | null;
}

/** mon_assets dataset kaydı */
export interface MonAsset {
  __dataId: string;
  name: string;
  type: string;
  itemId: string;
  description?: string | null;
  tags?: Array<{ key: string; value: string }> | null;
  status: 'active' | 'maintenance' | 'decommissioned';
  connection_info: Record<string, unknown>;
  collectible_config?: Array<{ code: string; enabled: boolean; params?: Record<string, unknown> }> | null;
}

/** Tree'de tek düğüm: Item veya Asset */
export type OrganizationTreeNode =
  | { type: 'item'; data: MonItem; children: OrganizationTreeNode[] }
  | { type: 'asset'; data: MonAsset; children: [] };

/** Seçim / form için */
export type OrganizationSelectedNode =
  | { type: 'item'; data: MonItem | Partial<MonItem> }
  | { type: 'asset'; data: MonAsset | Partial<MonAsset> }
  | null;

/** mon_asset_types kaydı (formda type seçimine göre connection/collectible alanları için) */
export interface CollectibleDefinition {
  code: string;
  name?: string;
  data_type?: string;
  metric_key?: string;
  oid?: string;
  path?: string;
  overridable_params?: string[];
}

export interface MonAssetType {
  __dataId: string;
  name?: string;
  collection_method?: string;
  description?: string | null;
  collectibles?: CollectibleDefinition[];
}
