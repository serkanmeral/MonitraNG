import {
  defaultOdakPackagePoDocumentAccessConfig,
  mergeOdakPackagePoDocumentAccessConfig,
  type OdakPackagePoDocumentAccessConfig,
} from '@/utils/odakSiparisPoDocumentAccess';
import {
  defaultOdakPackagePersonnelConfig,
  mergeOdakPackagePersonnelConfig,
  type OdakPackagePersonnelConfig,
} from '@/utils/odakSiparisPackagePersonnel';
import { ODAK_SIPARIS_CONFIG } from '@/utils/odakSiparisConfig';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  mergeOdakLineFieldPoliciesBlob,
  mergeOdakPackageFieldPoliciesBlob,
  mergeOdakShipmentFieldPoliciesBlob,
} from '@/utils/odakSiparisFieldPolicies';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import {
  defaultOdakLineListConfig,
  mergeOdakLineListConfig,
} from '@/utils/odakSiparisLineListSettings';
import {
  defaultOdakPackageListConfig,
  mergeOdakPackageListConfig,
  type OdakPackageListConfig,
} from '@/utils/odakSiparisPackageListSettings';
import {
  defaultOdakShipmentListConfig,
  mergeOdakShipmentListConfig,
  type OdakShipmentListConfig,
} from '@/utils/odakSiparisShipmentListSettings';
import { ocCreate, ocListDataset, ocUpdate } from '@/services/operationCoreService';

export type OdakHubSettingsScope =
  | 'packages_list'
  | 'lines_list'
  | 'shipments_list'
  | 'field_policies'
  | 'lines_field_policies'
  | 'shipments_field_policies'
  | 'package_po_document_access'
  | 'package_odak_personnel';

export type OdakHubListSettingsScope = 'packages_list' | 'lines_list' | 'shipments_list';

export type OdakHubFieldPoliciesScope = 'field_policies' | 'lines_field_policies' | 'shipments_field_policies';

export interface OdakHubSettingsRow {
  __dataId?: string;
  dataId?: string;
  scope: OdakHubSettingsScope;
  configJson: string;
}

function hubDataId(row: Record<string, unknown>): string {
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

function parseConfigJson<T>(json: unknown): T | null {
  if (json == null) return null;
  if (typeof json === 'object') return json as T;
  const s = String(json).trim();
  if (!s) return null;
  try {
    return JSON.parse(s) as T;
  } catch {
    return null;
  }
}

async function loadHubRow(scope: OdakHubSettingsScope): Promise<OdakHubSettingsRow | null> {
  const filter = encodeURIComponent(`scope:eq:${scope}`);
  const rows = await ocListDataset(ODAK_SIPARIS_CONFIG.hubSettingsDataset, { filter, limit: 1 });
  const first = rows[0] as Record<string, unknown> | undefined;
  if (!first) return null;
  return {
    __dataId: hubDataId(first),
    scope,
    configJson: String(first.configJson ?? first.ConfigJson ?? '{}'),
  };
}

const LIST_SCOPE_MERGE: Record<
  OdakHubListSettingsScope,
  (saved: unknown) => OdakHubListConfig
> = {
  packages_list: mergeOdakPackageListConfig,
  lines_list: mergeOdakLineListConfig,
  shipments_list: mergeOdakShipmentListConfig,
};

export async function loadOdakHubListConfig(scope: OdakHubListSettingsScope): Promise<{
  config: OdakHubListConfig;
  rowId: string | null;
}> {
  const row = await loadHubRow(scope);
  const parsed = row ? parseConfigJson(row.configJson) : null;
  return {
    config: LIST_SCOPE_MERGE[scope](parsed),
    rowId: row?.__dataId ?? null,
  };
}

export async function saveOdakHubListConfig(
  scope: OdakHubListSettingsScope,
  config: OdakHubListConfig,
  rowId: string | null
): Promise<string> {
  const body = {
    scope,
    configJson: JSON.stringify({ listConfig: config }),
  };
  if (rowId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, rowId, body);
    return rowId;
  }
  const created = (await ocCreate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, body)) as Record<string, unknown>;
  return hubDataId(created);
}

