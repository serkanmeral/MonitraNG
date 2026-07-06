// Document Intelligence (MngDocument) — Faz 1 tipleri.
// API: gateway /documents/api/v1/resources (camelCase JSON).

export const DI_RESOURCE_TYPE = {
  folder: 'folder',
  markdown: 'markdown',
  file: 'file',
} as const;

export type DiResourceType = (typeof DI_RESOURCE_TYPE)[keyof typeof DI_RESOURCE_TYPE];

/** Yetki aksiyonları (dm_resource_permissions.permissions). */
export const DI_PERMISSION_ACTIONS = [
  'view',
  'create',
  'edit',
  'delete',
  'upload',
  'download',
  'move',
  'share',
] as const;

export type DiPermissionAction = (typeof DI_PERMISSION_ACTIONS)[number];

/** Geçerli kullanıcının bir kaynak üzerindeki etkin (miras dahil) yetkileri. */
export interface DiEffectivePermission {
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canUpload: boolean;
  canDownload: boolean;
  canMove: boolean;
  canShare: boolean;
}

/** Tüm aksiyonlar açık (varsayılan / admin). */
export function diFullPermission(): DiEffectivePermission {
  return {
    canView: true,
    canCreate: true,
    canEdit: true,
    canDelete: true,
    canUpload: true,
    canDownload: true,
    canMove: true,
    canShare: true,
  };
}

/** Tek kaynak (klasör / markdown / dosya) metadata'sı. */
export interface DiResource {
  id: string;
  type: DiResourceType | string;
  parentId: string | null;
  ancestorIds: string[];
  name: string;
  title: string | null;
  description: string | null;
  tags: string[];
  contentType: string | null;
  mimeType: string | null;
  extension: string | null;
  size: number | null;
  currentVersionNumber: number;
  hasContent: boolean;
  /** Doküman durumu (yalnızca markdown): 'draft' | 'published'. Varsayılan 'published'. */
  status: string;
  /** Yüklenen dosyanın MinIO path'i (yalnızca type=file). İndirme için. */
  filePath: string | null;
  /** Yüklenen dosyanın orijinal adı (yalnızca type=file). */
  fileName: string | null;
  createdAt: string | null;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
  /** Geçerli kullanıcının bu kaynak üzerindeki etkin yetkileri (buton gating). */
  permissions: DiEffectivePermission;
}

/** Sol panel ağaç düğümü (yalnızca klasörler, iç içe). */
export interface DiTreeNode {
  id: string;
  name: string;
  parentId: string | null;
  children: DiTreeNode[];
}

/** Breadcrumb / yol bilgisi. */
export interface DiBreadcrumb {
  id: string;
  name: string;
}

export interface DiResourceListResult {
  items: DiResource[];
  total: number;
}

/** İlk yükleme / tam yenileme (tek API çağrısı). */
export interface DiResourceBootstrap {
  tree: DiTreeNode[];
  children: DiResourceListResult;
  breadcrumb: DiBreadcrumb[];
  selectedFolder: DiResource | null;
}

/** Klasör gezinme (ağaç hariç, tek API çağrısı). */
export interface DiResourceBrowseContext {
  children: DiResourceListResult;
  breadcrumb: DiBreadcrumb[];
  selectedFolder: DiResource | null;
}

export interface DiMarkdownContent {
  id: string;
  title: string | null;
  content: string;
  currentVersionNumber: number;
}

/** Sürüm geçmişi satırı (içerik hariç). */
export interface DiMarkdownVersion {
  versionNumber: number;
  changeNote: string | null;
  size: number | null;
  createdAt: string | null;
  createdBy: string | null;
  isCurrent: boolean;
}

/** Tek bir sürümün içeriği. */
export interface DiMarkdownVersionContent {
  versionNumber: number;
  content: string;
  changeNote: string | null;
  createdAt: string | null;
  createdBy: string | null;
}

// --- İstek modelleri ---

export interface DiCreateFolderRequest {
  parentId?: string | null;
  name: string;
  description?: string | null;
  tags?: string[];
}

export interface DiRenameRequest {
  name: string;
}

export interface DiMoveRequest {
  newParentId?: string | null;
}

