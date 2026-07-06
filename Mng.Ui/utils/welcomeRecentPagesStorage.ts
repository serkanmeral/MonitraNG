import { decodeJwt } from 'jose';

export interface WelcomeRecentPageEntry {
  path: string;
  title: string;
  visitedAt: number;
}

const MAX_RECENT = 5;
const STORAGE_PREFIX = 'welcome_recent_';

function pickString(...values: unknown[]): string {
  for (const v of values) {
    const s = String(v ?? '').trim();
    if (s) return s;
  }
  return '';
}

/** Son URL segmenti: /apps/operation-core/workspace → workspace */
export function humanizeAppPath(path: string): string {
  const segment = path.split('/').filter(Boolean).pop() || path;
  return segment.replace(/-/g, ' ');
}

function readStoredUserInfo(): Record<string, unknown> | null {
  if (typeof localStorage === 'undefined') return null;
  try {
    const raw = localStorage.getItem('userInfo');
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    return parsed && typeof parsed === 'object' ? parsed : null;
  } catch {
    return null;
  }
}

/** Pinia → cookie sırasıyla token al */
export function readAccessToken(): string | null {
  try {
    const authStore = useAuthStore();
    if (authStore.accessToken) return authStore.accessToken;
  } catch {
    /* pinia henüz yok */
  }

  try {
    const fromCookie = useCookie<string | null>('access_token').value;
    if (fromCookie) return fromCookie;
  } catch {
    /* Nuxt context dışı */
  }

  return null;
}

export function resolveWelcomeRecentStorageKey(token?: string | null): string | null {
  const accessToken = token ?? readAccessToken();
  const storedUser = readStoredUserInfo();

  let claims: Record<string, unknown> | null = null;
  if (accessToken) {
    try {
      claims = decodeJwt(accessToken) as Record<string, unknown>;
    } catch {
      claims = null;
    }
  }

  const userKey = pickString(
    claims?.sub,
    claims?.preferred_username,
    claims?.username,
    claims?.mng_person_id,
    storedUser?.sub,
    storedUser?.preferred_username,
    storedUser?.username,
    storedUser?.mng_person_id,
  );

  const resolvedUserKey =
    userKey || (accessToken ? `session_${accessToken.slice(0, 16)}` : '');
  if (!resolvedUserKey) return null;

  let domainKey = pickString(
    claims?.domain_name,
    claims?.domain_id,
    storedUser?.domain_name,
    storedUser?.domain_id,
  );

  if (!domainKey) {
    try {
      const authStore = useAuthStore();
      domainKey = pickString(authStore.domainName, authStore.domainInfo?.name, authStore.domainInfo?.id);
    } catch {
      /* ignore */
    }
  }

  if (!domainKey) domainKey = 'default';

  return `${STORAGE_PREFIX}${resolvedUserKey}_${domainKey}`;
}

function readEntriesByKey(key: string): WelcomeRecentPageEntry[] {
  if (typeof localStorage === 'undefined') return [];
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as WelcomeRecentPageEntry[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function readWelcomeRecentPages(): WelcomeRecentPageEntry[] {
  const key = resolveWelcomeRecentStorageKey();
  if (!key) return [];
  return readEntriesByKey(key);
}

/** localStorage'a yazar; başarılıysa güncel listeyi döner */
export function trackWelcomeRecentPage(path: string, title?: string): WelcomeRecentPageEntry[] | null {
  if (typeof localStorage === 'undefined') return null;
  if (!path.startsWith('/apps/')) return null;

  const key = resolveWelcomeRecentStorageKey();
  if (!key) return null;

  const normalizedTitle = (title || '').trim() || humanizeAppPath(path);
  const now = Date.now();
  const existing = readEntriesByKey(key).filter((e) => e.path !== path);
  const next: WelcomeRecentPageEntry[] = [
    { path, title: normalizedTitle, visitedAt: now },
    ...existing,
  ].slice(0, MAX_RECENT);

  try {
    localStorage.setItem(key, JSON.stringify(next));
    return next;
  } catch {
    return null;
  }
}