export async function loadOdakPackageListConfig(): Promise<{
  config: OdakPackageListConfig;
  rowId: string | null;
}> {
  const resp = await loadOdakHubListConfig('packages_list');
  return { config: resp.config as OdakPackageListConfig, rowId: resp.rowId };
}

export async function saveOdakPackageListConfig(
  config: OdakPackageListConfig,
  rowId: string | null
): Promise<string> {
  return saveOdakHubListConfig('packages_list', config, rowId);
}

export async function loadOdakLineListConfig(): Promise<{
  config: import('@/utils/odakSiparisLineListSettings').OdakLineListConfig;
  rowId: string | null;
}> {
  const resp = await loadOdakHubListConfig('lines_list');
  return { config: resp.config, rowId: resp.rowId };
}

export async function saveOdakLineListConfig(
  config: import('@/utils/odakSiparisLineListSettings').OdakLineListConfig,
  rowId: string | null
): Promise<string> {
  return saveOdakHubListConfig('lines_list', config, rowId);
}

export async function loadOdakShipmentListConfig(): Promise<{
  config: OdakShipmentListConfig;
  rowId: string | null;
}> {
  const resp = await loadOdakHubListConfig('shipments_list');
  return { config: resp.config as OdakShipmentListConfig, rowId: resp.rowId };
}

export async function saveOdakShipmentListConfig(
  config: OdakShipmentListConfig,
  rowId: string | null
): Promise<string> {
  return saveOdakHubListConfig('shipments_list', config, rowId);
}

const FIELD_POLICIES_SCOPE_MERGE: Record<
  OdakHubFieldPoliciesScope,
  (saved: unknown) => OdakFieldPoliciesBlob
> = {
  field_policies: mergeOdakPackageFieldPoliciesBlob,
  lines_field_policies: mergeOdakLineFieldPoliciesBlob,
  shipments_field_policies: mergeOdakShipmentFieldPoliciesBlob,
};

export async function loadOdakHubFieldPoliciesBlob(scope: OdakHubFieldPoliciesScope): Promise<{
  blob: OdakFieldPoliciesBlob;
  rowId: string | null;
}> {
  const row = await loadHubRow(scope);
  const parsed = row ? parseConfigJson(row.configJson) : null;
  return {
    blob: FIELD_POLICIES_SCOPE_MERGE[scope](parsed),
    rowId: row?.__dataId ?? null,
  };
}

export async function saveOdakHubFieldPoliciesBlob(
  scope: OdakHubFieldPoliciesScope,
  blob: OdakFieldPoliciesBlob,
  rowId: string | null
): Promise<string> {
  const body = {
    scope,
    configJson: JSON.stringify({ fieldPolicies: blob }),
  };
  if (rowId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, rowId, body);
    return rowId;
  }
  const created = (await ocCreate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, body)) as Record<string, unknown>;
  return hubDataId(created);
}

export async function loadOdakFieldPoliciesBlob(): Promise<{
  blob: OdakFieldPoliciesBlob;
  rowId: string | null;
}> {
  return loadOdakHubFieldPoliciesBlob('field_policies');
}

export async function saveOdakFieldPoliciesBlob(
  blob: OdakFieldPoliciesBlob,
  rowId: string | null
): Promise<string> {
  return saveOdakHubFieldPoliciesBlob('field_policies', blob, rowId);
}

export async function loadOdakLineFieldPoliciesBlob(): Promise<{
  blob: OdakFieldPoliciesBlob;
  rowId: string | null;
}> {
  return loadOdakHubFieldPoliciesBlob('lines_field_policies');
}

export async function saveOdakLineFieldPoliciesBlob(
  blob: OdakFieldPoliciesBlob,
  rowId: string | null
): Promise<string> {
  return saveOdakHubFieldPoliciesBlob('lines_field_policies', blob, rowId);
}

export async function loadOdakShipmentFieldPoliciesBlob(): Promise<{
  blob: OdakFieldPoliciesBlob;
  rowId: string | null;
}> {
  return loadOdakHubFieldPoliciesBlob('shipments_field_policies');
}

