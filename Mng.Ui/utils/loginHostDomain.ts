import { $fetch } from 'ofetch';

/** Host labels that must not be treated as a tenant on *.monitrang.com or *.localhost. */
const RESERVED_LOGIN_HOSTS = new Set(['app', 'api', 'www', 'mail', 'webmail', 'localhost']);

const DOMAIN_NAME_PATTERN = /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/;

/**
 * Tenant implied by the browser host.
 * abt.localhost and abt.monitrang.com → abt. localhost and app.monitrang.com → none.
 */
export function domainFromLoginHost(hostname?: string | null): string | null {
  const host = (hostname ?? '').split(':')[0].trim().toLowerCase();
  if (!host || host === 'localhost' || host === '127.0.0.1') return null;

  let label: string | null = null;
  if (host.endsWith('.localhost')) label = host.slice(0, -'.localhost'.length);
  else if (host.endsWith('.monitrang.com')) label = host.slice(0, -'.monitrang.com'.length);

  if (!label || label.includes('.')) return null;
  if (RESERVED_LOGIN_HOSTS.has(label)) return null;
  if (!DOMAIN_NAME_PATTERN.test(label)) return null;
  return label;
}

export function currentLoginHostDomain(): string | null {
  if (!import.meta.client || typeof window === 'undefined') return null;
  return domainFromLoginHost(window.location.hostname);
}

export interface DomainBranding {
  name: string;
  displayName: string;
  logo?: string | null;
  logoDark?: string | null;
  logoLight?: string | null;
  logoUrl?: string | null;
  updatedAt?: string | null;
}

/** Anonymous keeper call. The login page has no access token yet. */
export async function fetchDomainBranding(name: string): Promise<DomainBranding | null> {
  try {
    return await $fetch<DomainBranding>(`/api/keeper/domain/branding/${encodeURIComponent(name)}`);
  } catch {
    return null;
  }
}
