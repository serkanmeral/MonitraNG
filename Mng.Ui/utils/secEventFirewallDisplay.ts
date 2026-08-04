import type { SecEventListItem } from '@/types/apps/secEvent';

/** True for firewall / FortiGate / PAN / ASA style events. */
export function isFirewallSecEvent(
  item: Pick<SecEventListItem, 'sourceType' | 'sourceProduct' | 'parserId'> | null | undefined,
): boolean {
  if (!item) return false;
  const type = (item.sourceType ?? '').trim().toLowerCase();
  const product = (item.sourceProduct ?? '').trim().toLowerCase();
  const parser = (item.parserId ?? '').trim().toLowerCase();
  if (type === 'firewall') return true;
  if (parser.startsWith('firewall.')) return true;
  return (
    product.includes('forti')
    || product.includes('pan-os')
    || product.includes('panos')
    || product.includes('asa')
    || product.includes('palo')
  );
}

export function secEventBagField(
  fields: Record<string, unknown> | null | undefined,
  key: string,
): string | null {
  if (!fields || typeof fields !== 'object') return null;
  const raw = fields[key];
  if (raw == null) return null;
  if (typeof raw === 'object') return null;
  const s = String(raw).trim();
  return s || null;
}

/** Compact second-line for events table: policy · service · :dstPort */
export function firewallTableMetaLine(item: SecEventListItem): string | null {
  if (!isFirewallSecEvent(item)) return null;
  const parts: string[] = [];
  const policy = secEventBagField(item.fields, 'custom.policy_id');
  const service = secEventBagField(item.fields, 'custom.service');
  if (policy) parts.push(`policy ${policy}`);
  if (service) parts.push(service);
  if (item.networkDstPort != null && item.networkDstPort > 0) {
    parts.push(`:${item.networkDstPort}`);
  }
  return parts.length ? parts.join(' · ') : null;
}

export function firewallFlowEndpoint(item: SecEventListItem): string | null {
  const src = item.networkSrcIp?.trim();
  const dst = item.networkDstIp?.trim();
  const srcPort = secEventBagField(item.fields, 'custom.src_port');
  const dstPort =
    item.networkDstPort != null && item.networkDstPort > 0
      ? String(item.networkDstPort)
      : null;
  if (!src && !dst) return null;
  const left = src ? (srcPort ? `${src}:${srcPort}` : src) : '—';
  const right = dst ? (dstPort ? `${dst}:${dstPort}` : dst) : '—';
  return `${left} → ${right}`;
}
