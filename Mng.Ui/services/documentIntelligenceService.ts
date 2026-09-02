import { fetchFromDocuments, fetchBlobFromDataGateway, getAccessToken } from '@/services/apiService';
import {
  documentsApiErrorUserMessage,
  parseDocumentsApiErrorBody,
} from '@/utils/documentsApiError';
import { useAuthStore } from '@/stores/auth';
import {
  diFullPermission,
  type DiBreadcrumb,
  type DiCreateFileResourceRequest,
  type DiCreateNativeDocumentRequest,
  type DiCreateNativeOfficeRequest,
  type DiCreateFolderRequest,
  type DiCreateMarkdownRequest,
  type DiCreateResourceLinkRequest,
  type DiLinkedResource,
  type DiLinkedWorkItem,
  type DiResourceLink,
  type DiResourceLinkListResult,
  type DiEffectivePermission,
  type DiFolderPermissions,
  type DiGroupPermission,
  type DiMarkdownContent,
  type DiMarkdownVersion,
  type DiMarkdownVersionContent,
  type DiMoveRequest,
  type DiCloneResourceRequest,
  type DiRenameRequest,
  type DiResource,
  type DiResourceBootstrap,
  type DiResourceBrowseContext,
  type DiResourceListResult,
  type DiSetFolderPermissionsRequest,
  type DiTreeNode,
  type DiTreePath,
  type DiUpdateMarkdownRequest,
  type DiCreateTemplateFromSourceRequest,
  type DiCreateTemplateFromReferenceRequest,
  type DiCreateBlankTemplateRequest,
  type DiTemplateEditorSession,
  type DiResourceEditorSession,
  type DiDocumentEditorLockStatus,
  type DiResourceEditorOpenOptions,
  type DiEditorSessionStats,
  type DiCreateTemplateCategoryRequest,
  type DiDocxStructure,
  type DiRenameTemplateCategoryRequest,
  type DiTemplateCategory,
  type DiTemplateDetail,
  type DiTemplateListResult,
  type DiTemplateParameter,
  type DiTemplateSummary,
  type DiUpdateTemplateParametersRequest,
  type DiUpdateTemplateMetadataRequest,
  type DiUpdateTemplateLetterheadRequest,
  type DiUpdateTemplateFooterRequest,
  type DiUpdateTemplatePageStructureRequest,
  type DiTemplateLetterhead,
  type DiTemplateFooter,
  type DiTemplatePageLayout,
  type DiLetterhead,
  type DiLetterheadListResult,
  type DiLetterheadSettings,
  type DiLetterheadHeaderFields,
  type DiLetterheadGeneralDocNo,
  type DiLetterheadDesignSession,
  type DiCreateLetterheadRequest,
  type DiUpdateLetterheadRequest,
  type DiCoverPage,
  type DiCoverPageDefinition,
  type DiCoverPageListResult,
  type DiCoverPageSettings,
  type DiCoverPageDesignSession,
  type DiCreateCoverPageRequest,
  type DiUpdateCoverPageRequest,
  type DiTag,
  type DiTagListResult,
  type DiCreateTagRequest,
  type DiUpdateTagRequest,
  type DiUpdateResourceMetadataRequest,
  type DiDocumentContextType,
  type DiDocumentProducerDetail,
  type DiDocumentDataSourceDetail,
  type DiDocumentDataSourceSummary,
  type DiGenerateDocumentRequest,
  type DiGenerationRuntimeEnvelope,
  type DiGenerateDocumentResult,
  type DiDocumentGenerationStatus,
  type DiDocumentGenerationPreview,
  type DiGenerateFromTemplateRequest,
  type DiPreviewFromTemplateRequest,
  type DiTemplateGenerationPreviewSession,
} from '@/types/apps/documentIntelligence';
import { diCreateDefaultFooter, diCreateDefaultPageLayout, diNormalizePageLayout } from '@/utils/diPageLayout';

const BASE = '/api/v1/resources';
const LINKS_BASE = '/api/v1';
const TEMPLATES_BASE = '/api/v1/templates';
const TEMPLATE_CATEGORIES_BASE = '/api/v1/template-categories';
const LETTERHEADS_BASE = '/api/v1/letterheads';
const COVER_PAGES_BASE = '/api/v1/cover-pages';
const TAGS_BASE = '/api/v1/tags';
const GENERATE_BASE = '/api/v1/generate';

function asRecord(raw: unknown): Record<string, unknown> {
  return raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
}

function str(obj: Record<string, unknown>, key: string): string | null {
  const v = obj[key];
  return v == null || v === '' ? null : String(v);
}

function strArray(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((v) => String(v)).filter((v) => v.length > 0);
}

function num(obj: Record<string, unknown>, key: string): number | null {
  const v = obj[key];
  if (v == null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function mapEffective(raw: unknown): DiEffectivePermission {
  const o = asRecord(raw);
  // Backend her zaman doldurur; alan yoksa güvenli varsayılan açık (eski yanıtlarla uyum).
  if (!raw || typeof raw !== 'object') return diFullPermission();
  return {
    canView: Boolean(o.canView),
    canCreate: Boolean(o.canCreate),
    canEdit: Boolean(o.canEdit),
    canDelete: Boolean(o.canDelete),
    canUpload: Boolean(o.canUpload),
    canDownload: Boolean(o.canDownload),
    canMove: Boolean(o.canMove),
    canShare: Boolean(o.canShare),
  };
}

function mapResource(raw: unknown): DiResource {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    type: str(o, 'type') ?? 'folder',
    parentId: str(o, 'parentId'),
    ancestorIds: strArray(o.ancestorIds),
    name: str(o, 'name') ?? '',
    title: str(o, 'title'),
    description: str(o, 'description'),
    tags: strArray(o.tags),
    classificationTagId: str(o, 'classificationTagId'),
    contentType: str(o, 'contentType'),
    mimeType: str(o, 'mimeType'),
    extension: str(o, 'extension'),
    size: num(o, 'size'),
    currentVersionNumber: num(o, 'currentVersionNumber') ?? 0,
    hasContent: Boolean(o.hasContent),
    status: str(o, 'status') ?? 'published',
    filePath: str(o, 'filePath'),
    fileName: str(o, 'fileName'),
    origin: str(o, 'origin'),
    letterheadId: str(o, 'letterheadId'),
    documentNo: str(o, 'documentNo'),
    templateId: str(o, 'templateId'),
    templateCode: str(o, 'templateCode'),
    generationProfile: str(o, 'generationProfile'),
    createdAt: str(o, 'createdAt'),
    createdBy: str(o, 'createdBy'),
    updatedAt: str(o, 'updatedAt'),
    updatedBy: str(o, 'updatedBy'),
    permissions: mapEffective(o.permissions),
  };
}

function mapGroupPermission(raw: unknown): DiGroupPermission {
  const o = asRecord(raw);
  return {
    groupId: str(o, 'groupId'),
    groupName: str(o, 'groupName') ?? '',
    permissions: strArray(o.permissions),
  };
}

function mapFolderPermissions(raw: unknown): DiFolderPermissions {
  const o = asRecord(raw);
  const groupsRaw = o.groups;
  return {
    resourceId: str(o, 'resourceId') ?? '',
    inheritanceBroken: Boolean(o.inheritanceBroken),
    effectiveAnchorId: str(o, 'effectiveAnchorId'),
    groups: Array.isArray(groupsRaw) ? groupsRaw.map(mapGroupPermission) : [],
    effective: mapEffective(o.effective),
  };
}

function mapTreeNode(raw: unknown): DiTreeNode {
  const o = asRecord(raw);
  const childrenRaw = o.children;
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    parentId: str(o, 'parentId'),
    hasChildren: Boolean(o.hasChildren),
    children: Array.isArray(childrenRaw) ? childrenRaw.map(mapTreeNode) : [],
  };
}

function mapTreePath(raw: unknown): DiTreePath {
  const o = asRecord(raw);
  const breadcrumbRaw = o.breadcrumb;
  const segmentsRaw = o.segments;
  return {
    breadcrumb: Array.isArray(breadcrumbRaw)
      ? breadcrumbRaw.map((r) => {
          const b = asRecord(r);
          return { id: str(b, 'id') ?? '', name: str(b, 'name') ?? '' };
        })
      : [],
    segments: Array.isArray(segmentsRaw)
      ? segmentsRaw.map((r) => {
          const s = asRecord(r);
          return {
            parentId: str(s, 'parentId'),
            nodes: Array.isArray(s.nodes) ? s.nodes.map(mapTreeNode) : [],
          };
        })
      : [],
  };
}

function mapListResult(raw: unknown): DiResourceListResult {
  const o = asRecord(raw);
  const items = Array.isArray(o.items) ? o.items.map(mapResource) : [];
  return { items, total: num(o, 'total') ?? items.length };
}

/** Varsayılan klasör içeriği sayfa boyutu (bootstrap/browse/children). */
export const DI_CHILDREN_PAGE_SIZE = 25;

export const DI_CHILDREN_PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;

type DiChildrenQueryOptions = {
  skip?: number;
  limit?: number | null;
};

function buildListingQuery(
  base: Record<string, string | undefined>,
  options?: DiChildrenQueryOptions,
): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(base)) {
    if (value) params.set(key, value);
  }
  const skip = options?.skip ?? 0;
  if (skip > 0) params.set('skip', String(skip));
  if (options?.limit != null && options.limit > 0) params.set('limit', String(options.limit));
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}

