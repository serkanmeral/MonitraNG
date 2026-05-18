/**
 * Asset Type Tanımları sayfası — mon_asset_type_family ve mon_asset_types.
 * Referans: docs/content/Mng.Ui/support/specs/ASSET_TYPE_DEFINITIONS_SPEC.md
 */

/** mon_asset_type_family dataset kaydı */
export interface MonAssetTypeFamily {
  __dataId: string;
  name: string;
  code?: string | null;
  description?: string | null;
}

/** mon_asset_types collectible öğesi */
export interface CollectibleDefinition {
  code: string;
  name?: string;
  data_type?: string;
  metric_key?: string;
  oid?: string;
  path?: string;
  overridable_params?: string[];
}

/** mon_asset_types dataset kaydı (tam; family relation id ile) */
export interface MonAssetTypeFull {
  __dataId: string;
  name: string;
  family: string;
  collection_method: string;
  description?: string | null;
  collectibles: CollectibleDefinition[];
}

/** mon_collectible_templates dataset kaydı */
export interface MonCollectibleTemplate {
  __dataId: string;
  name: string;
  collection_method: string;
  description?: string | null;
  collectibles: CollectibleDefinition[];
}
