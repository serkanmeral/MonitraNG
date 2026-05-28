/** Workspace `settings.fieldPolicies` — admin UI sözleşmesi (MO merge). */

import type { OpFormFieldBehavior } from '@/types/apps/operationCore';
import { newConditionClauseId } from '@/utils/ocConditionClauses';
import { isOcPersonsUserPickerField } from '@/utils/ocDynamicFormField';
import { collectPersonIdsFromValue } from '@/utils/ocPersonPicker';

export const OC_WORKSPACE_FIELD_POLICIES_SETTINGS_KEY = 'fieldPolicies';

export type OcWorkspacePolicyScope = 'always' | 'conditional';

export const OC_WORKSPACE_FIELD_POLICY_KINDS = [
  'visibility',
  'readonly',
  'defaultValue',
] as const;
export type OcWorkspaceFieldPolicyKind = (typeof OC_WORKSPACE_FIELD_POLICY_KINDS)[number];

/** Genişletilebilir karşılaştırma operatörleri (MO aynı anahtarları kullanmalı). */
export const OC_POLICY_CONDITION_OPERATORS = ['eq', 'ne'] as const;
export type OcPolicyConditionOperator = (typeof OC_POLICY_CONDITION_OPERATORS)[number];

export interface OcWorkspacePolicyConditionClause {
  id: string;
  fieldKey: string;
  operator: OcPolicyConditionOperator;
  value: unknown;
}

export interface OcWorkspacePolicyConditions {
  /** Tüm maddeler AND ile birleşir. */
  clauses: OcWorkspacePolicyConditionClause[];
}

interface OcWorkspaceFieldPolicyBase {
  id: string;
  kind: OcWorkspaceFieldPolicyKind;
  scope: OcWorkspacePolicyScope;
  conditions?: OcWorkspacePolicyConditions;
}

export interface OcWorkspaceVisibilityPolicy extends OcWorkspaceFieldPolicyBase {
  kind: 'visibility';
  visible: boolean;
}

export interface OcWorkspaceReadonlyPolicy extends OcWorkspaceFieldPolicyBase {
  kind: 'readonly';
  readonly: boolean;
}

export interface OcWorkspaceDefaultValuePolicy extends OcWorkspaceFieldPolicyBase {
  kind: 'defaultValue';
  value: unknown;
}

export type OcWorkspaceFieldPolicy =
  | OcWorkspaceVisibilityPolicy
  | OcWorkspaceReadonlyPolicy
  | OcWorkspaceDefaultValuePolicy;

export interface OcWorkspaceFieldPoliciesBlob {
  policiesByField: Record<string, OcWorkspaceFieldPolicy[]>;
}

/** Koşul olarak seçilemeyen alanlar (bağlam / çok büyük metin). */
export const OC_POLICY_CONDITION_EXCLUDED_KEYS = new Set([
  'key',
  'workspaceId',
  'stateFlowId',
  'origin',
  'sla',
]);

export const OC_POLICY_CONDITION_FIELD_ORDER = [
  'stateId',
  'priorityId',
  'typeId',
  'assignee',
  'boardId',
  'category',
  'impact',
  'urgency',
  'severity',
  'reporter',
  'watchers',
  'assignmentGroups',
] as const;

export interface OcPolicyConditionFieldOption {
  key: string;
  label: string;
  fieldType?: string;
  relationDataset?: string | null;
  cardinality?: string;
}

export interface OcPolicyTargetFieldMeta {
  key: string;
  label: string;
  fieldType?: string;
  relationDataset?: string | null;
  cardinality?: string;
}

function emptyBlob(): OcWorkspaceFieldPoliciesBlob {
  return { policiesByField: {} };
}

function parseOperator(raw: unknown): OcPolicyConditionOperator {
  const s = String(raw ?? 'eq').toLowerCase();
  return s === 'ne' || s === 'neq' || s === 'notEquals' ? 'ne' : 'eq';
}

function parseClauseValue(raw: unknown): unknown {
  if (raw === null || raw === undefined) return null;
  if (typeof raw === 'boolean' || typeof raw === 'number') return raw;
  if (typeof raw === 'string') return raw;
  if (Array.isArray(raw)) {
    const arr = raw.map((v) => (v != null ? String(v).trim() : '')).filter(Boolean);
    return arr.length ? arr : null;
  }
  if (typeof raw === 'object') return raw;
  return String(raw);
}

