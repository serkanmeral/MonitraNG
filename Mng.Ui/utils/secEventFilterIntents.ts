/**
 * Intent-based filter builder for Security Events.
 * Recommended fields align with parse-rule extract targets (SecEventTargetFieldCatalog).
 */

export type SecEventFilterFieldMapTo =
  | 'actorUser'
  | 'srcIp'
  | 'dstIp'
  | 'dstPort'
  | 'sourceHost'
  | 'eventCode'
  | 'eventOutcome'
  | 'eventAction'
  | 'sourceType'
  | 'search';

export interface SecEventFilterIntentField {
  /** Catalog field name when applicable (e.g. actor.user). */
  catalogField?: string;
  mapTo: SecEventFilterFieldMapTo;
  /** i18n key under siemCenter.events.filterBuilder.fields.* */
  labelKey: string;
  /** empty / null = Any */
  anyAllowed?: boolean;
  input: 'text' | 'select';
  /** Fixed select options (value). Labels via actions.* or outcomes.* */
  options?: string[];
  /** When true, options come from intent.eventActions (+ "all"). */
  actionRefine?: boolean;
  placeholderKey?: string;
  hintKey?: string;
}

export interface SecEventFilterIntent {
  id: string;
  /** i18n: siemCenter.events.filterBuilder.intents.<id>.title */
  titleKey: string;
  descKey: string;
  icon: string;
  color?: string;
  /** Family of event.action values (OR). Empty = no action constraint. */
  eventActions: string[];
  /** Prefer prefix query when family shares a prefix (e.g. rdp.). */
  eventActionPrefix?: string;
  /** Optional default source.type */
  defaultSourceType?: string | null;
  fields: SecEventFilterIntentField[];
}

export interface SecEventFilterBuilderResult {
  intentId: string;
  /** Single action refine; when set, eventActions family is not sent. */
  eventAction?: string | null;
  /** Comma-ready family list when no single refine. */
  eventActions?: string[] | null;
  eventActionPrefix?: string | null;
  eventOutcome?: string | null;
  actorUser?: string | null;
  srcIp?: string | null;
  dstIp?: string | null;
  dstPort?: string | null;
  sourceHost?: string | null;
  eventCode?: string | null;
  sourceType?: string | null;
  search?: string | null;
}

export const SEC_EVENT_OUTCOME_OPTIONS = ['success', 'failure', 'unknown'] as const;

