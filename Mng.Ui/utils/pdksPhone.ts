const DB_NAME = 'monitrang-passage';
const STORE = 'vault';
const RECORD = 'phone';

export type PhoneVault = {
  publicKey: string;
  privateKey: CryptoKey;
  accessToken: string;
  refreshToken: string;
  domain: string;
};

type VaultRecord = PhoneVault & { id: string };

export type DeviceView = {
  status: string;
  personName?: string | null;
  photoRequired?: boolean;
};

export type PassagePreview = {
  status: string;
  message?: string | null;
  locationTitle?: string | null;
  doorTitle?: string | null;
  personName?: string | null;
  direction?: string | null;
  breakAction?: string | null;
  photoRequired?: boolean;
};

export type PassageResult = {
  result: string;
  rejectReason?: string | null;
  direction?: string | null;
  occurredAt?: string | null;
  displayName?: string | null;
  doorTitle?: string | null;
  greeting?: string | null;
  message?: string | null;
};

type TokenPayload = {
  accessToken?: string;
  refreshToken?: string;
  error?: string;
  errorDescription?: string;
};

function database(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id' });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function readPhone(): Promise<PhoneVault | null> {
  const db = await database();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly');
    const request = tx.objectStore(STORE).get(RECORD);
    request.onsuccess = () => {
      const row = request.result as VaultRecord | undefined;
      if (!row?.publicKey || !row.privateKey) {
        resolve(null);
        return;
      }
      resolve({
        publicKey: row.publicKey,
        privateKey: row.privateKey,
        accessToken: row.accessToken || '',
        refreshToken: row.refreshToken || '',
        domain: row.domain || '',
      });
    };
    request.onerror = () => reject(request.error);
  });
}

export async function writePhone(phone: PhoneVault): Promise<void> {
  const db = await database();
  const row: VaultRecord = { id: RECORD, ...phone };
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(row);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function clearPhone(): Promise<void> {
  const db = await database();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).delete(RECORD);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function createPhoneKey(): Promise<{ publicKey: string; privateKey: CryptoKey }> {
  const pair = await crypto.subtle.generateKey(
    { name: 'ECDSA', namedCurve: 'P-256' },
    true,
    ['sign'],
  );
  const spki = new Uint8Array(await crypto.subtle.exportKey('spki', pair.publicKey));
  const pkcs8 = new Uint8Array(await crypto.subtle.exportKey('pkcs8', pair.privateKey));
  const privateKey = await crypto.subtle.importKey(
    'pkcs8',
    pkcs8,
    { name: 'ECDSA', namedCurve: 'P-256' },
    false,
    ['sign'],
  );
  pkcs8.fill(0);
  return { publicKey: bytesToBase64(spki), privateKey };
}

export async function signNonce(privateKey: CryptoKey, nonce: string): Promise<string> {
  const signed = new Uint8Array(await crypto.subtle.sign(
    { name: 'ECDSA', hash: 'SHA-256' },
    privateKey,
    new TextEncoder().encode(nonce),
  ));
  return bytesToBase64(signed);
}

export function readPassageNonce(text: string, origin: string): string | null {
  const trimmed = text.trim();
  if (!trimmed) return null;
  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return null;
  }
  if (url.origin !== origin || !url.pathname.endsWith('/gecis')) return null;
  const nonce = url.searchParams.get('n')?.trim() ?? '';
  return nonce || null;
}

export function tokenLeftSeconds(token: string): number {
  try {
    const part = token.split('.')[1] || '';
    const json = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number };
    return (json.exp || 0) - Math.floor(Date.now() / 1000);
  } catch {
    return 0;
  }
}

export function domainFromToken(token: string): string {
  try {
    const part = token.split('.')[1] || '';
    const json = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/'))) as {
      domain_name?: string;
      domainName?: string;
    };
    return json.domain_name || json.domainName || '';
  } catch {
    return '';
  }
}

export async function loginPassage(username: string, password: string, domain: string): Promise<{
  accessToken: string;
  refreshToken: string;
}> {
  const body: { username: string; password: string; domain?: string } = { username, password };
  if (domain) body.domain = domain;
  const response = await fetch('/api/auth/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  const payload = await readJson<TokenPayload>(response);
  if (!response.ok || !payload.accessToken || !payload.refreshToken) {
    throw new Error(payload.errorDescription || payload.error || 'Giriş başarısız.');
  }
  return { accessToken: payload.accessToken, refreshToken: payload.refreshToken };
}

export async function refreshPassage(refreshToken: string, domain: string): Promise<{
  accessToken: string;
  refreshToken: string;
}> {
  const response = await fetch('/api/keeper/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken, domain }),
  });
  const payload = await readJson<TokenPayload>(response);
  if (!response.ok || !payload.accessToken || !payload.refreshToken) {
    throw new Error('Oturum kapandı. Yeniden giriş yapın.');
  }
  return { accessToken: payload.accessToken, refreshToken: payload.refreshToken };
}

export async function enrollPhone(accessToken: string, publicKey: string): Promise<DeviceView> {
  const response = await fetch('/api/keeper/auth/device', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ publicKey }),
  });
  return readDevice(response);
}

export async function readPhoneStatus(accessToken: string, publicKey: string): Promise<DeviceView> {
  const response = await fetch(`/api/keeper/auth/device?publicKey=${encodeURIComponent(publicKey)}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  return readDevice(response);
}

export async function previewPassage(extensionsBase: string, accessToken: string, publicKey: string, nonce: string): Promise<PassagePreview> {
  const response = await fetch(`${extensionsBase}/api/v1/pdks/passages/preview`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ nonce, publicKey }),
  });
  const payload = await readJson<PassagePreview>(response);
  if (!response.ok) {
    throw new Error(payload.message || 'Kod ayrılamadı.');
  }
  return payload;
}

export async function confirmPassage(
  extensionsBase: string,
  accessToken: string,
  publicKey: string,
  nonce: string,
  signature: string,
  photo?: string | null,
  action?: string | null,
): Promise<PassageResult> {
  const response = await fetch(`${extensionsBase}/api/v1/pdks/passages/confirm`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ nonce, publicKey, signature, photo: photo || undefined, action: action || undefined }),
  });
  const payload = await readJson<PassageResult>(response);
  if (!response.ok) {
    throw new Error(payload.message || 'Geçiş kaydedilemedi.');
  }
  return payload;
}

async function readDevice(response: Response): Promise<DeviceView> {
  const payload = await readJson<DeviceView & TokenPayload>(response);
  if (!response.ok) {
    throw new Error(payload.errorDescription || payload.error || 'Anahtar okunamadı.');
  }
  return payload;
}

async function readJson<T>(response: Response): Promise<T> {
  const text = await response.text();
  if (!text) return {} as T;
  return JSON.parse(text) as T;
}

function bytesToBase64(bytes: Uint8Array): string {
  let binary = '';
  const size = 0x8000;
  for (let index = 0; index < bytes.length; index += size) {
    binary += String.fromCharCode(...bytes.subarray(index, index + size));
  }
  return btoa(binary);
}