/** Klasör ağacı (yalnızca klasörler, iç içe — tam ağaç, geriye dönük). */
export async function diGetTree(): Promise<DiTreeNode[]> {
  const raw = await fetchFromDocuments(`${BASE}/tree`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

/** Lazy tree kök seviyesi. */
export async function diGetTreeRoots(): Promise<DiTreeNode[]> {
  const raw = await fetchFromDocuments(`${BASE}/tree/roots`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

/** Lazy tree: bir klasörün alt klasörleri. parentId null ise kök. */
export async function diGetTreeChildren(parentId: string | null): Promise<DiTreeNode[]> {
  const qs = parentId ? `?parentId=${encodeURIComponent(parentId)}` : '';
  const raw = await fetchFromDocuments(`${BASE}/tree/children${qs}`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

/** Derin link: breadcrumb + yol boyunca kardeş klasör segmentleri. */
export async function diGetTreePath(folderId: string): Promise<DiTreePath> {
  const raw = await fetchFromDocuments(`${BASE}/tree/path?folderId=${encodeURIComponent(folderId)}`, 'GET');
  return mapTreePath(raw);
}

/** Taşı/klon picker: görülebilir klasör adı araması. */
export async function diSearchTreeFolders(q: string, limit = 50): Promise<DiTreeNode[]> {
  const trimmed = q.trim();
  if (trimmed.length < 2) return [];
  const params = new URLSearchParams({ q: trimmed, limit: String(limit) });
  const raw = await fetchFromDocuments(`${BASE}/tree/search?${params.toString()}`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

function mapBootstrap(raw: unknown): DiResourceBootstrap {
  const o = asRecord(raw);
  const childrenRaw = o.children;
  const breadcrumbRaw = o.breadcrumb;
  const selectedRaw = o.selectedFolder;
  const treeRootsRaw = o.treeRoots;
  const treeRaw = o.tree;
  const treeRoots = Array.isArray(treeRootsRaw)
    ? treeRootsRaw.map(mapTreeNode)
    : Array.isArray(treeRaw)
      ? treeRaw.map(mapTreeNode)
      : [];
  return {
    treeRoots,
    tree: Array.isArray(treeRaw) ? treeRaw.map(mapTreeNode) : treeRoots,
    children: mapListResult(childrenRaw),
    breadcrumb: Array.isArray(breadcrumbRaw)
      ? breadcrumbRaw.map((r) => {
          const b = asRecord(r);
          return { id: str(b, 'id') ?? '', name: str(b, 'name') ?? '' };
        })
      : [],
    selectedFolder: selectedRaw ? mapResource(selectedRaw) : null,
  };
}

function mapBrowseContext(raw: unknown): DiResourceBrowseContext {
  const o = asRecord(raw);
  const selectedRaw = o.selectedFolder;
  const breadcrumbRaw = o.breadcrumb;
  return {
    children: mapListResult(o.children),
    breadcrumb: Array.isArray(breadcrumbRaw)
      ? breadcrumbRaw.map((r) => {
          const b = asRecord(r);
          return { id: str(b, 'id') ?? '', name: str(b, 'name') ?? '' };
        })
      : [],
    selectedFolder: selectedRaw ? mapResource(selectedRaw) : null,
  };
}

/** Ana ekran ilk yükleme / tam yenileme (ağaç + içerik, tek snapshot). */
export async function diGetBootstrap(
  folderId?: string | null,
  options?: DiChildrenQueryOptions,
): Promise<DiResourceBootstrap> {
  const qs = buildListingQuery(
    { folderId: folderId ?? undefined },
    { skip: options?.skip ?? 0, limit: options?.limit ?? DI_CHILDREN_PAGE_SIZE },
  );
  const raw = await fetchFromDocuments(`${BASE}/bootstrap${qs}`, 'GET');
  return mapBootstrap(raw);
}

/** Klasör gezinme (içerik + breadcrumb + seçili klasör, tek snapshot). */
export async function diGetBrowseContext(
  folderId?: string | null,
  options?: DiChildrenQueryOptions,
): Promise<DiResourceBrowseContext> {
  const qs = buildListingQuery(
    { folderId: folderId ?? undefined },
    { skip: options?.skip ?? 0, limit: options?.limit ?? DI_CHILDREN_PAGE_SIZE },
  );
  const raw = await fetchFromDocuments(`${BASE}/browse${qs}`, 'GET');
  return mapBrowseContext(raw);
}

/** Bir klasörün içeriği (klasör + markdown + dosya). parentId boşsa kök. limit verilmezse tümü. */
export async function diGetChildren(
  parentId?: string | null,
  options?: DiChildrenQueryOptions,
): Promise<DiResourceListResult> {
  const qs = buildListingQuery({ parentId: parentId ?? undefined }, options);
  const raw = await fetchFromDocuments(`${BASE}/children${qs}`, 'GET');
  return mapListResult(raw);
}

/** Full-text arama (DG regex: ad/başlık/açıklama + markdown içeriği). */
export async function diSearch(q: string, skip = 0, limit = 50): Promise<DiResourceListResult> {
  const params = new URLSearchParams({ q, skip: String(skip), limit: String(limit) });
  const raw = await fetchFromDocuments(`${BASE}/search?${params.toString()}`, 'GET');
  return mapListResult(raw);
}

/** Son güncellenen yayınlanmış markdown kayıtları. */
export async function diGetRecent(limit = 10): Promise<DiResourceListResult> {
  const params = new URLSearchParams({ limit: String(limit) });
  const raw = await fetchFromDocuments(`${BASE}/recent?${params.toString()}`, 'GET');
  return mapListResult(raw);
}

/** Kullanıcının düzenleyebildiği taslak markdown kayıtları. */
export async function diGetDrafts(limit = 50): Promise<DiResourceListResult> {
  const params = new URLSearchParams({ limit: String(limit) });
  const raw = await fetchFromDocuments(`${BASE}/drafts?${params.toString()}`, 'GET');
  return mapListResult(raw);
}

/** Tek kaynak metadata'sı. */
export async function diGetById(id: string): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}`, 'GET');
  return mapResource(raw);
}

/** Kök -> ... -> kaynak yol bilgisi (breadcrumb). */
export async function diGetBreadcrumb(id: string): Promise<DiBreadcrumb[]> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/breadcrumb`, 'GET');
  if (!Array.isArray(raw)) return [];
  return raw.map((r) => {
    const o = asRecord(r);
    return { id: str(o, 'id') ?? '', name: str(o, 'name') ?? '' };
  });
}

export async function diCreateFolder(request: DiCreateFolderRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/folder`, 'POST', request);
  return mapResource(raw);
}

export async function diRename(id: string, request: DiRenameRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/rename`, 'PUT', request);
  return mapResource(raw);
}

export async function diUpdateResourceMetadata(
  id: string,
  request: DiUpdateResourceMetadataRequest
): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/metadata`, 'PATCH', request);
  return mapResource(raw);
}

export async function diMove(id: string, request: DiMoveRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/move`, 'PUT', request);
  return mapResource(raw);
}

/** Markdown sayfa veya manual DOCX klonlar. */
export async function diCloneResource(id: string, request: DiCloneResourceRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/clone`, 'POST', request);
  return mapResource(raw);
}

/** Kaynağı siler. force=true: dolu klasörleri de siler (cascade). */
export async function diDelete(id: string, force = false): Promise<void> {
  const qs = force ? '?force=true' : '';
  await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}${qs}`, 'DELETE');
}

export async function diCreateMarkdown(request: DiCreateMarkdownRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown`, 'POST', request);
  return mapResource(raw);
}

export async function diUpdateMarkdown(id: string, request: DiUpdateMarkdownRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}`, 'PUT', request);
  return mapResource(raw);
}

export async function diGetMarkdownContent(id: string): Promise<DiMarkdownContent> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/content`, 'GET');
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    title: str(o, 'title'),
    content: str(o, 'content') ?? '',
    currentVersionNumber: num(o, 'currentVersionNumber') ?? 0,
  };
}

/** Markdown sürüm geçmişi (içerik hariç, en yeni önce). */
export async function diGetMarkdownVersions(id: string): Promise<DiMarkdownVersion[]> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions`, 'GET');
  if (!Array.isArray(raw)) return [];
  return raw.map((r) => {
    const o = asRecord(r);
    return {
      versionNumber: num(o, 'versionNumber') ?? 0,
      changeNote: str(o, 'changeNote'),
      size: num(o, 'size'),
      createdAt: str(o, 'createdAt'),
      createdBy: str(o, 'createdBy'),
      isCurrent: Boolean(o.isCurrent),
    };
  });
}

/** Tek bir markdown sürümünün içeriği. */
export async function diGetMarkdownVersionContent(id: string, versionNumber: number): Promise<DiMarkdownVersionContent> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions/${versionNumber}`, 'GET');
  const o = asRecord(raw);
  return {
    versionNumber: num(o, 'versionNumber') ?? versionNumber,
    content: str(o, 'content') ?? '',
    changeNote: str(o, 'changeNote'),
    createdAt: str(o, 'createdAt'),
    createdBy: str(o, 'createdBy'),
  };
}

/** Eski bir sürümü yeni sürüm olarak geri yükler. */
export async function diRestoreMarkdownVersion(id: string, versionNumber: number): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/versions/${versionNumber}/restore`, 'POST');
  return mapResource(raw);
}

