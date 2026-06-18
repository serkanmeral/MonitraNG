import {
  fetchBlobFromDataGateway,
  getDataGatewayProxyUrlWithAuth,
} from '@/services/apiService';
import { ocUpdate } from '@/services/operationCoreService';
import { typedBlobForPreview } from '@/utils/ocAttachmentPreview';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow, type OdakPoDocumentScope } from '@/utils/odakSiparisConfig';
import {
  canViewRestrictedPoDocuments,
  type OdakPackagePoDocumentAccessConfig,
  sanitizePackageRowPoDocuments,
} from '@/utils/odakSiparisPoDocumentAccess';
import { fetchOdakPackageById } from '@/utils/odakSiparisService';
import { isOcFileUploadPayload, type OcFileUploadPayload } from '@/utils/ocWorkItemFileFields';

export interface PoDocumentEntry {
  /** Liste anahtari */
  key: string;
  fileName: string;
  path: string | null;
  pending: OcFileUploadPayload | null;
  isPending: boolean;
  raw: unknown;
}

export function poDocumentPathFromValue(value: unknown): string | null {
  if (value == null) return null;
  if (typeof value === 'string') {
    const p = value.trim();
    return p || null;
  }
  if (typeof value === 'object' && !Array.isArray(value)) {
    const o = value as Record<string, unknown>;
    if (isOcFileUploadPayload(value)) return null;
    for (const key of ['path', 'Path', 'filePath', 'FilePath']) {
      const path = o[key];
      if (typeof path === 'string' && path.trim()) return path.trim();
    }
  }
  return null;
}

export function poDocumentFileName(value: unknown, fallback = 'PO.pdf'): string {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    const o = value as Record<string, unknown>;
    if (typeof o.file_name === 'string' && o.file_name.trim()) return o.file_name.trim();
    if (typeof o.originalFileName === 'string' && o.originalFileName.trim()) return o.originalFileName.trim();
  }
  const path = poDocumentPathFromValue(value);
  if (path) return path.split('/').pop() || fallback;
  if (isOcFileUploadPayload(value)) return value.originalFileName;
  return fallback;
}

function entryFromSingleValue(value: unknown, key: string, fallbackName: string): PoDocumentEntry | null {
  if (value == null) return null;
  if (isOcFileUploadPayload(value)) {
    return {
      key,
      fileName: value.originalFileName || fallbackName,
      path: null,
      pending: value,
      isPending: true,
      raw: value,
    };
  }
  const path = poDocumentPathFromValue(value);
  if (path || (typeof value === 'object' && value !== null)) {
    return {
      key,
      fileName: poDocumentFileName(value, fallbackName),
      path,
      pending: null,
      isPending: false,
      raw: value,
    };
  }
  return null;
}

/** PO dosya alanini liste satirlarina cevirir (tek veya dizi). */
export function listPoDocumentEntries(
  value: unknown,
  packageNo?: string,
  keyPrefix = 'po'
): PoDocumentEntry[] {
  const fallbackName = packageNo ? `${packageNo}.pdf` : 'PO.pdf';
  if (value == null) return [];
  if (Array.isArray(value)) {
    return value
      .map((item, index) => entryFromSingleValue(item, `${keyPrefix}-${index}`, fallbackName))
      .filter((e): e is PoDocumentEntry => e != null);
  }
  const single = entryFromSingleValue(value, keyPrefix, fallbackName);
  return single ? [single] : [];
}

export function hasStoredPoDocument(value: unknown): boolean {
  return listPoDocumentEntries(value).some((e) => !e.isPending && Boolean(e.path));
}

export function hasPendingPoUpload(value: unknown): boolean {
  return listPoDocumentEntries(value).some((e) => e.isPending);
}

export function isPoDocumentDirty(current: unknown, saved: unknown): boolean {
  if (current === saved) return false;
  if (hasPendingPoUpload(current)) return true;
  if (current == null && saved == null) return false;
  return JSON.stringify(current) !== JSON.stringify(saved);
}

/** Entry listesinden DG'ye yazilacak ham dizi degerini uretir. */
export function poDocumentsRawFromEntries(entries: PoDocumentEntry[]): unknown[] | null {
  if (!entries.length) return null;
  return entries.map((e) => (e.isPending && e.pending ? e.pending : e.raw));
}

export function removePoDocumentEntry(value: unknown, entryKey: string, keyPrefix = 'po'): unknown {
  const entries = listPoDocumentEntries(value, undefined, keyPrefix).filter((e) => e.key !== entryKey);
  return poDocumentsRawFromEntries(entries);
}

