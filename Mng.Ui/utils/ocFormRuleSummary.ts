import type { OpRule } from '@/types/apps/operationCore';

function conditionField(conditions: unknown): string | null {
  if (!conditions || typeof conditions !== 'object') return null;
  const c = conditions as Record<string, unknown>;
  const field = c.field ?? c.Field;
  return field != null ? String(field) : null;
}

function conditionCmp(conditions: unknown): string | null {
  if (!conditions || typeof conditions !== 'object') return null;
  const c = conditions as Record<string, unknown>;
  const cmp = c.cmp ?? c.Cmp;
  return cmp != null ? String(cmp) : null;
}

export function formatOcRuleConditionSummary(rule: OpRule): string {
  const field = conditionField(rule.conditions);
  const cmp = conditionCmp(rule.conditions);
  if (!field) return '—';
  if (cmp === 'empty') return `${field} boş`;
  if (cmp === 'notEmpty') return `${field} dolu`;
  if (cmp) return `${field} ${cmp}`;
  return field;
}

export function formatOcRuleActionsSummary(rule: OpRule): string {
  const actions = rule.actions;
  if (!Array.isArray(actions) || !actions.length) {
    if (rule.ruleType === 'validation' && rule.errorMessage) {
      return rule.errorMessage;
    }
    return '—';
  }
  const parts: string[] = [];
  for (const raw of actions) {
    if (!raw || typeof raw !== 'object') continue;
    const a = raw as Record<string, unknown>;
    const type = String(a.type ?? a.Type ?? '').toLowerCase();
    if (type === 'setfield') {
      const field = a.field ?? a.Field;
      parts.push(`setField(${field ?? '?'})`);
    } else if (type === 'setassignee') {
      parts.push('setAssignee');
    } else if (type) {
      parts.push(type);
    }
  }
  return parts.length ? parts.join(', ') : '—';
}

export function buildOcValidationRulePayload(input: {
  name: string;
  workspaceId: string;
  trigger: string;
  transitionKey: string;
  conditionField: string;
  errorMessage: string;
  typeId?: string;
}): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: input.name.trim(),
    workspaceId: input.workspaceId,
    ruleType: 'validation',
    trigger: input.trigger,
    applyMode: 'pre',
    conditions: { field: input.conditionField.trim(), cmp: 'empty' },
    errorMessage: input.errorMessage.trim(),
    isActive: true,
    priority: 100,
  };
  if (input.trigger === 'WorkItemTransition' && input.transitionKey.trim()) {
    body.transitionKey = input.transitionKey.trim();
  }
  if (input.typeId?.trim()) body.typeId = input.typeId.trim();
  return body;
}

export function buildOcDefaultSetFieldRulePayload(input: {
  name: string;
  workspaceId: string;
  trigger: string;
  field: string;
  value: string;
  typeId?: string;
}): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: input.name.trim(),
    workspaceId: input.workspaceId,
    ruleType: 'default',
    trigger: input.trigger,
    actions: [{ type: 'setField', field: input.field.trim(), value: input.value }],
    isActive: true,
    priority: 100,
  };
  if (input.typeId?.trim()) body.typeId = input.typeId.trim();
  return body;
}

export function buildOcDefaultSetAssigneeRulePayload(input: {
  name: string;
  workspaceId: string;
  trigger: string;
  assignee: string;
  typeId?: string;
}): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: input.name.trim(),
    workspaceId: input.workspaceId,
    ruleType: 'default',
    trigger: input.trigger,
    actions: [{ type: 'setAssignee', assignee: input.assignee.trim() }],
    isActive: true,
    priority: 100,
  };
  if (input.typeId?.trim()) body.typeId = input.typeId.trim();
  return body;
}
