import {
  fetchBlobFromDataGateway,
  getDataGatewayProxyUrlWithAuth,
} from '@/services/apiService';
import { ocUpdate } from '@/services/operationCoreService';
import { typedBlobForPreview } from '@/utils/ocAttachmentPreview';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import { fetchOdakPackageById } from '@/utils/odakSiparisService';
import { isOcFileUploadPayload, type OcFileUploadPayload } from '@/utils/ocWorkItemFileFields';

export interface PoDocumentEntry {
  /** Liste anahtari (tek dosyada "po") */
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

/** poDocument alanini liste satirlarina cevirir (tek veya dizi). */
export function listPoDocumentEntries(value: unknown, packageNo?: string): PoDocumentEntry[] {
  const fallbackName = packageNo ? `${packageNo}.pdf` : 'PO.pdf';
  if (value == null) return [];
  if (Array.isArray(value)) {
    return value
      .map((item, index) => entryFromSingleValue(item, `po-${index}`, fallbackName))
      .filter((e): e is PoDocumentEntry => e != null);
  }
  const single = entryFromSingleValue(value, 'po', fallbackName);
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

export function pendingPoPreviewDataUrl(payload: OcFileUploadPayload): string | null {
  const name = payload.originalFileName.toLowerCase();
  if (!name.endsWith('.pdf')) return null;
  return `data:application/pdf;base64,${payload.content}`;
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

export async function downloadPoEntry(entry: PoDocumentEntry): Promise<void> {
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
export async function resolvePoEntryPreviewBlobUrl(entry: PoDocumentEntry): Promise<string> {
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

export async function loadPackagePoState(packageId: string): Promise<{
  row: OdakPackageRow | null;
  poDocument: unknown;
  poVersion: string;
}> {
  const row = packageId ? await fetchOdakPackageById(packageId) : null;
  let poDocument = row?.poDocument ?? null;
  const legacyPath = row?.poDocumentPath?.trim();
  if (legacyPath && !poDocumentPathFromValue(poDocument)) {
    if (poDocument && typeof poDocument === 'object' && !Array.isArray(poDocument)) {
      poDocument = { ...(poDocument as Record<string, unknown>), path: legacyPath };
    } else {
      poDocument = legacyPath;
    }
  }
  return {
    row,
    poDocument,
    poVersion: row?.poVersion ?? '',
  };
}

export async function savePackagePoDocument(
  packageId: string,
  poDocument: unknown,
  poVersion: string
): Promise<void> {
  if (!packageId) throw new Error('Package id required');
  await ocUpdate(ODAK_SIPARIS_CONFIG.packagesDataset, packageId, {
    poDocument: poDocument ?? null,
    poVersion: poVersion.trim() || null,
  });
}

const PO_MAX_BYTES = 25 * 1024 * 1024;

export async function buildPoUploadPayload(file: File): Promise<OcFileUploadPayload> {
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
  return { content: base64, originalFileName: file.name };
}
