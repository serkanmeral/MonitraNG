import type { OpRule } from '@/types/apps/operationCore';
import {
  areConditionClausesComplete,
  clausesToMoRuleConditions,
  isConditionClauseComplete,
  isValuelessConditionOperator,
  moRuleConditionsToClauses,
  newConditionClauseId,
  type OcConditionClause,
  type OcConditionFieldOption,
  type OcRuleConditionOperator,
} from '@/utils/ocConditionClauses';
import { buildPolicyConditionFieldOptions } from '@/utils/ocWorkspaceFieldPolicies';

export const OC_WORKSPACE_RULE_TYPES = ['validation', 'default', 'automation'] as const;
export type OcWorkspaceRuleType = (typeof OC_WORKSPACE_RULE_TYPES)[number];

export const OC_WORKSPACE_RULE_TRIGGERS = [
  'WorkItemCreated',
  'WorkItemTransition',
  'WorkItemUpdated',
] as const;
export type OcWorkspaceRuleTrigger = (typeof OC_WORKSPACE_RULE_TRIGGERS)[number];

export const OC_WORKSPACE_RULE_APPLY_MODES = ['pre', 'post'] as const;
export type OcWorkspaceRuleApplyMode = (typeof OC_WORKSPACE_RULE_APPLY_MODES)[number];

export const OC_WORKSPACE_DEFAULT_ACTIONS = ['setField', 'setAssignee'] as const;
export type OcWorkspaceDefaultAction = (typeof OC_WORKSPACE_DEFAULT_ACTIONS)[number];

export const OC_WORKSPACE_AUTOMATION_ACTIONS = [
  'addWatcher',
  'createNotification',
  'sendEmailViaMngNotifiers',
  'createActivity',
] as const;
export type OcWorkspaceAutomationAction = (typeof OC_WORKSPACE_AUTOMATION_ACTIONS)[number];

export interface OcWorkspaceRuleScope {
  typeId?: string;
  boardId?: string;
  stateId?: string;
  fromStateId?: string;
  toStateId?: string;
  transitionKey?: string;
}

export interface OcWorkspaceRuleDraft {
  id?: string;
  name: string;
  description?: string;
  ruleType: OcWorkspaceRuleType;
  trigger: OcWorkspaceRuleTrigger;
  applyMode: OcWorkspaceRuleApplyMode;
  isActive: boolean;
  priority: number;
  scope: OcWorkspaceRuleScope;
  whenMode: 'always' | 'conditional';
  whenClauses: OcConditionClause[];
  errorMessage?: string;
  defaultAction: OcWorkspaceDefaultAction;
  defaultField?: string;
  defaultValue?: unknown;
  assignee?: string;
  automationAction: OcWorkspaceAutomationAction;
  watcher?: string;
  templateKey?: string;
  recipients?: string;
  activitySummary?: string;
  activityType?: string;
}

export interface OcWorkspaceRuleCatalogContext {
  fieldLabelByKey: Map<string, string>;
  typeTitleById: Map<string, string>;
  boardTitleById: Map<string, string>;
  stateTitleById: Map<string, string>;
  personTitleById: Map<string, string>;
  operatorLabels: Record<OcRuleConditionOperator | 'eq' | 'ne', string>;
  andJoin: string;
}

export function newWorkspaceRuleDraft(workspaceId: string, seed?: Partial<OcWorkspaceRuleDraft>): OcWorkspaceRuleDraft {
  return {
    name: '',
    description: '',
    ruleType: 'validation',
    trigger: 'WorkItemTransition',
    applyMode: 'pre',
    isActive: true,
    priority: 100,
    scope: {},
    whenMode: 'conditional',
    whenClauses: [],
    errorMessage: '',
    defaultAction: 'setField',
    defaultField: 'priorityId',
    defaultValue: null,
    assignee: '',
    automationAction: 'createActivity',
    watcher: '',
    templateKey: '',
    recipients: '',
    activitySummary: '',
    activityType: 'RuleAction',
    ...seed,
  };
}

function parseRecipientsList(raw: string | undefined): string[] {
  if (!raw?.trim()) return [];
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}

function recipientsToString(value: unknown): string {
  if (Array.isArray(value)) return value.map(String).join(', ');
  if (value != null && value !== '') return String(value);
  return '';
}

