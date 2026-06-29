export const DI_HOME_PATH = '/apps/document-intelligence';

/** Paylaşılabilir belge deep link URL'si. */
export function buildDiResourceUrl(resourceId: string): string {
  const id = resourceId.trim();
  if (!id) return DI_HOME_PATH;
  return `${DI_HOME_PATH}/r/${encodeURIComponent(id)}`;
}

/** Klasör gezintisi için URL (opsiyonel deep link). */
export function buildDiFolderUrl(folderId: string | null): string {
  if (!folderId?.trim()) return DI_HOME_PATH;
  return `${DI_HOME_PATH}?folderId=${encodeURIComponent(folderId.trim())}`;
}

/** Eski `?resourceId=` query parametresi (geriye dönük uyumluluk). */
export function parseLegacyResourceIdQuery(query: Record<string, unknown>): string | null {
  const raw = query.resourceId;
  if (typeof raw === 'string' && raw.trim()) return raw.trim();
  if (Array.isArray(raw) && typeof raw[0] === 'string' && raw[0].trim()) return raw[0].trim();
  return null;
}

/** `folderId` query — tanımsız: query yok; null: kök klasör. */
export function parseFolderIdQuery(query: Record<string, unknown>): string | null | undefined {
  if (!('folderId' in query)) return undefined;
  const raw = query.folderId;
  if (raw == null || raw === '') return null;
  if (typeof raw === 'string') return raw.trim() || null;
  if (Array.isArray(raw) && typeof raw[0] === 'string') return raw[0].trim() || null;
  return null;
}
