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
import { ocCreate, ocListDataset, ocUpdate, parseSingleDgRecordId } from '@/services/operationCoreService';
import { dgExtractMessage } from '@/utils/dgErrorUtils';

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

function mapHubRow(scope: OdakHubSettingsScope, row: Record<string, unknown>): OdakHubSettingsRow {
  return {
    __dataId: hubDataId(row),
    scope,
    configJson: String(row.configJson ?? row.ConfigJson ?? '{}'),
  };
}

function isDuplicateScopeError(error: unknown): boolean {
  const msg = dgExtractMessage(error, '').toLowerCase();
  return (
    msg.includes('benzersiz') ||
    msg.includes('unique') ||
    msg.includes('duplicate') ||
    msg.includes('already in use') ||
    msg.includes('zaten kullan')
  );
}

async function loadHubRow(scope: OdakHubSettingsScope): Promise<OdakHubSettingsRow | null> {
  // buildQuery URLSearchParams zaten encode eder — burada encodeURIComponent kullanma (cift encode filtreyi bozar).
  const filter = `scope:eq:${scope}`;
  const rows = await ocListDataset(ODAK_SIPARIS_CONFIG.hubSettingsDataset, { filter, limit: 1 });
  const first = rows[0] as Record<string, unknown> | undefined;
  if (first) return mapHubRow(scope, first);

  // Fallback: kucuk dataset — scope ile client-side eslestir
  const allRows = await ocListDataset(ODAK_SIPARIS_CONFIG.hubSettingsDataset, { limit: 50 });
  const match = allRows.find((row) => String((row as Record<string, unknown>).scope ?? '') === scope) as
    | Record<string, unknown>
    | undefined;
  return match ? mapHubRow(scope, match) : null;
}

/** Upsert hub row by scope — avoids duplicate create when rowId was lost after tab remount. */
async function saveHubRow(
  scope: OdakHubSettingsScope,
  configJson: string,
  rowId: string | null
): Promise<string> {
  const body = { scope, configJson };
  const normalizedRowId = rowId?.trim() || null;

  if (normalizedRowId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, normalizedRowId, body);
    return normalizedRowId;
  }

  const existing = await loadHubRow(scope);
  if (existing?.__dataId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, existing.__dataId, body);
    return existing.__dataId;
  }

  try {
    const created = await ocCreate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, body);
    const createdId = parseSingleDgRecordId(created);
    if (createdId) return createdId;
  } catch (error: unknown) {
    if (!isDuplicateScopeError(error)) throw error;
  }

  const reloaded = await loadHubRow(scope);
  if (reloaded?.__dataId) {
    await ocUpdate(ODAK_SIPARIS_CONFIG.hubSettingsDataset, reloaded.__dataId, body);
    return reloaded.__dataId;
  }

  throw new Error(`Hub ayar kaydi kaydedilemedi (scope=${scope})`);
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
  return saveHubRow(scope, JSON.stringify({ listConfig: config }), rowId);
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
  return saveHubRow(scope, JSON.stringify({ fieldPolicies: blob }), rowId);
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

export async function loadOdakHubFieldPoliciesOnly(
  scope: OdakHubFieldPoliciesScope,
  force = false
): Promise<OdakFieldPoliciesBlob> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return store.fieldPoliciesBlob(scope);
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

export async function loadOdakPackageHubRuntimeSettings(
  force = false
): Promise<OdakPackageHubRuntimeSettings> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return {
    listConfig: store.packageListConfig,
    fieldPolicies: store.packageFieldPolicies,
  };
}

/** @deprecated Use hub settings store invalidate(); kept for compatibility. */
export function invalidateOdakPackageHubSettingsCache(): void {
  void import('@/stores/apps/odakSiparisHubSettings').then(({ useOdakSiparisHubSettingsStore }) => {
    useOdakSiparisHubSettingsStore().invalidate();
  });
}

/** @deprecated Use hub settings store ensureReady(force). */
export function clearOdakPackageHubSettingsMemoryCache(): void {
  invalidateOdakPackageHubSettingsCache();
}

/** @deprecated Store handles freshness; no-op. */
export function consumeOdakHubSettingsDirtyFlag(): boolean {
  return false;
}

export async function loadOdakPackageListConfigOnly(force = false): Promise<OdakPackageListConfig> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return store.packageListConfig;
}

export async function loadOdakHubListConfigOnly(
  scope: OdakHubListSettingsScope,
  force = false
): Promise<OdakHubListConfig> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return store.listConfig(scope);
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
  return saveHubRow(
    'package_po_document_access',
    JSON.stringify({ poDocumentAccess: config }),
    rowId
  );
}

export async function loadOdakPackagePoDocumentAccessOnly(
  force = false
): Promise<OdakPackagePoDocumentAccessConfig> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return store.poDocumentAccessConfig;
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
  return saveHubRow(
    'package_odak_personnel',
    JSON.stringify({ packagePersonnel: config }),
    rowId
  );
}

export async function loadOdakPackagePersonnelOnly(force = false): Promise<OdakPackagePersonnelConfig> {
  const { useOdakSiparisHubSettingsStore } = await import('@/stores/apps/odakSiparisHubSettings');
  const store = useOdakSiparisHubSettingsStore();
  await store.ensureReady(force);
  return store.personnelConfig;
}