function parseAutomationActionFromRecord(
  a: Record<string, unknown>
): Partial<Pick<
  OcWorkspaceRuleDraft,
  'automationAction' | 'watcher' | 'templateKey' | 'recipients' | 'activitySummary' | 'activityType'
>> | null {
  const type = String(a.type ?? a.Type ?? '').toLowerCase();
  if (type === 'addwatcher') {
    return {
      automationAction: 'addWatcher',
      watcher: String(a.watcher ?? a.Watcher ?? a.value ?? a.Value ?? ''),
    };
  }
  if (type === 'createnotification') {
    return {
      automationAction: 'createNotification',
      templateKey: String(a.templateKey ?? a.TemplateKey ?? ''),
      recipients: recipientsToString(a.recipients ?? a.Recipients),
    };
  }
  if (type === 'sendemailviamngnotifiers') {
    return {
      automationAction: 'sendEmailViaMngNotifiers',
      templateKey: String(a.templateKey ?? a.TemplateKey ?? ''),
      recipients: recipientsToString(a.recipients ?? a.Recipients),
    };
  }
  if (type === 'createactivity') {
    return {
      automationAction: 'createActivity',
      activitySummary: String(a.summary ?? a.Summary ?? a.message ?? a.Message ?? ''),
      activityType: String(a.activityType ?? a.ActivityType ?? 'RuleAction'),
    };
  }
  return null;
}

function buildAutomationActionPayload(draft: OcWorkspaceRuleDraft): Record<string, unknown> {
  switch (draft.automationAction) {
    case 'addWatcher':
      return { type: 'addWatcher', watcher: String(draft.watcher ?? '').trim() };
    case 'createNotification': {
      const payload: Record<string, unknown> = {
        type: 'createNotification',
        templateKey: String(draft.templateKey ?? '').trim(),
      };
      const recipients = parseRecipientsList(draft.recipients);
      if (recipients.length) payload.recipients = recipients;
      return payload;
    }
    case 'sendEmailViaMngNotifiers': {
      const payload: Record<string, unknown> = {
        type: 'sendEmailViaMngNotifiers',
        templateKey: String(draft.templateKey ?? '').trim(),
      };
      const recipients = parseRecipientsList(draft.recipients);
      if (recipients.length) payload.recipients = recipients;
      return payload;
    }
    case 'createActivity':
    default:
      return {
        type: 'createActivity',
        summary: String(draft.activitySummary ?? '').trim(),
        activityType: String(draft.activityType ?? '').trim() || 'RuleAction',
      };
  }
}

export function buildRuleConditionFieldOptions(
  fields: OcConditionFieldOption[]
): OcConditionFieldOption[] {
  return buildPolicyConditionFieldOptions(fields);
}