export function isClauseValueFilled(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'boolean') return true;
  if (typeof value === 'number' && Number.isFinite(value)) return true;
  if (typeof value === 'string') return value.trim().length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

function parseClause(raw: unknown): OcWorkspacePolicyConditionClause | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const fieldKey = String(o.fieldKey ?? o.field ?? '').trim();
  if (!fieldKey) return null;
  const id = String(o.id ?? '').trim() || newWorkspacePolicyClauseId();
  const value = parseClauseValue(o.value);
  if (!isClauseValueFilled(value)) return null;
  return {
    id,
    fieldKey,
    operator: parseOperator(o.operator ?? o.op),
    value,
  };
}

function parseClauses(raw: unknown): OcWorkspacePolicyConditionClause[] {
  if (!Array.isArray(raw)) return [];
  return raw.map(parseClause).filter((c): c is OcWorkspacePolicyConditionClause => c != null);
}

function migrateLegacyConditionsObject(
  o: Record<string, unknown>
): OcWorkspacePolicyConditionClause[] {
  const fromClauses = parseClauses(o.clauses);
  if (fromClauses.length) return fromClauses;

  const migrated: OcWorkspacePolicyConditionClause[] = [];
  const stateId = o.stateId != null ? String(o.stateId).trim() : '';
  if (stateId) {
    migrated.push({
      id: newWorkspacePolicyClauseId(),
      fieldKey: 'stateId',
      operator: 'eq',
      value: stateId,
    });
  }
  const groups = Array.isArray(o.userGroups)
    ? o.userGroups.map((g) => String(g).trim()).filter(Boolean)
    : [];
  if (groups.length === 1) {
    migrated.push({
      id: newWorkspacePolicyClauseId(),
      fieldKey: 'assignmentGroups',
      operator: 'eq',
      value: groups[0],
    });
  } else if (groups.length > 1) {
    migrated.push({
      id: newWorkspacePolicyClauseId(),
      fieldKey: 'assignmentGroups',
      operator: 'eq',
      value: groups,
    });
  }
  return migrated;
}

function parseConditions(raw: unknown): OcWorkspacePolicyConditions | undefined {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  const clauses = migrateLegacyConditionsObject(raw as Record<string, unknown>);
  if (!clauses.length) return undefined;
  return { clauses };
}

function parsePolicy(raw: unknown): OcWorkspaceFieldPolicy | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = String(o.id ?? '').trim();
  if (!id) return null;
  const scope = o.scope === 'conditional' ? 'conditional' : 'always';
  const conditions = scope === 'conditional' ? parseConditions(o.conditions) : undefined;
  let kind = String(o.kind ?? '').trim() as OcWorkspaceFieldPolicyKind;
  if (!kind && typeof o.visible === 'boolean') kind = 'visibility';

  if (kind === 'visibility' && typeof o.visible === 'boolean') {
    return { id, kind, scope, visible: o.visible, conditions };
  }
  if (kind === 'readonly' && typeof o.readonly === 'boolean') {
    return { id, kind, scope, readonly: o.readonly, conditions };
  }
  if (kind === 'defaultValue' && isClauseValueFilled(parseClauseValue(o.value))) {
    return { id, kind, scope, value: parseClauseValue(o.value), conditions };
  }
  return null;
}

function parsePoliciesByField(raw: unknown): Record<string, OcWorkspaceFieldPolicy[]> {
  const out: Record<string, OcWorkspaceFieldPolicy[]> = {};
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return out;
  for (const [fieldKey, listRaw] of Object.entries(raw as Record<string, unknown>)) {
    if (!fieldKey.trim() || !Array.isArray(listRaw)) continue;
    const policies = listRaw.map(parsePolicy).filter((p): p is OcWorkspaceFieldPolicy => p != null);
    if (policies.length) out[fieldKey] = policies;
  }
  return out;
}

