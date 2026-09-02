import type { DiResource } from '@/types/apps/documentIntelligence';
import { DI_RESOURCE_ORIGIN } from '@/types/apps/documentIntelligence';

/** İnline önizlenebilir dosya türü. `none` = sadece indir. */
export type DiFilePreviewKind = 'image' | 'pdf' | 'text' | 'drawio' | 'none';

const IMAGE_EXTS = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg', 'avif', 'ico'];
const PDF_EXTS = ['pdf'];
const TEXT_EXTS = [
  'txt', 'md', 'markdown', 'csv', 'tsv', 'log', 'json', 'xml', 'yaml', 'yml',
  'ini', 'conf', 'env', 'html', 'htm', 'css', 'js', 'ts', 'sql',
];
const DOCX_EXTS = ['docx'];
const SHEET_EXTS = ['xlsx', 'xls'];
const PRESENTATION_EXTS = ['pptx', 'ppt'];

function isOfficeExtension(resource: DiResource, exts: string[], mimeToken: string): boolean {
  const ext = resExt(resource);
  if (exts.includes(ext)) return true;
  const mime = (resource.mimeType ?? '').toLowerCase();
  return mime.includes(mimeToken);
}

function isDocxExtension(resource: DiResource): boolean {
  return isOfficeExtension(resource, DOCX_EXTS, 'wordprocessingml');
}

function isSheetExtension(resource: DiResource): boolean {
  return isOfficeExtension(resource, SHEET_EXTS, 'spreadsheetml');
}

function isPresentationExtension(resource: DiResource): boolean {
  return isOfficeExtension(resource, PRESENTATION_EXTS, 'presentationml');
}

/** Görsel/PDF için boyut tavanı (bayt). Üzerindeyse önizleme yerine indir önerilir. */
export const DI_PREVIEW_MAX_BYTES = 25 * 1024 * 1024; // 25 MB
/** Düz metin önizleme tavanı (bayt) — DOM'u şişirmemek için daha düşük. */
export const DI_PREVIEW_TEXT_MAX_BYTES = 2 * 1024 * 1024; // 2 MB

function resExt(resource: DiResource): string {
  const name = resource.fileName ?? resource.name ?? '';
  const lower = name.toLowerCase();
  if (lower.endsWith('.drawio.xml') || lower.endsWith('.drawio')) return 'drawio';
  const fromField = (resource.extension ?? '').toLowerCase().replace(/^\./, '').trim();
  if (fromField === 'drawio' || fromField === 'drawio.xml') return 'drawio';
  if (fromField) return fromField;
  const dot = name.lastIndexOf('.');
  return dot >= 0 ? name.slice(dot + 1).toLowerCase().trim() : '';
}

export function isDiDrawioFile(resource: Pick<DiResource, 'type' | 'extension' | 'fileName' | 'name' | 'mimeType'>): boolean {
  if (resource.type !== 'file') return false;
  const mime = (resource.mimeType ?? '').toLowerCase();
  if (mime.includes('jgraph') || mime.includes('mxfile')) return true;
  const name = `${resource.fileName ?? ''} ${resource.name ?? ''}`.toLowerCase();
  if (name.includes('.drawio')) return true;
  const ext = (resource.extension ?? '').toLowerCase().replace(/^\./, '').trim();
  return ext === 'drawio' || ext === 'drawio.xml';
}

export function isDiVisualEvidence(resource: DiResource): boolean {
  if (resource.type !== 'file') return false;
  if (isDiDrawioFile(resource)) return true;
  const ext = resExt(resource);
  if (IMAGE_EXTS.includes(ext)) return true;
  return (resource.mimeType ?? '').toLowerCase().startsWith('image/');
}

