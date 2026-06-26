import { fetchFromDocuments, fetchBlobFromDataGateway } from '@/services/apiService';
import {
  diFullPermission,
  type DiBreadcrumb,
  type DiCreateFileResourceRequest,
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
  type DiRenameRequest,
  type DiResource,
  type DiResourceBootstrap,
  type DiResourceBrowseContext,
  type DiResourceListResult,
  type DiSetFolderPermissionsRequest,
  type DiTreeNode,
  type DiUpdateMarkdownRequest,
  type DiCreateTemplateFromSourceRequest,
  type DiCreateTemplateFromReferenceRequest,
  type DiCreateBlankTemplateRequest,
  type DiTemplateEditorSession,
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
} from '@/types/apps/documentIntelligence';
import { diNormalizePageLayout } from '@/utils/diPageLayout';

const BASE = '/api/v1/resources';
const LINKS_BASE = '/api/v1';
const TEMPLATES_BASE = '/api/v1/templates';
const TEMPLATE_CATEGORIES_BASE = '/api/v1/template-categories';

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
    contentType: str(o, 'contentType'),
    mimeType: str(o, 'mimeType'),
    extension: str(o, 'extension'),
    size: num(o, 'size'),
    currentVersionNumber: num(o, 'currentVersionNumber') ?? 0,
    hasContent: Boolean(o.hasContent),
    status: str(o, 'status') ?? 'published',
    filePath: str(o, 'filePath'),
    fileName: str(o, 'fileName'),
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
    children: Array.isArray(childrenRaw) ? childrenRaw.map(mapTreeNode) : [],
  };
}

function mapListResult(raw: unknown): DiResourceListResult {
  const o = asRecord(raw);
  const items = Array.isArray(o.items) ? o.items.map(mapResource) : [];
  return { items, total: num(o, 'total') ?? items.length };
}

/** Klasör ağacı (yalnızca klasörler, iç içe). */
export async function diGetTree(): Promise<DiTreeNode[]> {
  const raw = await fetchFromDocuments(`${BASE}/tree`, 'GET');
  return Array.isArray(raw) ? raw.map(mapTreeNode) : [];
}

function mapBootstrap(raw: unknown): DiResourceBootstrap {
  const o = asRecord(raw);
  const childrenRaw = o.children;
  const breadcrumbRaw = o.breadcrumb;
  const selectedRaw = o.selectedFolder;
  return {
    tree: Array.isArray(o.tree) ? o.tree.map(mapTreeNode) : [],
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
export async function diGetBootstrap(folderId?: string | null): Promise<DiResourceBootstrap> {
  const qs = folderId ? `?folderId=${encodeURIComponent(folderId)}` : '';
  const raw = await fetchFromDocuments(`${BASE}/bootstrap${qs}`, 'GET');
  return mapBootstrap(raw);
}

/** Klasör gezinme (içerik + breadcrumb + seçili klasör, tek snapshot). */
export async function diGetBrowseContext(folderId?: string | null): Promise<DiResourceBrowseContext> {
  const qs = folderId ? `?folderId=${encodeURIComponent(folderId)}` : '';
  const raw = await fetchFromDocuments(`${BASE}/browse${qs}`, 'GET');
  return mapBrowseContext(raw);
}

/** Bir klasörün içeriği (klasör + markdown + dosya). parentId boşsa kök. */
export async function diGetChildren(parentId?: string | null): Promise<DiResourceListResult> {
  const qs = parentId ? `?parentId=${encodeURIComponent(parentId)}` : '';
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

export async function diMove(id: string, request: DiMoveRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/${encodeURIComponent(id)}/move`, 'PUT', request);
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

export async function diCreateFileResource(request: DiCreateFileResourceRequest): Promise<DiResource> {
  const raw = await fetchFromDocuments(`${BASE}/file`, 'POST', request);
  return mapResource(raw);
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
  return {
    id: str(o, 'id') ?? '',
    name: str(o, 'name') ?? '',
    parentId: str(o, 'parentId'),
    children: Array.isArray(childrenRaw) ? childrenRaw.map(mapCategoryTreeNode) : [],
  };
}

function mapTemplateSummary(raw: unknown): DiTemplateSummary {
  const o = asRecord(raw);
  return {
    id: str(o, 'id') ?? '',
    categoryId: str(o, 'categoryId'),
    name: str(o, 'name') ?? '',
    code: str(o, 'code'),
    description: str(o, 'description'),
    sourceResourceId: str(o, 'sourceResourceId'),
    sourceStoragePath: str(o, 'sourceStoragePath'),
    sourceFileName: str(o, 'sourceFileName'),
    creationMode: str(o, 'creationMode') ?? 'fromTemplate',
    status: str(o, 'status') ?? 'draft',
    parameterCount: num(o, 'parameterCount') ?? 0,
    createdBy: str(o, 'createdBy'),
    createdAt: str(o, 'createdAt'),
    updatedAt: str(o, 'updatedAt'),
  };
}

function mapTemplateParameter(raw: unknown): DiTemplateParameter {
  const o = asRecord(raw);
  const inc = o.incremental;
  const bind = o.sourceBinding;
  return {
    key: str(o, 'key') ?? '',
    label: str(o, 'label') ?? '',
    dataType: str(o, 'dataType') ?? 'text',
    valueSourceMode: str(o, 'valueSourceMode') ?? 'manual',
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
    sourceBinding:
      bind && typeof bind === 'object'
        ? {
            regionKind: str(asRecord(bind), 'regionKind') ?? 'paragraph',
            paragraphIndex: num(asRecord(bind), 'paragraphIndex') ?? 0,
            originalText: str(asRecord(bind), 'originalText'),
            charStart: num(asRecord(bind), 'charStart'),
            charEnd: num(asRecord(bind), 'charEnd'),
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
    letterhead: mapTemplateLetterhead(o.letterhead),
    footer: mapTemplateFooter(o.footer),
    pageLayout: mapTemplatePageLayout(o.pageLayout),
    parameters: Array.isArray(paramsRaw) ? paramsRaw.map(mapTemplateParameter) : [],
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

export async function diPublishTemplate(templateId: string): Promise<DiTemplateDetail> {
  const raw = await fetchFromDocuments(
    `${TEMPLATES_BASE}/${encodeURIComponent(templateId)}/publish`,
    'POST'
  );
  return mapTemplateDetail(raw);
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
    if (data && typeof data === 'object') {
      const d = data as Record<string, unknown>;
      if (typeof d.messageTr === 'string' && d.messageTr) return d.messageTr;
      if (typeof d.message === 'string' && d.message) return d.message;
      const nested = d.error;
      if (nested && typeof nested === 'object') {
        const ne = nested as Record<string, unknown>;
        if (typeof ne.message === 'string' && ne.message) return ne.message;
      }
    }
    return error.message || fallback;
  }
  return fallback;
}