/** Yönetilen DOCX sürüm geçmişi (içerik hariç, en yeni önce). */
export async function diGetFileVersions(id: string): Promise<DiMarkdownVersion[]> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/versions`, 'GET');
  if (!Array.isArray(raw)) return [];
  return raw.map((r) => {
    const o = asRecord(r);
    return {
      versionNumber: num(o, 'versionNumber') ?? 0,
      changeNote: str(o, 'changeNote'),
      size: num(o, 'size'),
      createdAt: str(o, 'createdAt'),
      createdBy: str(o, 'createdBy'),
      isCurrent: Boolean(o.isCurrent),
    };
  });
}

/** Belirli bir DOCX sürümünü blob olarak indirir. */
export async function diDownloadFileVersion(
  resourceId: string,
  versionNumber: number,
  suggestedFileName?: string | null,
): Promise<{ blob: Blob; fileName: string }> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
  }
  const path = `${BASE}/${encodeURIComponent(resourceId)}/versions/${versionNumber}/download`;
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  const serverPath = cleanPath.replace(/^\/api\/v1\//, 'v1/');
  const fullUrl = `/api/documents/${serverPath}`;
  const res = await fetch(fullUrl, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` },
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  const blob = await res.blob();
  const headerName = parseContentDispositionFileName(res.headers.get('content-disposition'));
  const fileName = headerName || suggestedFileName || `document-v${versionNumber}.docx`;
  return { blob, fileName };
}

/** Eski bir DOCX sürümünü yeni sürüm olarak geri yükler. */
export async function diRestoreFileVersion(id: string, versionNumber: number): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/versions/${versionNumber}/restore`, 'POST');
  return mapResource(raw);
}

/** Belirli bir DOCX sürümünün değişiklik notunu günceller. */
export async function diUpdateFileVersionChangeNote(
  id: string,
  versionNumber: number,
  changeNote: string,
): Promise<DiMarkdownVersion> {
  const raw = await fetchFromDocuments(
    `${BASE}/${encodeURIComponent(id)}/versions/${versionNumber}`,
    'PATCH',
    { changeNote: changeNote || null },
  );
  const o = asRecord(raw);
  return {
    versionNumber: num(o, 'versionNumber') ?? versionNumber,
    changeNote: str(o, 'changeNote'),
    size: num(o, 'size'),
    createdAt: str(o, 'createdAt'),
    createdBy: str(o, 'createdBy'),
    isCurrent: Boolean(o.isCurrent),
  };
}

/** Belirli bir DOCX sürümünü salt okunur Collabora oturumunda açar. */
export async function diGetFileVersionPreviewSession(
  resourceId: string,
  versionNumber: number,
): Promise<DiResourceEditorSession> {
  const raw = await fetchFromDocuments(
    `${BASE}/${encodeURIComponent(resourceId)}/versions/${versionNumber}/preview-session`,
    'GET',
  );
  return mapResourceEditorSession(raw, resourceId);
}

function parseContentDispositionFileName(header: string | null): string | null {
  if (!header) return null;
  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8Match?.[1]) {
    try {
      return decodeURIComponent(utf8Match[1].trim());
    } catch {
      return utf8Match[1].trim();
    }
  }
  const plainMatch = /filename="?([^";]+)"?/i.exec(header);
  return plainMatch?.[1]?.trim() || null;
}

/** Bu sayfaya markdown iç linki veren diğer sayfalar (backlink). */
export async function diGetMarkdownBacklinks(id: string): Promise<DiResourceListResult> {
  const raw = await fetchFromDocuments(`${BASE}/markdown/${encodeURIComponent(id)}/backlinks`, 'GET');
  const o = asRecord(raw);
  const itemsRaw = o.items ?? o.Items;
  const items = Array.isArray(itemsRaw) ? itemsRaw.map(mapResource) : [];
  const total = num(o, 'total') ?? num(o, 'Total') ?? items.length;
  return { items, total };
}

export async function diCreateFileResource(request: DiCreateFileResourceRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/file`, 'POST', request);
  return mapResource(raw);
}

export async function diCreateNativeDocument(request: DiCreateNativeDocumentRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/documents`, 'POST', request);
  return mapResource(raw);
}

export async function diCreateNativeSheet(request: DiCreateNativeOfficeRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/sheets/native`, 'POST', request);
  return mapResource(raw);
}

export async function diCreateNativePresentation(request: DiCreateNativeOfficeRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/presentations/native`, 'POST', request);
  return mapResource(raw);
}

/** Yüklenen DOCX dosyasını sunucuda PDF'e dönüştürüp blob olarak döndürür. */
export async function diFetchResourcePreviewPdf(resourceId: string): Promise<Blob> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
  }
  const path = `${BASE}/${encodeURIComponent(resourceId)}/preview/pdf`;
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  const serverPath = cleanPath.replace(/^\/api\/v1\//, 'v1/');
  const fullUrl = `/api/documents/${serverPath}`;
  const res = await fetch(fullUrl, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` },
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  return await res.blob();
}

/** Güncel dosyayı MngDocument üzerinden indirir (sınıflandırma damgası uygulanır). */
export async function diDownloadResource(
  resourceId: string,
  suggestedFileName?: string | null,
): Promise<{ blob: Blob; fileName: string }> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
  }
  const path = `${BASE}/${encodeURIComponent(resourceId)}/download`;
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  const serverPath = cleanPath.replace(/^\/api\/v1\//, 'v1/');
  const fullUrl = `/api/documents/${serverPath}`;
  const res = await fetch(fullUrl, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` },
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  const blob = await res.blob();
  const headerName = parseContentDispositionFileName(res.headers.get('content-disposition'));
  const fileName = headerName || suggestedFileName || 'file';
  return { blob, fileName };
}

/** Yüklenen dosyayı DG üzerinden blob olarak indirir (binary MngDocument'ten geçmez). */
export async function diFetchFileBlob(filePath: string): Promise<Blob> {
  return fetchBlobFromDataGateway(`/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`);
}

// --- Faz 2: work item ↔ doküman bağlantıları ---

function mapResourceLink(raw: unknown): DiResourceLink {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    resourceId: str(o, 'resourceId') ?? '',
    targetModule: str(o, 'targetModule') ?? '',
    targetType: str(o, 'targetType') ?? '',
    targetId: str(o, 'targetId') ?? '',
    relationType: str(o, 'relationType') ?? 'reference',
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
  };
}

function mapLinkedWorkItem(raw: unknown): DiLinkedWorkItem {
  const o = asRecord(raw);
  return {
    linkId: str(o, 'linkId') ?? '',
    workItemId: str(o, 'workItemId') ?? '',
    workItemKey: str(o, 'workItemKey'),
    workItemTitle: str(o, 'workItemTitle'),
    boardId: str(o, 'boardId'),
    workspaceId: str(o, 'workspaceId'),
    relationType: str(o, 'relationType') ?? 'reference',
  };
}

function mapLinkedResource(raw: unknown): DiLinkedResource {
  const o = asRecord(raw);
  return {
    linkId: str(o, 'linkId') ?? '',
    resourceId: str(o, 'resourceId') ?? '',
    relationType: str(o, 'relationType') ?? 'reference',
    resourceType: str(o, 'resourceType'),
    name: str(o, 'name'),
    title: str(o, 'title'),
    mimeType: str(o, 'mimeType'),
    extension: str(o, 'extension'),
    permissions: mapEffective(o.permissions),
  };
}

function mapLinkListResult<T>(raw: unknown, mapItem: (item: unknown) => T): DiResourceLinkListResult<T> {
  const o = asRecord(raw);
  const itemsRaw = Array.isArray(o.items) ? o.items : [];
  return {
    items: itemsRaw.map(mapItem),
    total: num(o, 'total') ?? itemsRaw.length,
  };
}

export async function diCreateResourceLink(request: DiCreateResourceLinkRequest): Promise<DiResourceLink> {
  const raw = await fetchFromDocuments(`${LINKS_BASE}/resource-links`, 'POST', request);
  return mapResourceLink(raw);
}

export async function diDeleteResourceLink(linkId: string): Promise<void> {
  await fetchFromDocuments(`${LINKS_BASE}/resource-links/${encodeURIComponent(linkId)}`, 'DELETE');
}

export async function diGetLinkedWorkItems(resourceId: string): Promise<DiResourceLinkListResult<DiLinkedWorkItem>> {
  const raw = await fetchFromDocuments(
    `${LINKS_BASE}/resources/${encodeURIComponent(resourceId)}/linked-work-items`,
    'GET'
  );
  return mapLinkListResult(raw, mapLinkedWorkItem);
}

export async function diGetLinkedResourcesForWorkItem(
  workItemId: string
): Promise<DiResourceLinkListResult<DiLinkedResource>> {
  const raw = await fetchFromDocuments(
    `${LINKS_BASE}/work-items/${encodeURIComponent(workItemId)}/linked-resources`,
    'GET'
  );
  return mapLinkListResult(raw, mapLinkedResource);
}

// --- Grup bazlı klasör yetkilendirmesi + miras ---

/** Klasörün yetki yönetim görünümü (miras durumu + grup matrisi + etkin yetki). */
export async function diGetPermissions(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions`, 'GET');
  return mapFolderPermissions(raw);
}

/** Anchor (mirası kırık) klasörde grup yetki matrisini değiştirir (tam değişim). */
export async function diSetPermissions(folderId: string, request: DiSetFolderPermissionsRequest): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions`, 'PUT', request);
  return mapFolderPermissions(raw);
}

/** Klasörün yetki mirasını kırar (üst anchor'ın ACL'ini kopyalar). */
export async function diBreakInheritance(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions/break-inheritance`, 'POST');
  return mapFolderPermissions(raw);
}