/** Dosya adı / MIME ile yükleme anında görsel kanıt mı? */
export function isDiVisualEvidenceFile(fileName: string, mimeType?: string | null): boolean {
  const name = (fileName ?? '').toLowerCase();
  if (name.endsWith('.drawio') || name.endsWith('.drawio.xml') || name.includes('.drawio.')) return true;
  const mime = (mimeType ?? '').toLowerCase();
  if (mime.startsWith('image/') || mime.includes('jgraph') || mime.includes('mxfile')) return true;
  const dot = name.lastIndexOf('.');
  const ext = dot >= 0 ? name.slice(dot + 1) : '';
  return IMAGE_EXTS.includes(ext);
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
  drawio: 'application/vnd.jgraph.mxfile',
  pdf: 'application/pdf',
};

/** Dosya için önizlemede kullanılacak MIME tipi (yoksa null → blob'un kendi tipi). */
export function diPreviewMime(resource: DiResource): string | null {
  return EXT_MIME[resExt(resource)] ?? null;
}

/** Yüklenen dosya mı? (<c>origin=upload</c> veya legacy boş origin). */
export function isDiUploadedFile(resource: DiResource): boolean {
  if (resource.type !== 'file') return false;
  const origin = (resource.origin ?? '').trim();
  return !origin || origin === DI_RESOURCE_ORIGIN.upload;
}

/** Collabora editöründe açılabilen yönetilen döküman mı? */
export function isDiManagedDocument(resource: DiResource): boolean {
  if (resource.type !== 'file') return false;
  const origin = (resource.origin ?? '').trim();
  return origin === DI_RESOURCE_ORIGIN.native
    || origin === DI_RESOURCE_ORIGIN.manual
    || origin === DI_RESOURCE_ORIGIN.system;
}

/** Gotenberg ile PDF önizlenebilir yüklenmiş DOCX mi? */
export function isDiUploadDocxPreviewable(resource: DiResource): boolean {
  return isDiUploadedFile(resource) && isDocxExtension(resource);
}

/** Sunucu tarafı PDF dönüşümü ile önizleme mi? */
export function isDiServerRenderedPdfPreview(resource: DiResource): boolean {
  return isDiUploadDocxPreviewable(resource);
}

/** Uzantıya göre önizleme türünü (boyut sınırı dikkate alınmadan) döndürür. */
export function diPreviewKindByExt(resource: DiResource): DiFilePreviewKind {
  if (isDiUploadDocxPreviewable(resource)) return 'pdf';
  if (isDiDrawioFile(resource)) return 'drawio';
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

/** Collabora editöründe açılabilir DOCX dosyası mı? */
export function isDiDocxEditable(resource: DiResource): boolean {
  if (!isDiManagedDocument(resource)) return false;
  if (!resource.hasContent && !resource.filePath) return false;
  return isDocxExtension(resource);
}

/** Yönetilen XLSX (elektronik tablo) mi? */
export function isDiSheet(resource: DiResource): boolean {
  if (!isDiManagedDocument(resource)) return false;
  return isSheetExtension(resource);
}

/** Yönetilen PPTX (sunum) mi? */
export function isDiPresentation(resource: DiResource): boolean {
  if (!isDiManagedDocument(resource)) return false;
  return isPresentationExtension(resource);
}

/** Collabora editöründe açılabilir yönetilen Office dosyası mı? (DOCX / XLSX / PPTX) */
export function isDiOfficeEditable(resource: DiResource): boolean {
  if (!isDiManagedDocument(resource)) return false;
  if (!resource.hasContent && !resource.filePath) return false;
  return isDocxExtension(resource) || isSheetExtension(resource) || isPresentationExtension(resource);
}

/** Gotenberg export/pdf ile PDF indirilebilir yönetilen Office dosyası mı? */
export function isDiOfficePdfExportable(resource: DiResource): boolean {
  return isDiOfficeEditable(resource);
}

/** Klonlanabilir kaynak mı? (markdown veya yönetilen DOCX — upload hariç) */
export function isDiCloneable(resource: DiResource): boolean {
  if (resource.type === 'markdown') return true;
  if (!isDiManagedDocument(resource)) return false;
  return isDocxExtension(resource);
}
