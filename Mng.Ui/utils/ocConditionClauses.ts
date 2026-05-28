/** Paylaşılan koşul maddesi — workspace politikaları + op_rules (MO RuleConditionEvaluator uyumlu). */

export const OC_CONDITION_OPERATORS_EQ_NE = ['eq', 'ne'] as const;
export const OC_RULE_CONDITION_OPERATORS = [
  'eq',
  'ne',
  'empty',
  'notEmpty',
  'gt',
  'lt',
] as const;

export type OcConditionOperatorEqNe = (typeof OC_CONDITION_OPERATORS_EQ_NE)[number];
export type OcRuleConditionOperator = (typeof OC_RULE_CONDITION_OPERATORS)[number];
export type OcConditionOperator = OcConditionOperatorEqNe | OcRuleConditionOperator;

export interface OcConditionClause {
  id: string;
  fieldKey: string;
  operator: OcConditionOperator;
  value: unknown;
}

export interface OcConditionFieldOption {
  key: string;
  label: string;
  fieldType?: string;
  relationDataset?: string | null;
  cardinality?: string;
}

export function newConditionClauseId(): string {
  return `cl_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

export function isValuelessConditionOperator(operator: OcConditionOperator): boolean {
  return operator === 'empty' || operator === 'notEmpty';
}

export function isConditionClauseValueFilled(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim().length > 0;
  if (Array.isArray(value)) return value.length > 0;
  return true;
}

export function isConditionClauseComplete(clause: OcConditionClause): boolean {
  if (!clause.fieldKey.trim()) return false;
  if (isValuelessConditionOperator(clause.operator)) return true;
  return isConditionClauseValueFilled(clause.value);
}

export function areConditionClausesComplete(clauses: OcConditionClause[]): boolean {
  return clauses.length > 0 && clauses.every(isConditionClauseComplete);
}

/** MO op_rules: `{ op: "and", items: [{ field, cmp, value }] }` */
export function clausesToMoRuleConditions(
  clauses: OcConditionClause[]
): Record<string, unknown> | undefined {
  const complete = clauses.filter(isConditionClauseComplete);
  if (!complete.length) return undefined;
  return {
    op: 'and',
    items: complete.map((c) => {
      const item: Record<string, unknown> = {
        field: c.fieldKey.trim(),
        cmp: c.operator,
      };
      if (!isValuelessConditionOperator(c.operator)) {
        item.value = c.value;
      }
      return item;
    }),
  };
}

function parseMoRuleConditionLeaf(node: Record<string, unknown>): OcConditionClause | null {
  const field = node.field ?? node.Field;
  if (field == null || String(field).trim() === '') return null;
  const cmpRaw = node.cmp ?? node.Cmp ?? 'eq';
  const cmp = String(cmpRaw).toLowerCase() as OcConditionOperator;
  const value = node.value ?? node.Value;
  return {
    id: newConditionClauseId(),
    fieldKey: String(field).trim(),
    operator: (OC_RULE_CONDITION_OPERATORS as readonly string[]).includes(cmp) ? cmp : 'eq',
    value: value ?? null,
  };
}

/** MO op_rules veya legacy `{ field, cmp }` tek leaf. */
export function moRuleConditionsToClauses(raw: unknown): OcConditionClause[] {
  if (!raw || typeof raw !== 'object') return [];
  const o = raw as Record<string, unknown>;

  const op = String(o.op ?? o.Op ?? '').toLowerCase();
  const items = o.items ?? o.Items;
  if ((op === 'and' || op === 'or') && Array.isArray(items)) {
    const clauses: OcConditionClause[] = [];
    for (const item of items) {
      if (!item || typeof item !== 'object') continue;
      const leaf = parseMoRuleConditionLeaf(item as Record<string, unknown>);
      if (leaf) clauses.push(leaf);
    }
    return clauses;
  }

  if (o.field != null || o.Field != null) {
    const leaf = parseMoRuleConditionLeaf(o);
    return leaf ? [leaf] : [];
  }

  return [];
}

/** Workspace fieldPolicies: `{ clauses: [...] }` */
export function policyConditionsToClauses(raw: unknown): OcConditionClause[] {
  if (!raw || typeof raw !== 'object') return [];
  const clausesRaw = (raw as Record<string, unknown>).clauses ?? (raw as Record<string, unknown>).Clauses;
  if (!Array.isArray(clausesRaw)) return [];
  return clausesRaw
    .map((c) => {
      if (!c || typeof c !== 'object') return null;
      const row = c as Record<string, unknown>;
      const fieldKey = String(row.fieldKey ?? row.FieldKey ?? '').trim();
      if (!fieldKey) return null;
      const opRaw = String(row.operator ?? row.Operator ?? 'eq').toLowerCase();
      const operator: OcConditionOperator =
        opRaw === 'ne' ? 'ne' : opRaw === 'eq' ? 'eq' : 'eq';
      return {
        id: String(row.id ?? '').trim() || newConditionClauseId(),
        fieldKey,
        operator,
        value: row.value ?? row.Value ?? null,
      };
    })
    .filter((c): c is OcConditionClause => c != null);
}

export function clausesToPolicyConditions(
  clauses: OcConditionClause[]
): { clauses: OcConditionClause[] } | undefined {
  const complete = clauses.filter(isConditionClauseComplete);
  if (!complete.length) return undefined;
  return {
    clauses: complete.map((c) => ({
      id: c.id,
      fieldKey: c.fieldKey,
      operator: c.operator as OcConditionOperatorEqNe,
      value: c.value,
    })),
  };
}
