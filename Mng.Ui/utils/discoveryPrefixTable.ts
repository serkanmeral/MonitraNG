/** IPAM-style prefix table for honest subnet/site grouping (IPv4 LPM). */

export interface DiscoveryPrefix {
  cidr: string
  label: string
  vlanName?: string | null
}

/** Fallback when API does not return prefixes (local/dev). */
export const DEFAULT_DISCOVERY_PREFIXES: DiscoveryPrefix[] = [
  { cidr: '192.168.20.0/24', label: 'Odak ofis' },
]

export const UNSCOPED_SITE = 'Unscoped'
export const NO_IP_SITE = 'No IP'

export interface SiteBucket {
  id: string
  label: string
  detail?: string
  subnetCidr?: string
}

function ipv4ToUint(ip: string): number | null {
  const parts = ip.trim().split('.')
  if (parts.length !== 4 || !parts.every((p) => /^\d+$/.test(p))) return null
  const nums = parts.map(Number)
  if (nums.some((n) => n < 0 || n > 255)) return null
  return (((nums[0]! << 24) >>> 0) + (nums[1]! << 16) + (nums[2]! << 8) + nums[3]!) >>> 0
}

function parseCidr(cidr: string): { network: number, prefixLen: number } | null {
  const [ipPart, lenPart] = cidr.trim().split('/')
  if (!ipPart || lenPart == null) return null
  const network = ipv4ToUint(ipPart)
  const prefixLen = Number(lenPart)
  if (network == null || !Number.isInteger(prefixLen) || prefixLen < 0 || prefixLen > 32) return null
  const mask = prefixLen === 0 ? 0 : (0xffffffff << (32 - prefixLen)) >>> 0
  return { network: (network & mask) >>> 0, prefixLen }
}

function formatCidr(network: number, prefixLen: number): string {
  return `${(network >>> 24) & 255}.${(network >>> 16) & 255}.${(network >>> 8) & 255}.${network & 255}/${prefixLen}`
}

export function matchPrefix(ip: string, prefixes: DiscoveryPrefix[]): DiscoveryPrefix | null {
  const ipBits = ipv4ToUint(ip)
  if (ipBits == null) return null

  let best: DiscoveryPrefix | null = null
  let bestLen = -1

  for (const entry of prefixes) {
    if (!entry?.cidr) continue
    const parsed = parseCidr(entry.cidr)
    if (!parsed || parsed.prefixLen < bestLen) continue
    const mask = parsed.prefixLen === 0 ? 0 : (0xffffffff << (32 - parsed.prefixLen)) >>> 0
    if ((ipBits & mask) !== parsed.network) continue
    best = {
      cidr: formatCidr(parsed.network, parsed.prefixLen),
      label: (entry.label || entry.cidr).trim(),
      vlanName: entry.vlanName || null,
    }
    bestLen = parsed.prefixLen
  }

  return best
}

/** Resolve grouping bucket for a display IP using longest-prefix-match. */
export function resolveSiteBucket(ip: string | null | undefined, prefixes: DiscoveryPrefix[]): SiteBucket {
  const trimmed = (ip || '').trim()
  if (!trimmed || trimmed === '—') {
    return { id: 'site-no-ip', label: NO_IP_SITE }
  }

  const match = matchPrefix(trimmed, prefixes)
  if (!match) {
    return { id: 'site-unscoped', label: UNSCOPED_SITE, detail: trimmed }
  }

  return {
    id: `site-${match.cidr}`,
    label: match.label,
    detail: match.cidr,
    subnetCidr: match.cidr,
  }
}

/**
 * Pick the best site among candidate IPs (scan/AD first, then agent).
 * Avoids "Unscoped" when agent primaryIp is outside the prefix table
 * but the discovery/scan IP still matches (e.g. Odak LAN).
 */
export function resolveBestSiteBucket(
  candidates: Array<string | null | undefined>,
  prefixes: DiscoveryPrefix[],
  fallback?: { siteLabel?: string | null; subnetCidr?: string | null },
): SiteBucket {
  const seen = new Set<string>()
  let anyIp = false

  for (const raw of candidates) {
    const ip = (raw || '').trim()
    if (!ip || ip === '—') continue
    anyIp = true
    const key = ip.toLowerCase()
    if (seen.has(key)) continue
    seen.add(key)

    const site = resolveSiteBucket(ip, prefixes)
    if (site.subnetCidr) return site
  }

  const label = (fallback?.siteLabel || '').trim()
  const cidr = (fallback?.subnetCidr || '').trim()
  if (label && label !== UNSCOPED_SITE && label !== NO_IP_SITE && cidr) {
    return {
      id: `site-${cidr}`,
      label,
      detail: cidr,
      subnetCidr: cidr,
    }
  }

  if (!anyIp) return { id: 'site-no-ip', label: NO_IP_SITE }
  return { id: 'site-unscoped', label: UNSCOPED_SITE }
}