export interface DiCreateMarkdownRequest {
  parentId?: string | null;
  title: string;
  content: string;
  description?: string | null;
  tags?: string[];
  /** true ise taslak olarak oluşturur (status=draft). */
  isDraft?: boolean;
}

export interface DiUpdateMarkdownRequest {
  title?: string | null;
  content: string;
  description?: string | null;
  tags?: string[];
  expectedVersionNumber: number;
  /** true=taslak, false=yayınla, undefined=mevcut durumu koru. */
  isDraft?: boolean | null;
  /** Sürüm geçmişine yazılacak değişiklik notu (opsiyonel). */
  changeNote?: string | null;
}

/** Bir grup için verilen yetki aksiyonları. */
export interface DiGroupPermission {
  groupId: string | null;
  groupName: string;
  permissions: string[];
}

/** Klasörün yetki yönetim görünümü. */
export interface DiFolderPermissions {
  resourceId: string;
  /** Bu klasörün kendi ACL'i var mı (miras kırık mı). */
  inheritanceBroken: boolean;
  /** Etkin yetkilerin geldiği anchor klasör id'si (miras kaynağı). */
  effectiveAnchorId: string | null;
  groups: DiGroupPermission[];
  /** Geçerli kullanıcının bu klasör üzerindeki etkin yetkileri. */
  effective: DiEffectivePermission;
}

export interface DiSetFolderPermissionsRequest {
  groups: DiGroupPermission[];
}

export interface DiCreateFileResourceRequest {
  parentId?: string | null;
  name: string;
  description?: string | null;
  mimeType?: string | null;
  extension?: string | null;
  size?: number | null;
  tags?: string[];
  /** Base64 dosya içeriği (data URL öneki olmadan). */
  content: string;
  /** Orijinal dosya adı (indirmede kullanılır). */
  originalFileName?: string | null;
}

/** Ağaç kökü için sanal düğüm kimliği (UI). */
export const DI_ROOT_ID = '__di_root__';

/** dm_resource_links.relationType değerleri (Faz 2). */
export const DI_LINK_RELATION_TYPES = ['reference', 'attachment', 'evidence', 'output'] as const;
export type DiLinkRelationType = (typeof DI_LINK_RELATION_TYPES)[number];

export interface DiResourceLink {
  id: string;
  resourceId: string;
  targetModule: string;
  targetType: string;
  targetId: string;
  relationType: DiLinkRelationType | string;
  createdBy: string | null;
  createdAt: string | null;
}

export interface DiLinkedWorkItem {
  linkId: string;
  workItemId: string;
  workItemKey: string | null;
  workItemTitle: string | null;
  boardId: string | null;
  workspaceId: string | null;
  relationType: DiLinkRelationType | string;
}

export interface DiLinkedResource {
  linkId: string;
  resourceId: string;
  relationType: DiLinkRelationType | string;
  resourceType: string | null;
  name: string | null;
  title: string | null;
  mimeType: string | null;
  extension: string | null;
  permissions: DiEffectivePermission;
}

export interface DiCreateResourceLinkRequest {
  resourceId: string;
  targetModule: string;
  targetType: string;
  targetId: string;
  relationType: DiLinkRelationType | string;
}

export interface DiResourceLinkListResult<T> {
  items: T[];
  total: number;
}

// --- Document Designer (templates) ---

/** Belge tasarımcısı kategori ağacı sanal kök id (DiResourceTree ile uyumlu). */
export const DI_DESIGNER_ROOT_ID = '__di_designer_root__';

