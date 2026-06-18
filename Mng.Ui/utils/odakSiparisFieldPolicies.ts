/**
 * Odak Sipariş — hub alan yetkileri (grup + koşullu, OC fieldPolicies genişletmesi).
 */

import { newConditionClauseId } from '@/utils/ocConditionClauses';
import type { OdakLineRow, OdakPackageRow, OdakShipmentRow } from '@/utils/odakSiparisConfig';

export type OdakFieldPolicyEntity = 'packages' | 'lines' | 'shipments';

export type OdakFieldPolicyScope = 'always' | 'conditional';
export type OdakFieldPolicyKind = 'visibility' | 'readonly';

export interface OdakFieldPolicyConditionClause {
  id: string;
  fieldKey: string;
  operator: 'eq' | 'ne';
  value: unknown;
}

export interface OdakFieldPolicyConditions {
  clauses: OdakFieldPolicyConditionClause[];
}

interface OdakFieldPolicyBase {
  id: string;
  kind: OdakFieldPolicyKind;
  /** Boş = tüm gruplar */
  groups: string[];
  scope: OdakFieldPolicyScope;
  conditions?: OdakFieldPolicyConditions;
}

export interface OdakFieldVisibilityPolicy extends OdakFieldPolicyBase {
  kind: 'visibility';
  visible: boolean;
}

export interface OdakFieldReadonlyPolicy extends OdakFieldPolicyBase {
  kind: 'readonly';
  readonly: boolean;
}

export type OdakFieldPolicy = OdakFieldVisibilityPolicy | OdakFieldReadonlyPolicy;

export interface OdakFieldPoliciesBlob {
  policiesByField: Record<string, OdakFieldPolicy[]>;
}

export interface OdakPackageFieldMeta {
  key: string;
  label: string;
}

export const ODAK_PACKAGE_POLICY_FIELD_KEYS = [
  'packageNo',
  'name',
  'customerId',
  'customerContactId',
  'designContactId',
  'manufactureContactId',
  'status',
  'closedAt',
  'beginDate',
  'deliveryDate',
  'deliveryAddress',
  'notes',
  'partCount',
  'stockCount',
  'shippedCount',
  'lineCount',
  'poVersion',
  'customerPo',
  'projectNo',
  'poDocumentsGlobal',
  'poDocumentsRestricted',
] as const;

export type OdakPackagePolicyFieldKey = (typeof ODAK_PACKAGE_POLICY_FIELD_KEYS)[number];

export const ODAK_LINE_POLICY_FIELD_KEYS = [
  'lineNo',
  'customerProjectNo',
  'customerPoNo',
  'customerPoItemNo',
  'sasItemNo',
  'customerJobNo',
  'poItemRevNo',
  'description',
  'productId',
  'quantity',
  'unit',
  'shippedQuantity',
  'qualityReqs',
  'qualityRequirementIds',
  'isFai',
  'isFaiComplete',
  'deliveryDate',
  'shipmentDate',
  'shipmentAddress',
  'unitCost',
  'totalCost',
  'currency',
] as const;

export type OdakLinePolicyFieldKey = (typeof ODAK_LINE_POLICY_FIELD_KEYS)[number];

export const ODAK_SHIPMENT_POLICY_FIELD_KEYS = [
  'waybillNo',
  'shipmentDate',
  'status',
  'controlType',
  'shipmentAddress',
  'notes',
  'qcfStatus',
  'qcfReferenceNo',
  'qcfNotes',
] as const;

export type OdakShipmentPolicyFieldKey = (typeof ODAK_SHIPMENT_POLICY_FIELD_KEYS)[number];

/** Liste sütun key → policy alan adı (iş paketi) */
export const ODAK_PACKAGE_LIST_KEY_TO_FIELD: Record<string, string> = {
  displayNo: 'packageNo',
  name: 'name',
  customer: 'customerId',
  customerContact: 'customerContactId',
  designContact: 'designContactId',
  manufactureContact: 'manufactureContactId',
  customerPo: 'customerPo',
  projectNo: 'projectNo',
  poVersion: 'poVersion',
  partCount: 'partCount',
  stockCount: 'stockCount',
  shippedCount: 'shippedCount',
  lineCount: 'lineCount',
  statusLabel: 'status',
  beginDate: 'beginDate',
  deliveryDate: 'deliveryDate',
  closedAt: 'closedAt',
  deliveryAddress: 'deliveryAddress',
  notes: 'notes',
};

