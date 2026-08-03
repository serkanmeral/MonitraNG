import type { SecEventQuery } from '@/types/apps/secEvent';
import type { SecEventSavedFilter } from '@/types/apps/secEventFilterCatalog';

function parseCsv(raw: string | undefined | null): string[] {
  if (!raw?.trim()) return [];
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
}

/**
 * Maps a saved/active filter (scope + fields) to Reactor sec-events query params.
 * Panel time range is applied by the caller.
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
> {
  const out: ReturnType<typeof mapSecEventSavedFilterToQuery> = {};
  const scope = filter.scope ?? {};

  if (scope.type?.trim()) out.sourceType = scope.type.trim();
  if (scope.product?.trim()) out.sourceProduct = scope.product.trim();

  const hosts = (scope.hosts ?? []).map((h) => h.trim()).filter(Boolean);
  if (hosts.length === 1) out.sourceHost = hosts[0];
  else if (hosts.length > 1) out.sourceHosts = hosts.join(',');

  for (const clause of filter.fields ?? []) {
    const value = (clause.value ?? '').trim();
    if (!value) continue;

    switch (clause.field) {
      case 'event.code': {
        if (clause.op === 'in') {
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
        if (clause.op === 'contains') {
          // Prefix-style contains → actionPrefix when value looks like a family
          out.eventActionPrefix = value.endsWith('.') ? value : undefined;
          if (!out.eventActionPrefix) out.search = value;
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
        break;
    }
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