/** Klasörün kendi ACL'ini silip yetki mirasını geri yükler. */
export async function diRestoreInheritance(folderId: string): Promise<DiFolderPermissions> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(folderId)}/permissions/restore-inheritance`, 'POST');
  return mapFolderPermissions(raw);
}

// --- Document Designer (templates) ---

function mapTemplateCategory(raw: unknown): DiTemplateCategory {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    parentId: str(o, 'parentId'),
    ancestorIds: strArray(o.ancestorIds),
    name: str(o, 'name') ?? '',
    description: str(o, 'description'),
    sortOrder: num(o, 'sortOrder') ?? 0,
    status: str(o, 'status') ?? 'active',
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

function mapCategoryTreeNode(raw: unknown): DiTreeNode {
  const o = asRecord(raw);
  const childrenRaw = o.children;
  const children = Array.isArray(childrenRaw) ? childrenRaw.map(mapCategoryTreeNode) : [];
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    parentId: str(o, 'parentId'),
    hasChildren: children.length > 0,
    children,
  };
}

function inferTemplateOutputFormat(
  sourceFileName?: string | null,
  sourceStoragePath?: string | null,
): string {
  const pathOrName = (sourceFileName || sourceStoragePath || '').trim();
  if (!pathOrName) return 'docx';
  const ext = pathOrName.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'xlsx' || ext === 'xlsm') return 'xlsx';
  if (ext === 'pptx') return 'pptx';
  return 'docx';
}

function mapTemplateSummary(raw: unknown): DiTemplateSummary {
  const o = asRecord(raw);
  const sourceFileName = str(o, 'sourceFileName');
  const sourceStoragePath = str(o, 'sourceStoragePath');
  return {
    id: str(o, 'id') ?? '',
    categoryId: str(o, 'categoryId'),
    name: str(o, 'name') ?? '',
    code: str(o, 'code'),
    description: str(o, 'description'),
    sourceResourceId: str(o, 'sourceResourceId'),
    sourceStoragePath,
    sourceFileName,
    outputFormat: str(o, 'outputFormat') ?? inferTemplateOutputFormat(sourceFileName, sourceStoragePath),
    creationMode: str(o, 'creationMode') ?? 'fromTemplate',
    status: str(o, 'status') ?? 'draft',
    parameterCount: num(o, 'parameterCount') ?? 0,
    primaryContextType: str(o, 'primaryContextType'),
    generationProfile: str(o, 'generationProfile'),
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

function mapDocBinding(raw: unknown): DiTemplateDocBinding | null {
  if (!raw || typeof raw !== 'object') return null;
  const b = asRecord(raw);
  return {
    regionKind: str(b, 'regionKind') ?? 'paragraph',
    paragraphIndex: num(b, 'paragraphIndex') ?? 0,
    originalText: str(b, 'originalText'),
    charStart: num(b, 'charStart'),
    charEnd: num(b, 'charEnd'),
    tableIndex: num(b, 'tableIndex'),
    headerRowIndex: num(b, 'headerRowIndex'),
    templateRowIndex: num(b, 'templateRowIndex'),
  };
}

function mapTemplateParameter(raw: unknown): DiTemplateParameter {
  const o = asRecord(raw);
  const inc = o.incremental;
  const bind = o.docBinding ?? o.sourceBinding;
  const ctx = o.contextBinding;
  const vs = o.valueSource;
  const binding = mapDocBinding(bind);
  return {
    key: str(o, 'key') ?? '',
    label: str(o, 'label') ?? '',
    kind: str(o, 'kind') ?? 'scalar',
    dataType: str(o, 'dataType') ?? 'text',
    valueSourceMode: str(o, 'valueSourceMode') ?? 'manual',
    dataSourceRef: str(o, 'dataSourceRef'),
    defaultValue: str(o, 'defaultValue'),
    format: str(o, 'format'),
    incremental:
      inc && typeof inc === 'object'
        ? {
            format: str(asRecord(inc), 'format') ?? '',
            startValue: num(asRecord(inc), 'startValue') ?? 1,
            incrementStep: num(asRecord(inc), 'incrementStep') ?? 1,
            scopeKey: str(asRecord(inc), 'scopeKey'),
            resetPolicy: str(asRecord(inc), 'resetPolicy') ?? 'none',
          }
        : null,
    docBinding: binding,
    sourceBinding: binding,
    contextBinding:
      ctx && typeof ctx === 'object'
        ? {
            path: str(asRecord(ctx), 'path') ?? '',
            fallbackPath: str(asRecord(ctx), 'fallbackPath'),
            defaultValue: str(asRecord(ctx), 'defaultValue'),
            format: str(asRecord(ctx), 'format'),
          }
        : null,
    valueSource:
      vs && typeof vs === 'object'
        ? {
            mode: str(asRecord(vs), 'mode') ?? undefined,
            provider: str(asRecord(vs), 'provider') ?? undefined,
            dataset: str(asRecord(vs), 'dataset'),
            queryName: str(asRecord(vs), 'queryName'),
            idFrom: str(asRecord(vs), 'idFrom'),
            query: str(asRecord(vs), 'query'),
            path: str(asRecord(vs), 'path'),
            fallbackPath: str(asRecord(vs), 'fallbackPath'),
            field: str(asRecord(vs), 'field'),
            format: str(asRecord(vs), 'format'),
            defaultValue: str(asRecord(vs), 'defaultValue'),
            match:
              asRecord(vs).match && typeof asRecord(vs).match === 'object'
                ? (asRecord(vs).match as Record<string, unknown>)
                : null,
            parameters:
              asRecord(vs).parameters && typeof asRecord(vs).parameters === 'object'
                ? (asRecord(vs).parameters as Record<string, unknown>)
                : null,
            columns: Array.isArray(asRecord(vs).columns)
              ? (asRecord(vs).columns as unknown[]).map((col) => {
                  const c = asRecord(col);
                  return {
                    sourceField: str(c, 'sourceField') ?? '',
                    header: str(c, 'header'),
                    format: str(c, 'format'),
                  };
                })
              : null,
          }
        : null,
  };
}

function mapTemplateLetterhead(raw: unknown): DiTemplateLetterhead | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = asRecord(raw);
  return {
    enabled: Boolean(o.enabled),
    showLogo: o.showLogo !== false,
    showDocumentName: o.showDocumentName !== false,
    showDocumentNumber: o.showDocumentNumber !== false,
    showGeneratedAt: o.showGeneratedAt !== false,
  };
}

function mapTemplateFooter(raw: unknown): DiTemplateFooter | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = asRecord(raw);
  return {
    enabled: Boolean(o.enabled),
    showFormRevision: o.showFormRevision !== false,
    showOfficeColumns: o.showOfficeColumns !== false,
    showAddresses: o.showAddresses !== false,
    showContacts: o.showContacts !== false,
    showDividerLine: o.showDividerLine !== false,
  };
}

function mapTemplatePageLayout(raw: unknown): DiTemplatePageLayout | null {
  if (!raw || typeof raw !== 'object') return null;
  const o = asRecord(raw);
  return diNormalizePageLayout({
    marginTopTwips: num(o, 'marginTopTwips') ?? undefined,
    marginRightTwips: num(o, 'marginRightTwips') ?? undefined,
    marginBottomTwips: num(o, 'marginBottomTwips') ?? undefined,
    marginLeftTwips: num(o, 'marginLeftTwips') ?? undefined,
    headerDistanceTwips: num(o, 'headerDistanceTwips') ?? undefined,
    footerDistanceTwips: num(o, 'footerDistanceTwips') ?? undefined,
    footerLeftIndentTwips: num(o, 'footerLeftIndentTwips') ?? undefined,
  });
}

function mapTemplateDetail(raw: unknown): DiTemplateDetail {
  const summary = mapTemplateSummary(raw);
  const o = asRecord(raw);
  const paramsRaw = o.parameters;
  return {
    ...summary,
    schemaVersion: str(o, 'schemaVersion') ?? '1.0',
    primaryContextType: summary.primaryContextType ?? str(o, 'primaryContextType'),
    generationProfile: summary.generationProfile ?? str(o, 'generationProfile'),
    defaultLetterheadId: str(o, 'defaultLetterheadId'),
    defaultCoverPageId: str(o, 'defaultCoverPageId'),
    letterhead: mapTemplateLetterhead(o.letterhead),
    footer: mapTemplateFooter(o.footer),
    pageLayout: mapTemplatePageLayout(o.pageLayout),
    parameters: Array.isArray(paramsRaw) ? paramsRaw.map(mapTemplateParameter) : [],
  };
}

function mapLetterheadHeaderFields(raw: unknown): DiLetterheadHeaderFields {
  const o = asRecord(raw);
  return {
    documentName: o.documentName !== false,
    docNo: o.docNo !== false,
    generatedAt: o.generatedAt !== false,
    createPerson: Boolean(o.createPerson),
  };
}

function mapLetterheadGeneralDocNo(raw: unknown): DiLetterheadGeneralDocNo {
  const o = asRecord(raw);
  const scopeModeRaw = str(o, 'scopeMode') ?? 'letterhead';
  const scopeMode =
    scopeModeRaw === 'global' || scopeModeRaw === 'custom' ? scopeModeRaw : 'letterhead';
  return {
    enabled: o.enabled !== false,
    format: str(o, 'format') ?? '{yyyy}-{0:D4}',
    scopeMode,
    scopeKey: str(o, 'scopeKey'),
    resetPolicy: str(o, 'resetPolicy') ?? 'yearly',
    startValue: num(o, 'startValue') ?? 1,
    incrementStep: num(o, 'incrementStep') ?? 1,
  };
}

function mapLetterheadFooterSettings(raw: unknown): DiLetterheadFooterSettings {
  const o = asRecord(raw);
  if ('tableRows' in o || 'tableColumns' in o) {
    return {
      enabled: o.enabled === true,
      tableRows: Math.max(1, Math.min(12, num(o, 'tableRows') ?? 1)),
      tableColumns: Math.max(1, Math.min(6, num(o, 'tableColumns') ?? 1)),
    };
  }
  // Legacy Odak boolean footer → default 2×2 table when enabled
  const legacyEnabled = o.enabled !== false;
  return {
    enabled: legacyEnabled,
    tableRows: legacyEnabled ? 2 : 1,
    tableColumns: legacyEnabled ? 2 : 1,
  };
}

function mapLetterheadSettings(raw: unknown): DiLetterheadSettings {
  const o = asRecord(raw);
  return {
    headerFields: mapLetterheadHeaderFields(o.headerFields),
    generalDocNo: mapLetterheadGeneralDocNo(o.generalDocNo),
    footer: mapLetterheadFooterSettings(o.footer),
    pageLayout: mapTemplatePageLayout(o.pageLayout) ?? diCreateDefaultPageLayout(),
  };
}

export function diCreateDefaultLetterheadFooterSettings(): DiLetterheadFooterSettings {
  return {
    enabled: false,
    tableRows: 1,
    tableColumns: 1,
  };
}

export function diCreateDefaultLetterheadSettings(): DiLetterheadSettings {
  return {
    headerFields: {
      documentName: true,
      docNo: true,
      generatedAt: true,
      createPerson: false,
    },
    generalDocNo: {
      enabled: true,
      format: '{yyyy}-{0:D4}',
      scopeMode: 'letterhead',
      scopeKey: null,
      resetPolicy: 'yearly',
      startValue: 1,
      incrementStep: 1,
    },
    footer: diCreateDefaultLetterheadFooterSettings(),
    pageLayout: diCreateDefaultPageLayout(),
  };
}

function mapLetterhead(raw: unknown): DiLetterhead {
  const o = asRecord(raw);
  const letterheadRaw = o.letterhead;
  const letterhead = mapTemplateLetterhead(letterheadRaw) ?? {
    enabled: true,
    showLogo: true,
    showDocumentName: true,
    showDocumentNumber: true,
    showGeneratedAt: true,
  };
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    code: str(o, 'code') ?? '',
    description: str(o, 'description'),
    isDefault: Boolean(o.isDefault),
    isActive: o.isActive !== false,
    letterhead,
    settings: mapLetterheadSettings(o.settings ?? {}),
    designStoragePath: str(o, 'designStoragePath'),
    designFileName: str(o, 'designFileName'),
    hasDesign: Boolean(o.hasDesign) || Boolean(str(o, 'designStoragePath')),
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

export async function diGetTemplateCategoryTree(): Promise<DiTreeNode[]> {
  const raw = await fetchFromDocuments(`${TEMPLATE_CATEGORIES_BASE}/tree`, 'GET');
  return Array.isArray(raw) ? raw.map(mapCategoryTreeNode) : [];
}

export async function diCreateTemplateCategory(
  request: DiCreateTemplateCategoryRequest
): Promise<DiTemplateCategory> {
  const raw = await fetchFromDocuments(TEMPLATE_CATEGORIES_BASE, 'POST', request);
  return mapTemplateCategory(raw);
}

export async function diRenameTemplateCategory(
  id: string,
  request: DiRenameTemplateCategoryRequest
): Promise<DiTemplateCategory> {
  const raw = await fetchFromDocuments(
    `${TEMPLATE_CATEGORIES_BASE}/${encodeURIComponent(id)}/rename`,
    'PUT',
    request
  );
  return mapTemplateCategory(raw);
}

export async function diDeleteTemplateCategory(id: string): Promise<void> {
  await fetchFromDocuments(`${TEMPLATE_CATEGORIES_BASE}/${encodeURIComponent(id)}`, 'DELETE');
}

export async function diListTemplates(categoryId?: string | null): Promise<DiTemplateListResult> {
  const query = categoryId ? `?categoryId=${encodeURIComponent(categoryId)}` : '';
  const raw = await fetchFromDocuments(`${TEMPLATES_BASE}${query}`, 'GET');
  const o = asRecord(raw);
  const itemsRaw = Array.isArray(o.items) ? o.items : [];
  return {
    items: itemsRaw.map(mapTemplateSummary),
    total: num(o, 'total') ?? itemsRaw.length,
  };
}

export async function diGetTemplate(id: string): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(`${TEMPLATES_BASE}/${encodeURIComponent(id)}`, 'GET');
  return mapTemplateDetail(raw);
}

export async function diDeleteTemplate(id: string): Promise<void> {
  await fetchFromDocuments(`${TEMPLATES_BASE}/${encodeURIComponent(id)}`, 'DELETE');
}

export async function diCreateTemplateFromSource(
  request: DiCreateTemplateFromSourceRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(`${TEMPLATES_BASE}/from-source`, 'POST', request);
  return mapTemplateDetail(raw);
}

export async function diCreateTemplateFromReference(
  request: DiCreateTemplateFromReferenceRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(`${TEMPLATES_BASE}/from-reference`, 'POST', request);
  return mapTemplateDetail(raw);
}

export async function diCreateBlankTemplate(
  request: DiCreateBlankTemplateRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(`${TEMPLATES_BASE}/blank`, 'POST', request);
  return mapTemplateDetail(raw);
}

export async function diDuplicateTemplate(
  templateId: string,
  request: DiDuplicateTemplateRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/duplicate`,
    'POST',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diGetTemplateEditorSession(templateId: string): Promise<DiTemplateEditorSession> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/editor-session`,
    'GET'
  );
  const o = asRecord(raw);
  return {
    templateId: str(o, 'templateId') ?? templateId,
    editorUrl: str(o, 'editorUrl') ?? '',
    accessToken: str(o, 'accessToken') ?? '',
    wopiSrc: str(o, 'wopiSrc') ?? '',
    readOnly: Boolean(o.readOnly),
  };
}

const RESOURCE_EDITOR_TEMPLATE_KEY = 'di-resource-editor-template';

function mapResourceEditorSession(
  raw: unknown,
  resourceId: string
): DiResourceEditorSession {
  const o = asRecord(raw);
  return {
    resourceId: str(o, 'resourceId') ?? resourceId,
    editorUrl: str(o, 'editorUrl') ?? '',
    accessToken: str(o, 'accessToken') ?? '',
    wopiSrc: str(o, 'wopiSrc') ?? '',
    readOnly: Boolean(o.readOnly),
    lockedByOthers: Boolean(o.lockedByOthers),
    lockEnforced: Boolean(o.lockEnforced),
  };
}

function mapDocumentEditorLockStatus(raw: unknown): DiDocumentEditorLockStatus {
  const o = asRecord(raw);
  const editorsRaw = o.activeEditors ?? o.ActiveEditors;
  const activeEditors = Array.isArray(editorsRaw)
    ? editorsRaw.map((item) => {
        const e = asRecord(item);
        return {
          userId: str(e, 'userId') ?? '',
          userName: str(e, 'userName') ?? '',
          lastSeenAt: str(e, 'lastSeenAt') ?? '',
          isCurrentUser: Boolean(e.isCurrentUser),
        };
      })
    : [];

  return {
    resourceId: str(o, 'resourceId'),
    templateId: str(o, 'templateId'),
    letterheadId: str(o, 'letterheadId'),
    isLocked: Boolean(o.isLocked ?? o.isLockedByOthers),
    isLockedByOthers: Boolean(o.isLockedByOthers),
    isLockedBySelf: Boolean(o.isLockedBySelf),
    warnOnActiveEditor: o.warnOnActiveEditor !== false,
    enforceExclusiveLock: o.enforceExclusiveLock !== false,
    canBypassLock: Boolean(o.canBypassLock),
    activeEditors,
  };
}

/** Döküman editör kilidi — başka kullanıcı düzenliyor mu? */
export async function diGetResourceEditorLockStatus(resourceId: string): Promise<DiDocumentEditorLockStatus> {
  const raw = await fetchFromDocuments(
    `${BASE}/${encodeURIComponent(resourceId)}/editor-lock-status`,
    'GET',
  );
  return mapDocumentEditorLockStatus(raw);
}

async function diGetResourceEditorSessionViaTemplate(
  resourceId: string,
  resourceName?: string
): Promise<DiResourceEditorSession> {
  const cacheKey = `${RESOURCE_EDITOR_TEMPLATE_KEY}:${resourceId}`;

  if (import.meta.client) {
    const cachedTemplateId = sessionStorage.getItem(cacheKey);
    if (cachedTemplateId) {
      try {
        const session = await diGetTemplateEditorSession(cachedTemplateId);
        return {
          resourceId,
          editorUrl: session.editorUrl,
          accessToken: session.accessToken,
          wopiSrc: session.wopiSrc,
          readOnly: session.readOnly,
          viaTemplateFallback: true,
        };
      } catch {
        sessionStorage.removeItem(cacheKey);
      }
    }
  }

  const tpl = await diCreateTemplateFromSource({
    sourceResourceId: resourceId,
    name: resourceName?.trim() || undefined,
  });

  if (import.meta.client) {
    sessionStorage.setItem(cacheKey, tpl.id);
  }

  const session = await diGetTemplateEditorSession(tpl.id);
  return {
    resourceId,
    editorUrl: session.editorUrl,
    accessToken: session.accessToken,
    wopiSrc: session.wopiSrc,
    readOnly: session.readOnly,
    viaTemplateFallback: true,
  };
}

export async function diGetResourceEditorSession(
  resourceId: string,
  resourceName?: string,
  options?: DiResourceEditorOpenOptions,
): Promise<DiResourceEditorSession> {
  const params = new URLSearchParams();
  if (options?.readOnly === true) params.set('readOnly', 'true');
  if (options?.readOnly === false) params.set('readOnly', 'false');
  if (options?.bypassLock) params.set('bypassLock', 'true');
  if (import.meta.client && typeof window !== 'undefined' && window.location.origin) {
    params.set('postMessageOrigin', window.location.origin);
  }
  const query = params.toString();
  const path = `${BASE}/${encodeURIComponent(resourceId)}/editor-session${query ? `?${query}` : ''}`;

  try {
    const raw = await fetchFromDocuments(path, 'GET');
    return mapResourceEditorSession(raw, resourceId);
  } catch (error: unknown) {
    if (diErrorStatus(error) === 404) {
      return diGetResourceEditorSessionViaTemplate(resourceId, resourceName);
    }
    throw error;
  }
}

const EDITOR_SESSIONS_BASE = '/api/v1/editor-sessions';

/** Collabora WOPI oturumunu sonlandırır (dialog/sayfa kapanışı). */
export async function diEndEditorSession(accessToken: string): Promise<void> {
  const token = accessToken?.trim();
  if (!token) return;
  await fetchFromDocuments(
    `${EDITOR_SESSIONS_BASE}/${encodeURIComponent(token)}/end`,
    'POST',
  );
}

/**
 * Sekme kapanışında oturumu sonlandırır (fetch keepalive — Authorization ile).
 * pagehide / beforeunload sırasında async çağrılar tamamlanmayabilir.
 */
export function diEndEditorSessionKeepalive(accessToken: string): void {
  const token = accessToken?.trim();
  if (!token || !import.meta.client) return;

  const bearer = getAccessToken();
  if (!bearer) return;

  const url = `/api/documents/v1/editor-sessions/${encodeURIComponent(token)}/end`;
  try {
    void fetch(url, {
      method: 'POST',
      headers: { Authorization: `Bearer ${bearer}` },
      keepalive: true,
    });
  } catch {
    // Best-effort on tab close.
  }
}

/** Oturumu zorla kapat (manager/admin veya oturum sahibi). */
export async function diRevokeEditorSession(accessToken: string): Promise<void> {
  const token = accessToken?.trim();
  if (!token) return;
  await fetchFromDocuments(
    `${EDITOR_SESSIONS_BASE}/${encodeURIComponent(token)}`,
    'DELETE',
  );
}

/** Aktif editör oturumu istatistikleri (D-E1 / D-E3). */
export async function diGetEditorSessionStats(): Promise<DiEditorSessionStats> {
  const raw = await fetchFromDocuments(`${EDITOR_SESSIONS_BASE}/stats`, 'GET');
  const o = asRecord(raw);
  const limits = asRecord(o.limits);
  const collabora = asRecord(o.collaboraHomeMode);
  const byUserRaw = Array.isArray(o.byUser) ? o.byUser : [];
  const sessionsRaw = Array.isArray(o.sessions) ? o.sessions : null;

  return {
    activeConnections: num(o, 'activeConnections') ?? 0,
    activeDocuments: num(o, 'activeDocuments') ?? 0,
    limits: {
      maxConnections: num(limits, 'maxConnections') ?? 0,
      maxDocuments: num(limits, 'maxDocuments') ?? 0,
      maxSessionsPerUser: num(limits, 'maxSessionsPerUser') ?? 0,
    },
    collaboraHomeMode: {
      maxConnections: num(collabora, 'maxConnections') ?? 0,
      maxDocuments: num(collabora, 'maxDocuments') ?? 0,
    },
    byUser: byUserRaw.map((item) => {
      const u = asRecord(item);
      return {
        userId: str(u, 'userId') ?? '',
        displayName: str(u, 'displayName') ?? '',
        connectionCount: num(u, 'connectionCount') ?? 0,
      };
    }),
    sessions: sessionsRaw
      ? sessionsRaw.map((item) => {
          const s = asRecord(item);
          return {
            accessToken: str(s, 'accessToken'),
            tokenPrefix: str(s, 'tokenPrefix') ?? '',
            resourceId: str(s, 'resourceId'),
            templateId: str(s, 'templateId'),
            letterheadId: str(s, 'letterheadId'),
            kind: str(s, 'kind') ?? '',
            officeKind: str(s, 'officeKind'),
            displayName: str(s, 'displayName'),
            userId: str(s, 'userId') ?? '',
            userName: str(s, 'userName') ?? '',
            readOnly: Boolean(s.readOnly),
            createdAt: str(s, 'createdAt') ?? '',
            lastSeenAt: str(s, 'lastSeenAt') ?? '',
          };
        })
      : null,
  };
}

export async function diGetDocxStructure(resourceId: string): Promise<DiDocxStructure> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/source/${encodeURIComponent(resourceId)}/structure`,
    'GET'
  );
  return mapDocxStructure(raw, resourceId);
}

