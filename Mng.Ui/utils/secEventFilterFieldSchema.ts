import type {
  SecEventFilterFieldKey,
  SecEventFilterFieldOp,
} from '@/types/apps/secEventFilterCatalog';
import type { SecEventTargetFieldDefinition } from '@/types/apps/secEventParseRules';

export interface SecEventFilterFieldSchema {
  field: SecEventFilterFieldKey;
  /** Prefer catalog label; i18n key used as fallback. */
  label: string;
  labelKey?: string;
  input: 'text' | 'select';
  ops: SecEventFilterFieldOp[];
  /** Fixed select options when input=select */
  options?: string[];
  group?: string;
  isCustom?: boolean;
}

const OUTCOMES = ['success', 'failure', 'unknown'];
const RDP_CODES = ['21', '23', '24', '25'];

const UI_OPS: SecEventFilterFieldOp[] = ['eq', 'neq', 'in', 'contains', 'prefix'];

function mapOps(raw: string[] | undefined): SecEventFilterFieldOp[] {
  const mapped = (raw ?? ['eq'])
    .map((o) => o.trim().toLowerCase())
    .filter((o): o is SecEventFilterFieldOp => (UI_OPS as string[]).includes(o));
  return mapped.length ? mapped : ['eq'];
}

/** Offline / API-failure fallback aligned with SecEventTargetFieldCatalog core. */
export function createFallbackSecEventFilterFieldSchemas(): SecEventFilterFieldSchema[] {
  return [
    {
      field: 'event.code',
      label: 'Event code',
      labelKey: 'siemCenter.events.filterCatalog.fields.eventCode',
      input: 'text',
      ops: ['eq', 'in'],
    },
    {
      field: 'event.outcome',
      label: 'Outcome',
      labelKey: 'siemCenter.events.filterCatalog.fields.outcome',
      input: 'select',
      ops: ['eq'],
      options: OUTCOMES,
    },
    {
      field: 'event.action',
      label: 'Action',
      labelKey: 'siemCenter.events.filterCatalog.fields.action',
      input: 'text',
      ops: ['eq', 'contains', 'prefix'],
    },
    {
      field: 'actor.user',
      label: 'User',
      labelKey: 'siemCenter.events.filterCatalog.fields.user',
      input: 'text',
      ops: ['eq', 'contains'],
    },
    {
      field: 'network.srcIp',
      label: 'Source IP',
      labelKey: 'siemCenter.events.filterCatalog.fields.srcIp',
      input: 'text',
      ops: ['eq', 'contains'],
    },
    {
      field: 'network.dstIp',
      label: 'Destination IP',
      labelKey: 'siemCenter.events.filterCatalog.fields.dstIp',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'network.dstPort',
      label: 'Destination port',
      labelKey: 'siemCenter.events.filterCatalog.fields.dstPort',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'event.actionPrefix',
      label: 'Action prefix',
      labelKey: 'siemCenter.events.filterCatalog.fields.actionPrefix',
      input: 'text',
      ops: ['eq'],
    },
    {
      field: 'search',
      label: 'Free text',
      labelKey: 'siemCenter.events.filterCatalog.fields.search',
      input: 'text',
      ops: ['contains'],
    },
  ];
}

/**
 * Build filter editor schemas from Event Log target-field catalog (+ UI helpers).
 * When `allowedFields` is set (product-scoped parse extracts), only those + helpers are shown.
 * Core identity/network fields always remain available.
 */
