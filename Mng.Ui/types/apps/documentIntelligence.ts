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