export async function diGetTemplateDocxStructure(templateId: string): Promise<DiDocxStructure> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/source/structure`,
    'GET'
  );
  return mapDocxStructure(raw, templateId);
}

function mapDocxStructure(raw: unknown, fallbackId: string): DiDocxStructure {
  const o = asRecord(raw);
  const parasRaw = o.paragraphs;
  const phRaw = o.placeholders;
  const warnRaw = o.placeholderWarnings;
  return {
    templateId: str(o, 'templateId'),
    resourceId: str(o, 'resourceId') ?? fallbackId,
    fileName: str(o, 'fileName'),
    tableCount: num(o, 'tableCount') ?? 0,
    paragraphs: Array.isArray(parasRaw)
      ? parasRaw.map((p) => {
          const pr = asRecord(p);
          return { index: num(pr, 'index') ?? 0, text: str(pr, 'text') ?? '' };
        })
      : [],
    placeholders: Array.isArray(phRaw)
      ? phRaw.map((p) => {
          const pr = asRecord(p);
          return {
            key: str(pr, 'key') ?? '',
            token: str(pr, 'token') ?? '',
            occurrenceCount: num(pr, 'occurrenceCount') ?? 0,
          };
        })
      : [],
    placeholderWarnings: Array.isArray(warnRaw)
      ? warnRaw.filter((w): w is string => typeof w === 'string')
      : [],
  };
}

export async function diUpdateTemplateParameters(
  templateId: string,
  request: DiUpdateTemplateParametersRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/parameters`,
    'PUT',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diUpdateTemplateMetadata(
  templateId: string,
  request: DiUpdateTemplateMetadataRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/metadata`,
    'PUT',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diUpdateTemplateLetterhead(
  templateId: string,
  request: DiUpdateTemplateLetterheadRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/letterhead`,
    'PUT',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diUpdateTemplateFooter(
  templateId: string,
  request: DiUpdateTemplateFooterRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/footer`,
    'PUT',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diUpdateTemplatePageStructure(
  templateId: string,
  request: DiUpdateTemplatePageStructureRequest
): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/page-structure`,
    'PUT',
    request
  );
  return mapTemplateDetail(raw);
}