export const SEC_EVENT_FILTER_INTENTS: SecEventFilterIntent[] = [
  {
    id: 'rdp',
    titleKey: 'siemCenter.events.filterBuilder.intents.rdp.title',
    descKey: 'siemCenter.events.filterBuilder.intents.rdp.desc',
    icon: 'mdi-remote-desktop',
    color: 'primary',
    eventActions: ['rdp.logon', 'rdp.logoff', 'rdp.disconnect', 'rdp.reconnect'],
    eventActionPrefix: 'rdp.',
    defaultSourceType: null,
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.rdpAction',
        anyAllowed: true,
        input: 'select',
        actionRefine: true,
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
        hintKey: 'siemCenter.events.filterBuilder.hints.rdpUser',
      },
      {
        catalogField: 'network.srcIp',
        mapTo: 'srcIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.clientIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
        hintKey: 'siemCenter.events.filterBuilder.hints.rdpClientIp',
      },
      {
        catalogField: 'source.host',
        mapTo: 'sourceHost',
        labelKey: 'siemCenter.events.filterBuilder.fields.sessionHost',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyHost',
        hintKey: 'siemCenter.events.filterBuilder.hints.rdpSessionHost',
      },
      {
        catalogField: 'event.outcome',
        mapTo: 'eventOutcome',
        labelKey: 'siemCenter.events.filterBuilder.fields.outcome',
        anyAllowed: true,
        input: 'select',
        options: [...SEC_EVENT_OUTCOME_OPTIONS],
      },
    ],
  },
  {
    id: 'login',
    titleKey: 'siemCenter.events.filterBuilder.intents.login.title',
    descKey: 'siemCenter.events.filterBuilder.intents.login.desc',
    icon: 'mdi-account-key',
    color: 'warning',
    eventActions: [
      'login_failed',
      'login_success',
      'login_success_after_failures',
      'privileged_login_outside_window',
      'logoff',
      'explicit_credentials',
      'account_locked',
    ],
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.loginAction',
        anyAllowed: true,
        input: 'select',
        actionRefine: true,
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
      },
      {
        catalogField: 'network.srcIp',
        mapTo: 'srcIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.sourceIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'source.host',
        mapTo: 'sourceHost',
        labelKey: 'siemCenter.events.filterBuilder.fields.host',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyHost',
      },
      {
        catalogField: 'event.code',
        mapTo: 'eventCode',
        labelKey: 'siemCenter.events.filterBuilder.fields.eventCode',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.eventCode',
      },
      {
        catalogField: 'event.outcome',
        mapTo: 'eventOutcome',
        labelKey: 'siemCenter.events.filterBuilder.fields.outcome',
        anyAllowed: true,
        input: 'select',
        options: [...SEC_EVENT_OUTCOME_OPTIONS],
      },
    ],
  },
  {
    id: 'firewall',
    titleKey: 'siemCenter.events.filterBuilder.intents.firewall.title',
    descKey: 'siemCenter.events.filterBuilder.intents.firewall.desc',
    icon: 'mdi-firewall',
    color: 'error',
    eventActions: ['denied_flow', 'allowed_flow', 'new_flow', 'rule_change'],
    defaultSourceType: 'firewall',
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.flowAction',
        anyAllowed: true,
        input: 'select',
        actionRefine: true,
      },
      {
        catalogField: 'network.srcIp',
        mapTo: 'srcIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.sourceIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'network.dstIp',
        mapTo: 'dstIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.destIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'network.dstPort',
        mapTo: 'dstPort',
        labelKey: 'siemCenter.events.filterBuilder.fields.destPort',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyPort',
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
      },
      {
        catalogField: 'event.outcome',
        mapTo: 'eventOutcome',
        labelKey: 'siemCenter.events.filterBuilder.fields.outcome',
        anyAllowed: true,
        input: 'select',
        options: [...SEC_EVENT_OUTCOME_OPTIONS],
      },
    ],
  },
  {
    id: 'directory',
    titleKey: 'siemCenter.events.filterBuilder.intents.directory.title',
    descKey: 'siemCenter.events.filterBuilder.intents.directory.desc',
    icon: 'mdi-account-group',
    color: 'info',
    eventActions: [
      'account_created',
      'account_deleted',
      'account_enabled',
      'group_member_added',
      'group_changed',
      'directory_object_modified',
      'directory_object_created',
      'directory_object_deleted',
      'privileged_assigned',
    ],
    defaultSourceType: 'ad',
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.directoryAction',
        anyAllowed: true,
        input: 'select',
        actionRefine: true,
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
      },
      {
        catalogField: 'source.host',
        mapTo: 'sourceHost',
        labelKey: 'siemCenter.events.filterBuilder.fields.host',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyHost',
      },
      {
        catalogField: 'event.code',
        mapTo: 'eventCode',
        labelKey: 'siemCenter.events.filterBuilder.fields.eventCode',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.eventCode',
      },
    ],
  },
  {
    id: 'linux_auth',
    titleKey: 'siemCenter.events.filterBuilder.intents.linux_auth.title',
    descKey: 'siemCenter.events.filterBuilder.intents.linux_auth.desc',
    icon: 'mdi-linux',
    color: 'success',
    eventActions: [
      'login_failed',
      'login_success',
      'privilege_denied',
      'privilege_escalation',
    ],
    defaultSourceType: 'linux-journal',
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.linuxAction',
        anyAllowed: true,
        input: 'select',
        actionRefine: true,
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
      },
      {
        catalogField: 'network.srcIp',
        mapTo: 'srcIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.sourceIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'source.host',
        mapTo: 'sourceHost',
        labelKey: 'siemCenter.events.filterBuilder.fields.host',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyHost',
      },
      {
        catalogField: 'event.outcome',
        mapTo: 'eventOutcome',
        labelKey: 'siemCenter.events.filterBuilder.fields.outcome',
        anyAllowed: true,
        input: 'select',
        options: [...SEC_EVENT_OUTCOME_OPTIONS],
      },
    ],
  },
  {
    id: 'custom',
    titleKey: 'siemCenter.events.filterBuilder.intents.custom.title',
    descKey: 'siemCenter.events.filterBuilder.intents.custom.desc',
    icon: 'mdi-tune-variant',
    eventActions: [],
    fields: [
      {
        catalogField: 'event.action',
        mapTo: 'eventAction',
        labelKey: 'siemCenter.events.filterBuilder.fields.anyAction',
        anyAllowed: true,
        input: 'select',
        options: [], // filled by UI from SEC_EVENT_ACTION_OPTIONS
      },
      {
        catalogField: 'actor.user',
        mapTo: 'actorUser',
        labelKey: 'siemCenter.events.filterBuilder.fields.user',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyUser',
      },
      {
        catalogField: 'network.srcIp',
        mapTo: 'srcIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.sourceIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'network.dstIp',
        mapTo: 'dstIp',
        labelKey: 'siemCenter.events.filterBuilder.fields.destIp',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyIp',
      },
      {
        catalogField: 'source.host',
        mapTo: 'sourceHost',
        labelKey: 'siemCenter.events.filterBuilder.fields.host',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.anyHost',
      },
      {
        catalogField: 'event.code',
        mapTo: 'eventCode',
        labelKey: 'siemCenter.events.filterBuilder.fields.eventCode',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.eventCode',
      },
      {
        catalogField: 'event.outcome',
        mapTo: 'eventOutcome',
        labelKey: 'siemCenter.events.filterBuilder.fields.outcome',
        anyAllowed: true,
        input: 'select',
        options: [...SEC_EVENT_OUTCOME_OPTIONS],
      },
      {
        mapTo: 'sourceType',
        labelKey: 'siemCenter.events.filterBuilder.fields.sourceType',
        anyAllowed: true,
        input: 'select',
        options: [
          'firewall',
          'ad',
          'endpoint',
          'metric',
          'windows-eventlog',
          'linux-journal',
        ],
      },
      {
        catalogField: 'message',
        mapTo: 'search',
        labelKey: 'siemCenter.events.filterBuilder.fields.message',
        anyAllowed: true,
        input: 'text',
        placeholderKey: 'siemCenter.events.filterBuilder.placeholders.message',
      },
    ],
  },
];