export function buildSecEventFilterFieldSchemasFromCatalog(
  catalogFields: SecEventTargetFieldDefinition[],
  options?: {
    product?: string | null;
    /** When set, restrict to these field names (+ always-keep core). */
    allowedFields?: Set<string> | null;
  },
): SecEventFilterFieldSchema[] {
  const product = (options?.product ?? '').trim().toLowerCase() || null;
  const allowed = options?.allowedFields ?? null;
  const alwaysKeep = new Set([
    'event.code',
    'event.outcome',
    'event.action',
    'actor.user',
    'network.srcIp',
    'network.dstIp',
    'network.dstPort',
    'event.actionPrefix',
    'search',
  ]);

  const schemas: SecEventFilterFieldSchema[] = [];

  for (const f of catalogFields) {
    if (!f.name || f.queryable === false) continue;
    if (allowed && !allowed.has(f.name) && !alwaysKeep.has(f.name)) continue;
    const ops = mapOps(f.queryOperators);
    const isOutcome = f.name === 'event.outcome';
    const isEventCode = f.name === 'event.code';
    schemas.push({
      field: f.name,
      label: f.label || f.name,
      input: isOutcome || (isEventCode && product === 'rdp-session') ? 'select' : 'text',
      ops,
      options: isOutcome
        ? OUTCOMES
        : isEventCode && product === 'rdp-session'
          ? RDP_CODES
          : undefined,
      group: f.group,
      isCustom: f.isCustom,
    });
  }

  // UI-only helpers (not extract targets, but useful for query)
  if (!schemas.some((s) => s.field === 'event.actionPrefix')) {
    schemas.push({
      field: 'event.actionPrefix',
      label: 'Action prefix',
      labelKey: 'siemCenter.events.filterCatalog.fields.actionPrefix',
      input: 'text',
      ops: ['eq'],
    });
  }
  if (!schemas.some((s) => s.field === 'search')) {
    schemas.push({
      field: 'search',
      label: 'Free text',
      labelKey: 'siemCenter.events.filterCatalog.fields.search',
      input: 'text',
      ops: ['contains'],
    });
  }

  // Ensure event.code exists even on older Reactor builds without catalog entry
  if (!schemas.some((s) => s.field === 'event.code')) {
    schemas.unshift({
      field: 'event.code',
      label: 'Event code',
      labelKey: 'siemCenter.events.filterCatalog.fields.eventCode',
      input: product === 'rdp-session' ? 'select' : 'text',
      ops: ['eq', 'in'],
      options: product === 'rdp-session' ? RDP_CODES : undefined,
    });
  } else if (product === 'rdp-session') {
    const idx = schemas.findIndex((s) => s.field === 'event.code');
    if (idx >= 0) {
      schemas[idx] = {
        ...schemas[idx],
        input: 'select',
        options: RDP_CODES,
      };
    }
  }

  return schemas;
}

/** Collect extract target field names from published parse rules matching a product. */
export function collectParseExtractFieldsForProduct(
  rules: Array<{
    enabled?: boolean;
    match?: { sourceProduct?: string[] | null };
    extract?: Array<{ to?: string | null; groups?: Record<string, string> | null }>;
  }>,
  product: string | null | undefined,
): Set<string> | null {
  const p = (product ?? '').trim().toLowerCase();
  if (!p) return null;

  const out = new Set<string>();
  for (const rule of rules) {
    if (rule.enabled === false) continue;
    const products = (rule.match?.sourceProduct ?? []).map((x) => String(x).trim().toLowerCase());
    if (!products.length) continue;
    const match =
      products.includes(p)
      || (p === 'rdp-session' && products.some((x) => x === 'windows' || x.includes('rdp')))
      || (p.startsWith('mnglogs') && products.some((x) => x.includes('agent') || x.includes('mnglogs')))
      || products.some((x) => p.includes(x) || x.includes(p));
    if (!match) continue;

    for (const step of rule.extract ?? []) {
      if (step.to?.trim()) out.add(step.to.trim());
      if (step.groups) {
        for (const target of Object.values(step.groups)) {
          if (target?.trim()) out.add(target.trim());
        }
      }
    }
  }

  return out.size ? out : null;
}

/** @deprecated Prefer buildSecEventFilterFieldSchemasFromCatalog — kept for call-site compat. */
export function listSecEventFilterFieldSchemas(options: {
  type?: string | null;
  product?: string | null;
}): SecEventFilterFieldSchema[] {
  return buildSecEventFilterFieldSchemasFromCatalog([], { product: options.product });
}