export async function diListLetterheads(activeOnly = false): Promise<DiLetterheadListResult> {
  const query = activeOnly ? '?activeOnly=true' : '';
  const raw = await fetchFromDocuments(`${LETTERHEADS_BASE}${query}`, 'GET');
  const o = asRecord(raw);
  const itemsRaw = Array.isArray(o.items) ? o.items : [];
  return {
    items: itemsRaw.map(mapLetterhead),
    total: num(o, 'total') ?? itemsRaw.length,
  };
}

export async function diGetLetterhead(id: string): Promise<DiLetterhead> {
  const raw = await fetchFromDocuments(`${LETTERHEADS_BASE}/${encodeURIComponent(id)}`, 'GET');
  return mapLetterhead(raw);
}

export async function diGetLetterheadDesignSession(id: string): Promise<DiLetterheadDesignSession> {
  const raw = await fetchFromDocuments(
    `${LETTERHEADS_BASE}/${encodeURIComponent(id)}/design-session`,
    'GET'
  );
  const o = asRecord(raw);
  const previewRaw = o.footerPreviewLines;
  return {
    letterheadId: str(o, 'letterheadId') ?? id,
    editorUrl: str(o, 'editorUrl') ?? '',
    accessToken: str(o, 'accessToken') ?? '',
    wopiSrc: str(o, 'wopiSrc') ?? '',
    readOnly: Boolean(o.readOnly),
    designFooterSource: str(o, 'designFooterSource') ?? 'programmatic',
    footerPreviewLines: Array.isArray(previewRaw)
      ? previewRaw.filter((line): line is string => typeof line === 'string')
      : [],
  };
}

export async function diCreateLetterhead(request: DiCreateLetterheadRequest): Promise<DiLetterhead> {
  const raw = await fetchFromDocuments(LETTERHEADS_BASE, 'POST', request);
  return mapLetterhead(raw);
}

export async function diUpdateLetterhead(
  id: string,
  request: DiUpdateLetterheadRequest
): Promise<DiLetterhead> {
  const raw = await fetchFromDocuments(
    `${LETTERHEADS_BASE}/${encodeURIComponent(id)}`,
    'PUT',
    request
  );
  return mapLetterhead(raw);
}

export async function diDeleteLetterhead(id: string): Promise<void> {
  await fetchFromDocuments(`${LETTERHEADS_BASE}/${encodeURIComponent(id)}`, 'DELETE');
}

function mapCoverPageDefinition(raw: unknown): DiCoverPageDefinition {
  const o = asRecord(raw);
  return {
    showLogo: o.showLogo !== false,
    showDocumentName: o.showDocumentName !== false,
    showDocNo: o.showDocNo !== false,
    showGeneratedAt: o.showGeneratedAt !== false,
    showCustomerName: o.showCustomerName !== false,
  };
}