export function getSecEventFilterIntent(id: string | null | undefined): SecEventFilterIntent | null {
  if (!id) return null;
  return SEC_EVENT_FILTER_INTENTS.find((i) => i.id === id) ?? null;
}

export function emptyFieldValues(intent: SecEventFilterIntent): Record<string, string | null> {
  const values: Record<string, string | null> = {};
  for (const f of intent.fields) values[f.mapTo] = '';
  return values;
}

/** Build API-oriented result from intent + form values (null/blank = Any). */
export function buildSecEventFilterBuilderResult(
  intent: SecEventFilterIntent,
  values: Record<string, string | null | undefined>,
): SecEventFilterBuilderResult {
  const pick = (key: SecEventFilterFieldMapTo): string | null => {
    const v = values[key];
    if (v == null) return null;
    const t = String(v).trim();
    return t.length ? t : null;
  };

  const refinedAction = pick('eventAction');
  // Family actions are the intent contract — always attach when no single refine.
  const family =
    !refinedAction && intent.eventActions.length > 0
      ? [...intent.eventActions]
      : null;

  const result: SecEventFilterBuilderResult = {
    intentId: intent.id,
    eventAction: refinedAction,
    eventActions: family,
    eventActionPrefix:
      !refinedAction && intent.eventActionPrefix
        ? intent.eventActionPrefix
        : null,
    eventOutcome: pick('eventOutcome'),
    actorUser: pick('actorUser'),
    srcIp: pick('srcIp'),
    dstIp: pick('dstIp'),
    dstPort: pick('dstPort'),
    sourceHost: pick('sourceHost'),
    eventCode: pick('eventCode'),
    sourceType: pick('sourceType') ?? intent.defaultSourceType ?? null,
    search: pick('search'),
  };

  // custom intent: no default sourceType unless chosen
  if (intent.id === 'custom' && !pick('sourceType')) {
    result.sourceType = null;
  }

  return result;
}

/** Resolve OR-list of actions for an applied intent (single refine wins). */
export function resolveSecEventIntentActions(
  intentId: string | null | undefined,
  eventAction: string | null | undefined,
  eventActionsCsv: string | null | undefined,
  eventActionPrefix?: string | null,
): { eventAction?: string; eventActions?: string; eventActionPrefix?: string } {
  const single = eventAction?.trim();
  if (single) return { eventAction: single };

  const intent = getSecEventFilterIntent(intentId);
  const prefix = (eventActionPrefix ?? intent?.eventActionPrefix)?.trim();
  // Prefer prefix for families like rdp.* (avoids CSV query-string issues).
  if (prefix) return { eventActionPrefix: prefix };

  const csv = eventActionsCsv?.trim();
  if (csv) return { eventActions: csv };

  if (intent?.eventActions?.length) {
    return { eventActions: intent.eventActions.join(',') };
  }

  return {};
}

/** Client-side guard: drop rows that do not match the applied action constraint. */
export function rowMatchesActionConstraint(
  eventAction: string | null | undefined,
  constraint: { eventAction?: string; eventActions?: string; eventActionPrefix?: string },
  extras?: { eventCode?: string | null; sourceProduct?: string | null },
): boolean {
  const action = (eventAction ?? '').trim();
  if (constraint.eventAction) return action === constraint.eventAction;
  if (constraint.eventActionPrefix) {
    const prefix = constraint.eventActionPrefix;
    if (action.startsWith(prefix)) return true;
    // Agent RDP package may land before action normalize (raw message / codes only).
    if (prefix === 'rdp.' || prefix.toLowerCase() === 'rdp.') {
      const code = (extras?.eventCode ?? '').trim();
      if (['21', '23', '24', '25'].includes(code)) return true;
      const product = (extras?.sourceProduct ?? '').trim().toLowerCase();
      if (product === 'rdp-session' || product === 'rdp-sessions') return true;
    }
    return false;
  }
  if (constraint.eventActions) {
    const set = new Set(
      constraint.eventActions.split(',').map((s) => s.trim()).filter(Boolean),
    );
    return set.has(action);
  }
  return true;
}
