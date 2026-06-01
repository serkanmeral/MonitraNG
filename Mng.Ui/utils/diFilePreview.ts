import type { DiResource } from '@/types/apps/documentIntelligence';

/** İnline önizlenebilir dosya türü. `none` = sadece indir. */
export type DiFilePreviewKind = 'image' | 'pdf' | 'text' | 'none';

const IMAGE_EXTS = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg', 'avif', 'ico'];
const PDF_EXTS = ['pdf'];
const TEXT_EXTS = [
  'txt', 'md', 'markdown', 'csv', 'tsv', 'log', 'json', 'xml', 'yaml', 'yml',
  'ini', 'conf', 'env', 'html', 'htm', 'css', 'js', 'ts', 'sql',
];

/** Görsel/PDF için boyut tavanı (bayt). Üzerindeyse önizleme yerine indir önerilir. */
export const DI_PREVIEW_MAX_BYTES = 25 * 1024 * 1024; // 25 MB
/** Düz metin önizleme tavanı (bayt) — DOM'u şişirmemek için daha düşük. */
export const DI_PREVIEW_TEXT_MAX_BYTES = 2 * 1024 * 1024; // 2 MB

function resExt(resource: DiResource): string {
  const fromField = (resource.extension ?? '').toLowerCase().replace(/^\./, '').trim();
  if (fromField) return fromField;
  const name = resource.fileName ?? resource.name ?? '';
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

/** Dosya için önizlemede kullanılacak MIME tipi (yoksa null → blob'un kendi tipi). */
export function diPreviewMime(resource: DiResource): string | null {
  return EXT_MIME[resExt(resource)] ?? null;
}

/** Uzantıya göre önizleme türünü (boyut sınırı dikkate alınmadan) döndürür. */
export function diPreviewKindByExt(resource: DiResource): DiFilePreviewKind {
  const ext = resExt(resource);
  if (IMAGE_EXTS.includes(ext)) return 'image';
  if (PDF_EXTS.includes(ext)) return 'pdf';
  if (TEXT_EXTS.includes(ext)) return 'text';
  return 'none';
}

/** Boyut sınırını da dikkate alarak gerçek önizleme türünü döndürür. */
export function diPreviewKind(resource: DiResource): DiFilePreviewKind {
  if (resource.type !== 'file') return 'none';
  const kind = diPreviewKindByExt(resource);
  if (kind === 'none') return 'none';
  const size = resource.size;
  if (size != null && Number.isFinite(size)) {
    if (kind === 'text' && size > DI_PREVIEW_TEXT_MAX_BYTES) return 'none';
    if (kind !== 'text' && size > DI_PREVIEW_MAX_BYTES) return 'none';
  }
  return kind;
}

/** Dosya inline önizlenebilir mi? */
export function isDiPreviewable(resource: DiResource): boolean {
  return diPreviewKind(resource) !== 'none';
}