function mapCoverPageSettings(raw: unknown): DiCoverPageSettings {
  const o = asRecord(raw);
  return {
    pageLayout: mapTemplatePageLayout(o.pageLayout) ?? diCreateDefaultPageLayout(),
  };
}

export function diCreateDefaultCoverPageDefinition(): DiCoverPageDefinition {
  return {
    showLogo: true,
    showDocumentName: true,
    showDocNo: true,
    showGeneratedAt: true,
    showCustomerName: true,
  };
}

export function diCreateDefaultCoverPageSettings(): DiCoverPageSettings {
  return { pageLayout: diCreateDefaultPageLayout() };
}

function mapCoverPage(raw: unknown): DiCoverPage {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    code: str(o, 'code') ?? '',
    description: str(o, 'description'),
    isDefault: o.isDefault === true,
    isActive: o.isActive !== false,
    definition: mapCoverPageDefinition(o.definition),
    settings: mapCoverPageSettings(o.settings ?? {}),
    designStoragePath: str(o, 'designStoragePath'),
    designFileName: str(o, 'designFileName'),
    hasDesign: Boolean(o.hasDesign),
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

export async function diListCoverPages(activeOnly = false): Promise<DiCoverPageListResult> {
  const query = activeOnly ? '?activeOnly=true' : '';
  const raw = await fetchFromDocuments(`${COVER_PAGES_BASE}${query}`, 'GET');
  const o = asRecord(raw);
  const itemsRaw = Array.isArray(o.items) ? o.items : [];
  return {
    items: itemsRaw.map(mapCoverPage),
    total: num(o, 'total') ?? itemsRaw.length,
  };
}

export async function diGetCoverPage(id: string): Promise<DiCoverPage> {
  const raw = await fetchFromDocuments(`${COVER_PAGES_BASE}/${encodeURIComponent(id)}`, 'GET');
  return mapCoverPage(raw);
}

export async function diGetCoverPageDesignSession(id: string): Promise<DiCoverPageDesignSession> {
  const raw = await fetchFromDocuments(
    `${COVER_PAGES_BASE}/${encodeURIComponent(id)}/design-session`,
    'GET'
  );
  const o = asRecord(raw);
  return {
    coverPageId: str(o, 'coverPageId') ?? id,
    editorUrl: str(o, 'editorUrl') ?? '',
    accessToken: str(o, 'accessToken') ?? '',
    wopiSrc: str(o, 'wopiSrc') ?? '',
    readOnly: o.readOnly !== false,
  };
}

export async function diCreateCoverPage(request: DiCreateCoverPageRequest): Promise<DiCoverPage> {
  const raw = await fetchFromDocuments(COVER_PAGES_BASE, 'POST', request);
  return mapCoverPage(raw);
}

export async function diUpdateCoverPage(
  id: string,
  request: DiUpdateCoverPageRequest
): Promise<DiCoverPage> {
  const raw = await fetchFromDocuments(
    `${COVER_PAGES_BASE}/${encodeURIComponent(id)}`,
    'PUT',
    request
  );
  return mapCoverPage(raw);
}

export async function diDeleteCoverPage(id: string): Promise<void> {
  await fetchFromDocuments(`${COVER_PAGES_BASE}/${encodeURIComponent(id)}`, 'DELETE');
}

function mapTag(raw: unknown): DiTag {
  const o = asRecord(raw);
  const kind = str(o, 'kind') ?? 'organizational';
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    color: str(o, 'color'),
    description: str(o, 'description'),
    isActive: o.isActive !== false,
    kind,
    sensitivity: num(o, 'sensitivity') ?? 0,
    persistToFile: o.persistToFile == null ? kind === 'classification' : Boolean(o.persistToFile),
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

export async function diListTags(activeOnly = false, kind?: string): Promise<DiTagListResult> {
  const params = new URLSearchParams();
  if (activeOnly) params.set('activeOnly', 'true');
  if (kind) params.set('kind', kind);
  const query = params.toString() ? `?${params.toString()}` : '';
  const raw = await fetchFromDocuments(`${TAGS_BASE}${query}`, 'GET');
  const o = asRecord(raw);
  const itemsRaw = Array.isArray(o.items) ? o.items : Array.isArray(o.Items) ? o.Items : [];
  const items = itemsRaw.map(mapTag);
  const total = num(o, 'total') ?? num(o, 'Total') ?? items.length;
  return { items, total };
}

export async function diCreateTag(request: DiCreateTagRequest): Promise<DiTag> {
  const raw = await fetchFromDocuments(TAGS_BASE, 'POST', request);
  return mapTag(raw);
}

export async function diUpdateTag(id: string, request: DiUpdateTagRequest): Promise<DiTag> {
  const raw = await fetchFromDocuments(`${TAGS_BASE}/${encodeURIComponent(id)}`, 'PUT', request);
  return mapTag(raw);
}

export async function diDeleteTag(id: string): Promise<void> {
  await fetchFromDocuments(`${TAGS_BASE}/${encodeURIComponent(id)}`, 'DELETE');
}

export async function diPublishTemplate(templateId: string): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/publish`,
    'POST'
  );
  return mapTemplateDetail(raw);
}

export async function diUnpublishTemplate(templateId: string): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/unpublish`,
    'POST'
  );
  return mapTemplateDetail(raw);
}

function mapContextType(raw: unknown): DiDocumentContextType {
  const o = asRecord(raw);
  const fieldsRaw = o.fields;
  return {
    type: str(o, 'type') ?? '',
    displayName: str(o, 'displayName') ?? '',
    rootDataset: str(o, 'rootDataset') ?? '',
    fields: Array.isArray(fieldsRaw)
      ? fieldsRaw.map((f) => {
          const fr = asRecord(f);
          return {
            path: str(fr, 'path') ?? '',
            label: str(fr, 'label') ?? '',
            dataType: str(fr, 'dataType') ?? 'text',
          };
        })
      : [],
  };
}

function mapGenerateResult(raw: unknown): DiGenerateDocumentResult {
  const o = asRecord(raw);
  const folderRaw = o.folderPath;
  const valuesRaw = o.resolvedValues;
  const undefinedParameterKeys = strArray(o.undefinedParameterKeys);
  const unresolvedParameterKeys = strArray(o.unresolvedParameterKeys);
  const remainingPlaceholderKeys = strArray(o.remainingPlaceholderKeys);
  return {
    profileCode: str(o, 'profileCode') ?? '',
    contextType: str(o, 'contextType') ?? '',
    contextId: str(o, 'contextId') ?? '',
    templateId: str(o, 'templateId') ?? '',
    templateCode: str(o, 'templateCode') ?? '',
    letterheadId: str(o, 'letterheadId'),
    letterheadCode: str(o, 'letterheadCode'),
    letterheadName: str(o, 'letterheadName'),
    coverPageId: str(o, 'coverPageId'),
    coverPageCode: str(o, 'coverPageCode'),
    coverPageName: str(o, 'coverPageName'),
    docNo: str(o, 'docNo'),
    resourceId: str(o, 'resourceId') ?? '',
    fileName: str(o, 'fileName') ?? '',
    folderPath: Array.isArray(folderRaw) ? folderRaw.map((x) => String(x)) : [],
    generatedAt: str(o, 'generatedAt') ?? '',
    resolvedValues:
      valuesRaw && typeof valuesRaw === 'object'
        ? Object.fromEntries(
            Object.entries(valuesRaw as Record<string, unknown>).map(([k, v]) => [k, String(v ?? '')])
          )
        : {},
    undefinedParameterKeys,
    unresolvedParameterKeys,
    remainingPlaceholderKeys,
    hasParameterWarnings:
      Boolean(o.hasParameterWarnings) ||
      undefinedParameterKeys.length > 0 ||
      unresolvedParameterKeys.length > 0,
  };
}

export async function diListDocumentContextTypes(): Promise<DiDocumentContextType[]> {
  const raw = await fetchFromDocuments(`${GENERATE_BASE}/context-types`, 'GET');
  return Array.isArray(raw) ? raw.map(mapContextType) : [];
}

function mapDocumentProducerDetail(raw: unknown): DiDocumentProducerDetail {
  const o = asRecord(raw);
  const folderRaw = o.outputFolderPath;
  const writebackRaw = o.writebackFields;
  return {
    code: str(o, 'code') ?? '',
    displayName: str(o, 'displayName') ?? '',
    contextType: str(o, 'contextType') ?? '',
    templateCode: str(o, 'templateCode') ?? '',
    outputFormat: str(o, 'outputFormat') ?? 'docx',
    outputFolderPath: Array.isArray(folderRaw) ? folderRaw.map((x) => String(x)) : [],
    fileNamePattern: str(o, 'fileNamePattern') ?? '',
    idempotencyDataset: str(o, 'idempotencyDataset'),
    idempotencyGuardField: str(o, 'idempotencyGuardField'),
    writebackFields: Array.isArray(writebackRaw) ? writebackRaw.map((x) => String(x)) : [],
  };
}

function mapDocumentDataSourceSummary(raw: unknown): DiDocumentDataSourceSummary {
  const o = asRecord(raw);
  const matchRaw = o.match;
  return {
    code: str(o, 'code') ?? '',
    displayName: str(o, 'displayName') ?? '',
    provider: str(o, 'provider') ?? '',
    mode: str(o, 'mode') ?? '',
    dataset: str(o, 'dataset'),
    query: str(o, 'query'),
    match:
      matchRaw && typeof matchRaw === 'object' ? (matchRaw as Record<string, unknown>) : null,
    columnCount: num(o, 'columnCount') ?? 0,
  };
}