export function appendPoDocumentUpload(
  current: unknown,
  payload: OcFileUploadPayload,
  keyPrefix = 'po'
): unknown[] {
  const existing = poDocumentsRawFromEntries(listPoDocumentEntries(current, undefined, keyPrefix)) ?? [];
  return [...existing, payload];
}

function appendLegacyPath(items: unknown[], path: string | null | undefined): unknown[] {
  const p = path?.trim();
  if (!p) return items;
  const already = items.some((item) => poDocumentPathFromValue(item) === p);
  if (already) return items;
  return [...items, p];
}

/** Legacy + yeni alanlardan normalize edilmis PO belge dizileri. */
export function normalizePoDocumentsFromRow(row: OdakPackageRow | null | undefined): {
  poDocumentsGlobal: unknown;
  poDocumentsRestricted: unknown;
} {
  let globalItems = poDocumentsRawFromEntries(
    listPoDocumentEntries(row?.poDocumentsGlobal ?? null, undefined, 'global')
  ) ?? [];

  if (!globalItems.length) {
    const legacyDoc = row?.poDocument ?? null;
    if (legacyDoc != null) {
      const legacyEntries = listPoDocumentEntries(legacyDoc, undefined, 'legacy');
      globalItems = legacyEntries.map((e) => (e.isPending && e.pending ? e.pending : e.raw));
    }
  }
  globalItems = appendLegacyPath(globalItems, row?.poDocumentPath);

  let restrictedItems =
    poDocumentsRawFromEntries(
      listPoDocumentEntries(row?.poDocumentsRestricted ?? null, undefined, 'restricted')
    ) ?? [];
  restrictedItems = appendLegacyPath(restrictedItems, row?.poDocumentPathRedacted);

  return {
    poDocumentsGlobal: globalItems.length ? globalItems : null,
    poDocumentsRestricted: restrictedItems.length ? restrictedItems : null,
  };
}

export function pendingPoPreviewDataUrl(payload: OcFileUploadPayload): string | null {
  const name = payload.originalFileName.toLowerCase();
  if (!name.endsWith('.pdf')) return null;
  return `data:application/pdf;base64,${payload.content}`;
}

function assertPoDownloadAllowed(
  scope: OdakPoDocumentScope,
  userGroups: string[],
  accessConfig: OdakPackagePoDocumentAccessConfig
): void {
  if (scope === 'restricted' && !canViewRestrictedPoDocuments(userGroups, accessConfig)) {
    throw new Error('PO access denied');
  }
}

/** iframe/object icin onizleme URL — kayitli: auth proxy, bekleyen: data URL. */
export function poEntryPreviewUrl(entry: PoDocumentEntry | null): string | null {
  if (!entry) return null;
  if (entry.isPending && entry.pending) {
    return pendingPoPreviewDataUrl(entry.pending);
  }
  if (entry.path) {
    return getDataGatewayProxyUrlWithAuth(
      `/api/v1/files/download?filePath=${encodeURIComponent(entry.path)}`
    );
  }
  return null;
}

export function poEntryDownloadUrl(entry: PoDocumentEntry | null): string | null {
  if (!entry?.path) return null;
  return getDataGatewayProxyUrlWithAuth(
    `/api/v1/files/download?filePath=${encodeURIComponent(entry.path)}`
  );
}

export async function downloadPoEntry(
  entry: PoDocumentEntry,
  scope: OdakPoDocumentScope = 'global',
  userGroups: string[] = [],
  accessConfig: OdakPackagePoDocumentAccessConfig = { restrictedViewerGroups: [] }
): Promise<void> {
  assertPoDownloadAllowed(scope, userGroups, accessConfig);

  if (entry.isPending && entry.pending) {
    const url = pendingPoPreviewDataUrl(entry.pending);
    if (!url) return;
    const a = document.createElement('a');
    a.href = url;
    a.download = entry.fileName;
    a.click();
    return;
  }
  if (!entry.path) return;
  const blob = await fetchBlobFromDataGateway(
    `/api/v1/files/download?filePath=${encodeURIComponent(entry.path)}`
  );
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = entry.fileName;
  a.click();
  URL.revokeObjectURL(url);
}