export const ODAK_LINE_LIST_KEY_TO_FIELD: Record<string, string> = {
  lineNo: 'lineNo',
  customerProjectNo: 'customerProjectNo',
  customerPoNo: 'customerPoNo',
  customerPoItemNo: 'customerPoItemNo',
  sasItemNo: 'sasItemNo',
  customerJobNo: 'customerJobNo',
  poItemRevNo: 'poItemRevNo',
  description: 'description',
  quantity: 'quantity',
  unit: 'unit',
  shippedQuantity: 'shippedQuantity',
  remainingQuantity: 'remainingQuantity',
  deliveryDate: 'deliveryDate',
  shipmentDate: 'shipmentDate',
  shipmentAddress: 'shipmentAddress',
  qualityReqs: 'qualityReqs',
  qualityRequirementIds: 'qualityRequirementIds',
  isFai: 'isFai',
  isFaiComplete: 'isFaiComplete',
  unitCost: 'unitCost',
  totalCost: 'totalCost',
  currency: 'currency',
};

export const ODAK_SHIPMENT_LIST_KEY_TO_FIELD: Record<string, string> = {
  waybillNo: 'waybillNo',
  shipmentDate: 'shipmentDate',
  status: 'status',
  lineQty: 'lineQty',
  qcfStatus: 'qcfStatus',
  controlType: 'controlType',
  shipmentAddress: 'shipmentAddress',
  notes: 'notes',
  qcfReferenceNo: 'qcfReferenceNo',
  qcfNotes: 'qcfNotes',
};

const DEFAULT_HIDE_COST_POLICY_ID = 'odfp_default_hide_cost';

function defaultHideVisibilityPolicy(idSuffix: string): OdakFieldVisibilityPolicy {
  return {
    id: `${DEFAULT_HIDE_COST_POLICY_ID}_${idSuffix}`,
    kind: 'visibility',
    groups: [],
    scope: 'always',
    visible: false,
  };
}

/** Hub kaydı yokken kalem maliyet alanları varsayılan gizli. */
export function defaultOdakLineFieldPoliciesBlob(): OdakFieldPoliciesBlob {
  return {
    policiesByField: {
      unitCost: [defaultHideVisibilityPolicy('unitCost')],
      totalCost: [defaultHideVisibilityPolicy('totalCost')],
      currency: [defaultHideVisibilityPolicy('currency')],
    },
  };
}

export function mergeOdakLineFieldPoliciesBlob(saved: unknown): OdakFieldPoliciesBlob {
  const parsed = parseOdakFieldPoliciesBlob(saved ?? {});
  if (!Object.keys(parsed.policiesByField).length) {
    return defaultOdakLineFieldPoliciesBlob();
  }
  return parsed;
}

export function mergeOdakShipmentFieldPoliciesBlob(saved: unknown): OdakFieldPoliciesBlob {
  return parseOdakFieldPoliciesBlob(saved ?? {});
}

export function mergeOdakPackageFieldPoliciesBlob(saved: unknown): OdakFieldPoliciesBlob {
  return parseOdakFieldPoliciesBlob(saved ?? {});
}

export function newOdakFieldPolicyId(): string {
  return `odfp_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`;
}

export function emptyOdakFieldPoliciesBlob(): OdakFieldPoliciesBlob {
  return { policiesByField: {} };
}

function parseStringArray(raw: unknown): string[] {
  if (raw == null) return [];
  if (Array.isArray(raw)) return raw.map((v) => String(v).trim()).filter(Boolean);
  return [];
}