function mapDocumentDataSourceDetail(raw: unknown): DiDocumentDataSourceDetail {
  const summary = mapDocumentDataSourceSummary(raw);
  const o = asRecord(raw);
  const paramsRaw = o.parameters;
  const columnsRaw = o.columns;
  return {
    ...summary,
    queryName: str(o, 'queryName'),
    idFrom: str(o, 'idFrom'),
    parameters:
      paramsRaw && typeof paramsRaw === 'object' ? (paramsRaw as Record<string, unknown>) : null,
    columns: Array.isArray(columnsRaw)
      ? columnsRaw.map((col) => {
          const c = asRecord(col);
          return {
            sourceField: str(c, 'sourceField') ?? '',
            header: str(c, 'header'),
            format: str(c, 'format'),
          };
        })
      : [],
  };
}

export async function diGetDocumentProducer(code: string): Promise<DiDocumentProducerDetail | null> {
  try {
    const raw = await fetchFromDocuments(
      `${GENERATE_BASE}/producers/${encodeURIComponent(code)}`,
      'GET'
    );
    return mapDocumentProducerDetail(raw);
  } catch {
    return null;
  }
}

export async function diListDocumentDataSources(): Promise<DiDocumentDataSourceSummary[]> {
  const raw = await fetchFromDocuments(`${GENERATE_BASE}/data-sources`, 'GET');
  return Array.isArray(raw) ? raw.map(mapDocumentDataSourceSummary) : [];
}

export async function diGetDocumentDataSource(code: string): Promise<DiDocumentDataSourceDetail | null> {
  try {
    const raw = await fetchFromDocuments(
      `${GENERATE_BASE}/data-sources/${encodeURIComponent(code)}`,
      'GET'
    );
    return mapDocumentDataSourceDetail(raw);
  } catch {
    return null;
  }
}

export async function diPreviewDocumentGeneration(
  profileCode: string,
  contextId: string
): Promise<DiDocumentGenerationPreview> {
  const q = new URLSearchParams({ profileCode, contextId });
  const raw = await fetchFromDocuments(`${GENERATE_BASE}/preview?${q.toString()}`, 'GET');
  const o = asRecord(raw);
  const valuesRaw = o.values;
  const missingRaw = o.missingKeys;
  return {
    profileCode: str(o, 'profileCode') ?? profileCode,
    contextType: str(o, 'contextType') ?? '',
    contextId: str(o, 'contextId') ?? contextId,
    values:
      valuesRaw && typeof valuesRaw === 'object'
        ? Object.fromEntries(
            Object.entries(valuesRaw as Record<string, unknown>).map(([k, v]) => [k, String(v ?? '')])
          )
        : {},
    missingKeys: Array.isArray(missingRaw) ? missingRaw.map((x) => String(x)) : [],
    undefinedParameterKeys: strArray(o.undefinedParameterKeys),
    unresolvedParameterKeys: strArray(o.unresolvedParameterKeys),
  };
}

export async function diGetDocumentGenerationStatus(
  profileCode: string,
  contextId: string
): Promise<DiDocumentGenerationStatus> {
  const q = new URLSearchParams({ profileCode, contextId });
  const raw = await fetchFromDocuments(`${GENERATE_BASE}/status?${q.toString()}`, 'GET');
  const o = asRecord(raw);
  return {
    profileCode: str(o, 'profileCode') ?? profileCode,
    contextType: str(o, 'contextType') ?? '',
    contextId: str(o, 'contextId') ?? contextId,
    generated: Boolean(o.generated),
    docNo: str(o, 'docNo'),
    resourceId: str(o, 'resourceId'),
    fileName: str(o, 'fileName'),
    generatedAt: str(o, 'generatedAt'),
  };
}

export async function diGenerateDocument(request: DiGenerateDocumentRequest): Promise<DiGenerateDocumentResult> {
  const raw = await fetchFromDocuments(GENERATE_BASE, 'POST', request);
  return mapGenerateResult(raw);
}

/** Generic producer run (G0) — maps to generation profile until dm_document_producers (G4). */
export async function diRunGeneration(
  envelope: DiGenerationRuntimeEnvelope
): Promise<DiGenerateDocumentResult> {
  const raw = await fetchFromDocuments(`${GENERATE_BASE}/run`, 'POST', envelope);
  return mapGenerateResult(raw);
}

function mapGenerationPreview(raw: unknown): DiDocumentGenerationPreview {
  const o = asRecord(raw);
  const valuesRaw = o.values;
  const missingRaw = o.missingKeys;
  return {
    profileCode: str(o, 'profileCode') ?? '',
    contextType: str(o, 'contextType') ?? '',
    contextId: str(o, 'contextId') ?? '',
    values:
      valuesRaw && typeof valuesRaw === 'object'
        ? Object.fromEntries(
            Object.entries(valuesRaw as Record<string, unknown>).map(([k, v]) => [k, String(v ?? '')])
          )
        : {},
    missingKeys: Array.isArray(missingRaw) ? missingRaw.map((x) => String(x)) : [],
    undefinedParameterKeys: strArray(o.undefinedParameterKeys),
    unresolvedParameterKeys: strArray(o.unresolvedParameterKeys),
  };
}

/** Şablondan manuel üretim — parametre önizlemesi (D4). */
export async function diPreviewFromTemplateGeneration(
  templateId: string,
  request?: DiPreviewFromTemplateRequest
): Promise<DiDocumentGenerationPreview> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/preview-generation`,
    'POST',
    request ?? {}
  );
  return mapGenerationPreview(raw);
}

/** Şablondan üretim Collabora önizlemesi — merge + antet + salt okunur oturum (D4). */
export async function diCreateTemplateGenerationPreviewSession(
  templateId: string,
  request?: DiPreviewFromTemplateRequest
): Promise<DiTemplateGenerationPreviewSession> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/preview-session`,
    'POST',
    request ?? {}
  );
  const o = asRecord(raw);
  const valuesRaw = o.values;
  const values: Record<string, string> = {};
  if (valuesRaw && typeof valuesRaw === 'object' && !Array.isArray(valuesRaw)) {
    for (const [key, value] of Object.entries(valuesRaw as Record<string, unknown>)) {
      values[key] = String(value ?? '');
    }
  }
  return {
    templateId: str(o, 'templateId') ?? templateId,
    editorUrl: str(o, 'editorUrl') ?? '',
    accessToken: str(o, 'accessToken') ?? '',
    wopiSrc: str(o, 'wopiSrc') ?? '',
    readOnly: o.readOnly !== false,
    profileCode: str(o, 'profileCode') ?? '',
    values,
    missingKeys: strArray(o.missingKeys),
    undefinedParameterKeys: strArray(o.undefinedParameterKeys),
    unresolvedParameterKeys: strArray(o.unresolvedParameterKeys),
    remainingPlaceholderKeys: strArray(o.remainingPlaceholderKeys),
  };
}

/** Şablondan manuel döküman üretimi — merge + kaynak ağacına kayıt (D4). */
export async function diGenerateFromTemplate(
  templateId: string,
  request: DiGenerateFromTemplateRequest
): Promise<DiGenerateDocumentResult> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/generate`,
    'POST',
    request
  );
  return mapGenerateResult(raw);
}

/** Şablon DOCX → merge → PDF (smoke / önizleme). */
export async function diRenderTemplatePdf(
  templateId: string,
  options?: { values?: Record<string, string>; preserveMissingPlaceholders?: boolean }
): Promise<Blob> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
  }
  const path = `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/render/pdf`;
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  const serverPath = cleanPath.replace(/^\/api\/v1\//, 'v1/');
  const fullUrl = `/api/documents/${serverPath}`;
  const body: Record<string, unknown> = {};
  if (options?.values && Object.keys(options.values).length > 0) {
    body.values = options.values;
  }
  if (options?.preserveMissingPlaceholders) {
    body.preserveMissingPlaceholders = true;
  }
  const res = await fetch(fullUrl, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/pdf',
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  return await res.blob();
}

/** DOCX kaynağını PDF olarak indirir (native/manual/system/upload — D4). */
export async function diFetchResourceExportPdf(resourceId: string): Promise<Blob> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
  }
  const path = `${BASE}/${encodeURIComponent(resourceId)}/export/pdf`;
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  const serverPath = cleanPath.replace(/^\/api\/v1\//, 'v1/');
  const fullUrl = `/api/documents/${serverPath}`;
  const res = await fetch(fullUrl, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/pdf' },
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  return await res.blob();
}

/** MngDocument/HTTP hata gövdesinden `code` döndürür (guard ayrımı için, örn. RESOURCE_HAS_CHILDREN). */
export function diErrorCode(error: unknown): string | null {
  if (!(error instanceof Error)) return null;
  const data = (error as { data?: unknown }).data;
  if (data && typeof data === 'object') {
    const code = (data as Record<string, unknown>).code;
    if (typeof code === 'string') return code;
  }
  return null;
}

/** HTTP durum kodunu döndürür (örn. 409 conflict ayrımı). */
export function diErrorStatus(error: unknown): number | null {
  if (!error || typeof error !== 'object') return null;
  const e = error as Record<string, unknown>;
  const sc = e.statusCode ?? e.status;
  return typeof sc === 'number' ? sc : null;
}

/** Hata gövdesinden okunabilir mesaj çıkarır (messageTr > message > genel). */
export function diExtractMessage(error: unknown, fallback: string): string {
  if (error instanceof Error) {
    const data = (error as { data?: unknown }).data;
    const fromBody = documentsApiErrorUserMessage(data, diErrorStatus(error) ?? 500, fallback);
    if (fromBody !== fallback || parseDocumentsApiErrorBody(data)) {
      return fromBody;
    }
    return error.message && error.message !== 'Internal Server Error' ? error.message : fallback;
  }
  return fallback;
}
