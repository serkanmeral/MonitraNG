/** op_fields.options — file alanı kısıtları (DG upload öncesi UI doğrulaması). */

export interface OcFileFieldOptions {
  maxSizeBytes: number;
  allowedExtensions: string[];
}

export const OC_FILE_FIELD_DEFAULT_MAX_SIZE_BYTES = 5 * 1024 * 1024;

export const OC_FILE_EXTENSION_PRESETS = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.gif',
  '.webp',
  '.doc',
  '.docx',
  '.xls',
  '.xlsx',
  '.txt',
  '.csv',
  '.zip',
] as const;

/** v-combobox bazen `{ title, value }` nesnesi döndürür — string'e indirger. */
export function coerceOcFileExtensionInput(raw: unknown): string {
  if (raw == null) return '';
  if (typeof raw === 'string') return raw;
  if (typeof raw === 'number' || typeof raw === 'boolean') return String(raw);
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    if (typeof o.value === 'string') return o.value;
    if (typeof o.title === 'string') return o.title;
  }
  return String(raw);
}

export function normalizeOcFileExtension(raw: unknown): string | null {
  const trimmed = coerceOcFileExtensionInput(raw).trim().toLowerCase();
  if (!trimmed) return null;
  const ext = trimmed.startsWith('.') ? trimmed : `.${trimmed}`;
  if (!/^\.[a-z0-9]+$/i.test(ext)) return null;
  return ext;
}

export function normalizeOcFileExtensionList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  const out: string[] = [];
  for (const item of raw) {
    const ext = normalizeOcFileExtension(item);
    if (ext && !out.includes(ext)) out.push(ext);
  }
  return out;
}

export function parseOcFileFieldOptions(raw: unknown): OcFileFieldOptions {
  const obj =
    raw && typeof raw === 'object' && !Array.isArray(raw) ? (raw as Record<string, unknown>) : {};

  const maxRaw = obj.maxSizeBytes ?? obj.maxSize ?? obj.max_size_bytes;
  let maxSizeBytes = OC_FILE_FIELD_DEFAULT_MAX_SIZE_BYTES;
  if (typeof maxRaw === 'number' && Number.isFinite(maxRaw) && maxRaw > 0) {
    maxSizeBytes = Math.round(maxRaw);
  } else if (typeof maxRaw === 'string' && maxRaw.trim()) {
    const n = Number.parseInt(maxRaw, 10);
    if (Number.isFinite(n) && n > 0) maxSizeBytes = n;
  }

  const mbRaw = obj.maxSizeMb ?? obj.maxSizeMB;
  if (typeof mbRaw === 'number' && Number.isFinite(mbRaw) && mbRaw > 0) {
    maxSizeBytes = Math.round(mbRaw * 1024 * 1024);
  }

  const extRaw = obj.allowedExtensions ?? obj.allowed_extensions ?? obj.extensions;
  const allowedExtensions: string[] = [];
  if (Array.isArray(extRaw)) {
    for (const ext of normalizeOcFileExtensionList(extRaw)) {
      if (!allowedExtensions.includes(ext)) allowedExtensions.push(ext);
    }
  } else if (typeof extRaw === 'string' && extRaw.trim()) {
    for (const part of extRaw.split(/[,;\s]+/)) {
      const ext = normalizeOcFileExtension(part);
      if (ext && !allowedExtensions.includes(ext)) allowedExtensions.push(ext);
    }
  }

  return { maxSizeBytes, allowedExtensions };
}

export function buildOcFileFieldOptionsPayload(
  maxSizeMb: number,
  allowedExtensions: unknown
): Record<string, unknown> {
  const mb = Number.isFinite(maxSizeMb) && maxSizeMb > 0 ? maxSizeMb : 5;
  const exts = normalizeOcFileExtensionList(
    Array.isArray(allowedExtensions) ? allowedExtensions : []
  );
  const payload: Record<string, unknown> = {
    maxSizeBytes: Math.round(mb * 1024 * 1024),
  };
  if (exts.length) payload.allowedExtensions = exts;
  return payload;
}

export function formatOcFileFieldOptionsSummary(
  options: OcFileFieldOptions,
  labels: { anyType: string; maxMb: (n: string) => string }
): string {
  const mb = (options.maxSizeBytes / (1024 * 1024)).toFixed(0);
  const types =
    options.allowedExtensions.length > 0
      ? options.allowedExtensions.join(', ')
      : labels.anyType;
  return `${labels.maxMb(mb)} · ${types}`;
}
