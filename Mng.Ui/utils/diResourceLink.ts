import { getDataGatewayProxyUrlWithAuth } from '@/services/apiService';

export const DI_HOME_PATH = '/apps/document-intelligence';

/** Markdown görsel embed: DG filePath (di-fp: encoded path). */
export const DI_FILE_PATH_MARKDOWN_PREFIX = 'di-fp:';

const DI_RESOURCE_PATH_RE = /\/apps\/document-intelligence\/r\/([^/?#]+)/i;

/** Paylaşılabilir belge deep link URL'si. */
export function buildDiResourceUrl(resourceId: string): string {
  const id = resourceId.trim();
  if (!id) return DI_HOME_PATH;
  return `${DI_HOME_PATH}/r/${encodeURIComponent(id)}`;
}

/** Tam ekran Collabora editör sayfası (yeni sekme). */
export function buildDiResourceEditorUrl(
  resourceId: string,
  options?: { readOnly?: boolean; bypassLock?: boolean },
): string {
  const id = resourceId.trim();
  if (!id) return DI_HOME_PATH;
  const params = new URLSearchParams();
  if (options?.readOnly) params.set('readOnly', '1');
  if (options?.bypassLock) params.set('bypassLock', '1');
  const query = params.toString();
  return `${DI_HOME_PATH}/editor/resource/${encodeURIComponent(id)}${query ? `?${query}` : ''}`;
}

/** Markdown editöründe kullanılacak göreli DI iç link URL'si. */
export function buildDiResourceMarkdownHref(resourceId: string): string {
  return buildDiResourceUrl(resourceId);
}

/** Yüklenen dosya görselleri için markdown img hedefi (kalıcı filePath). */
export function buildDiFilePathMarkdownHref(filePath: string): string {
  return `${DI_FILE_PATH_MARKDOWN_PREFIX}${encodeURIComponent(filePath.trim())}`;
}

export function parseDiFilePathFromMarkdownHref(href: string | null | undefined): string | null {
  const trimmed = (href ?? '').trim();
  if (!trimmed.startsWith(DI_FILE_PATH_MARKDOWN_PREFIX)) return null;
  try {
    return decodeURIComponent(trimmed.slice(DI_FILE_PATH_MARKDOWN_PREFIX.length));
  } catch {
    return null;
  }
}

/**
 * Markdown link hedefinden DI kaynak kimliği çıkarır.
 * Desteklenen: `/apps/document-intelligence/r/{id}`, tam URL, `?resourceId=`.
 */
export function parseDiResourceIdFromHref(href: string | null | undefined): string | null {
  const trimmed = (href ?? '').trim();
  if (!trimmed) return null;

  try {
    const url = trimmed.startsWith('http') ? new URL(trimmed) : new URL(trimmed, 'http://local');
    const pathMatch = url.pathname.match(DI_RESOURCE_PATH_RE);
    if (pathMatch?.[1]) return decodeURIComponent(pathMatch[1]).trim();
    const legacy = url.searchParams.get('resourceId')?.trim();
    if (legacy) return legacy;
  } catch {
    /* fall through */
  }

  const direct = DI_RESOURCE_PATH_RE.exec(trimmed);
  if (direct?.[1]) return decodeURIComponent(direct[1]).trim();

  const legacyQuery = /[?&]resourceId=([^&#]+)/i.exec(trimmed);
  if (legacyQuery?.[1]) return decodeURIComponent(legacyQuery[1]).trim();

  return null;
}

export function isDiInternalResourceHref(href: string | null | undefined): boolean {
  return parseDiResourceIdFromHref(href) != null;
}

/** Render sonrası di-fp: img src → kimlik doğrulamalı indirme URL'si. */
export function rewriteDiFileImagesInHtml(html: string): string {
  if (!import.meta.client || !html) return html;

  const root = document.createElement('div');
  root.innerHTML = html;
  root.querySelectorAll('img[src]').forEach((node) => {
    const img = node as HTMLImageElement;
    const filePath = parseDiFilePathFromMarkdownHref(img.getAttribute('src'));
    if (!filePath) return;
    img.src = getDataGatewayProxyUrlWithAuth(
      `/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`
    );
    img.loading = 'lazy';
  });
  return root.innerHTML;
}

/** Render sonrası DI iç linklerini modal tıklaması için işaretler (Vue Router navigasyonunu engeller). */
export function rewriteDiInternalLinksInHtml(html: string): string {
  if (!import.meta.client || !html) return html;

  const root = document.createElement('div');
  root.innerHTML = html;
  root.querySelectorAll('a[href]').forEach((node) => {
    const anchor = node as HTMLAnchorElement;
    const resourceId = parseDiResourceIdFromHref(anchor.getAttribute('href'));
    if (!resourceId) return;
    anchor.setAttribute('data-di-resource-id', resourceId);
    anchor.setAttribute('href', '#');
    anchor.classList.add('di-internal-resource-link');
    anchor.removeAttribute('target');
    anchor.removeAttribute('rel');
  });
  return root.innerHTML;
}

export function parseDiResourceIdFromAnchor(anchor: HTMLAnchorElement): string | null {
  const fromData = anchor.getAttribute('data-di-resource-id')?.trim();
  if (fromData) return fromData;
  return parseDiResourceIdFromHref(anchor.getAttribute('href'));
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