export function parseOpRuleToDraft(rule: OpRule): OcWorkspaceRuleDraft {
  const whenClauses = moRuleConditionsToClauses(rule.conditions);
  const ruleType = (rule.ruleType?.toLowerCase() ?? 'default') as OcWorkspaceRuleType;
  const actions = Array.isArray(rule.actions) ? rule.actions : [];

  let defaultAction: OcWorkspaceDefaultAction = 'setField';
  let defaultField = 'priorityId';
  let defaultValue: unknown = null;
  let assignee = '';
  let automationAction: OcWorkspaceAutomationAction = 'createActivity';
  let watcher = '';
  let templateKey = '';
  let recipients = '';
  let activitySummary = '';
  let activityType = 'RuleAction';

  for (const raw of actions) {
    if (!raw || typeof raw !== 'object') continue;
    const a = raw as Record<string, unknown>;
    const type = String(a.type ?? a.Type ?? '').toLowerCase();
    const automation = parseAutomationActionFromRecord(a);
    if (automation) {
      automationAction = automation.automationAction ?? automationAction;
      watcher = automation.watcher ?? watcher;
      templateKey = automation.templateKey ?? templateKey;
      recipients = automation.recipients ?? recipients;
      activitySummary = automation.activitySummary ?? activitySummary;
      activityType = automation.activityType ?? activityType;
      continue;
    }
    if (type === 'setassignee') {
      defaultAction = 'setAssignee';
      assignee = String(a.assignee ?? a.Assignee ?? a.value ?? a.Value ?? '');
    } else if (type === 'setfield') {
      defaultAction = 'setField';
      defaultField = String(a.field ?? a.Field ?? 'priorityId');
      defaultValue = a.value ?? a.Value ?? null;
    }
  }

  const resolvedRuleType = OC_WORKSPACE_RULE_TYPES.includes(ruleType) ? ruleType : 'default';

  return {
    id: rule.__dataId,
    name: rule.name,
    description: rule.description ?? '',
    ruleType: resolvedRuleType,
    trigger: (OC_WORKSPACE_RULE_TRIGGERS as readonly string[]).includes(rule.trigger)
      ? (rule.trigger as OcWorkspaceRuleTrigger)
      : 'WorkItemCreated',
    applyMode:
      rule.applyMode?.toLowerCase() === 'post' ? 'post' : 'pre',
    isActive: rule.isActive !== false,
    priority: rule.priority ?? 100,
    scope: {
      typeId: rule.typeId ?? undefined,
      boardId: rule.boardId ?? undefined,
      stateId: rule.stateId ?? undefined,
      fromStateId: rule.fromStateId ?? undefined,
      toStateId: rule.toStateId ?? undefined,
      transitionKey: rule.transitionKey?.trim() || undefined,
    },
    whenMode: whenClauses.length ? 'conditional' : 'always',
    whenClauses: whenClauses.length ? whenClauses : [],
    errorMessage: rule.errorMessage ?? '',
    defaultAction,
    defaultField,
    defaultValue,
    assignee,
    automationAction,
    watcher,
    templateKey,
    recipients,
    activitySummary,
    activityType,
  };
}

export function validateWorkspaceRuleDraft(draft: OcWorkspaceRuleDraft): string | null {
  if (!draft.name.trim()) return 'name';
  if (draft.ruleType === 'validation') {
    if (!draft.errorMessage?.trim()) return 'errorMessage';
    if (draft.trigger === 'WorkItemTransition' && !draft.scope.transitionKey?.trim()) {
      return 'transitionKey';
    }
    if (draft.whenMode === 'conditional' && !areConditionClausesComplete(draft.whenClauses)) {
      return 'conditions';
    }
  } else if (draft.ruleType === 'default') {
    if (draft.defaultAction === 'setAssignee' && !String(draft.assignee ?? '').trim()) {
      return 'assignee';
    }
    if (draft.defaultAction === 'setField' && !draft.defaultField?.trim()) {
      return 'defaultField';
    }
  } else if (draft.ruleType === 'automation') {
    switch (draft.automationAction) {
      case 'addWatcher':
        if (!String(draft.watcher ?? '').trim()) return 'watcher';
        break;
      case 'createNotification':
      case 'sendEmailViaMngNotifiers':
        if (!String(draft.templateKey ?? '').trim()) return 'templateKey';
        break;
      case 'createActivity':
        if (!String(draft.activitySummary ?? '').trim()) return 'activitySummary';
        break;
    }
  }
  return null;
}

export function buildOpRulePayloadFromDraft(
  draft: OcWorkspaceRuleDraft,
  workspaceId: string
): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: draft.name.trim(),
    workspaceId,
    ruleType: draft.ruleType,
    trigger: draft.trigger,
    isActive: draft.isActive,
    priority: draft.priority,
  };

  if (draft.description?.trim()) body.description = draft.description.trim();

  const scope = draft.scope;
  if (scope.typeId) body.typeId = scope.typeId;
  if (scope.boardId) body.boardId = scope.boardId;
  if (scope.stateId) body.stateId = scope.stateId;
  if (scope.fromStateId) body.fromStateId = scope.fromStateId;
  if (scope.toStateId) body.toStateId = scope.toStateId;
  if (scope.transitionKey?.trim()) body.transitionKey = scope.transitionKey.trim();

  if (draft.whenMode === 'conditional') {
    const conditions = clausesToMoRuleConditions(draft.whenClauses);
    if (conditions) body.conditions = conditions;
  }

  if (draft.ruleType === 'validation') {
    body.applyMode = draft.applyMode;
    body.errorMessage = draft.errorMessage?.trim() ?? '';
  } else if (draft.ruleType === 'automation') {
    body.actions = [buildAutomationActionPayload(draft)];
  } else {
    if (draft.defaultAction === 'setAssignee') {
      body.actions = [{ type: 'setAssignee', assignee: String(draft.assignee ?? '').trim() }];
    } else {
      body.actions = [
        {
          type: 'setField',
          field: draft.defaultField?.trim(),
          value: draft.defaultValue,
        },
      ];
    }
  }

  return body;
}