function parseConditions(raw: unknown): OdakFieldPolicyConditions | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const obj = raw as Record<string, unknown>;
  const clausesRaw = obj.clauses ?? obj.Clauses;
  if (!Array.isArray(clausesRaw)) return undefined;
  const clauses: OdakFieldPolicyConditionClause[] = [];
  for (const item of clausesRaw) {
    if (!item || typeof item !== 'object') continue;
    const c = item as Record<string, unknown>;
    const fieldKey = String(c.fieldKey ?? c.FieldKey ?? '').trim();
    if (!fieldKey) continue;
    clauses.push({
      id: String(c.id ?? c.Id ?? newConditionClauseId()),
      fieldKey,
      operator: String(c.operator ?? c.Operator ?? 'eq') === 'ne' ? 'ne' : 'eq',
      value: c.value ?? c.Value,
    });
  }
  return clauses.length ? { clauses } : undefined;
}

function parsePolicy(raw: unknown): OdakFieldPolicy | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const kind = String(o.kind ?? o.Kind ?? '').trim() as OdakFieldPolicyKind;
  if (kind !== 'visibility' && kind !== 'readonly') return null;
  const base: OdakFieldPolicyBase = {
    id: String(o.id ?? o.Id ?? newOdakFieldPolicyId()),
    kind,
    groups: parseStringArray(o.groups ?? o.Groups),
    scope: String(o.scope ?? o.Scope ?? 'always') === 'conditional' ? 'conditional' : 'always',
    conditions: parseConditions(o.conditions ?? o.Conditions),
  };
  if (kind === 'visibility') {
    return { ...base, kind: 'visibility', visible: o.visible !== false && o.Visible !== false };
  }
  return { ...base, kind: 'readonly', readonly: o.readonly !== false && o.Readonly !== false };
}

export function parseOdakFieldPoliciesBlob(raw: unknown): OdakFieldPoliciesBlob {
  if (!raw || typeof raw !== 'object') return emptyOdakFieldPoliciesBlob();
  const root = raw as Record<string, unknown>;
  const fp = root.fieldPolicies ?? root.FieldPolicies ?? root;
  if (!fp || typeof fp !== 'object') return emptyOdakFieldPoliciesBlob();
  const byField = (fp as Record<string, unknown>).policiesByField ?? (fp as Record<string, unknown>).PoliciesByField;
  if (!byField || typeof byField !== 'object') return emptyOdakFieldPoliciesBlob();

  const policiesByField: Record<string, OdakFieldPolicy[]> = {};
  for (const [fieldKey, list] of Object.entries(byField as Record<string, unknown>)) {
    if (!Array.isArray(list)) continue;
    const parsed = list.map(parsePolicy).filter((p): p is OdakFieldPolicy => p != null);
    if (parsed.length) policiesByField[fieldKey] = parsed;
  }
  return { policiesByField };
}

export function packageRecordForPolicyEval(row: OdakPackageRow): Record<string, unknown> {
  return { ...row };
}

export function lineRecordForPolicyEval(row: OdakLineRow | OdakLineFormLike): Record<string, unknown> {
  return { ...row };
}

export function shipmentRecordForPolicyEval(row: OdakShipmentRow | OdakShipmentFormLike): Record<string, unknown> {
  return { ...row };
}

/** Minimal form shapes for policy eval while editing. */
export interface OdakLineFormLike {
  lineNo?: number | null;
  customerProjectNo?: string;
  customerPoNo?: string;
  customerPoItemNo?: number | string | null;
  sasItemNo?: string;
  customerJobNo?: string;
  poItemRevNo?: string;
  description?: string;
  productId?: string | null;
  quantity?: number | null;
  unit?: string;
  shippedQuantity?: number | null;
  qualityReqs?: string;
  isFai?: boolean;
  isFaiComplete?: boolean;
  deliveryDate?: string;
  shipmentDate?: string;
  shipmentAddress?: string;
  unitCost?: number | null;
  totalCost?: number | null;
  currency?: string;
}

export interface OdakShipmentFormLike {
  waybillNo?: string;
  shipmentDate?: string;
  status?: string;
  controlType?: string;
  shipmentAddress?: string;
  notes?: string;
  qcfStatus?: string;
  qcfReferenceNo?: string;
  qcfNotes?: string;
}

function groupMatches(policyGroups: string[], userGroups: string[]): boolean {
  if (!policyGroups.length) return true;
  const lower = userGroups.map((g) => g.toLowerCase());
  return policyGroups.some((g) => lower.includes(g.toLowerCase()));
}

