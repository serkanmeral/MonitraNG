import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SiemDiscoveryAgentInfo, SiemDiscoveryHost } from '@/types/apps/siemDiscovery';

export function isIpv4Literal(value: string): boolean {
  return /^\d{1,3}(\.\d{1,3}){3}$/.test(value.trim());
}

export function shortHostKey(hostname: string): string {
  const h = hostname.trim().toLowerCase();
  if (!h) return '';
  if (isIpv4Literal(h)) return h;
  return h.split('.')[0] || h;
}

function asString(v: unknown): string | null {
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return null;
}

/** Prefer agent machine name when discovery hostname is a bare scan IP. */
export function resolveDisplayHostname(
  rawHostname: string,
  agent?: SiemDiscoveryAgentInfo | null,
): string {
  const h = (rawHostname || '').trim();
  const machine = (agent?.machine || '').trim();
  if (machine && (!h || h === '—' || isIpv4Literal(h))) return machine;
  return h || machine || '—';
}

type HostHints = Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null | undefined;

/** Keys used for sec-event search + client-side host filter. */
export function discoveryHostMatchKeys(
  hostname: string,
  host?: HostHints,
): string[] {
  const keys = new Set<string>();
  const add = (raw?: string | null) => {
    const v = (raw || '').trim().toLowerCase();
    if (!v || v === '—') return;
    keys.add(v);
    const short = shortHostKey(v);
    if (short) keys.add(short);
  };

  add(hostname);
  add(host?.hostname);
  add(host?.ip);
  add(host?.agent?.machine);
  add(host?.agent?.primaryIp);
  for (const ip of host?.agent?.ipAddresses ?? []) add(ip);

  return [...keys];
}

/**
 * Prefer a DNS/host name over a scan IP for Reactor multi_match (source.host).
 */
export function preferredSecEventSearchTerm(
  hostname: string,
  host?: HostHints,
): string {
  const keys = discoveryHostMatchKeys(hostname, host);
  const named = keys.find((k) => k && !isIpv4Literal(k));
  if (named) return named;
  return shortHostKey(hostname) || hostname.trim().toLowerCase();
}

export function secEventMatchesDiscoveryHost(
  item: SecEventListItem,
  hostname: string,
  host?: HostHints,
): boolean {
  const wants = discoveryHostMatchKeys(hostname, host);
  if (!wants.length) return false;

  const fields = item.fields ?? {};
  const candidates = [
    item.sourceHost,
    asString(fields.machine),
    asString(fields.hostname),
    asString(fields['host.name']),
    asString(fields.primaryIp),
    ...(Array.isArray(fields.ipAddresses)
      ? fields.ipAddresses.map((x) => asString(x)).filter(Boolean) as string[]
      : []),
  ];

  for (const raw of candidates) {
    const src = (raw || '').trim().toLowerCase();
    if (!src) continue;
    const srcShort = shortHostKey(src);
    for (const want of wants) {
      if (
        src === want
        || srcShort === want
        || src.includes(want)
        || want.includes(srcShort)
      ) {
        return true;
      }
    }
  }
  return false;
}