function resolveCatalogTitle(map: Map<string, string>, id: string | null | undefined): string | null {
  if (!id) return null;
  return map.get(id) ?? null;
}

export function formatRuleWhenSummary(
  rule: OpRule,
  ctx: OcWorkspaceRuleCatalogContext
): string {
  const clauses = moRuleConditionsToClauses(rule.conditions);
  if (!clauses.length) return '—';
  return clauses
    .map((c) => {
      const fieldLabel = ctx.fieldLabelByKey.get(c.fieldKey) ?? c.fieldKey;
      const opLabel = ctx.operatorLabels[c.operator as keyof typeof ctx.operatorLabels] ?? c.operator;
      if (isValuelessConditionOperator(c.operator)) {
        return `${fieldLabel} ${opLabel}`;
      }
      let valueLabel = '';
      if (c.fieldKey === 'typeId') {
        valueLabel = resolveCatalogTitle(ctx.typeTitleById, String(c.value ?? '')) ?? String(c.value ?? '');
      } else if (c.fieldKey === 'stateId' || c.fieldKey === 'fromStateId' || c.fieldKey === 'toStateId') {
        valueLabel = resolveCatalogTitle(ctx.stateTitleById, String(c.value ?? '')) ?? String(c.value ?? '');
      } else if (c.fieldKey === 'boardId') {
        valueLabel = resolveCatalogTitle(ctx.boardTitleById, String(c.value ?? '')) ?? String(c.value ?? '');
      } else if (c.fieldKey === 'assignee') {
        valueLabel = resolveCatalogTitle(ctx.personTitleById, String(c.value ?? '')) ?? String(c.value ?? '');
      } else {
        valueLabel = String(c.value ?? '');
      }
      return `${fieldLabel} ${opLabel} ${valueLabel}`.trim();
    })
    .join(` ${ctx.andJoin} `);
}

export function formatRuleScopeSummary(rule: OpRule, ctx: OcWorkspaceRuleCatalogContext): string {
  const parts: string[] = [];
  if (rule.typeId) {
    parts.push(resolveCatalogTitle(ctx.typeTitleById, rule.typeId) ?? rule.typeId);
  }
  if (rule.boardId) {
    parts.push(resolveCatalogTitle(ctx.boardTitleById, rule.boardId) ?? rule.boardId);
  }
  if (rule.fromStateId || rule.toStateId) {
    const from = rule.fromStateId
      ? resolveCatalogTitle(ctx.stateTitleById, rule.fromStateId) ?? rule.fromStateId
      : '…';
    const to = rule.toStateId
      ? resolveCatalogTitle(ctx.stateTitleById, rule.toStateId) ?? rule.toStateId
      : '…';
    parts.push(`${from} → ${to}`);
  } else if (rule.stateId) {
    parts.push(resolveCatalogTitle(ctx.stateTitleById, rule.stateId) ?? rule.stateId);
  }
  if (rule.transitionKey) parts.push(rule.transitionKey);
  return parts.length ? parts.join(' · ') : '—';
}

function draftToScopeRule(draft: OcWorkspaceRuleDraft): OpRule {
  return {
    __dataId: '',
    name: draft.name,
    workspaceId: '',
    ruleType: draft.ruleType,
    trigger: draft.trigger,
    typeId: draft.scope.typeId ?? null,
    boardId: draft.scope.boardId ?? null,
    stateId: draft.scope.stateId ?? null,
    fromStateId: draft.scope.fromStateId ?? null,
    toStateId: draft.scope.toStateId ?? null,
    transitionKey: draft.scope.transitionKey ?? null,
  };
}

function draftToWhenRule(draft: OcWorkspaceRuleDraft): OpRule {
  return {
    ...draftToScopeRule(draft),
    conditions:
      draft.whenMode === 'conditional' ? clausesToMoRuleConditions(draft.whenClauses) : undefined,
  };
}