export async function saveOdakShipmentFieldPoliciesBlob(
  blob: OdakFieldPoliciesBlob,
  rowId: string | null
): Promise<string> {
  return saveOdakHubFieldPoliciesBlob('shipments_field_policies', blob, rowId);
}

const fieldPoliciesOnlyCache = new Map<
  OdakHubFieldPoliciesScope,
  { blob: OdakFieldPoliciesBlob; at: number }
>();

export async function loadOdakHubFieldPoliciesOnly(
  scope: OdakHubFieldPoliciesScope,
  force = false
): Promise<OdakFieldPoliciesBlob> {
  const now = Date.now();
  if (!force) {
    const cached = fieldPoliciesOnlyCache.get(scope);
    if (cached && now - cached.at < RUNTIME_CACHE_MS) {
      return cached.blob;
    }
    if (scope === 'field_policies' && runtimeCache && now - runtimeCacheAt < RUNTIME_CACHE_MS) {
      return runtimeCache.fieldPolicies;
    }
  }
  const resp = await loadOdakHubFieldPoliciesBlob(scope);
  fieldPoliciesOnlyCache.set(scope, { blob: resp.blob, at: now });
  if (scope === 'field_policies' && runtimeCache) {
    runtimeCache = { ...runtimeCache, fieldPolicies: resp.blob };
    runtimeCacheAt = now;
  }
  return resp.blob;
}

export async function loadOdakLineFieldPoliciesOnly(force = false) {
  return loadOdakHubFieldPoliciesOnly('lines_field_policies', force);
}

export async function loadOdakShipmentFieldPoliciesOnly(force = false) {
  return loadOdakHubFieldPoliciesOnly('shipments_field_policies', force);
}

export interface OdakPackageHubRuntimeSettings {
  listConfig: OdakPackageListConfig;
  fieldPolicies: OdakFieldPoliciesBlob;
}

let runtimeCache: OdakPackageHubRuntimeSettings | null = null;
let runtimeCacheAt = 0;
const listConfigOnlyCache = new Map<OdakHubListSettingsScope, { config: OdakHubListConfig; at: number }>();
let poDocumentAccessCache: { config: OdakPackagePoDocumentAccessConfig; at: number } | null = null;
let personnelCache: { config: OdakPackagePersonnelConfig; at: number } | null = null;
const RUNTIME_CACHE_MS = 30_000;

export async function loadOdakPackageHubRuntimeSettings(
  force = false
): Promise<OdakPackageHubRuntimeSettings> {
  const now = Date.now();
  if (!force && runtimeCache && now - runtimeCacheAt < RUNTIME_CACHE_MS) {
    return runtimeCache;
  }
  const [list, fields] = await Promise.all([
    loadOdakPackageListConfig(),
    loadOdakHubFieldPoliciesBlob('field_policies'),
  ]);
  runtimeCache = {
    listConfig: list.config,
    fieldPolicies: fields.blob,
  };
  runtimeCacheAt = now;
  listConfigOnlyCache.set('packages_list', { config: list.config, at: now });
  return runtimeCache;
}

export function invalidateOdakPackageHubSettingsCache(): void {
  runtimeCache = null;
  runtimeCacheAt = 0;
  listConfigOnlyCache.clear();
  fieldPoliciesOnlyCache.clear();
  poDocumentAccessCache = null;
  personnelCache = null;
}

export async function loadOdakPackageListConfigOnly(): Promise<OdakPackageListConfig> {
  const settings = await loadOdakPackageHubRuntimeSettings();
  return settings.listConfig;
}

export async function loadOdakHubListConfigOnly(
  scope: OdakHubListSettingsScope,
  force = false
): Promise<OdakHubListConfig> {
  const now = Date.now();
  if (!force) {
    const cached = listConfigOnlyCache.get(scope);
    if (cached && now - cached.at < RUNTIME_CACHE_MS) {
      return cached.config;
    }
    if (scope === 'packages_list' && runtimeCache && now - runtimeCacheAt < RUNTIME_CACHE_MS) {
      return runtimeCache.listConfig;
    }
  }

  const resp = await loadOdakHubListConfig(scope);
  listConfigOnlyCache.set(scope, { config: resp.config, at: now });
  if (scope === 'packages_list' && runtimeCache) {
    runtimeCache = { ...runtimeCache, listConfig: resp.config as OdakPackageListConfig };
    runtimeCacheAt = now;
  }
  return resp.config;
}