/** Eski visibilityByField → policiesByField. */
function migrateVisibilityByField(
  raw: unknown,
  into: Record<string, OcWorkspaceFieldPolicy[]>
): void {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return;
  for (const [fieldKey, listRaw] of Object.entries(raw as Record<string, unknown>)) {
    if (!fieldKey.trim() || !Array.isArray(listRaw)) continue;
    const bucket = [...(into[fieldKey] ?? [])];
    const ids = new Set(bucket.map((p) => p.id));
    for (const item of listRaw) {
      const parsed = parsePolicy(item);
      if (!parsed || ids.has(parsed.id)) continue;
      bucket.push(parsed);
      ids.add(parsed.id);
    }
    if (bucket.length) into[fieldKey] = bucket;
  }
}

export function parseWorkspaceFieldPoliciesFromSettings(
  settings: Record<string, unknown> | null | undefined
): OcWorkspaceFieldPoliciesBlob {
  if (!settings) return emptyBlob();
  const raw = settings[OC_WORKSPACE_FIELD_POLICIES_SETTINGS_KEY];
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return emptyBlob();
  const blob = raw as Record<string, unknown>;

  const policiesByField = parsePoliciesByField(blob.policiesByField);
  migrateVisibilityByField(blob.visibilityByField ?? blob.visibility, policiesByField);

  return { policiesByField };
}

export function mergeFieldPoliciesIntoSettings(
  settings: Record<string, unknown> | null | undefined,
  blob: OcWorkspaceFieldPoliciesBlob
): Record<string, unknown> {
  const base = settings && typeof settings === 'object' && !Array.isArray(settings) ? { ...settings } : {};
  const policiesByField: Record<string, OcWorkspaceFieldPolicy[]> = {};
  for (const [key, list] of Object.entries(blob.policiesByField)) {
    if (list.length) policiesByField[key] = list;
  }
  return {
    ...base,
    [OC_WORKSPACE_FIELD_POLICIES_SETTINGS_KEY]: { policiesByField },
  };
}