export interface DiTemplateCategory {
  id: string;
  parentId: string | null;
  ancestorIds: string[];
  name: string;
  description: string | null;
  sortOrder: number;
  status: string;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface DiCreateTemplateCategoryRequest {
  name: string;
  description?: string;
  parentId?: string | null;
}

export interface DiRenameTemplateCategoryRequest {
  name: string;
}

export type DiTemplateValueSourceMode =
  | 'manual'
  | 'incremental'
  | 'computed'
  | 'binding'
  | 'static'
  | 'context'
  | 'generated';

export interface DiTemplateIncrementalOptions {
  format: string;
  startValue?: number;
  incrementStep?: number;
  scopeKey?: string | null;
  resetPolicy?: string;
}

export interface DiTemplateContextBinding {
  path: string;
  fallbackPath?: string | null;
  defaultValue?: string | null;
  format?: string | null;
}

export interface DiTemplateDocBinding {
  regionKind: string;
  paragraphIndex: number;
  originalText?: string | null;
  charStart?: number | null;
  charEnd?: number | null;
}

/** @deprecated use DiTemplateDocBinding */
export type DiTemplateSourceBinding = DiTemplateDocBinding;

export interface DiTemplateParameter {
  key: string;
  label: string;
  dataType: string;
  valueSourceMode: DiTemplateValueSourceMode | string;
  defaultValue?: string | null;
  format?: string | null;
  incremental?: DiTemplateIncrementalOptions | null;
  docBinding?: DiTemplateDocBinding | null;
  /** @deprecated use docBinding */
  sourceBinding?: DiTemplateDocBinding | null;
  contextBinding?: DiTemplateContextBinding | null;
}

export interface DiTemplateSummary {
  id: string;
  categoryId: string | null;
  name: string;
  code: string | null;
  description: string | null;
  sourceResourceId: string | null;
  sourceStoragePath: string | null;
  sourceFileName: string | null;
  creationMode: string;
  status: string;
  parameterCount: number;
  primaryContextType?: string | null;
  generationProfile?: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface DiDocumentContextField {
  path: string;
  label: string;
  dataType: string;
}

export interface DiDocumentContextType {
  type: string;
  displayName: string;
  rootDataset: string;
  fields: DiDocumentContextField[];
}

export interface DiGenerateDocumentRequest {
  profileCode: string;
  templateCode?: string;
  context: { type: string; id: string };
  overrides?: Record<string, string>;
}

export interface DiGenerateDocumentResult {
  profileCode: string;
  contextType: string;
  contextId: string;
  templateId: string;
  templateCode: string;
  letterheadId?: string | null;
  letterheadCode?: string | null;
  letterheadName?: string | null;
  docNo?: string | null;
  resourceId: string;
  fileName: string;
  folderPath: string[];
  generatedAt: string;
  resolvedValues: Record<string, string>;
  undefinedParameterKeys: string[];
  unresolvedParameterKeys: string[];
  remainingPlaceholderKeys: string[];
  hasParameterWarnings: boolean;
}

export interface DiDocumentGenerationStatus {
  profileCode: string;
  contextType: string;
  contextId: string;
  generated: boolean;
  docNo?: string | null;
  resourceId?: string | null;
  fileName?: string | null;
  generatedAt?: string | null;
}

export interface DiDocumentGenerationPreview {
  profileCode: string;
  contextType: string;
  contextId: string;
  values: Record<string, string>;
  missingKeys: string[];
  undefinedParameterKeys: string[];
  unresolvedParameterKeys: string[];
}

export interface DiTemplateDetail extends DiTemplateSummary {
  schemaVersion: string;
  defaultLetterheadId?: string | null;
  letterhead: DiTemplateLetterhead | null;
  footer: DiTemplateFooter | null;
  pageLayout: DiTemplatePageLayout | null;
  parameters: DiTemplateParameter[];
}

export interface DiLetterheadHeaderFields {
  documentName: boolean;
  docNo: boolean;
  generatedAt: boolean;
  createPerson: boolean;
}

export interface DiLetterheadGeneralDocNo {
  enabled: boolean;
  format: string;
  scopeMode: 'letterhead' | 'global' | 'custom';
  scopeKey?: string | null;
  resetPolicy: string;
  startValue: number;
  incrementStep: number;
}

export interface DiLetterheadFooterSettings {
  enabled: boolean;
  tableRows: number;
  tableColumns: number;
}

export interface DiLetterheadSettings {
  headerFields: DiLetterheadHeaderFields;
  generalDocNo: DiLetterheadGeneralDocNo;
  footer: DiLetterheadFooterSettings;
  pageLayout: DiTemplatePageLayout;
}

export interface DiLetterhead {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
  letterhead: DiTemplateLetterhead;
  settings: DiLetterheadSettings;
  designStoragePath?: string | null;
  designFileName?: string | null;
  hasDesign?: boolean;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface DiLetterheadListResult {
  items: DiLetterhead[];
  total: number;
}

export interface DiCreateLetterheadRequest {
  name: string;
  code: string;
  description?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  letterhead: DiTemplateLetterhead;
  settings?: DiLetterheadSettings;
}

export interface DiUpdateLetterheadRequest {
  name: string;
  code: string;
  description?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  letterhead: DiTemplateLetterhead;
  settings?: DiLetterheadSettings;
}

export interface DiTemplatePageLayout {
  marginTopTwips: number;
  marginRightTwips: number;
  marginBottomTwips: number;
  marginLeftTwips: number;
  headerDistanceTwips: number;
  footerDistanceTwips: number;
  footerLeftIndentTwips: number;
}

export interface DiTemplateFooter {
  enabled: boolean;
  showFormRevision: boolean;
  showOfficeColumns: boolean;
  showAddresses: boolean;
  showContacts: boolean;
  showDividerLine: boolean;
}

export interface DiTemplateLetterhead {
  enabled: boolean;
  showLogo: boolean;
  showDocumentName: boolean;
  showDocumentNumber: boolean;
  showGeneratedAt: boolean;
}

export interface DiTemplateListResult {
  items: DiTemplateSummary[];
  total: number;
}

export interface DiDocxParagraph {
  index: number;
  text: string;
}

export interface DiDocxPlaceholder {
  key: string;
  token: string;
  occurrenceCount: number;
}

export interface DiDocxStructure {
  templateId?: string | null;
  resourceId: string;
  fileName: string | null;
  paragraphs: DiDocxParagraph[];
  tableCount: number;
  placeholders: DiDocxPlaceholder[];
  placeholderWarnings: string[];
}

export interface DiCreateTemplateFromSourceRequest {
  name?: string;
  description?: string;
  sourceResourceId: string;
}

export interface DiCreateTemplateFromReferenceRequest {
  categoryId: string;
  name?: string;
  description?: string;
  content: string;
  fileName: string;
  size?: number;
}

export interface DiUpdateTemplateParametersRequest {
  primaryContextType?: string | null;
  generationProfile?: string | null;
  parameters: DiTemplateParameter[];
}

export interface DiUpdateTemplateMetadataRequest {
  name: string;
  code: string;
}

export interface DiCreateBlankTemplateRequest {
  categoryId: string;
  name?: string;
  code: string;
  letterhead?: DiTemplateLetterhead | null;
  footer?: DiTemplateFooter | null;
}

export interface DiDuplicateTemplateRequest {
  categoryId: string;
  name: string;
  code: string;
  description?: string | null;
  letterhead?: DiTemplateLetterhead | null;
  footer?: DiTemplateFooter | null;
  pageLayout?: DiTemplatePageLayout | null;
}

export interface DiUpdateTemplateFooterRequest {
  footer: DiTemplateFooter;
}

export interface DiUpdateTemplateLetterheadRequest {
  letterhead: DiTemplateLetterhead;
}

export interface DiUpdateTemplatePageStructureRequest {
  pageLayout?: DiTemplatePageLayout | null;
  defaultLetterheadId?: string | null;
  footer?: DiTemplateFooter | null;
}

export interface DiLetterheadDesignSession {
  letterheadId: string;
  editorUrl: string;
  accessToken: string;
  wopiSrc: string;
  readOnly: boolean;
  designFooterSource?: 'design' | 'pending' | 'disabled' | 'custom' | 'blocks' | 'legacy' | 'programmatic' | string;
  footerPreviewLines?: string[];
}

export interface DiTemplateEditorSession {
  templateId: string;
  editorUrl: string;
  accessToken: string;
  wopiSrc: string;
  readOnly: boolean;
}

export interface DiResourceEditorSession {
  resourceId: string;
  editorUrl: string;
  accessToken: string;
  wopiSrc: string;
  readOnly: boolean;
  /** Prod'da resource editor-session yoksa şablon kopyası üzerinden açıldı. */
  viaTemplateFallback?: boolean;
}