function draftToThenRule(draft: OcWorkspaceRuleDraft): OpRule {
  const base = draftToScopeRule(draft);
  if (draft.ruleType === 'validation') {
    return { ...base, errorMessage: draft.errorMessage ?? null };
  }
  if (draft.ruleType === 'automation') {
    return { ...base, actions: [buildAutomationActionPayload(draft)] };
  }
  const actions: Record<string, unknown>[] = [];
  if (draft.defaultAction === 'setAssignee' && draft.assignee) {
    actions.push({ type: 'setAssignee', assignee: draft.assignee });
  } else if (draft.defaultAction === 'setField' && draft.defaultField) {
    actions.push({ type: 'setField', field: draft.defaultField, value: draft.defaultValue });
  }
  return { ...base, actions };
}

export function formatRuleDraftScopeSummary(
  draft: OcWorkspaceRuleDraft,
  ctx: OcWorkspaceRuleCatalogContext,
  anyLabel: string
): string {
  const summary = formatRuleScopeSummary(draftToScopeRule(draft), ctx);
  return summary === '—' ? anyLabel : summary;
}

export function formatRuleDraftWhenSummary(
  draft: OcWorkspaceRuleDraft,
  ctx: OcWorkspaceRuleCatalogContext,
  alwaysLabel: string
): string {
  if (draft.whenMode === 'always') return alwaysLabel;
  const clauses = draft.whenClauses.filter((c) => isConditionClauseComplete(c));
  if (!clauses.length) return alwaysLabel;
  return formatRuleWhenSummary(draftToWhenRule({ ...draft, whenClauses: clauses }), ctx);
}

export function formatRuleDraftThenSummary(
  draft: OcWorkspaceRuleDraft,
  ctx: OcWorkspaceRuleCatalogContext,
  unsetLabel: string
): string {
  const summary = formatRuleThenSummary(draftToThenRule(draft), ctx);
  return summary === '—' ? unsetLabel : summary;
}

export function formatRuleThenSummary(rule: OpRule, ctx: OcWorkspaceRuleCatalogContext): string {
  if (rule.ruleType === 'validation') {
    return rule.errorMessage?.trim() || '—';
  }
  const actions = Array.isArray(rule.actions) ? rule.actions : [];
  const parts: string[] = [];
  for (const raw of actions) {
    if (!raw || typeof raw !== 'object') continue;
    const a = raw as Record<string, unknown>;
    const type = String(a.type ?? a.Type ?? '').toLowerCase();
    if (type === 'setfield') {
      const field = String(a.field ?? a.Field ?? '?');
      const fieldLabel = ctx.fieldLabelByKey.get(field) ?? field;
      parts.push(`${fieldLabel} := ${String(a.value ?? a.Value ?? '')}`);
    } else if (type === 'setassignee') {
      const id = String(a.assignee ?? a.Assignee ?? a.value ?? a.Value ?? '');
      const name = resolveCatalogTitle(ctx.personTitleById, id) ?? id;
      parts.push(`assignee := ${name}`);
    } else if (type === 'addwatcher') {
      const id = String(a.watcher ?? a.Watcher ?? a.value ?? a.Value ?? '');
      const name = resolveCatalogTitle(ctx.personTitleById, id) ?? id;
      parts.push(`watcher + ${name}`);
    } else if (type === 'createnotification' || type === 'sendemailviamngnotifiers') {
      const key = String(a.templateKey ?? a.TemplateKey ?? '?');
      parts.push(`${type}: ${key}`);
    } else if (type === 'createactivity') {
      parts.push(String(a.summary ?? a.Summary ?? a.message ?? a.Message ?? 'activity'));
    } else if (type) {
      parts.push(type);
    }
  }
  return parts.length ? parts.join(', ') : '—';
}

export function seedEmptyRuleClause(fieldKey: string): OcConditionClause {
  return {
    id: newConditionClauseId(),
    fieldKey,
    operator: 'empty',
    value: null,
  };
}

export function isRuleDraftComplete(draft: OcWorkspaceRuleDraft): boolean {
  return validateWorkspaceRuleDraft(draft) === null;
}
