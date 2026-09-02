// Document Intelligence (MngDocument) — Faz 1 tipleri.
// API: gateway /documents/api/v1/resources (camelCase JSON).

export const DI_RESOURCE_TYPE = {
  folder: 'folder',
  markdown: 'markdown',
  file: 'file',
} as const;

export type DiResourceType = (typeof DI_RESOURCE_TYPE)[keyof typeof DI_RESOURCE_TYPE];

/** dm_resources.origin değerleri. */
export const DI_RESOURCE_ORIGIN = {
  upload: 'upload',
  native: 'native',
  manual: 'manual',
  system: 'system',
} as const;

export type DiResourceOrigin = (typeof DI_RESOURCE_ORIGIN)[keyof typeof DI_RESOURCE_ORIGIN];

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
  /** DLP birincil sınıflandırma (dm_tags id, kind=classification). */
  classificationTagId: string | null;
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
  /** Kaynak kökeni: upload | native | manual | system (yalnızca type=file). */
  origin: string | null;
  letterheadId: string | null;
  documentNo: string | null;
  templateId: string | null;
  templateCode: string | null;
  generationProfile: string | null;
  createdAt: string | null;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
  /** Geçerli kullanıcının bu kaynak üzerindeki etkin yetkileri (buton gating). */
  permissions: DiEffectivePermission;
}

/** Sol panel ağaç düğümü (yalnızca klasörler). */
export interface DiTreeNode {
  id: string;
  name: string;
  parentId: string | null;
  /** Alt klasör var mı (lazy tree). */
  hasChildren: boolean;
  children: DiTreeNode[];
  /** UI ipucu: rapor yaprağı vb. (DI klasörleri genelde yok). */
  kind?: 'folder' | 'file';
}

/** Lazy tree derin link segmenti. */
export interface DiTreePathSegment {
  parentId: string | null;
  nodes: DiTreeNode[];
}