export async function loadOdakLineListConfigOnly(force = false) {
  return loadOdakHubListConfigOnly('lines_list', force) as Promise<
    import('@/utils/odakSiparisLineListSettings').OdakLineListConfig
  >;
}

export async function loadOdakShipmentListConfigOnly(force = false) {
  return loadOdakHubListConfigOnly('shipments_list', force) as Promise<OdakShipmentListConfig>;
}

/** Varsayılan hub list yapılandırmaları (scope kaydı yoksa). */
export function defaultOdakHubListConfig(scope: OdakHubListSettingsScope): OdakHubListConfig {
  switch (scope) {
    case 'packages_list':
      return defaultOdakPackageListConfig();
    case 'lines_list':
      return defaultOdakLineListConfig();
    case 'shipments_list':
      return defaultOdakShipmentListConfig();
  }
}

/** Kayıtlı hub list yapılandırmasını birleştirir. */
export function mergeOdakHubListConfig(scope: OdakHubListSettingsScope, saved: unknown): OdakHubListConfig {
  return LIST_SCOPE_MERGE[scope](saved);
}

export async function loadOdakPackagePoDocumentAccessConfig(): Promise<{
  config: OdakPackagePoDocumentAccessConfig;
  rowId: string | null;
}> {
  const row = await loadHubRow('package_po_document_access');
  const parsed = row ? parseConfigJson(row.configJson) : null;
  return {
    config: mergeOdakPackagePoDocumentAccessConfig(parsed),
    rowId: row?.__dataId ?? null,
  };
}

export async function saveOdakPackagePoDocumentAccessConfig(
  config: OdakPackagePoDocumentAccessConfig,
  rowId: string | null
): Promise<string> {
  const body = {
    scope: 'package_po_document_access' as const,
    configJson: JSON.stringify({ poDocumentAccess: config }),
  };
  if (rowId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, rowId, body);
    return rowId;
  }
  const created = (await ocCreate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, body)) as Record<string, unknown>;
  return hubDataId(created);
}

export async function loadOdakPackagePoDocumentAccessOnly(
  force = false
): Promise<OdakPackagePoDocumentAccessConfig> {
  const now = Date.now();
  if (!force && poDocumentAccessCache && now - poDocumentAccessCache.at < RUNTIME_CACHE_MS) {
    return poDocumentAccessCache.config;
  }
  const resp = await loadOdakPackagePoDocumentAccessConfig();
  poDocumentAccessCache = { config: resp.config, at: now };
  return resp.config;
}

export async function loadOdakPackagePersonnelConfig(): Promise<{
  config: OdakPackagePersonnelConfig;
  rowId: string | null;
}> {
  const row = await loadHubRow('package_odak_personnel');
  const parsed = row ? parseConfigJson(row.configJson) : null;
  return {
    config: mergeOdakPackagePersonnelConfig(parsed),
    rowId: row?.__dataId ?? null,
  };
}

export async function saveOdakPackagePersonnelConfig(
  config: OdakPackagePersonnelConfig,
  rowId: string | null
): Promise<string> {
  const body = {
    scope: 'package_odak_personnel' as const,
    configJson: JSON.stringify({ packagePersonnel: config }),
  };
  if (rowId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, rowId, body);
    return rowId;
  }
  const created = (await ocCreate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, body)) as Record<string, unknown>;
  return hubDataId(created);
}

export async function loadOdakPackagePersonnelOnly(force = false): Promise<OdakPackagePersonnelConfig> {
  const now = Date.now();
  if (!force && personnelCache && now - personnelCache.at < RUNTIME_CACHE_MS) {
    return personnelCache.config;
  }
  const resp = await loadOdakPackagePersonnelConfig();
  personnelCache = { config: resp.config, at: now };
  return resp.config;
}
