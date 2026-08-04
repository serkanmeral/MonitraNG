import type { DiscoveryHostDto } from '@/services/siemDiscoveryService';

export interface SecEventHostDirectoryEntry {
  /** Value stored in filter scope / sent as sourceHost. */
  filterValue: string;
  hostname: string;
  ip: string;
  /** Combobox title, e.g. "monitrang · 192.168.20.20". */
  label: string;
}

const IPV4_RE = /^\d{1,3}(?:\.\d{1,3}){3}$/;

export function looksLikeIpv4(value: string | null | undefined): boolean {
  return !!value && IPV4_RE.test(value.trim());
}

export function isFirewallSourceProduct(product?: string | null): boolean {
  const p = (product ?? '').trim().toLowerCase();
  if (!p) return false;
  return (
    p.includes('forti')
    || p === 'firewall'
    || p.includes('denied_flow')
    || p.endsWith('-firewall')
  );
}

function preferredFilterValue(host: DiscoveryHostDto): string {
  const hostname = (host.hostname || '').trim();
  const sam = (host.samAccountName || '').trim();
  const ip = (host.ip || '').trim();
  if (hostname && !looksLikeIpv4(hostname)) return hostname;
  if (sam && !looksLikeIpv4(sam)) return sam;
  return hostname || ip || sam;
}

function buildLabel(filterValue: string, hostname: string, ip: string): string {
  const name = (hostname && !looksLikeIpv4(hostname) ? hostname : filterValue).trim();
  const addr = ip.trim();
  if (name && addr && name !== addr) return `${name} · ${addr}`;
  return name || addr || filterValue;
}

/** Index discovery rows for host picker labels and IP→hostname resolve. */
export function buildSecEventHostDirectory(
  hosts: DiscoveryHostDto[],
): Map<string, SecEventHostDirectoryEntry> {
  const byKey = new Map<string, SecEventHostDirectoryEntry>();

  const index = (key: string | null | undefined, entry: SecEventHostDirectoryEntry) => {
    const k = (key ?? '').trim().toLowerCase();
    if (!k) return;
    const existing = byKey.get(k);
    // Prefer entries that have both name and IP.
    if (existing?.ip && existing.hostname && !looksLikeIpv4(existing.hostname)) return;
    byKey.set(k, entry);
  };

  for (const host of hosts) {
    const filterValue = preferredFilterValue(host);
    if (!filterValue) continue;
    const hostname = (host.hostname || host.samAccountName || filterValue).trim();
    const ip = (host.ip || '').trim();
    const entry: SecEventHostDirectoryEntry = {
      filterValue,
      hostname,
      ip,
      label: buildLabel(filterValue, hostname, ip),
    };
    index(filterValue, entry);
    index(hostname, entry);
    index(host.samAccountName, entry);
    index(ip, entry);
    // Short hostname (TERMINAL.odak.local → TERMINAL)
    const short = hostname.includes('.') ? hostname.split('.')[0] : '';
    if (short && !looksLikeIpv4(short)) index(short, entry);
  }

  return byKey;
}

export function formatSecEventHostLabel(
  raw: string,
  directory: Map<string, SecEventHostDirectoryEntry>,
): string {
  const key = raw.trim().toLowerCase();
  if (!key) return raw;
  const hit = directory.get(key);
  if (hit?.label) return hit.label;
  return raw;
}

/** Prefer canonical source.host (hostname) when user typed/selected an IP. */
export function resolveSecEventHostFilterValue(
  raw: string,
  directory: Map<string, SecEventHostDirectoryEntry>,
): string {
  const trimmed = raw.trim();
  if (!trimmed) return trimmed;
  const hit = directory.get(trimmed.toLowerCase());
  if (hit?.filterValue) return hit.filterValue;
  return trimmed;
}

export function buildSecEventHostComboItems(
  directory: Map<string, SecEventHostDirectoryEntry>,
  extraValues: string[],
): { title: string; value: string }[] {
  const seen = new Set<string>();
  const items: { title: string; value: string }[] = [];

  const add = (value: string, title?: string) => {
    const v = value.trim();
    if (!v) return;
    const key = v.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    items.push({
      value: v,
      title: title || formatSecEventHostLabel(v, directory),
    });
  };

  // Discovery-backed entries first (rich labels).
  const uniqueEntries = new Map<string, SecEventHostDirectoryEntry>();
  for (const entry of directory.values()) {
    uniqueEntries.set(entry.filterValue.toLowerCase(), entry);
  }
  for (const entry of uniqueEntries.values()) {
    add(entry.filterValue, entry.label);
  }

  for (const extra of extraValues) add(extra);

  return items.sort((a, b) => a.title.localeCompare(b.title, undefined, { sensitivity: 'base' }));
}
