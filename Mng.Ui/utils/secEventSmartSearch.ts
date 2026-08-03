/**
 * Smart search tokens for Security Events explorer.
 * Prefixes: user:/actor: · ip:/src: · dst: · host: · id:/eventid:/code:
 * Bare 3–6 digit tokens → eventCode. Remaining text → free-text search.
 */

export type SecEventSmartSearchFields = {
  actorUser?: string;
  srcIp?: string;
  dstIp?: string;
  sourceHost?: string;
  eventCode?: string;
  /** Residual free-text (no prefix). */
  search?: string;
};

export type SecEventSmartSearchFieldKey = keyof SecEventSmartSearchFields;

const PREFIX_MAP: Record<string, Exclude<SecEventSmartSearchFieldKey, 'search'>> = {
  user: 'actorUser',
  actor: 'actorUser',
  ip: 'srcIp',
  src: 'srcIp',
  srcip: 'srcIp',
  dst: 'dstIp',
  dstip: 'dstIp',
  host: 'sourceHost',
  hostname: 'sourceHost',
  id: 'eventCode',
  eventid: 'eventCode',
  code: 'eventCode',
};

const BARE_EVENT_CODE = /^\d{3,6}$/;

/** Split on whitespace; keep quoted segments as one token (quotes stripped). */
export function tokenizeSecEventSearch(input: string): string[] {
  const tokens: string[] = [];
  const re = /"([^"]*)"|(\S+)/g;
  let match: RegExpExecArray | null;
  while ((match = re.exec(input)) !== null) {
    const token = match[1] !== undefined ? match[1] : match[2];
    if (token) tokens.push(token);
  }
  return tokens;
}

export function parseSecEventSmartSearch(input: string): SecEventSmartSearchFields {
  const trimmed = (input ?? '').trim();
  if (!trimmed) return {};

  const result: SecEventSmartSearchFields = {};
  const free: string[] = [];

  for (const token of tokenizeSecEventSearch(trimmed)) {
    const colon = token.indexOf(':');
    if (colon > 0) {
      const prefix = token.slice(0, colon).toLowerCase();
      const value = token.slice(colon + 1).trim();
      const field = PREFIX_MAP[prefix];
      if (field && value) {
        result[field] = value;
        continue;
      }
    }

    if (BARE_EVENT_CODE.test(token) && !result.eventCode) {
      result.eventCode = token;
      continue;
    }

    free.push(token);
  }

  if (free.length > 0) result.search = free.join(' ');
  return result;
}

/**
 * Merge a newly parsed draft into the current applied text filters.
 * Prefixed fields override when present; residual free-text replaces only when present in the draft.
 */
export function mergeSecEventSmartSearch(
  current: SecEventSmartSearchFields,
  incoming: SecEventSmartSearchFields,
): SecEventSmartSearchFields {
  return {
    actorUser: incoming.actorUser ?? current.actorUser,
    srcIp: incoming.srcIp ?? current.srcIp,
    dstIp: incoming.dstIp ?? current.dstIp,
    sourceHost: incoming.sourceHost ?? current.sourceHost,
    eventCode: incoming.eventCode ?? current.eventCode,
    search: incoming.search !== undefined ? incoming.search : current.search,
  };
}

export function clearSecEventSmartField(
  current: SecEventSmartSearchFields,
  key: SecEventSmartSearchFieldKey,
): SecEventSmartSearchFields {
  const next = { ...current };
  delete next[key];
  return next;
}

export function hasSecEventSmartFilters(fields: SecEventSmartSearchFields): boolean {
  return !!(
    fields.actorUser?.trim()
    || fields.srcIp?.trim()
    || fields.dstIp?.trim()
    || fields.sourceHost?.trim()
    || fields.eventCode?.trim()
    || fields.search?.trim()
  );
}