function clauseMatches(clause: OdakFieldPolicyConditionClause, record: Record<string, unknown>): boolean {
  const actual = record[clause.fieldKey];
  const compareActual =
    actual === true ? 'true' : actual === false ? 'false' : String(actual ?? '');
  const expected =
    clause.value === true ? 'true' : clause.value === false ? 'false' : String(clause.value ?? '');
  if (clause.operator === 'ne') return compareActual !== expected;
  return compareActual === expected;
}

function policyConditionsMatch(policy: OdakFieldPolicy, record: Record<string, unknown>): boolean {
  if (policy.scope === 'always') return true;
  const clauses = policy.conditions?.clauses ?? [];
  if (!clauses.length) return true;
  return clauses.every((c) => clauseMatches(c, record));
}

export interface OdakFieldAccess {
  visible: boolean;
  editable: boolean;
}

export function resolveOdakFieldAccess(
  fieldKey: string,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): OdakFieldAccess {
  const policies = blob?.policiesByField?.[fieldKey];
  if (!policies?.length) {
    return { visible: true, editable: true };
  }

  const matching = policies.filter(
    (p) => groupMatches(p.groups, userGroups) && policyConditionsMatch(p, record)
  );
  if (!matching.length) {
    return { visible: true, editable: true };
  }

  let visible = true;
  let editable = true;
  for (const p of matching) {
    if (p.kind === 'visibility' && p.visible === false) visible = false;
    if (p.kind === 'readonly' && p.readonly === true) editable = false;
  }
  if (!visible) editable = false;
  return { visible, editable };
}

export function resolveOdakListColumnAccess(
  listColumnKey: string,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined,
  listKeyToField: Record<string, string> = ODAK_PACKAGE_LIST_KEY_TO_FIELD
): OdakFieldAccess {
  const fieldKey = listKeyToField[listColumnKey] ?? listColumnKey;
  return resolveOdakFieldAccess(fieldKey, userGroups, record, blob);
}

export function resolveOdakPackageListColumnAccess(
  listColumnKey: string,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): OdakFieldAccess {
  return resolveOdakListColumnAccess(listColumnKey, userGroups, record, blob, ODAK_PACKAGE_LIST_KEY_TO_FIELD);
}

export function resolveOdakLineListColumnAccess(
  listColumnKey: string,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): OdakFieldAccess {
  return resolveOdakListColumnAccess(listColumnKey, userGroups, record, blob, ODAK_LINE_LIST_KEY_TO_FIELD);
}

export function resolveOdakShipmentListColumnAccess(
  listColumnKey: string,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): OdakFieldAccess {
  return resolveOdakListColumnAccess(listColumnKey, userGroups, record, blob, ODAK_SHIPMENT_LIST_KEY_TO_FIELD);
}

export function filterPayloadByFieldAccess(
  payload: Record<string, unknown>,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(payload)) {
    const access = resolveOdakFieldAccess(key, userGroups, record, blob);
    if (access.editable) out[key] = value;
  }
  return out;
}

export function filterPackagePayloadByFieldAccess(
  payload: Record<string, unknown>,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): Record<string, unknown> {
  return filterPayloadByFieldAccess(payload, userGroups, record, blob);
}

export function filterLinePayloadByFieldAccess(
  payload: Record<string, unknown>,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): Record<string, unknown> {
  return filterPayloadByFieldAccess(payload, userGroups, record, blob);
}

export function filterShipmentPayloadByFieldAccess(
  payload: Record<string, unknown>,
  userGroups: string[],
  record: Record<string, unknown>,
  blob: OdakFieldPoliciesBlob | null | undefined
): Record<string, unknown> {
  return filterPayloadByFieldAccess(payload, userGroups, record, blob);
}

export function policiesForOdakField(blob: OdakFieldPoliciesBlob, fieldKey: string): OdakFieldPolicy[] {
  return blob.policiesByField[fieldKey] ?? [];
}

export function setPoliciesForOdakField(
  blob: OdakFieldPoliciesBlob,
  fieldKey: string,
  policies: OdakFieldPolicy[]
): OdakFieldPoliciesBlob {
  const next = { ...blob.policiesByField };
  if (policies.length) next[fieldKey] = policies;
  else delete next[fieldKey];
  return { policiesByField: next };
}
