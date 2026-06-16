import type { OcAttachment } from '@/types/apps/operationCore';

/** İnline önizlenebilir ek türü. `none` = sadece indir. */
export type OcAttachmentPreviewKind = 'image' | 'pdf' | 'text' | 'none';

const IMAGE_EXTS = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg', 'avif', 'ico'];
const PDF_EXTS = ['pdf'];
const TEXT_EXTS = [
  'txt', 'md', 'markdown', 'csv', 'tsv', 'log', 'json', 'xml', 'yaml', 'yml',
  'ini', 'conf', 'env', 'html', 'htm', 'css', 'js', 'ts', 'sql',
];

/** Görsel/PDF için boyut tavanı (KB). Üzerindeyse önizleme yerine indir önerilir. */
export const PREVIEW_MAX_KB = 25 * 1024; // 25 MB
/** Düz metin önizleme tavanı (KB) — DOM'u şişirmemek için daha düşük. */
export const PREVIEW_TEXT_MAX_KB = 2 * 1024; // 2 MB

function attExt(att: OcAttachment): string {
  const fromField = (att.fileExt ?? '').toLowerCase().replace(/^\./, '').trim();
  if (fromField) return fromField;
  const name = att.fileName ?? '';
  const dot = name.lastIndexOf('.');
  return dot >= 0 ? name.slice(dot + 1).toLowerCase().trim() : '';
}

/** Uzantı → MIME tipi. DG bazen `application/octet-stream` döndürür; iframe/img
 *  octet-stream blob URL'ini render etmeyip indirir. Doğru MIME ile yeniden saralım. */
const EXT_MIME: Record<string, string> = {
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
  bmp: 'image/bmp',
  svg: 'image/svg+xml',
  avif: 'image/avif',
  ico: 'image/x-icon',
  pdf: 'application/pdf',
};

/** Ek için önizlemede kullanılacak MIME tipi (yoksa null → blob'un kendi tipi kullanılır). */
export function previewMime(att: OcAttachment): string | null {
  return EXT_MIME[attExt(att)] ?? null;
}

/** Uzantıya göre önizleme MIME tipi (yoksa null). */
export function previewMimeForFileName(fileName: string): string | null {
  const dot = fileName.lastIndexOf('.');
  const ext = dot >= 0 ? fileName.slice(dot + 1).toLowerCase().trim() : '';
  return EXT_MIME[ext] ?? null;
}

/**
 * DG octet-stream döndürürse iframe render etmeyip indirir; doğru MIME ile yeniden sar.
 */
export function typedBlobForPreview(blob: Blob, fileName: string): Blob {
  const mime = previewMimeForFileName(fileName);
  if (!mime || blob.type === mime) return blob;
  return new Blob([blob], { type: mime });
}

/** Uzantıya göre önizleme türünü (boyut sınırı dikkate alınmadan) döndürür. */
export function previewKindByExt(att: OcAttachment): OcAttachmentPreviewKind {
  const ext = attExt(att);
  if (IMAGE_EXTS.includes(ext)) return 'image';
  if (PDF_EXTS.includes(ext)) return 'pdf';
  if (TEXT_EXTS.includes(ext)) return 'text';
  return 'none';
}

/** Boyut sınırını da dikkate alarak gerçek önizleme türünü döndürür. */
export function previewKind(att: OcAttachment): OcAttachmentPreviewKind {
  const kind = previewKindByExt(att);
  if (kind === 'none') return 'none';
  const sizeKb = att.fileSizeKb;
  if (sizeKb != null && Number.isFinite(sizeKb)) {
    if (kind === 'text' && sizeKb > PREVIEW_TEXT_MAX_KB) return 'none';
    if (kind !== 'text' && sizeKb > PREVIEW_MAX_KB) return 'none';
  }
  return kind;
}

/** Ek inline önizlenebilir mi? */
export function isPreviewable(att: OcAttachment): boolean {
  return previewKind(att) !== 'none';
}