/** Kayitli dosya icin blob URL; bekleyen yukleme icin data URL. Caller revokeObjectURL ile temizlemeli (data URL haric). */
export async function resolvePoEntryPreviewBlobUrl(
  entry: PoDocumentEntry,
  scope: OdakPoDocumentScope = 'global',
  userGroups: string[] = [],
  accessConfig: OdakPackagePoDocumentAccessConfig = { restrictedViewerGroups: [] }
): Promise<string> {
  assertPoDownloadAllowed(scope, userGroups, accessConfig);

  if (entry.isPending && entry.pending) {
    const url = pendingPoPreviewDataUrl(entry.pending);
    if (!url) throw new Error('Preview not available');
    return url;
  }
  if (!entry.path) throw new Error('No file path');
  const blob = await fetchBlobFromDataGateway(
    `/api/v1/files/download?filePath=${encodeURIComponent(entry.path)}`
  );
  const previewName = entry.fileName.toLowerCase().endsWith('.pdf')
    ? entry.fileName
    : `${entry.fileName}.pdf`;
  const typed = typedBlobForPreview(blob, previewName);
  return URL.createObjectURL(typed);
}

export interface PackagePoState {
  row: OdakPackageRow | null;
  poDocumentsGlobal: unknown;
  poDocumentsRestricted: unknown;
  poVersion: string;
}

export async function loadPackagePoState(
  packageId: string,
  options?: {
    userGroups?: string[];
    accessConfig?: OdakPackagePoDocumentAccessConfig;
  }
): Promise<PackagePoState> {
  const userGroups = options?.userGroups ?? [];
  const accessConfig = options?.accessConfig ?? { restrictedViewerGroups: [] };

  let row = packageId ? await fetchOdakPackageById(packageId) : null;
  if (row) {
    row = sanitizePackageRowPoDocuments(row, userGroups, accessConfig);
  }

  const normalized = normalizePoDocumentsFromRow(row);
  return {
    row,
    poDocumentsGlobal: normalized.poDocumentsGlobal,
    poDocumentsRestricted: normalized.poDocumentsRestricted,
    poVersion: row?.poVersion ?? '',
  };
}

export async function savePackagePoDocuments(
  packageId: string,
  payload: {
    poDocumentsGlobal: unknown;
    poDocumentsRestricted: unknown;
    poVersion: string;
  }
): Promise<void> {
  if (!packageId) throw new Error('Package id required');
  await ocUpdate(ODAK_SIPARIS_CONFIG.packagesDataset, packageId, {
    poDocumentsGlobal: payload.poDocumentsGlobal ?? null,
    poDocumentsRestricted: payload.poDocumentsRestricted ?? null,
    poVersion: payload.poVersion.trim() || null,
    poDocument: null,
  });
}

/** @deprecated savePackagePoDocuments kullanin */
export async function savePackagePoDocument(
  packageId: string,
  poDocument: unknown,
  poVersion: string
): Promise<void> {
  await savePackagePoDocuments(packageId, {
    poDocumentsGlobal: poDocument ?? null,
    poDocumentsRestricted: null,
    poVersion,
  });
}

const PO_MAX_BYTES = 25 * 1024 * 1024;

export function poUploadFolderForScope(scope: OdakPoDocumentScope): string {
  return scope === 'restricted' ? 'po-restricted' : 'po-global';
}

export async function buildPoUploadPayload(
  file: File,
  scope: OdakPoDocumentScope = 'global'
): Promise<OcFileUploadPayload & { folder?: string }> {
  if (!file.name.toLowerCase().endsWith('.pdf')) {
    throw new Error('PDF only');
  }
  if (file.size > PO_MAX_BYTES) {
    throw new Error('File too large');
  }
  const base64 = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? '');
      const comma = result.indexOf(',');
      resolve(comma >= 0 ? result.slice(comma + 1) : result);
    };
    reader.onerror = () => reject(reader.error ?? new Error('read failed'));
    reader.readAsDataURL(file);
  });
  return {
    content: base64,
    originalFileName: file.name,
    folder: poUploadFolderForScope(scope),
  };
}

export function isPoDocumentsStateDirty(
  current: { global: unknown; restricted: unknown; poVersion: string },
  saved: { global: unknown; restricted: unknown; poVersion: string }
): boolean {
  return (
    isPoDocumentDirty(current.global, saved.global) ||
    isPoDocumentDirty(current.restricted, saved.restricted) ||
    current.poVersion.trim() !== saved.poVersion.trim()
  );
}

export function hasAnyStoredPoDocument(global: unknown, restricted: unknown): boolean {
  return hasStoredPoDocument(global) || hasStoredPoDocument(restricted);
}

export function hasAnyPendingPoUpload(global: unknown, restricted: unknown): boolean {
  return hasPendingPoUpload(global) || hasPendingPoUpload(restricted);
}
