import type { SecEventQuery } from '@/types/apps/secEvent';
import type {
  SecEventFilterFieldOp,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';

function parseCsv(raw: string | undefined | null): string[] {
  if (!raw?.trim()) return [];
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}

/** Fields with dedicated Reactor query params (hot path). */
const DEDICATED_FIELDS = new Set([
  'event.code',
  'event.outcome',
  'event.action',
  'event.actionPrefix',
  'actor.user',
  'network.srcIp',
  'network.dstIp',
  'network.dstPort',
  'search',
]);

/**
 * Maps a saved/active filter (scope + fields) to Reactor sec-events query params.
 * Panel time range is applied by the caller.
 * Catalog / custom.* fields go into fieldFilters JSON.
 */
export function mapSecEventSavedFilterToQuery(
  filter: Pick<SecEventSavedFilter, 'scope' | 'fields'>,
): Pick<
  SecEventQuery,
  | 'sourceType'
  | 'sourceProduct'
  | 'sourceHost'
  | 'sourceHosts'
  | 'eventAction'
  | 'eventActions'
  | 'eventActionPrefix'
  | 'eventOutcome'
  | 'eventCode'
  | 'eventCodes'
  | 'actorUser'
  | 'srcIp'
  | 'dstIp'
  | 'dstPort'
  | 'search'
  | 'fieldFilters'
> {
  const out: ReturnType<typeof mapSecEventSavedFilterToQuery> = {};
  const scope = filter.scope ?? {};
  const generic: Array<{ field: string; op: string; value: string }> = [];

  if (scope.type?.trim()) out.sourceType = scope.type.trim();
  if (scope.product?.trim()) out.sourceProduct = scope.product.trim();

  const hosts = (scope.hosts ?? []).map((h) => h.trim()).filter(Boolean);
  if (hosts.length === 1) out.sourceHost = hosts[0];
  else if (hosts.length > 1) out.sourceHosts = hosts.join(',');

  for (const clause of filter.fields ?? []) {
    const value = (clause.value ?? '').trim();
    if (!value) continue;
    const field = (clause.field ?? '').trim();
    if (!field) continue;
    const op = (clause.op ?? 'eq') as SecEventFilterFieldOp;

    if (!DEDICATED_FIELDS.has(field)) {
      generic.push({ field, op, value });
      continue;
    }

    switch (field) {
      case 'event.code': {
        if (op === 'in') {
          const codes = parseCsv(value);
          if (codes.length === 1) out.eventCode = codes[0];
          else if (codes.length > 1) out.eventCodes = codes.join(',');
        } else {
          out.eventCode = value;
        }
        break;
      }
      case 'event.outcome':
        out.eventOutcome = value;
        break;
      case 'event.action':
        if (op === 'contains') {
          out.eventActionPrefix = value.endsWith('.') ? value : undefined;
          if (!out.eventActionPrefix) out.search = value;
        } else if (op === 'prefix') {
          out.eventActionPrefix = value;
        } else if (op === 'in') {
          const actions = parseCsv(value);
          if (actions.length === 1) out.eventAction = actions[0];
          else if (actions.length > 1) out.eventActions = actions.join(',');
        } else {
          out.eventAction = value;
        }
        break;
      case 'event.actionPrefix':
        out.eventActionPrefix = value;
        break;
      case 'actor.user':
        out.actorUser = value;
        break;
      case 'network.srcIp':
        out.srcIp = value;
        break;
      case 'network.dstIp':
        out.dstIp = value;
        break;
      case 'network.dstPort':
        out.dstPort = value;
        break;
      case 'search':
        out.search = value;
        break;
      default:
        generic.push({ field, op, value });
        break;
    }
  }

  if (generic.length) {
    out.fieldFilters = JSON.stringify(generic);
  }

  // When product is rdp-session and codes cover the RDP family, also send action prefix
  // so Reactor matches raw-message actions until normalizer is deployed.
  if (
    scope.product?.trim().toLowerCase() === 'rdp-session'
    && !out.eventAction
    && !out.eventActions
    && !out.eventActionPrefix
  ) {
    out.eventActionPrefix = 'rdp.';
  }

  return out;
}

export function createEmptyActiveFilter(): SecEventSavedFilter {
  return {
    id: '',
    categoryId: '',
    name: '',
    isSystem: false,
    scope: {},
    fields: [],
  };
}

export function cloneFilterAsUserCopy(
  source: SecEventSavedFilter,
  categoryId: string,
  name: string,
): SecEventSavedFilter {
  return {
    id: `flt-user-${Date.now().toString(36)}`,
    categoryId,
    name,
    description: source.description ?? null,
    isSystem: false,
    scope: {
      type: source.scope?.type ?? null,
      product: source.scope?.product ?? null,
      hosts: [...(source.scope?.hosts ?? [])],
    },
    fields: source.fields.map((f) => ({ ...f })),
  };
}