export function newWorkspacePolicyId(): string {
  return `pol_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

export function newWorkspacePolicyClauseId(): string {
  return newConditionClauseId();
}

export function isClauseComplete(clause: OcWorkspacePolicyConditionClause): boolean {
  return !!clause.fieldKey.trim() && isClauseValueFilled(clause.value);
}

export function areAllClausesComplete(clauses: OcWorkspacePolicyConditionClause[]): boolean {
  return clauses.length > 0 && clauses.every(isClauseComplete);
}

export function isWorkspacePolicyComplete(policy: OcWorkspaceFieldPolicy): boolean {
  if (policy.scope === 'always') {
    if (policy.kind === 'defaultValue') return isClauseValueFilled(policy.value);
    return true;
  }
  if (!areAllClausesComplete(policy.conditions?.clauses ?? [])) return false;
  if (policy.kind === 'defaultValue') return isClauseValueFilled(policy.value);
  return true;
}

export function buildPolicyConditionFieldOptions(
  fields: OcPolicyConditionFieldOption[]
): OcPolicyConditionFieldOption[] {
  const eligible = fields.filter((f) => f.key && !OC_POLICY_CONDITION_EXCLUDED_KEYS.has(f.key));
  const orderIndex = new Map(OC_POLICY_CONDITION_FIELD_ORDER.map((k, i) => [k, i]));
  return [...eligible].sort((a, b) => {
    const ai = orderIndex.get(a.key as (typeof OC_POLICY_CONDITION_FIELD_ORDER)[number]);
    const bi = orderIndex.get(b.key as (typeof OC_POLICY_CONDITION_FIELD_ORDER)[number]);
    if (ai != null && bi != null) return ai - bi;
    if (ai != null) return -1;
    if (bi != null) return 1;
    return a.label.localeCompare(b.label, undefined, { sensitivity: 'base' });
  });
}

export function policiesForField(
  blob: OcWorkspaceFieldPoliciesBlob,
  fieldKey: string
): OcWorkspaceFieldPolicy[] {
  return blob.policiesByField[fieldKey] ?? [];
}

export function setPoliciesForField(
  blob: OcWorkspaceFieldPoliciesBlob,
  fieldKey: string,
  policies: OcWorkspaceFieldPolicy[]
): OcWorkspaceFieldPoliciesBlob {
  const policiesByField = { ...blob.policiesByField };
  if (policies.length) policiesByField[fieldKey] = policies;
  else delete policiesByField[fieldKey];
  return { ...blob, policiesByField };
}

export type OcPolicyFieldMetaForResolve = {
  fieldType?: string;
  cardinality?: string;
};

export interface OcPolicyValueResolveContext {
  fieldLabelByKey: Map<string, string>;
  fieldMetaByKey: Map<string, OcPolicyFieldMetaForResolve>;
  stateTitleById: Map<string, string>;
  priorityTitleById: Map<string, string>;
  typeTitleById: Map<string, string>;
  boardTitleById: Map<string, string>;
  personTitleById: Map<string, string>;
}

/** Politika özetlerinde kişi adı göstermek için tüm ilgili kullanıcı id'leri. */
/** Politika metinlerinde ad çözümlemesi için referans verilen katalog id'leri. */
export function collectPolicyReferencedCatalogIds(blob: OcWorkspaceFieldPoliciesBlob): {
  stateIds: string[];
  typeIds: string[];
  priorityIds: string[];
  boardIds: string[];
} {
  const stateIds = new Set<string>();
  const typeIds = new Set<string>();
  const priorityIds = new Set<string>();
  const boardIds = new Set<string>();

  const addValue = (fieldKey: string, value: unknown) => {
    const ids: string[] = [];
    if (Array.isArray(value)) {
      for (const v of value) {
        const s = v != null ? String(v).trim() : '';
        if (s) ids.push(s);
      }
    } else if (value != null && value !== '') {
      const s = String(value).trim();
      if (s) ids.push(s);
    }
    for (const id of ids) {
      if (fieldKey === 'stateId') stateIds.add(id);
      else if (fieldKey === 'typeId') typeIds.add(id);
      else if (fieldKey === 'priorityId') priorityIds.add(id);
      else if (fieldKey === 'boardId') boardIds.add(id);
    }
  };

  for (const [targetFieldKey, policies] of Object.entries(blob.policiesByField)) {
    for (const policy of policies) {
      if (policy.kind === 'defaultValue') addValue(targetFieldKey, policy.value);
      for (const clause of policy.conditions?.clauses ?? []) {
        addValue(clause.fieldKey, clause.value);
      }
    }
  }

  return {
    stateIds: [...stateIds],
    typeIds: [...typeIds],
    priorityIds: [...priorityIds],
    boardIds: [...boardIds],
  };
}

export function mergeCatalogTitleMaps(
  base: Map<string, string>,
  items: { value: string; title: string }[]
): Map<string, string> {
  const out = new Map(base);
  for (const item of items) {
    const id = String(item.value ?? '').trim();
    const title = String(item.title ?? '').trim();
    if (id && title) out.set(id, title);
  }
  return out;
}

export function collectPersonIdsFromWorkspacePolicies(
  blob: OcWorkspaceFieldPoliciesBlob,
  fieldMetaByKey: Map<string, OcPolicyFieldMetaForResolve>
): string[] {
  const ids = new Set<string>();
  for (const [targetFieldKey, policies] of Object.entries(blob.policiesByField)) {
    const targetMeta = fieldMetaByKey.get(targetFieldKey);
    for (const policy of policies) {
      if (
        policy.kind === 'defaultValue' &&
        isOcPersonsUserPickerField(targetFieldKey, targetMeta ?? null)
      ) {
        for (const id of collectPersonIdsFromValue(policy.value)) ids.add(id);
      }
      for (const clause of policy.conditions?.clauses ?? []) {
        const clauseMeta = fieldMetaByKey.get(clause.fieldKey);
        if (!isOcPersonsUserPickerField(clause.fieldKey, clauseMeta ?? null)) continue;
        for (const id of collectPersonIdsFromValue(clause.value)) ids.add(id);
      }
    }
  }
  return [...ids];
}

export type OcPolicySummaryLabels = {
  kindVisibility: string;
  kindReadonly: string;
  kindDefaultValue: string;
  scopeAlways: string;
  scopeConditional: string;
  alwaysVisible: string;
  alwaysHidden: string;
  conditionalVisible: string;
  conditionalHidden: string;
  alwaysReadonly: string;
  alwaysEditable: string;
  conditionalReadonly: string;
  conditionalEditable: string;
  defaultValueAlways: string;
  defaultValueConditional: string;
  operatorEq: string;
  operatorNe: string;
  andJoin: string;
  emptyConditions: string;
};

function operatorLabel(operator: OcPolicyConditionOperator, labels: OcPolicySummaryLabels): string {
  return operator === 'ne' ? labels.operatorNe : labels.operatorEq;
}

export function resolvePolicyConditionValueTitle(
  fieldKey: string,
  value: unknown,
  ctx: OcPolicyValueResolveContext
): string {
  const mapForField = (map: Map<string, string>) => {
    if (Array.isArray(value)) {
      return value.map((v) => map.get(String(v)) ?? String(v)).join(', ');
    }
    const id = String(value);
    return map.get(id) ?? id;
  };

  const fieldMeta = ctx.fieldMetaByKey.get(fieldKey);
  if (isOcPersonsUserPickerField(fieldKey, fieldMeta ?? null)) {
    return mapForField(ctx.personTitleById);
  }

  if (fieldKey === 'stateId') return mapForField(ctx.stateTitleById);
  if (fieldKey === 'priorityId') return mapForField(ctx.priorityTitleById);
  if (fieldKey === 'typeId') return mapForField(ctx.typeTitleById);
  if (fieldKey === 'boardId') return mapForField(ctx.boardTitleById);
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (Array.isArray(value)) return value.map(String).join(', ');
  return String(value);
}

function formatConditionsTail(
  policy: OcWorkspaceFieldPolicy,
  ctx: OcPolicyValueResolveContext,
  labels: OcPolicySummaryLabels
): string {
  if (policy.scope === 'always') return labels.scopeAlways;
  const clauses = policy.conditions?.clauses ?? [];
  if (!clauses.length) return labels.emptyConditions;
  const parts = clauses.map((c) => {
    const fieldLabel = ctx.fieldLabelByKey.get(c.fieldKey) ?? c.fieldKey;
    const op = operatorLabel(c.operator, labels);
    const val = resolvePolicyConditionValueTitle(c.fieldKey, c.value, ctx);
    return `${fieldLabel} ${op} ${val}`;
  });
  return `${labels.scopeConditional}: ${parts.join(` ${labels.andJoin} `)}`;
}

export function formatWorkspaceFieldPolicySummary(
  policy: OcWorkspaceFieldPolicy,
  targetFieldKey: string,
  ctx: OcPolicyValueResolveContext,
  labels: OcPolicySummaryLabels
): string {
  const tail = formatConditionsTail(policy, ctx, labels);

  if (policy.kind === 'visibility') {
    if (policy.scope === 'always') {
      return policy.visible ? labels.alwaysVisible : labels.alwaysHidden;
    }
    const head = policy.visible ? labels.conditionalVisible : labels.conditionalHidden;
    return `${head} (${tail})`;
  }

  if (policy.kind === 'readonly') {
    if (policy.scope === 'always') {
      return policy.readonly ? labels.alwaysReadonly : labels.alwaysEditable;
    }
    const head = policy.readonly ? labels.conditionalReadonly : labels.conditionalEditable;
    return `${head} (${tail})`;
  }

  const valTitle = resolvePolicyConditionValueTitle(targetFieldKey, policy.value, ctx);
  if (policy.scope === 'always') {
    return `${labels.defaultValueAlways}: ${valTitle}`;
  }
  return `${labels.defaultValueConditional}: ${valTitle} (${tail})`;
}

export function workspacePolicyKindLabel(
  kind: OcWorkspaceFieldPolicyKind,
  labels: Pick<
    OcPolicySummaryLabels,
    'kindVisibility' | 'kindReadonly' | 'kindDefaultValue'
  >
): string {
  if (kind === 'readonly') return labels.kindReadonly;
  if (kind === 'defaultValue') return labels.kindDefaultValue;
  return labels.kindVisibility;
}

/** @deprecated use formatWorkspaceFieldPolicySummary */
export const formatWorkspaceVisibilityPolicySummary = formatWorkspaceFieldPolicySummary;