export interface DiTreePath {
  breadcrumb: DiBreadcrumb[];
  segments: DiTreePathSegment[];
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
  /** Lazy tree kök seviyesi. */
  treeRoots: DiTreeNode[];
  /** Geriye dönük tam ağaç (boş olabilir). */
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

export interface DiUpdateResourceMetadataRequest {
  tags?: string[];
  description?: string | null;
  /** Boş string sınıflandırmayı kaldırır. */
  classificationTagId?: string | null;
}

export interface DiCloneResourceRequest {
  parentId?: string | null;
  name: string;
  /** Manual DOCX için zorunlu */
  documentNo?: string | null;
}

/** dm_tags katalog kaydı. */
export interface DiTag {
  id: string;
  name: string;
  color: string | null;
  description: string | null;
  isActive: boolean;
  kind: string;
  sensitivity: number;
  persistToFile: boolean;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface DiTagListResult {
  items: DiTag[];
  total: number;
}

export interface DiCreateTagRequest {
  name: string;
  color?: string | null;
  description?: string | null;
  isActive?: boolean;
  kind?: string;
  sensitivity?: number;
  persistToFile?: boolean;
}

export interface DiUpdateTagRequest {
  name: string;
  color?: string | null;
  description?: string | null;
  isActive?: boolean;
  kind?: string;
  sensitivity?: number;
  persistToFile?: boolean;
}

export interface DiCreateMarkdownRequest {
  parentId?: string | null;
  title: string;
  content: string;
  description?: string | null;
  tags?: string[];
  classificationTagId?: string | null;
  /** true ise taslak olarak oluşturur (status=draft). */
  isDraft?: boolean;
}

export interface DiUpdateMarkdownRequest {
  title?: string | null;
  content: string;
  description?: string | null;
  tags?: string[];
  /** Boş string sınıflandırmayı kaldırır. */
  classificationTagId?: string | null;
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
  classificationTagId?: string | null;
  /** Base64 dosya içeriği (data URL öneki olmadan). */
  content: string;
  /** Orijinal dosya adı (indirmede kullanılır). */
  originalFileName?: string | null;
}

export interface DiCreateNativeDocumentRequest {
  parentId?: string | null;
  name: string;
  /** İş kodu (documentNo); domain geneli benzersiz. */
  documentNo: string;
  description?: string | null;
  tags?: string[];
  classificationTagId?: string | null;
  /** Boş/null → antetsiz boş DOCX. */
  letterheadId?: string | null;
  /** Antet seçildiyse doldurulacak header parametreleri. */
  selectedHeaderFields?: DiLetterheadHeaderFields | null;
}

export interface DiCreateNativeOfficeRequest {
  parentId?: string | null;
  name: string;
  /** Boş bırakılırsa sunucu dosya adından üretir. */
  documentNo?: string | null;
  description?: string | null;
  tags?: string[];
  classificationTagId?: string | null;
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
  tableIndex?: number | null;
  headerRowIndex?: number | null;
  templateRowIndex?: number | null;
}

/** @deprecated use DiTemplateDocBinding */
export type DiTemplateSourceBinding = DiTemplateDocBinding;

export interface DiTemplateValueSource {
  mode?: string;
  provider?: string;
  dataset?: string | null;
  queryName?: string | null;
  idFrom?: string | null;
  query?: string | null;
  match?: Record<string, unknown> | null;
  parameters?: Record<string, unknown> | null;
  path?: string | null;
  fallbackPath?: string | null;
  field?: string | null;
  format?: string | null;
  defaultValue?: string | null;
  columns?: Array<{ sourceField: string; header?: string | null; format?: string | null }> | null;
}

export interface DiDocumentProducerDetail {
  code: string;
  displayName: string;
  contextType: string;
  templateCode: string;
  outputFormat: string;
  outputFolderPath: string[];
  fileNamePattern: string;
  idempotencyDataset?: string | null;
  idempotencyGuardField?: string | null;
  writebackFields: string[];
}

export interface DiDocumentDataSourceSummary {
  code: string;
  displayName: string;
  provider: string;
  mode: string;
  dataset?: string | null;
  query?: string | null;
  match?: Record<string, unknown> | null;
  columnCount: number;
}

export interface DiDocumentDataSourceDetail extends DiDocumentDataSourceSummary {
  queryName?: string | null;
  idFrom?: string | null;
  parameters?: Record<string, unknown> | null;
  columns: Array<{ sourceField: string; header?: string | null; format?: string | null }>;
}

export interface DiTemplateParameter {
  key: string;
  label: string;
  /** scalar · table · list · chart */
  kind?: string;
  dataType: string;
  valueSourceMode: DiTemplateValueSourceMode | string;
  dataSourceRef?: string | null;
  defaultValue?: string | null;
  format?: string | null;
  incremental?: DiTemplateIncrementalOptions | null;
  docBinding?: DiTemplateDocBinding | null;
  /** @deprecated use docBinding */
  sourceBinding?: DiTemplateDocBinding | null;
  contextBinding?: DiTemplateContextBinding | null;
  valueSource?: DiTemplateValueSource | null;
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
  /** docx | xlsx | pptx — kaynak dosya uzantısından türetilir. */
  outputFormat?: string;
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

export interface DiGenerationRuntimeEnvelope {
  producerCode: string;
  context: { type: string; id: string };
  scope?: { workspaceId?: string | null; domainId?: string | null };
  params?: Record<string, string>;
  overrides?: Record<string, string>;
  trigger?: { kind?: string; correlationId?: string | null };
  templateCode?: string | null;
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
  coverPageId?: string | null;
  coverPageCode?: string | null;
  coverPageName?: string | null;
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

export interface DiGenerateFromTemplateRequest {
  parentFolderId: string;
  documentName?: string | null;
  overrides?: Record<string, string>;
  /** Table parameter key → row dictionaries (reporting filtered rows, etc.). */
  tableOverrides?: Record<string, Record<string, unknown>[]>;
  /** Default true: keep {{key}} for empty/missing scalars. */
  preserveMissingPlaceholders?: boolean;
  includeCoverPage?: boolean;
  coverPageId?: string | null;
}

export interface DiPreviewFromTemplateRequest {
  overrides?: Record<string, string>;
  allocateCounters?: boolean;
  documentName?: string | null;
}

export interface DiTemplateGenerationPreviewSession {
  templateId: string;
  editorUrl: string;
  accessToken: string;
  wopiSrc: string;
  readOnly: boolean;
  profileCode: string;
  values: Record<string, string>;
  missingKeys: string[];
  undefinedParameterKeys: string[];
  unresolvedParameterKeys: string[];
  remainingPlaceholderKeys: string[];
}

export interface DiTemplateDetail extends DiTemplateSummary {
  schemaVersion: string;
  defaultLetterheadId?: string | null;
  defaultCoverPageId?: string | null;
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

export interface DiCoverPageDefinition {
  showLogo: boolean;
  showDocumentName: boolean;
  showDocNo: boolean;
  showGeneratedAt: boolean;
  showCustomerName: boolean;
}

export interface DiCoverPageSettings {
  pageLayout: DiTemplatePageLayout;
}

export interface DiCoverPage {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
  definition: DiCoverPageDefinition;
  settings: DiCoverPageSettings;
  designStoragePath?: string | null;
  designFileName?: string | null;
  hasDesign?: boolean;
  createdBy: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface DiCoverPageListResult {
  items: DiCoverPage[];
  total: number;
}

export interface DiCreateCoverPageRequest {
  name: string;
  code: string;
  description?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  definition?: DiCoverPageDefinition;
  settings?: DiCoverPageSettings;
}

export interface DiUpdateCoverPageRequest {
  name: string;
  code: string;
  description?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  definition?: DiCoverPageDefinition;
  settings?: DiCoverPageSettings;
}

export interface DiCoverPageDesignSession {
  coverPageId: string;
  editorUrl: string;
  accessToken: string;
  wopiSrc: string;
  readOnly: boolean;
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
  defaultCoverPageId?: string | null;
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
  lockedByOthers?: boolean;
  lockEnforced?: boolean;
}

export interface DiDocumentActiveEditor {
  userId: string;
  userName: string;
  lastSeenAt: string;
  isCurrentUser: boolean;
}

export interface DiDocumentEditorLockStatus {
  resourceId?: string | null;
  templateId?: string | null;
  letterheadId?: string | null;
  isLocked: boolean;
  isLockedByOthers: boolean;
  isLockedBySelf: boolean;
  warnOnActiveEditor: boolean;
  enforceExclusiveLock: boolean;
  canBypassLock: boolean;
  activeEditors: DiDocumentActiveEditor[];
}

export type DiEditorLockChoice = 'edit' | 'readOnly' | 'cancel';

export interface DiResourceEditorOpenOptions {
  readOnly?: boolean;
  bypassLock?: boolean;
}

export interface DiEditorSessionLimits {
  maxConnections: number;
  maxDocuments: number;
  maxSessionsPerUser: number;
}

export interface DiEditorSessionUserStats {
  userId: string;
  displayName: string;
  connectionCount: number;
}

export interface DiEditorSessionItem {
  accessToken?: string | null;
  tokenPrefix: string;
  resourceId?: string | null;
  templateId?: string | null;
  letterheadId?: string | null;
  kind: string;
  officeKind?: string | null;
  displayName?: string | null;
  userId: string;
  userName: string;
  readOnly: boolean;
  createdAt: string;
  lastSeenAt: string;
}

export interface DiEditorSessionStats {
  activeConnections: number;
  activeDocuments: number;
  limits: DiEditorSessionLimits;
  collaboraHomeMode: { maxConnections: number; maxDocuments: number };
  byUser: DiEditorSessionUserStats[];
  sessions: DiEditorSessionItem[] | null;
}
